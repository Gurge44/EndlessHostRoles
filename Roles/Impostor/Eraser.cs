using Hazel;
using System.Collections.Generic;
using TOHE.Roles.Crewmate;
using UnityEngine;
using static TOHE.Translator;

namespace TOHE.Roles.Impostor;

internal static class Eraser
{
    private static readonly int Id = 16800;
    public static List<byte> playerIdList = [];

    public static readonly string[] EraseMode =
    [
        "EKill",
        "EVote"
    ];

    public static readonly string[] WhenTargetIsNeutralAction =
    [
        "E2Block",
        "E2Kill"
    ];

    private static OptionItem EraseLimitOpt;
    private static OptionItem EraseMethod;
    private static OptionItem WhenTargetIsNeutral;
    public static OptionItem HideVote;

    private static List<byte> didVote = [];
    private static Dictionary<byte, int> EraseLimit = [];
    private static List<byte> PlayerToErase = [];

    public static void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.OtherRoles, CustomRoles.Eraser);
        EraseMethod = StringOptionItem.Create(Id + 10, "EraseMethod", EraseMode, 0, TabGroup.OtherRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Eraser]);
        WhenTargetIsNeutral = StringOptionItem.Create(Id + 11, "WhenTargetIsNeutral", WhenTargetIsNeutralAction, 0, TabGroup.OtherRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Eraser]);
        EraseLimitOpt = IntegerOptionItem.Create(Id + 12, "EraseLimit", new(1, 15, 1), 1, TabGroup.OtherRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Eraser])
            .SetValueFormat(OptionFormat.Times);
        HideVote = BooleanOptionItem.Create(Id + 13, "EraserHideVote", false, TabGroup.OtherRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Eraser]);
    }
    public static void Init()
    {
        playerIdList = [];
        EraseLimit = [];
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        EraseLimit.TryAdd(playerId, EraseLimitOpt.GetInt());
        Logger.Info($"{Utils.GetPlayerById(playerId)?.GetNameWithRole().RemoveHtmlTags()} : 剩余{EraseLimit[playerId]}次", "Eraser");
    }
    public static bool IsEnable => playerIdList.Count > 0;
    private static void SendRPC(byte playerId)
    {
        if (!IsEnable || !Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetEraseLimit, SendOption.Reliable, -1);
        writer.Write(playerId);
        writer.Write(EraseLimit[playerId]);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ReceiveRPC(MessageReader reader)
    {
        byte PlayerId = reader.ReadByte();
        int Limit = reader.ReadInt32();
        if (EraseLimit.ContainsKey(PlayerId))
            EraseLimit[PlayerId] = Limit;
        else
            EraseLimit.Add(PlayerId, 0);
    }
    public static string GetProgressText(byte playerId) => Utils.ColorString(EraseLimit[playerId] > 0 ? Utils.GetRoleColor(CustomRoles.Eraser) : Color.gray, EraseLimit.TryGetValue(playerId, out var x) ? $"({x})" : "Invalid");

    public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (Medic.ProtectList.Contains(target.PlayerId)) return false;
        if (EraseLimit[killer.PlayerId] < 1) return true;
        if (target.PlayerId == killer.PlayerId) return true;
        if (CustomRolesHelper.IsNeutral(target.GetCustomRole()) && EraseMethod.GetInt() == 0)
        {
            killer.Notify(GetString("EraserEraseNeutralNotice"));
            if (WhenTargetIsNeutral.GetInt() == 1) return true;
            else
            {
                killer.SetKillCooldown(5f);
                return false;
            }
        }

        if (EraseMethod.GetInt() == 0)
        {
            if (EraseLimit[killer.PlayerId] < 1) return true;
            return killer.CheckDoubleTrigger(target, () =>
            {
                EraseLimit[killer.PlayerId]--;
                killer.SetKillCooldown();
                killer.Notify(GetString("TargetErasedInRound"));
                if (!PlayerToErase.Contains(target.PlayerId))
                    PlayerToErase.Add(target.PlayerId);
            });
        }
        return false;
    }

    public static void OnVote(PlayerControl player, PlayerControl target)
    {
        if (player == null || target == null || EraseMethod.GetInt() == 0) return;
        if (didVote.Contains(player.PlayerId)) return;
        didVote.Add(player.PlayerId);

        if (EraseLimit.ContainsKey(player.PlayerId) && EraseLimit[player.PlayerId] < 1) return;

        if (target.PlayerId == player.PlayerId)
        {
            Utils.SendMessage(GetString("EraserEraseSelf"), player.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Eraser), GetString("EraserEraseMsgTitle")));
            return;
        }

        if (target.GetCustomRole().IsNeutral())
        {
            Utils.SendMessage(string.Format(GetString("EraserEraseNeutralNotice"), target.GetRealName()), player.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Eraser), GetString("EraserEraseMsgTitle")));
            return;
        }

        if (EraseLimit.ContainsKey(player.PlayerId)) EraseLimit[player.PlayerId]--;
        SendRPC(player.PlayerId);

        if (!PlayerToErase.Contains(target.PlayerId))
            PlayerToErase.Add(target.PlayerId);

        Utils.SendMessage(string.Format(GetString("EraserEraseNotice"), target.GetRealName()), player.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Eraser), GetString("EraserEraseMsgTitle")));

        Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: target);
    }
    public static void OnReportDeadBody()
    {
        //PlayerToErase = new();
        didVote = [];
    }
    public static void AfterMeetingTasks()
    {
        foreach (byte pc in PlayerToErase.ToArray())
        {
            var player = Utils.GetPlayerById(pc);
            if (player == null)
                continue;
            player.RpcSetCustomRole(CustomRolesHelper.GetErasedRole(player.GetCustomRole()));
            NameNotifyManager.Notify(player, GetString("LostRoleByEraser"));
            Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags()} 被擦除了", "Eraser");
            player.MarkDirtySettings();
        }
        PlayerToErase = [];
    }
}
