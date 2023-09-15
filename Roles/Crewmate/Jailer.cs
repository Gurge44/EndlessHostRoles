using Hazel;
using System.Collections.Generic;
using System.Linq;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Crewmate;

public static class Jailor
{
    private static readonly int Id = 63420;
    public static List<byte> playerIdList = new();
    public static Dictionary<byte, byte> JailorTarget = new();
    public static Dictionary<byte, int> JailorExeLimit = new();
    public static Dictionary<byte, bool> JailorHasExe = new();
    public static Dictionary<byte, bool> JailorDidVote = new();

    public static OptionItem JailCooldown;
    public static OptionItem MaxExecution;
    public static OptionItem NBCanBeExe;
    public static OptionItem NCCanBeExe;
    public static OptionItem NECanBeExe;
    public static OptionItem NKCanBeExe;
    public static OptionItem CKCanBeExe;
    public static OptionItem notifyJailedOnMeeting;


    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Jailor);
        JailCooldown = FloatOptionItem.Create(Id + 10, "JailorJailCooldown", new(0f, 999f, 1f), 15f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Jailor])
            .SetValueFormat(OptionFormat.Seconds);
        //MaxExecution = IntegerOptionItem.Create(Id + 11, "JailorMaxExecution", new(1, 14, 1), 3, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Jailor])
        //    .SetValueFormat(OptionFormat.Times);
        //NBCanBeExe = BooleanOptionItem.Create(Id + 12, "JailorNBCanBeExe", true, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Jailor]);
        //NCCanBeExe = BooleanOptionItem.Create(Id + 13, "JailorNCCanBeExe", true, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Jailor]);
        //NECanBeExe = BooleanOptionItem.Create(Id + 14, "JailorNECanBeExe", true, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Jailor]);
        //NKCanBeExe = BooleanOptionItem.Create(Id + 15, "JailorNKCanBeExe", true, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Jailor]);
        //CKCanBeExe = BooleanOptionItem.Create(Id + 16, "JailorCKCanBeExe", false, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Jailor]);
        //CovenCanBeExe = BooleanOptionItem.Create(Id + 17, "JailorCovenCanBeExe", true, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Jailor]);
        notifyJailedOnMeeting = BooleanOptionItem.Create(Id + 18, "notifyJailedOnMeeting", true, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Jailor]);
    }

    public static void Init()
    {
        playerIdList = new();
        JailorExeLimit = new();
        JailorTarget = new();
        JailorHasExe = new();
        JailorDidVote = new();
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        JailorExeLimit.Add(playerId, MaxExecution.GetInt());
        JailorTarget.Add(playerId, byte.MaxValue);
        JailorHasExe.Add(playerId, false);
        JailorDidVote.Add(playerId, false);

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }
    public static bool IsEnable => playerIdList.Any();
    public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = Utils.GetPlayerById(id).IsAlive() ? JailCooldown.GetFloat() : 0f;
    public static string GetProgressText(byte playerId) => Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jailor).ShadeColor(0.25f), JailorExeLimit.TryGetValue(playerId, out var exeLimit) ? $"({exeLimit})" : "Invalid");


    public static void SendRPC(byte jailerId, byte targetId = byte.MaxValue, bool setTarget = true)
    {
        MessageWriter writer;
        if (!setTarget)
        {
            writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetJailorExeLimit, SendOption.Reliable, -1);
            writer.Write(jailerId);
            writer.Write(JailorExeLimit[jailerId]);
            writer.Write(JailorHasExe[jailerId]);
            writer.Write(JailorDidVote[jailerId]);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
            return;
        }
        writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetJailorTarget, SendOption.Reliable, -1);
        writer.Write(jailerId);
        writer.Write(targetId);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader, bool setTarget = true)
    {
        byte jailerId = reader.ReadByte();
        if (!setTarget)
        {
            int points = reader.ReadInt32();
            if (JailorExeLimit.ContainsKey(jailerId)) JailorExeLimit[jailerId] = points;
            else JailorExeLimit.Add(jailerId, MaxExecution.GetInt());

            bool executed = reader.ReadBoolean();
            if (JailorHasExe.ContainsKey(jailerId)) JailorHasExe[jailerId] = executed;
            else JailorHasExe.Add(jailerId, false);

            bool didvote = reader.ReadBoolean();
            if (JailorDidVote.ContainsKey(jailerId)) JailorDidVote[jailerId] = didvote;
            else JailorDidVote.Add(jailerId, false);

            return;
        }

        byte targetId = reader.ReadByte();
        JailorTarget[jailerId] = targetId;
    }

    public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (!killer.Is(CustomRoles.Jailor)) return true;
        if (killer == null || target == null) return true;
        if (JailorTarget[killer.PlayerId] != byte.MaxValue)
        {
            killer.Notify(GetString("JailorTargetAlreadySelected"));
            return false;
        }
        JailorTarget[killer.PlayerId] = target.PlayerId;
        killer.Notify(GetString("SuccessfullyJailed"));
        killer.ResetKillCooldown();
        killer.SetKillCooldown();
        SendRPC(killer.PlayerId, target.PlayerId, true);
        return false;
    }

    public static void OnReportDeadBody()
    {
        foreach (var targetId in JailorTarget.Values)
        {
            if (targetId == byte.MaxValue) continue;
            var tpc = Utils.GetPlayerById(targetId);
            if (tpc == null) continue;
            if (notifyJailedOnMeeting.GetBool() && tpc.IsAlive())
                _ = new LateTask(() =>
                {
                    Utils.SendMessage(GetString("JailedNotifyMsg"), targetId, title: Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jailor), GetString("JailorTitle")));
                }, 0.3f, "JailorNotifyJailed");
        }
    }

    public static void OnVote(PlayerControl voter, PlayerControl target)
    {
        if (voter == null || target == null) return;
        if (!voter.Is(CustomRoles.Jailor)) return;
        if (JailorDidVote[voter.PlayerId]) return;
        if (JailorTarget[voter.PlayerId] == byte.MaxValue) return;
        JailorDidVote[voter.PlayerId] = true;
        if (target.PlayerId == JailorTarget[voter.PlayerId])
        {
            if (JailorExeLimit[voter.PlayerId] > 0)
            {
                JailorExeLimit[voter.PlayerId] = JailorExeLimit[voter.PlayerId] - 1;
                JailorHasExe[voter.PlayerId] = true;
            }
            else JailorHasExe[voter.PlayerId] = false;
        }
        SendRPC(voter.PlayerId, setTarget: false);
    }

    public static bool CanBeExecuted(this CustomRoles role)
    {
        return (role.IsNB() && NBCanBeExe.GetBool()) ||
                (role.IsNC() && NCCanBeExe.GetBool()) ||
                (role.IsNE() && NCCanBeExe.GetBool()) ||
                (role.IsNK() && NKCanBeExe.GetBool()) ||
                (role.IsCK() && CKCanBeExe.GetBool()) ||
                role.IsImpostorTeamV3();
    }

    public static void AfterMeetingTasks()
    {
        foreach (var pid in JailorHasExe.Keys)
        {
            var targetId = JailorTarget[pid];
            if (targetId != byte.MaxValue && JailorHasExe[pid])
            {
                var tpc = Utils.GetPlayerById(targetId);
                if (tpc.IsAlive())
                {
                    CheckForEndVotingPatch.TryAddAfterMeetingDeathPlayers(PlayerState.DeathReason.Execution, targetId);
                    tpc.SetRealKiller(Utils.GetPlayerById(pid));
                }
                if (!tpc.GetCustomRole().CanBeExecuted())
                {
                    JailorExeLimit[pid] = 0;
                    SendRPC(pid, setTarget: false);
                }
            }
            JailorHasExe[pid] = false;
            JailorTarget[pid] = byte.MaxValue;
            JailorDidVote[pid] = false;
            SendRPC(pid, byte.MaxValue, setTarget: true);
        }
    }

}