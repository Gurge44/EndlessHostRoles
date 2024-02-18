using HarmonyLib;
using Hazel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using static TOHE.Translator;

namespace TOHE.Roles.Crewmate;

public static class NiceSwapper
{
    private static readonly int Id = 1986523;

    public static OptionItem SwapMax;
    public static OptionItem HideMsg;
    public static OptionItem CanSwapSelf;
    public static OptionItem CanStartMeeting;
    public static OptionItem NiceSwapperAbilityUseGainWithEachTaskCompleted;
    public static OptionItem AbilityChargesWhenFinishedTasks;

    private static (byte, byte) SwapTargets;
    private static byte NiceSwapperId = byte.MaxValue;

    public static void SetupCustomOption()
    {
        Options.SetupSingleRoleOptions(Id, TabGroup.OtherRoles, CustomRoles.NiceSwapper, 1);
        SwapMax = IntegerOptionItem.Create(Id + 3, "NiceSwapperMax", new(0, 20, 1), 1, TabGroup.OtherRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.NiceSwapper])
            .SetValueFormat(OptionFormat.Times);
        CanSwapSelf = BooleanOptionItem.Create(Id + 2, "CanSwapSelfVotes", true, TabGroup.OtherRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.NiceSwapper]);
        CanStartMeeting = BooleanOptionItem.Create(Id + 4, "JesterCanUseButton", true, TabGroup.OtherRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.NiceSwapper]);
        NiceSwapperAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(Id + 6, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.1f), 0.3f, TabGroup.OtherRoles, false)
        .SetParent(Options.CustomRoleSpawnChances[CustomRoles.NiceSwapper])
        .SetValueFormat(OptionFormat.Times);
        AbilityChargesWhenFinishedTasks = FloatOptionItem.Create(Id + 7, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.1f), 0.2f, TabGroup.OtherRoles, false)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.NiceSwapper])
            .SetValueFormat(OptionFormat.Times);
        HideMsg = BooleanOptionItem.Create(Id + 5, "SwapperHideMsg", true, TabGroup.OtherRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.NiceSwapper]);
    }

    public static void Init()
    {
        SwapTargets = (byte.MaxValue, byte.MaxValue);
        NiceSwapperId = byte.MaxValue;
    }

    public static void Add(byte playerId)
    {
        NiceSwapperId = playerId;
        playerId.SetAbilityUseLimit(SwapMax.GetInt());
    }

    public static bool IsEnable => NiceSwapperId != byte.MaxValue;

    public static string ProgressText => Utils.GetAbilityUseLimitDisplay(NiceSwapperId);

    public static bool SwapMsg(PlayerControl pc, string msg, bool isUI = false)
    {
        var originMsg = msg;

        if (!AmongUsClient.Instance.AmHost || !GameStates.IsInGame || pc == null || pc.GetCustomRole() != CustomRoles.NiceSwapper) return false;
        if (!pc.IsAlive())
        {
            if (!isUI) Utils.SendMessage(GetString("SwapDead"), pc.PlayerId);
            pc.ShowPopUp(GetString("SwapDead"));
            return true;
        }
        if (pc.GetAbilityUseLimit() < 1)
        {
            if (!isUI) Utils.SendMessage(GetString("NiceSwapperTrialMax"), pc.PlayerId);
            pc.ShowPopUp(GetString("NiceSwapperTrialMax"));
            return true;
        }

        int operate = 0;
        msg = msg.ToLower().TrimStart().TrimEnd();
        if (CheckCommand(ref msg, "id|guesslist|gl编号|玩家编号|玩家id|id列表|玩家列表|列表|所有id|全部id")) operate = 1;
        else if (CheckCommand(ref msg, "sw|换票|换|swap|st", false)) operate = 2;
        else return false;

        switch (operate)
        {
            case 1:
                Utils.SendMessage(GuessManager.GetFormatString(), pc.PlayerId);
                return true;
            case 2:
                {
                    if (HideMsg.GetBool() && !isUI) ChatManager.SendPreviousMessagesToAll();
                    else if (pc.AmOwner && !isUI) Utils.SendMessage(originMsg, 255, pc.GetRealName());

                    if (!MsgToPlayerAndRole(msg, out byte targetId, out string error))
                    {
                        Utils.SendMessage(error, pc.PlayerId);
                        return true;
                    }

                    var target = Utils.GetPlayerById(targetId);
                    if (target == null) break;

                    bool targetIsntSelected = SwapTargets.Item1 != target.PlayerId && SwapTargets.Item2 != target.PlayerId; // Whether the picked target isn't already being swapped

                    bool Vote1Empty = (SwapTargets.Item1 == byte.MaxValue) && targetIsntSelected; // Whether the first swapping slot is suitable to swap this target
                    bool Vote2Available = (SwapTargets.Item1 != byte.MaxValue) && (SwapTargets.Item2 == byte.MaxValue) && targetIsntSelected; // Whether the second swapping slot is suitable to swap this target

                    if (Vote1Empty && (CanSwapSelf.GetBool() || target.PlayerId != pc.PlayerId)) // Take first slot
                    {
                        SwapTargets.Item1 = target.PlayerId;

                        if (HideMsg.GetBool() && !isUI) ChatManager.SendPreviousMessagesToAll();
                        if (!isUI) Utils.SendMessage(GetString("Swap1"), pc.PlayerId);
                        Logger.Info($"{pc.GetNameWithRole().RemoveHtmlTags()} chose to swap {target.GetNameWithRole()} (first target)", "Swapper");
                    }

                    else if (Vote2Available && (CanSwapSelf.GetBool() || target.PlayerId != pc.PlayerId)) // Take second slot
                    {
                        SwapTargets.Item2 = target.PlayerId;

                        if (HideMsg.GetBool() && !isUI) ChatManager.SendPreviousMessagesToAll();
                        if (!isUI) Utils.SendMessage(GetString("Swap2"), pc.PlayerId);
                        Logger.Info($"{pc.GetNameWithRole().RemoveHtmlTags()} chose to swap {target.GetNameWithRole()} (second target)", "Swapper");
                    }

                    else if (target.PlayerId == SwapTargets.Item1) // If this player is already chosen to be swapped in the first slot, cancel it
                    {
                        SwapTargets.Item1 = byte.MaxValue;

                        if (HideMsg.GetBool() && !isUI) ChatManager.SendPreviousMessagesToAll();
                        if (!isUI) Utils.SendMessage(GetString("CancelSwap1"), pc.PlayerId);
                        Logger.Info($"{pc.GetNameWithRole().RemoveHtmlTags()} canceled swapping on {target.GetNameWithRole()} (first target)", "Swapper");
                    }

                    else if (target.PlayerId == SwapTargets.Item2) // If this player is already chosen to be swapped in the second slot, cancel it
                    {
                        SwapTargets.Item2 = byte.MaxValue;

                        if (HideMsg.GetBool() && !isUI) ChatManager.SendPreviousMessagesToAll();
                        if (!isUI) Utils.SendMessage(GetString("CancelSwap2"), pc.PlayerId);
                        Logger.Info($"{pc.GetNameWithRole().RemoveHtmlTags()} canceled swapping on {target.GetNameWithRole()} (second target)", "Swapper");
                    }

                    else if (pc.PlayerId == target.PlayerId && !CanSwapSelf.GetBool()) // When the Swapper tries to swap themselves but they aren't allowed to
                    {
                        if (HideMsg.GetBool() && !isUI) ChatManager.SendPreviousMessagesToAll();
                        if (!isUI) Utils.SendMessage(GetString("CantSwapSelf"), pc.PlayerId);
                        else pc.ShowPopUp(GetString("CantSwapSelf"));
                    }

                    _ = new LateTask(() => Utils.NotifyRoles(isForMeeting: true, NoCache: true), 0.2f);

                    break;
                }
        }
        return true;
    }

    public static void OnExileFinish()
    {
        if (SwapTargets != (byte.MaxValue, byte.MaxValue))
        {
            Utils.GetPlayerById(NiceSwapperId).RpcRemoveAbilityUse();
            SwapTargets = (byte.MaxValue, byte.MaxValue);
        }
    }

    public static void OnCheckForEndVoting()
    {
        if (!(SwapTargets != (byte.MaxValue, byte.MaxValue))) return;

        var playerStates = MeetingHud.Instance.playerStates;
        var votedFor2 = playerStates.Where(x => x.VotedFor == SwapTargets.Item2).ToList();

        playerStates.DoIf(x => x.VotedFor == SwapTargets.Item1, x =>
        {
            x.UnsetVote();
            if (x.TargetPlayerId == 0) MeetingHud.Instance.CmdCastVote(x.TargetPlayerId, SwapTargets.Item2);
            else MeetingHud.Instance.CastVote(x.TargetPlayerId, SwapTargets.Item2);
            x.VotedFor = SwapTargets.Item2;
        });
        playerStates.DoIf(votedFor2.Contains, x =>
        {
            x.UnsetVote();
            if (x.TargetPlayerId == 0) MeetingHud.Instance.CmdCastVote(x.TargetPlayerId, SwapTargets.Item1);
            else MeetingHud.Instance.CastVote(x.TargetPlayerId, SwapTargets.Item1);
            x.VotedFor = SwapTargets.Item1;
        });

        PlayerControl Target1 = Utils.GetPlayerById(SwapTargets.Item1);
        PlayerControl Target2 = Utils.GetPlayerById(SwapTargets.Item2);
        if (Target1 == null || Target2 == null) return;

        Utils.SendMessage(string.Format(GetString("SwapVote"), Target1.GetRealName(), Target2.GetRealName()), 255, Utils.ColorString(Utils.GetRoleColor(CustomRoles.NiceSwapper), GetString("SwapTitle")));
    }

    private static bool MsgToPlayerAndRole(string msg, out byte id, out string error)
    {
        if (msg.StartsWith("/")) msg = msg.Replace("/", string.Empty);

        Regex r = new("\\d+");
        MatchCollection mc = r.Matches(msg);
        string result = string.Empty;
        for (int i = 0; i < mc.Count; i++)
        {
            result += mc[i];
        }

        if (int.TryParse(result, out int num))
        {
            id = Convert.ToByte(num);
        }
        else
        {
            id = byte.MaxValue;
            error = GetString("SwapHelp");
            return false;
        }

        PlayerControl target = Utils.GetPlayerById(id);
        if (target == null || target.Data.IsDead)
        {
            error = GetString("SwapNull");
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static bool CheckCommand(ref string msg, string command, bool exact = true)
    {
        var comList = command.Split('|');
        foreach (string str in comList)
        {
            if (exact)
            {
                if (msg == "/" + str) return true;
            }
            else
            {
                if (msg.StartsWith("/" + str))
                {
                    msg = msg.Replace("/" + str, string.Empty);
                    return true;
                }
            }
        }
        return false;
    }

    private static void SendRPC(byte playerId)
    {
        if (!IsEnable || !Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetNiceSwapperVotes, SendOption.Reliable, -1);
        writer.Write(playerId);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader, PlayerControl pc)
    {
        byte PlayerId = reader.ReadByte();
        SwapMsg(pc, $"/sw {PlayerId}");
    }

    private static void SwapperOnClick(byte playerId, MeetingHud __instance)
    {
        Logger.Msg($"Click: ID {playerId}", "NiceSwapper UI");

        var pc = Utils.GetPlayerById(playerId);
        if (pc == null || !pc.IsAlive() || !GameStates.IsVoting) return;

        if (AmongUsClient.Instance.AmHost) SwapMsg(PlayerControl.LocalPlayer, $"/sw {playerId}", true);
        else SendRPC(playerId);

        if (PlayerControl.LocalPlayer.GetCustomRole() == CustomRoles.NiceSwapper && PlayerControl.LocalPlayer.IsAlive())
        {
            bool forceAll = true;
            __instance.playerStates.ToList().ForEach(x => { if ((forceAll || !Main.PlayerStates.TryGetValue(x.TargetPlayerId, out var ps) || ps.IsDead) && x.transform.FindChild("ShootButton") != null) UnityEngine.Object.Destroy(x.transform.FindChild("ShootButton").gameObject); });
            CreateSwapperButton(__instance);
        }
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    class StartMeetingPatch
    {
        public static void Postfix(MeetingHud __instance)
        {
            if (PlayerControl.LocalPlayer.GetCustomRole() == CustomRoles.NiceSwapper && PlayerControl.LocalPlayer.IsAlive())
                CreateSwapperButton(__instance);
        }
    }

    public static void CreateSwapperButton(MeetingHud __instance)
    {
        foreach (PlayerVoteArea pva in __instance.playerStates.ToArray())
        {
            var pc = Utils.GetPlayerById(pva.TargetPlayerId);
            if (pc == null || !pc.IsAlive()) continue;

            GameObject template = pva.Buttons.transform.Find("CancelButton").gameObject;
            GameObject targetBox = UnityEngine.Object.Instantiate(template, pva.transform);
            targetBox.name = "ShootButton";
            targetBox.transform.localPosition = new Vector3(-0.35f, 0.03f, -1.31f);
            SpriteRenderer renderer = targetBox.GetComponent<SpriteRenderer>();

            if ((pc.PlayerId == pva.TargetPlayerId) && (SwapTargets.Item1 == pc.PlayerId || SwapTargets.Item2 == pc.PlayerId)) renderer.sprite = CustomButton.Get("SwapYes");
            else renderer.sprite = CustomButton.Get("SwapNo");

            PassiveButton button = targetBox.GetComponent<PassiveButton>();
            button.OnClick.RemoveAllListeners();
            button.OnClick.AddListener((Action)(() => SwapperOnClick(pva.TargetPlayerId, __instance)));
        }
    }
}


