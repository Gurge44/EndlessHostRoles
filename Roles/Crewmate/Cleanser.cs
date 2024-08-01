using System.Collections.Generic;
using EHR.Modules;
using Hazel;
using UnityEngine;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Crewmate;

public class Cleanser : RoleBase
{
    private const int Id = 23420;
    public static List<byte> playerIdList = [];
    public static List<byte> CleansedPlayers = [];
    public static Dictionary<byte, bool> DidVote = [];

    public static OptionItem CleanserUsesOpt;
    public static OptionItem CleansedCanGetAddon;
    public static OptionItem CancelVote;
    private byte CleanserId;

    public byte CleanserTarget = byte.MaxValue;
    public int CleanserUses;

    public override bool IsEnable => playerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Cleanser);
        CleanserUsesOpt = new IntegerOptionItem(Id + 10, "MaxCleanserUses", new(1, 14, 1), 3, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Cleanser])
            .SetValueFormat(OptionFormat.Times);
        CleansedCanGetAddon = new BooleanOptionItem(Id + 11, "CleansedCanGetAddon", false, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Cleanser]);
        CancelVote = CreateVoteCancellingUseSetting(Id + 12, CustomRoles.Cleanser, TabGroup.CrewmateRoles);
    }

    public override void Init()
    {
        playerIdList = [];
        CleanserTarget = byte.MaxValue;
        CleanserUses = 0;
        CleansedPlayers = [];
        DidVote = [];
        CleanserId = byte.MaxValue;
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        CleanserTarget = byte.MaxValue;
        CleanserUses = 0;
        DidVote.Add(playerId, false);
        CleanserId = playerId;
    }

    //public static string GetProgressText(byte playerId) => Utils.ColorString(CleanserUsesOpt.GetInt() - CleanserUses[playerId] > 0 ? Utils.GetRoleColor(CustomRoles.Cleanser).ShadeColor(0.25f) : Color.gray, CleanserUses.TryGetValue(playerId, out var x) ? $"({CleanserUsesOpt.GetInt() - x})" : "Invalid");
    public override string GetProgressText(byte playerId, bool comms)
    {
        Color x = CleanserUsesOpt.GetInt() - CleanserUses > 0 ? Utils.GetRoleColor(CustomRoles.Cleanser) : Color.gray;
        return Utils.ColorString(x, $"({CleanserUsesOpt.GetInt() - CleanserUses})");
    }

    public void SendRPC(byte playerId)
    {
        if (!IsEnable || !Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetCleanserCleanLimit, SendOption.Reliable);
        writer.Write(playerId);
        writer.Write(CleanserUses);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        byte CleanserId = reader.ReadByte();
        if (Main.PlayerStates[CleanserId].Role is not Cleanser cs) return;
        int Limit = reader.ReadInt32();
        cs.CleanserUses = Limit;
    }

    public override bool OnVote(PlayerControl voter, PlayerControl target)
    {
        if (DidVote[voter.PlayerId] || Main.DontCancelVoteList.Contains(voter.PlayerId)) return false;
        DidVote[voter.PlayerId] = true;
        if (CleanserUses >= CleanserUsesOpt.GetInt()) return false;
        if (target.PlayerId == voter.PlayerId)
        {
            Utils.SendMessage(GetString("CleanserRemoveSelf"), voter.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Cleanser), GetString("CleanserTitle")));
            return false;
        }

        if (CleanserTarget != byte.MaxValue) return false;

        CleanserUses++;
        CleanserTarget = target.PlayerId;
        Logger.Info($"{voter.GetNameWithRole().RemoveHtmlTags()} cleansed {target.GetNameWithRole().RemoveHtmlTags()}", "Cleansed");
        CleansedPlayers.Add(target.PlayerId);
        Utils.SendMessage(string.Format(GetString("CleanserRemovedRole"), target.GetRealName()), voter.PlayerId, title: Utils.ColorString(Utils.GetRoleColor(CustomRoles.Cleanser), GetString("CleanserTitle")));
        SendRPC(voter.PlayerId);

        Main.DontCancelVoteList.Add(voter.PlayerId);
        return true;
    }

    public override void AfterMeetingTasks()
    {
        if (CleanserTarget == byte.MaxValue || CleanserId == byte.MaxValue) return;
        DidVote[CleanserId] = false;
        var targetPc = Utils.GetPlayerById(CleanserTarget);
        if (targetPc == null) return;
        targetPc.RpcSetCustomRole(CustomRoles.Cleansed);
        Logger.Info($"Removed all the add ons of {targetPc.GetNameWithRole().RemoveHtmlTags()}", "Cleanser");
        CleanserTarget = byte.MaxValue;
        targetPc.MarkDirtySettings();
        targetPc.Notify(GetString("LostAddonByCleanser"));
    }
}