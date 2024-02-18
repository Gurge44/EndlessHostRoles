using Hazel;
using System.Collections.Generic;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Crewmate;

public static class Jailor
{
    private static readonly int Id = 63420;
    public static List<byte> playerIdList = [];
    public static Dictionary<byte, byte> JailorTarget = [];
    public static Dictionary<byte, int> JailorExeLimit = [];
    public static Dictionary<byte, bool> JailorHasExe = [];
    public static Dictionary<byte, bool> JailorDidVote = [];

    public static OptionItem JailCooldown;
    public static OptionItem notifyJailedOnMeeting;
    public static OptionItem UsePet;

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Jailor);
        JailCooldown = FloatOptionItem.Create(Id + 10, "JailorJailCooldown", new(0f, 60f, 1f), 15f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Jailor])
            .SetValueFormat(OptionFormat.Seconds);
        notifyJailedOnMeeting = BooleanOptionItem.Create(Id + 18, "notifyJailedOnMeeting", true, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Jailor]);
        UsePet = CreatePetUseSetting(Id + 11, CustomRoles.Jailor);
    }

    public static void Init()
    {
        playerIdList = [];
        JailorExeLimit = [];
        JailorTarget = [];
        JailorHasExe = [];
        JailorDidVote = [];
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        JailorTarget.Add(playerId, byte.MaxValue);
        JailorHasExe.Add(playerId, false);
        JailorDidVote.Add(playerId, false);

        if (!AmongUsClient.Instance.AmHost || (UsePets.GetBool() && UsePet.GetBool())) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }
    public static bool IsEnable => playerIdList.Count > 0;
    public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = Utils.GetPlayerById(id).IsAlive() ? JailCooldown.GetFloat() : 0f;
    public static string GetProgressText(byte playerId) => Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jailor).ShadeColor(0.25f), JailorExeLimit.TryGetValue(playerId, out var exeLimit) ? $"({exeLimit})" : "Invalid");


    public static void SendRPC(byte jailerId, byte targetId = byte.MaxValue, bool setTarget = true)
    {
        if (!IsEnable || !Utils.DoRPC) return;
        MessageWriter writer;
        if (!setTarget)
        {
            writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetJailorExeLimit, SendOption.Reliable);
            writer.Write(jailerId);
            writer.Write(JailorExeLimit[jailerId]);
            writer.Write(JailorHasExe[jailerId]);
            writer.Write(JailorDidVote[jailerId]);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
            return;
        }

        writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetJailorTarget, SendOption.Reliable);
        writer.Write(jailerId);
        writer.Write(targetId);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader, bool setTarget = true)
    {
        byte jailerId = reader.ReadByte();
        if (!setTarget)
        {
            _ = reader.ReadInt32();
            //if (JailorExeLimit.ContainsKey(jailerId)) JailorExeLimit[jailerId] = points;
            //else JailorExeLimit.Add(jailerId, MaxExecution.GetInt());

            bool executed = reader.ReadBoolean();
            if (!JailorHasExe.TryAdd(jailerId, false)) JailorHasExe[jailerId] = executed;

            bool didvote = reader.ReadBoolean();
            if (!JailorDidVote.TryAdd(jailerId, false)) JailorDidVote[jailerId] = didvote;

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
        SendRPC(killer.PlayerId, target.PlayerId);
        return false;
    }

    public static void OnReportDeadBody()
    {
        if (!notifyJailedOnMeeting.GetBool()) return;
        foreach (var targetId in JailorTarget.Values)
        {
            if (targetId == byte.MaxValue) continue;
            var tpc = Utils.GetPlayerById(targetId);
            if (tpc == null) continue;
            if (tpc.IsAlive())
            {
                _ = new LateTask(() =>
                {
                    Utils.SendMessage(GetString("JailedNotifyMsg"), targetId, title: Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jailor), GetString("JailorTitle")));
                }, 0.3f, "JailorNotifyJailed");
            }
        }
    }

}