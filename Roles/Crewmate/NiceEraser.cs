using Hazel;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static TOHE.Translator;

namespace TOHE.Roles.Crewmate;

internal static class NiceEraser
{
    private static readonly int Id = 5580;
    public static List<byte> playerIdList = [];

    private static OptionItem EraseLimitOpt;
    public static OptionItem HideVote;

    private static List<byte> didVote = [];
    public static Dictionary<byte, int> EraseLimit = [];
    private static List<byte> PlayerToErase = [];

    public static void SetupCustomOption()
    {
        Options.SetupSingleRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.NiceEraser, 1);
        EraseLimitOpt = IntegerOptionItem.Create(Id + 12, "EraseLimit", new(1, 15, 1), 1, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.NiceEraser])
            .SetValueFormat(OptionFormat.Times);
        HideVote = BooleanOptionItem.Create(Id + 13, "NiceEraserHideVote", false, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.NiceEraser]);
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
        Logger.Info($"{Utils.GetPlayerById(playerId)?.GetNameWithRole().RemoveHtmlTags()} : 剩余{EraseLimit[playerId]}次", "NiceEraser");
    }
    public static bool IsEnable => playerIdList.Any();
    public static void SendRPC(byte playerId)
    {
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
    public static string GetProgressText(byte playerId) => Utils.ColorString(EraseLimit[playerId] > 0 ? Utils.GetRoleColor(CustomRoles.NiceEraser) : Color.gray, EraseLimit.TryGetValue(playerId, out var x) ? $"({x})" : "Invalid");

    public static void OnVote(PlayerControl player, PlayerControl target)
    {
        if (player == null || target == null) return;
        if (didVote.Contains(player.PlayerId)) return;
        didVote.Add(player.PlayerId);

        if (EraseLimit.ContainsKey(player.PlayerId) && EraseLimit[player.PlayerId] < 1) return;

        if (target.PlayerId == player.PlayerId)
        {
            Utils.SendMessage(GetString("EraserEraseSelf"), player.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.NiceEraser), GetString("EraserEraseMsgTitle")));
            return;
        }

        if (target.GetCustomRole().IsNeutral())
        {
            Utils.SendMessage(string.Format(GetString("EraserEraseNeutralNotice"), target.GetRealName()), player.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.NiceEraser), GetString("EraserEraseMsgTitle")));
            return;
        }

        if (EraseLimit.ContainsKey(player.PlayerId)) EraseLimit[player.PlayerId]--;
        SendRPC(player.PlayerId);

        if (!PlayerToErase.Contains(target.PlayerId))
            PlayerToErase.Add(target.PlayerId);

        Utils.SendMessage(string.Format(GetString("EraserEraseNotice"), target.GetRealName()), player.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.NiceEraser), GetString("EraserEraseMsgTitle")));

        Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: target);
    }
    public static void OnReportDeadBody()
    {
        PlayerToErase = [];
        didVote = [];
    }
    public static void AfterMeetingTasks()
    {
        foreach (byte pc in PlayerToErase.ToArray())
        {
            var player = Utils.GetPlayerById(pc);
            if (player == null) continue;
            player.RpcSetCustomRole(CustomRolesHelper.GetErasedRole(player.GetCustomRole()));
            NameNotifyManager.Notify(player, GetString("LostRoleByNiceEraser"));
            Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags()} 被擦除了", "NiceEraser");
            player.MarkDirtySettings();
        }
    }
}
