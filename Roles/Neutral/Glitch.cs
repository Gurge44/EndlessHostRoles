using AmongUs.GameOptions;
using Hazel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static TOHE.Options;

namespace TOHE.Roles.Neutral;

public static class Glitch
{
    private static readonly int Id = 18125;
    public static List<byte> playerIdList = [];

    public static Dictionary<byte, long> hackedIdList = [];

    public static OptionItem KillCooldown;
    public static OptionItem HackCooldown;
    public static OptionItem HackDuration;
    public static OptionItem MimicCooldown;
    public static OptionItem MimicDuration;
    public static OptionItem CanVent;
    public static OptionItem CanVote;
    private static OptionItem HasImpostorVision;

    public static int HackCDTimer;
    public static int KCDTimer;
    public static int MimicCDTimer;
    public static int MimicDurTimer;
    public static long LastHack;
    public static long LastKill;
    public static long LastMimic;

    private static bool isShifted;
    //    public static OptionItem CanUseSabotage;

    public static void SetupCustomOption()
    {
        //Glitchは1人固定
        SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Glitch, 1, zeroOne: false);
        KillCooldown = IntegerOptionItem.Create(Id + 10, "KillCooldown", new(0, 180, 1), 25, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Glitch])
            .SetValueFormat(OptionFormat.Seconds);
        HackCooldown = IntegerOptionItem.Create(Id + 11, "HackCooldown", new(0, 180, 1), 20, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Glitch])
            .SetValueFormat(OptionFormat.Seconds);
        HackDuration = FloatOptionItem.Create(Id + 14, "HackDuration", new(0f, 60f, 1f), 15f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Glitch])
            .SetValueFormat(OptionFormat.Seconds);
        MimicCooldown = IntegerOptionItem.Create(Id + 15, "MimicCooldown", new(0, 180, 1), 30, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Glitch])
            .SetValueFormat(OptionFormat.Seconds);
        MimicDuration = FloatOptionItem.Create(Id + 16, "MimicDuration", new(0f, 60f, 1f), 10f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Glitch])
            .SetValueFormat(OptionFormat.Seconds);
        CanVent = BooleanOptionItem.Create(Id + 12, "CanVent", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Glitch]);
        CanVote = BooleanOptionItem.Create(Id + 17, "CanVote", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Glitch]);
        HasImpostorVision = BooleanOptionItem.Create(Id + 13, "ImpostorVision", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Glitch]);
    }
    public static void Init()
    {
        playerIdList = [];
        hackedIdList = [];
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);

        HackCDTimer = 10;
        KCDTimer = 10;
        MimicCDTimer = 10;
        MimicDurTimer = 0;

        isShifted = false;

        var ts = Utils.GetTimeStamp();

        LastKill = ts;
        LastHack = ts;
        LastMimic = ts;

        _ = new LateTask(() =>
        {
            SendRPCSyncLongs(LastKill, LastHack, LastMimic);
            SendRPCSyncTimers(MimicCDTimer, MimicDurTimer, HackCDTimer, KCDTimer);
            SendRPCSyncSS(isShifted);
        }, 7f, "Glitch RPCs");

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }
    public static void SetHudActive(HudManager __instance, bool isActive)
    {
        __instance.SabotageButton.ToggleVisible(true);
    }

    public static bool IsEnable => playerIdList.Count > 0;
    public static void SendRPCSyncTimers(int mimicCDTimer, int mimicDurTimer, int hackCDTimer, int KCDTimer)
    {
        if (!IsEnable || !Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncGlitchTimers, SendOption.Reliable, -1);
        writer.Write(mimicCDTimer);
        writer.Write(mimicDurTimer);
        writer.Write(hackCDTimer);
        writer.Write(KCDTimer);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void SendRPCSyncMimic(int mimicCDTimer, int mimicDurTimer, long lastMimic)
    {
        if (!IsEnable || !Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncGlitchMimic, SendOption.Reliable, -1);
        writer.Write(mimicCDTimer);
        writer.Write(mimicDurTimer);
        writer.Write(lastMimic);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void SendRPCSyncLongs(long lastKill, long lastHack, long lastMimic)
    {
        if (!IsEnable || !Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncGlitchLongs, SendOption.Reliable, -1);
        writer.Write(lastKill);
        writer.Write(lastHack);
        writer.Write(lastMimic);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void SendRPCSyncKill(long lastKill, int KCDtimer)
    {
        if (!IsEnable || !Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncGlitchKill, SendOption.Reliable, -1);
        writer.Write(lastKill);
        writer.Write(KCDtimer);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void SendRPCSyncHack(long lastHack, int hackCDTimer)
    {
        if (!IsEnable || !Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncGlitchHack, SendOption.Reliable, -1);
        writer.Write(lastHack);
        writer.Write(hackCDTimer);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void SendRPCSyncSS(bool isShifted)
    {
        if (!IsEnable || !Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncGlitchSS, SendOption.Reliable, -1);
        writer.Write(isShifted);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ReceiveRPCSyncSS(MessageReader reader)
    {
        if (AmongUsClient.Instance.AmHost) return;

        isShifted = reader.ReadBoolean();
    }
    public static void ReceiveRPCSyncHack(MessageReader reader)
    {
        if (AmongUsClient.Instance.AmHost) return;

        LastHack = long.Parse(reader.ReadString());
        HackCDTimer = reader.ReadInt32();
    }
    public static void ReceiveRPCSyncKill(MessageReader reader)
    {
        if (AmongUsClient.Instance.AmHost) return;

        LastKill = long.Parse(reader.ReadString());
        KCDTimer = reader.ReadInt32();
    }
    public static void ReceiveRPCSyncLongs(MessageReader reader)
    {
        if (AmongUsClient.Instance.AmHost) return;

        LastKill = long.Parse(reader.ReadString());
        LastHack = long.Parse(reader.ReadString());
        LastMimic = long.Parse(reader.ReadString());
    }
    public static void ReceiveRPCSyncMimic(MessageReader reader)
    {
        if (AmongUsClient.Instance.AmHost) return;

        MimicCDTimer = reader.ReadInt32();
        MimicDurTimer = reader.ReadInt32();
        LastMimic = long.Parse(reader.ReadString());
    }
    public static void ReceiveRPCSyncTimers(MessageReader reader)
    {
        if (AmongUsClient.Instance.AmHost) return;

        MimicCDTimer = reader.ReadInt32();
        MimicDurTimer = reader.ReadInt32();
        HackCDTimer = reader.ReadInt32();
        KCDTimer = reader.ReadInt32();
    }
    public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = 1f;
    public static void ApplyGameOptions(IGameOptions opt) => opt.SetVision(HasImpostorVision.GetBool());
    public static void Mimic(PlayerControl pc)
    {
        if (pc == null) return;
        if (!pc.Is(CustomRoles.Glitch)) return;
        if (!pc.IsAlive()) return;
        if (MimicCDTimer > 0) return;
        if (isShifted) return;

        var playerlist = Main.AllAlivePlayerControls.Where(a => a.PlayerId != pc.PlayerId).ToArray();

        try
        {
            pc.RpcShapeshift(playerlist[IRandom.Instance.Next(0, playerlist.Length)], false);

            isShifted = true;
            SendRPCSyncSS(isShifted);
            LastMimic = Utils.GetTimeStamp();
            MimicCDTimer = MimicCooldown.GetInt();
            MimicDurTimer = MimicDuration.GetInt();
            SendRPCSyncMimic(MimicCDTimer, MimicDurTimer, LastMimic);
        }
        catch (System.Exception ex)
        {
            Logger.Error(ex.ToString(), "Glitch.Mimic.RpcShapeshift");
        }
    }
    public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer == null) return false;
        if (target == null) return false;

        if (KCDTimer > 0 && HackCDTimer > 0) return false;

        if (killer.CheckDoubleTrigger(target, () =>
        {
            if (HackCDTimer <= 0)
            {
                Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
                HackCDTimer = HackCooldown.GetInt();
                hackedIdList.TryAdd(target.PlayerId, Utils.GetTimeStamp());
                LastHack = Utils.GetTimeStamp();
                SendRPCSyncHack(LastHack, HackCDTimer);
            }
        }))
        {
            if (KCDTimer > 0) return false;
            LastKill = Utils.GetTimeStamp();
            KCDTimer = KillCooldown.GetInt();
            SendRPCSyncKill(LastKill, KCDTimer);
            return true;
        }
        else return false;
    }
    public static void UpdateHackCooldown(PlayerControl player)
    {
        if (HackCDTimer > 180 || HackCDTimer < 0) HackCDTimer = 0;
        if (KCDTimer > 180 || KCDTimer < 0) KCDTimer = 0;
        if (MimicCDTimer > 180 || MimicCDTimer < 0) MimicCDTimer = 0;
        if (MimicDurTimer > 180 || MimicDurTimer < 0) MimicDurTimer = 0;

        bool change = false;
        foreach (var pc in hackedIdList)
        {
            if (pc.Value + HackDuration.GetInt() < Utils.GetTimeStamp())
            {
                hackedIdList.Remove(pc.Key);
                change = true;
            }
        }

        if (player == null) return;
        if (!player.Is(CustomRoles.Glitch)) return;

        if (change) { Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player); }

        if (!player.IsAlive())
        {
            HackCDTimer = 0;
            KCDTimer = 0;
            MimicCDTimer = 0;
            MimicDurTimer = 0;
            return;
        }

        if (MimicDurTimer > 0)
        {
            try { MimicDurTimer = (int)(MimicDuration.GetInt() - (Utils.GetTimeStamp() - LastMimic)); }
            catch { MimicDurTimer = 0; }
            if (MimicDurTimer > 180) MimicDurTimer = 0;
        }
        if ((MimicDurTimer <= 0 || !GameStates.IsInTask) && isShifted)
        {
            try
            {
                player.RpcShapeshift(player, false);
                isShifted = false;
                SendRPCSyncSS(isShifted);
            }
            catch (System.Exception ex)
            {
                Logger.Error(ex.ToString(), "Glitch.Mimic.RpcRevertShapeshift");
            }
            if (!GameStates.IsInTask)
            {
                MimicDurTimer = 0;
            }
        }

        if (HackCDTimer <= 0 && KCDTimer <= 0 && MimicCDTimer <= 0 && MimicDurTimer <= 0) return;

        try { HackCDTimer = (int)(HackCooldown.GetInt() - (Utils.GetTimeStamp() - LastHack)); }
        catch { HackCDTimer = 0; }
        if (HackCDTimer > 180 || HackCDTimer < 0) HackCDTimer = 0;

        try { KCDTimer = (int)(KillCooldown.GetInt() - (Utils.GetTimeStamp() - LastKill)); }
        catch { KCDTimer = 0; }
        if (KCDTimer > 180 || KCDTimer < 0) KCDTimer = 0;

        try { MimicCDTimer = (int)(MimicCooldown.GetInt() + MimicDuration.GetInt() - (Utils.GetTimeStamp() - LastMimic)); }
        catch { MimicCDTimer = 0; }
        if (MimicCDTimer > 180 || MimicCDTimer < 0) MimicCDTimer = 0;

        if (!player.IsModClient())
        {
            var sb = new StringBuilder();

            if (MimicDurTimer > 0) sb.Append($"\n{string.Format(Translator.GetString("MimicDur"), MimicDurTimer)}");
            if (MimicCDTimer > 0 && MimicDurTimer <= 0) sb.Append($"\n{string.Format(Translator.GetString("MimicCD"), MimicCDTimer)}");
            if (HackCDTimer > 0) sb.Append($"\n{string.Format(Translator.GetString("HackCD"), HackCDTimer)}");
            if (KCDTimer > 0) sb.Append($"\n{string.Format(Translator.GetString("KCD"), KCDTimer)}");

            string ns = sb.ToString();

            if ((!NameNotifyManager.Notice.TryGetValue(player.PlayerId, out var a) || a.TEXT != ns) && ns != string.Empty) player.Notify(ns, 1.1f);
        }

        SendRPCSyncTimers(MimicCDTimer, MimicDurTimer, HackCDTimer, KCDTimer);
    }
    public static string GetHudText(PlayerControl player)
    {
        if (player == null) return string.Empty;
        if (!player.Is(CustomRoles.Glitch)) return string.Empty;
        if (!player.IsAlive()) return string.Empty;

        var sb = new StringBuilder();

        if (MimicDurTimer > 0) sb.Append($"{string.Format(Translator.GetString("MimicDur"), MimicDurTimer)}\n");
        if (MimicCDTimer > 0 && MimicDurTimer <= 0) sb.Append($"{string.Format(Translator.GetString("MimicCD"), MimicCDTimer)}\n");
        if (HackCDTimer > 0) sb.Append($"{string.Format(Translator.GetString("HackCD"), HackCDTimer)}\n");
        if (KCDTimer > 0) sb.Append($"{string.Format(Translator.GetString("KCD"), KCDTimer)}\n");

        return sb.ToString();
    }
    public static void AfterMeetingTasks()
    {
        var timestamp = Utils.GetTimeStamp();
        LastKill = timestamp;
        LastHack = timestamp;
        LastMimic = timestamp;
        KCDTimer = 10;
        HackCDTimer = 10;
        MimicCDTimer = 10;
        SendRPCSyncLongs(LastKill, LastHack, LastMimic);
        SendRPCSyncTimers(MimicCDTimer, MimicDurTimer, HackCDTimer, KCDTimer);
    }
}
