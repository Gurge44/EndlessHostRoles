using System;
using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using EHR.Neutral;
using HarmonyLib;
using Hazel;
using UnityEngine;
using static EHR.RandomSpawn;

namespace EHR;

internal static class SoloKombatManager
{
    private static Dictionary<byte, float> PlayerHPMax = [];
    private static Dictionary<byte, float> PlayerHP = [];
    public static Dictionary<byte, float> PlayerHPReco = [];
    public static Dictionary<byte, float> PlayerATK = [];
    public static Dictionary<byte, float> PlayerDF = [];

    private static Dictionary<byte, float> OriginalSpeed = [];
    public static Dictionary<byte, int> KBScore = [];
    public static int RoundTime;

    private static readonly Dictionary<byte, (string TEXT, long TIMESTAMP)> NameNotify = [];

    private static Dictionary<byte, int> BackCountdown = [];
    private static Dictionary<byte, long> LastHurt = [];

    public static bool SoloAlive(this PlayerControl pc) => PlayerHP[pc.PlayerId] > 0f;

    public static void SetupCustomOption()
    {
        KB_GameTime = new IntegerOptionItem(66_233_001, "KB_GameTime", new(30, 300, 5), 180, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.SoloKombat)
            .SetColor(new Color32(245, 82, 82, byte.MaxValue))
            .SetValueFormat(OptionFormat.Seconds)
            .SetHeader(true);
        KB_ATKCooldown = new FloatOptionItem(66_223_008, "KB_ATKCooldown", new(1f, 10f, 0.1f), 1f, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.SoloKombat)
            .SetColor(new Color32(245, 82, 82, byte.MaxValue))
            .SetValueFormat(OptionFormat.Seconds);
        KB_HPMax = new FloatOptionItem(66_233_002, "KB_HPMax", new(10f, 990f, 5f), 100f, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.SoloKombat)
            .SetColor(new Color32(245, 82, 82, byte.MaxValue))
            .SetValueFormat(OptionFormat.Health);
        KB_ATK = new FloatOptionItem(66_233_003, "KB_ATK", new(1f, 100f, 1f), 8f, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.SoloKombat)
            .SetColor(new Color32(245, 82, 82, byte.MaxValue))
            .SetValueFormat(OptionFormat.Health);
        KB_RecoverPerSecond = new FloatOptionItem(66_233_005, "KB_RecoverPerSecond", new(1f, 180f, 1f), 2f, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.SoloKombat)
            .SetColor(new Color32(245, 82, 82, byte.MaxValue))
            .SetValueFormat(OptionFormat.Health);
        KB_RecoverAfterSecond = new IntegerOptionItem(66_233_004, "KB_RecoverAfterSecond", new(0, 60, 1), 8, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.SoloKombat)
            .SetColor(new Color32(245, 82, 82, byte.MaxValue))
            .SetValueFormat(OptionFormat.Seconds);
        KB_ResurrectionWaitingTime = new IntegerOptionItem(66_233_006, "KB_ResurrectionWaitingTime", new(3, 990, 1), 15, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.SoloKombat)
            .SetColor(new Color32(245, 82, 82, byte.MaxValue))
            .SetValueFormat(OptionFormat.Seconds);
        KB_KillBonusMultiplier = new FloatOptionItem(66_233_007, "KB_KillBonusMultiplier", new(0.25f, 5f, 0.25f), 1.25f, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.SoloKombat)
            .SetColor(new Color32(245, 82, 82, byte.MaxValue))
            .SetValueFormat(OptionFormat.Multiplier);
        KB_BootVentWhenDead = new BooleanOptionItem(66_233_009, "KB_BootVentWhenDead", false, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.SoloKombat)
            .SetColor(new Color32(245, 82, 82, byte.MaxValue));
    }

    public static void Init()
    {
        if (Options.CurrentGameMode != CustomGameMode.SoloKombat) return;

        PlayerHPMax = [];
        PlayerHP = [];
        PlayerHPReco = [];
        PlayerATK = [];
        PlayerDF = [];

        LastHurt = [];
        OriginalSpeed = [];
        BackCountdown = [];
        KBScore = [];
        RoundTime = KB_GameTime.GetInt() + 8;

        foreach (PlayerControl pc in Main.AllAlivePlayerControls)
        {
            PlayerHPMax.TryAdd(pc.PlayerId, KB_HPMax.GetFloat());
            PlayerHP.TryAdd(pc.PlayerId, KB_HPMax.GetFloat());
            PlayerHPReco.TryAdd(pc.PlayerId, KB_RecoverPerSecond.GetFloat());
            PlayerATK.TryAdd(pc.PlayerId, KB_ATK.GetFloat());
            PlayerDF.TryAdd(pc.PlayerId, 0f);

            KBScore.TryAdd(pc.PlayerId, 0);

            LastHurt.TryAdd(pc.PlayerId, Utils.TimeStamp);
        }
    }

    private static void SendRPCSyncKBBackCountdown(PlayerControl player)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncKBBackCountdown, SendOption.Reliable, player.GetClientId());
        int x = BackCountdown.GetValueOrDefault(player.PlayerId, -1);
        writer.Write(x);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPCSyncBackCountdown(MessageReader reader)
    {
        int num = reader.ReadInt32();
        if (num == -1)
            BackCountdown.Remove(PlayerControl.LocalPlayer.PlayerId);
        else
        {
            BackCountdown.TryAdd(PlayerControl.LocalPlayer.PlayerId, num);
            BackCountdown[PlayerControl.LocalPlayer.PlayerId] = num;
        }
    }

    private static void SendRPCSyncKBPlayer(byte playerId)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncKBPlayer, SendOption.Reliable);
        writer.Write(playerId);
        writer.Write(PlayerHPMax[playerId]);
        writer.Write(PlayerHP[playerId]);
        writer.Write(PlayerHPReco[playerId]);
        writer.Write(PlayerATK[playerId]);
        writer.Write(PlayerDF[playerId]);
        writer.Write(KBScore[playerId]);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPCSyncKBPlayer(MessageReader reader)
    {
        byte PlayerId = reader.ReadByte();
        PlayerHPMax[PlayerId] = reader.ReadSingle();
        PlayerHP[PlayerId] = reader.ReadSingle();
        PlayerHPReco[PlayerId] = reader.ReadSingle();
        PlayerATK[PlayerId] = reader.ReadSingle();
        PlayerDF[PlayerId] = reader.ReadSingle();
        KBScore[PlayerId] = reader.ReadInt32();
    }

    private static void SendRPCSyncNameNotify(PlayerControl pc)
    {
        if (pc.AmOwner || !pc.IsModClient()) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncKBNameNotify, SendOption.Reliable, pc.GetClientId());
        writer.Write(NameNotify.TryGetValue(pc.PlayerId, out (string TEXT, long TIMESTAMP) value) ? value.TEXT : "");
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPCSyncNameNotify(MessageReader reader)
    {
        var name = reader.ReadString();
        NameNotify.Remove(PlayerControl.LocalPlayer.PlayerId);
        if (!string.IsNullOrEmpty(name))
            NameNotify.Add(PlayerControl.LocalPlayer.PlayerId, (name, 0));
    }

    public static string GetDisplayHealth(PlayerControl pc)
        => pc.SoloAlive() ? Utils.ColorString(GetHealthColor(pc), $"{(int)PlayerHP[pc.PlayerId]}/{(int)PlayerHPMax[pc.PlayerId]}") : string.Empty;

    private static Color32 GetHealthColor(PlayerControl pc)
    {
        var x = (int)(PlayerHP[pc.PlayerId] / PlayerHPMax[pc.PlayerId] * 10 * 50);
        int R = 255;
        int G = 255;
        int B = 0;
        if (x > 255) R -= x - 255;
        else G = x;
        return new((byte)R, (byte)G, (byte)B, byte.MaxValue);
    }

    public static void GetNameNotify(PlayerControl player, ref string name)
    {
        if (Options.CurrentGameMode != CustomGameMode.SoloKombat || player == null) return;
        if (BackCountdown.TryGetValue(player.PlayerId, out int value))
        {
            name = string.Format(Translator.GetString("KBBackCountDown"), value);
            NameNotify.Remove(player.PlayerId);
            return;
        }

        if (NameNotify.TryGetValue(player.PlayerId, out (string TEXT, long TIMESTAMP) value1))
        {
            name = value1.TEXT;
        }
    }

    public static int GetRankOfScore(byte playerId)
    {
        try
        {
            int ms = KBScore[playerId];
            int rank = 1 + KBScore.Values.Count(x => x > ms);
            rank += KBScore.Where(x => x.Value == ms).ToList().IndexOf(new(playerId, ms));
            return rank;
        }
        catch
        {
            return Main.AllPlayerControls.Length;
        }
    }

    public static string GetHudText()
    {
        return string.Format(Translator.GetString("KBTimeRemain"), RoundTime.ToString());
    }

    public static void OnPlayerAttack(PlayerControl killer, PlayerControl target)
    {
        if (killer == null || target == null || Options.CurrentGameMode != CustomGameMode.SoloKombat) return;
        if (!killer.SoloAlive() || !target.SoloAlive() || target.inVent) return;

        var dmg = PlayerATK[killer.PlayerId] - PlayerDF[target.PlayerId];
        PlayerHP[target.PlayerId] = Math.Max(0f, PlayerHP[target.PlayerId] - dmg);

        if (!target.SoloAlive())
        {
            OnPlayerDead(target);
            OnPlayerKill(killer);
        }

        LastHurt[target.PlayerId] = Utils.TimeStamp;

        killer.SetKillCooldown(KB_ATKCooldown.GetFloat(), target);
        RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
        RPC.PlaySoundRPC(target.PlayerId, Sounds.KillSound);
        if (!target.IsModClient() && !target.AmOwner)
            target.RpcGuardAndKill();

        SendRPCSyncKBPlayer(target.PlayerId);
        Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
        Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer);
    }

    private static void OnPlayerBack(PlayerControl pc)
    {
        BackCountdown.Remove(pc.PlayerId);
        PlayerHP[pc.PlayerId] = PlayerHPMax[pc.PlayerId];
        SendRPCSyncKBPlayer(pc.PlayerId);

        LastHurt[pc.PlayerId] = Utils.TimeStamp;
        Main.AllPlayerSpeed[pc.PlayerId] = OriginalSpeed[pc.PlayerId];
        pc.MarkDirtySettings();

        RPC.PlaySoundRPC(pc.PlayerId, Sounds.TaskComplete);
        pc.RpcGuardAndKill();

        PlayerRandomSpwan(pc);
    }

    private static void PlayerRandomSpwan(PlayerControl pc)
    {
        SpawnMap map = Main.CurrentMap switch
        {
            MapNames.Skeld => new SkeldSpawnMap(),
            MapNames.Mira => new MiraHQSpawnMap(),
            MapNames.Polus => new PolusSpawnMap(),
            MapNames.Airship => new AirshipSpawnMap(),
            MapNames.Fungle => new FungleSpawnMap(),
            MapNames.Dleks => new DleksSpawnMap(),
            _ => null
        };

        map?.RandomTeleport(pc);
    }

    private static void OnPlayerDead(PlayerControl target)
    {
        OriginalSpeed.Remove(target.PlayerId);
        OriginalSpeed.Add(target.PlayerId, Main.AllPlayerSpeed[target.PlayerId]);

        target.TP(Pelican.GetBlackRoomPS());
        Main.AllPlayerSpeed[target.PlayerId] = Main.MinSpeed;
        target.MarkDirtySettings();

        BackCountdown.TryAdd(target.PlayerId, KB_ResurrectionWaitingTime.GetInt());
        SendRPCSyncKBBackCountdown(target);
    }

    private static void OnPlayerKill(PlayerControl killer)
    {
        killer.KillFlash();
        if (PlayerControl.LocalPlayer.Is(CustomRoles.GM))
            PlayerControl.LocalPlayer.KillFlash();

        KBScore[killer.PlayerId]++;

        float addRate = IRandom.Instance.Next(3, 5 + GetRankOfScore(killer.PlayerId)) / 100f;
        addRate *= KB_KillBonusMultiplier.GetFloat();
        if (killer.IsHost()) addRate /= 2f;
        float addin;
        switch (IRandom.Instance.Next(0, 4))
        {
            case 0:
                addin = PlayerHPMax[killer.PlayerId] * addRate;
                PlayerHPMax[killer.PlayerId] += addin;
                AddNameNotify(killer, string.Format(Translator.GetString("KB_Buff_HPMax"), addin.ToString("0.0#####")));
                break;
            case 1:
                addin = PlayerHPReco[killer.PlayerId] * addRate * 2;
                PlayerHPReco[killer.PlayerId] += addin;
                AddNameNotify(killer, string.Format(Translator.GetString("KB_Buff_HPReco"), addin.ToString("0.0#####")));
                break;
            case 2:
                addin = PlayerATK[killer.PlayerId] * addRate;
                PlayerATK[killer.PlayerId] += addin;
                AddNameNotify(killer, string.Format(Translator.GetString("KB_Buff_ATK"), addin.ToString("0.0#####")));
                break;
            case 3:
                addin = Math.Max(PlayerDF[killer.PlayerId], 1f) * addRate * 3;
                PlayerDF[killer.PlayerId] += addin;
                AddNameNotify(killer, string.Format(Translator.GetString("KB_Buff_DF"), addin.ToString("0.0#####")));
                break;
        }
    }

    private static void AddNameNotify(PlayerControl pc, string text, int time = 5)
    {
        NameNotify.Remove(pc.PlayerId);
        NameNotify.Add(pc.PlayerId, (text, Utils.TimeStamp + time));
        SendRPCSyncNameNotify(pc);
        SendRPCSyncKBPlayer(pc.PlayerId);
        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    class FixedUpdatePatch
    {
        private static long LastFixedUpdate;

        public static void Postfix( /*PlayerControl __instance*/)
        {
            if (!GameStates.IsInTask || Options.CurrentGameMode != CustomGameMode.SoloKombat) return;

            if (AmongUsClient.Instance.AmHost)
            {
                foreach (var pc in Main.AllPlayerControls.Where(pc => !pc.SoloAlive()).ToArray())
                {
                    if (pc.inVent && KB_BootVentWhenDead.GetBool()) pc.MyPhysics.RpcExitVent(2);
                    var pos = Pelican.GetBlackRoomPS();
                    var dis = Vector2.Distance(pos, pc.Pos());
                    if (dis > 1f) pc.TP(pos);
                }

                if (LastFixedUpdate == Utils.TimeStamp) return;
                LastFixedUpdate = Utils.TimeStamp;

                RoundTime--;

                if (!AmongUsClient.Instance.AmHost) return;

                foreach (var pc in Main.AllPlayerControls)
                {
                    bool notifyRoles = false;
                    if (LastHurt[pc.PlayerId] + KB_RecoverAfterSecond.GetInt() < Utils.TimeStamp && PlayerHP[pc.PlayerId] < PlayerHPMax[pc.PlayerId] && pc.SoloAlive() && !pc.inVent)
                    {
                        PlayerHP[pc.PlayerId] += PlayerHPReco[pc.PlayerId];
                        PlayerHP[pc.PlayerId] = Math.Min(PlayerHPMax[pc.PlayerId], PlayerHP[pc.PlayerId]);
                        SendRPCSyncKBPlayer(pc.PlayerId);
                        notifyRoles = true;
                    }

                    if (pc.SoloAlive() && !pc.inVent)
                    {
                        var pos = Pelican.GetBlackRoomPS();
                        var dis = Vector2.Distance(pos, pc.Pos());
                        if (dis < 1.1f) PlayerRandomSpwan(pc);
                    }

                    if (BackCountdown.ContainsKey(pc.PlayerId))
                    {
                        BackCountdown[pc.PlayerId]--;
                        if (BackCountdown[pc.PlayerId] <= 0)
                            OnPlayerBack(pc);
                        SendRPCSyncKBBackCountdown(pc);
                        notifyRoles = true;
                    }

                    if (NameNotify.ContainsKey(pc.PlayerId) && NameNotify[pc.PlayerId].TIMESTAMP < Utils.TimeStamp)
                    {
                        NameNotify.Remove(pc.PlayerId);
                        SendRPCSyncNameNotify(pc);
                        notifyRoles = true;
                    }

                    if (notifyRoles) Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
                }
            }
        }
    }

    // Options
    // ReSharper disable InconsistentNaming
    private static OptionItem KB_GameTime;
    public static OptionItem KB_ATKCooldown;
    private static OptionItem KB_HPMax;
    private static OptionItem KB_ATK;
    private static OptionItem KB_RecoverAfterSecond;
    private static OptionItem KB_RecoverPerSecond;
    private static OptionItem KB_ResurrectionWaitingTime;
    private static OptionItem KB_KillBonusMultiplier;

    private static OptionItem KB_BootVentWhenDead;
    // ReSharper restore InconsistentNaming
}