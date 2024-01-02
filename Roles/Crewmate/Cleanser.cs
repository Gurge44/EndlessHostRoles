using Hazel;
using System.Collections.Generic;
using UnityEngine;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Crewmate;
public static class Cleanser
{
    private static readonly int Id = 23420;
    public static List<byte> playerIdList = [];
    public static Dictionary<byte, byte> CleanserTarget = [];
    public static Dictionary<byte, int> CleanserUses = [];
    public static List<byte> CleansedPlayers = [];
    public static Dictionary<byte, bool> DidVote = [];

    public static OptionItem CleanserUsesOpt;
    public static OptionItem CleansedCanGetAddon;
    public static OptionItem CancelVote;

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Cleanser);
        CleanserUsesOpt = IntegerOptionItem.Create(Id + 10, "MaxCleanserUses", new(1, 14, 1), 3, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Cleanser])
            .SetValueFormat(OptionFormat.Times);
        CleansedCanGetAddon = BooleanOptionItem.Create(Id + 11, "CleansedCanGetAddon", false, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Cleanser]);
        CancelVote = CreateVoteCancellingUseSetting(Id + 12, CustomRoles.Cleanser, TabGroup.CrewmateRoles);
    }
    public static void Init()
    {
        playerIdList = [];
        CleanserTarget = [];
        CleanserUses = [];
        CleansedPlayers = [];
        DidVote = [];
    }

    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        CleanserTarget.Add(playerId, byte.MaxValue);
        CleanserUses.Add(playerId, 0);
        DidVote.Add(playerId, false);
    }

    public static bool IsEnable => playerIdList.Count > 0;

    //public static string GetProgressText(byte playerId) => Utils.ColorString(CleanserUsesOpt.GetInt() - CleanserUses[playerId] > 0 ? Utils.GetRoleColor(CustomRoles.Cleanser).ShadeColor(0.25f) : Color.gray, CleanserUses.TryGetValue(playerId, out var x) ? $"({CleanserUsesOpt.GetInt() - x})" : "Invalid");
    public static string GetProgressText(byte playerId)
    {
        if (!CleanserUses.ContainsKey(playerId)) return "Invalid";
        Color x;
        if (CleanserUsesOpt.GetInt() - CleanserUses[playerId] > 0)
            x = Utils.GetRoleColor(CustomRoles.Cleanser);
        else x = Color.gray;
        return Utils.ColorString(x, $"({CleanserUsesOpt.GetInt() - CleanserUses[playerId]})");
    }
    public static void SendRPC(byte playerId)
    {
        if (!IsEnable || !Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetCleanserCleanLimit, SendOption.Reliable, -1);
        writer.Write(playerId);
        writer.Write(CleanserUses[playerId]);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        byte CleanserId = reader.ReadByte();
        int Limit = reader.ReadInt32();
        if (CleanserUses.ContainsKey(CleanserId))
            CleanserUses[CleanserId] = Limit;
        else
            CleanserUses.Add(CleanserId, 0);
    }

    public static bool OnVote(PlayerControl voter, PlayerControl target)
    {
        if (!voter.Is(CustomRoles.Cleanser)) return false;
        if (DidVote[voter.PlayerId] || Main.DontCancelVoteList.Contains(voter.PlayerId)) return false;
        DidVote[voter.PlayerId] = true;
        if (CleanserUses[voter.PlayerId] >= CleanserUsesOpt.GetInt()) return false;
        if (target.PlayerId == voter.PlayerId)
        {
            Utils.SendMessage(GetString("CleanserRemoveSelf"), voter.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Cleanser), GetString("CleanserTitle")));
            return false;
        }
        if (CleanserTarget[voter.PlayerId] != byte.MaxValue) return false;

        CleanserUses[voter.PlayerId]++;
        CleanserTarget[voter.PlayerId] = target.PlayerId;
        Logger.Info($"{voter.GetNameWithRole().RemoveHtmlTags()} cleansed {target.GetNameWithRole().RemoveHtmlTags()}", "Cleansed");
        CleansedPlayers.Add(target.PlayerId);
        Utils.SendMessage(string.Format(GetString("CleanserRemovedRole"), target.GetRealName()), voter.PlayerId, title: Utils.ColorString(Utils.GetRoleColor(CustomRoles.Cleanser), GetString("CleanserTitle")));
        SendRPC(voter.PlayerId);

        Main.DontCancelVoteList.Add(voter.PlayerId);
        return true;
    }

    public static void AfterMeetingTasks()
    {
        foreach (var pid in CleanserTarget.Keys)
        {
            DidVote[pid] = false;
            if (pid == byte.MaxValue) continue;
            var targetid = CleanserTarget[pid];
            if (targetid == byte.MaxValue) continue;
            var targetpc = Utils.GetPlayerById(targetid);
            if (targetpc == null) continue;
            //var allAddons = targetpc.GetCustomSubRoles();
            targetpc.RpcSetCustomRole(CustomRoles.Cleansed);
            Logger.Info($"Removed all the add ons of {targetpc.GetNameWithRole().RemoveHtmlTags()}", "Cleanser");
            CleanserTarget[pid] = byte.MaxValue;
            targetpc.Notify(GetString("LostAddonByCleanser"));
            targetpc.MarkDirtySettings();
        }
        //Utils.MarkEveryoneDirtySettings();
    }

}