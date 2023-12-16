using AmongUs.GameOptions;
using HarmonyLib;
using Hazel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TOHE.Roles.Crewmate;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Neutral;

public static class Werewolf
{
    private static readonly int Id = 12850;
    public static List<byte> playerIdList = [];

    private static OptionItem KillCooldown;
    private static OptionItem HasImpostorVision;
    public static OptionItem RampageCD;
    public static OptionItem RampageDur;

    private static Dictionary<byte, long> RampageTime = [];
    public static Dictionary<byte, long> lastTime = [];
    private static int CD;

    public static void SetupCustomOption()
    {
        SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Werewolf, 1, zeroOne: false);
        KillCooldown = FloatOptionItem.Create(Id + 10, "KillCooldown", new(0f, 180f, 0.5f), 3f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Werewolf])
            .SetValueFormat(OptionFormat.Seconds);
        HasImpostorVision = BooleanOptionItem.Create(Id + 11, "ImpostorVision", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Werewolf]);
        RampageCD = FloatOptionItem.Create(Id + 12, "WWRampageCD", new(0f, 180f, 2.5f), 35f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Werewolf])
            .SetValueFormat(OptionFormat.Seconds);
        RampageDur = FloatOptionItem.Create(Id + 13, "WWRampageDur", new(0f, 180f, 1f), 12f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Werewolf])
            .SetValueFormat(OptionFormat.Seconds);
    }
    public static void Init()
    {
        playerIdList = [];
        CD = 0;
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }
    public static bool IsEnable => playerIdList.Count > 0;
    public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    public static void ApplyGameOptions(IGameOptions opt) => opt.SetVision(HasImpostorVision.GetBool());
    private static void SendRPC(PlayerControl pc)
    {
        if (pc.AmOwner || !IsEnable || !Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetWWTimer, SendOption.Reliable, pc.GetClientId());
        writer.Write((RampageTime.TryGetValue(pc.PlayerId, out var x) ? x : -1).ToString());
        writer.Write((lastTime.TryGetValue(pc.PlayerId, out var y) ? y : -1).ToString());
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ReceiveRPC(MessageReader reader)
    {
        RampageTime = [];
        lastTime = [];
        long rampage = long.Parse(reader.ReadString());
        long last = long.Parse(reader.ReadString());
        if (rampage > 0) RampageTime.Add(PlayerControl.LocalPlayer.PlayerId, rampage);
        if (last > 0) lastTime.Add(PlayerControl.LocalPlayer.PlayerId, last);
    }
    public static bool CanRampage(byte id)
        => GameStates.IsInTask && !RampageTime.ContainsKey(id) && !lastTime.ContainsKey(id);
    public static bool IsRampaging(byte id) => RampageTime.ContainsKey(id);

    private static long lastFixedTime;
    public static void AfterMeetingTasks()
    {
        lastTime = [];
        RampageTime = [];
        foreach (var pc in Main.AllAlivePlayerControls.Where(x => playerIdList.Contains(x.PlayerId)).ToArray())
        {
            lastTime.Add(pc.PlayerId, Utils.GetTimeStamp());
            SendRPC(pc);
        }
    }
    public static void OnFixedUpdate(PlayerControl player)
    {
        if (!GameStates.IsInTask || !IsEnable) return;
        if (player == null) return;
        if (!player.Is(CustomRoles.Werewolf)) return;

        var now = Utils.GetTimeStamp();

        if (lastTime.TryGetValue(player.PlayerId, out var WWtime) && !player.IsModClient())
        {
            var cooldown = WWtime + (long)RampageCD.GetFloat() - now;
            if ((int)cooldown != CD) player.Notify(string.Format(GetString("CDPT"), cooldown + 1), 1.1f);
            CD = (int)cooldown;
        }

        if (lastTime.TryGetValue(player.PlayerId, out var time) && time + (long)RampageCD.GetFloat() < now)
        {
            lastTime.Remove(player.PlayerId);
            if (!player.IsModClient()) player.Notify(GetString("WWCanRampage"));
            SendRPC(player);
            CD = 0;
        }

        if (lastFixedTime != now)
        {
            lastFixedTime = now;
            Dictionary<byte, long> newList = [];
            List<byte> refreshList = [];
            foreach (var it in RampageTime)
            {
                var pc = Utils.GetPlayerById(it.Key);
                if (pc == null) continue;
                var remainTime = it.Value + (long)RampageDur.GetFloat() - now;
                if (remainTime < 0)
                {
                    lastTime.Add(pc.PlayerId, now);
                    pc.Notify(GetString("WWRampageOut"));
                    SendRPC(pc);
                    continue;
                }
                else if (remainTime <= 10)
                {
                    if (!pc.IsModClient()) pc.Notify(string.Format(GetString("WWRampageCountdown"), remainTime + 1));
                }
                newList.Add(it.Key, it.Value);
            }
            RampageTime.Where(x => !newList.ContainsKey(x.Key)).Do(x => refreshList.Add(x.Key));
            RampageTime = newList;
            refreshList.Do(x => SendRPC(Utils.GetPlayerById(x)));
        }
    }

    public static void OnEnterVent(PlayerControl pc)
    {
        if (pc == null) return;
        if (!pc.Is(CustomRoles.Werewolf)) return;

        if (!AmongUsClient.Instance.AmHost || IsRampaging(pc.PlayerId)) return;
        _ = new LateTask(() =>
        {
            if (CanRampage(pc.PlayerId))
            {
                RampageTime.Add(pc.PlayerId, Utils.GetTimeStamp());
                SendRPC(pc);
                pc.Notify(GetString("WWRampaging"), RampageDur.GetFloat());
            }
            else return;
        }, 0.5f, "Werewolf Vent");
    }
    public static string GetHudText(PlayerControl pc)
    {
        if (pc == null || !GameStates.IsInTask || !PlayerControl.LocalPlayer.IsAlive() || !pc.Is(CustomRoles.Werewolf)) return string.Empty;
        var str = new StringBuilder();
        if (IsRampaging(pc.PlayerId))
        {
            var remainTime = RampageTime[pc.PlayerId] + (long)RampageDur.GetFloat() - Utils.GetTimeStamp();
            str.Append(string.Format(GetString("WWRampageCountdown"), remainTime + 1));
        }
        else if (lastTime.TryGetValue(pc.PlayerId, out var time))
        {
            var cooldown = time + (long)RampageCD.GetFloat() - Utils.GetTimeStamp();
            str.Append(string.Format(GetString("WWCD"), cooldown + 2));
        }
        else
        {
            str.Append(GetString("WWCanRampage"));
        }
        return str.ToString();
    }

    public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (Medic.ProtectList.Contains(target.PlayerId)) return false;
        if (!IsRampaging(killer.PlayerId)) return false;
        return true;
    }
}
