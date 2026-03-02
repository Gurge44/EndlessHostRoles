using System;
using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using Hazel;
using UnityEngine;
using static EHR.Translator;

namespace EHR.Roles;

public class Swapper : RoleBase
{
    private const int Id = 642680;

    public static OptionItem SwapMax;
    public static OptionItem HideMsg;
    public static OptionItem CanSwapSelf;
    public static OptionItem CanStartMeeting;
    public static OptionItem SwapperAbilityUseGainWithEachTaskCompleted;
    public static OptionItem AbilityChargesWhenFinishedTasks;

    public static (byte, byte) SwapTargets = (byte.MaxValue, byte.MaxValue);
    private static byte SwapperId = byte.MaxValue;

    public override bool IsEnable => SwapperId != byte.MaxValue;
    public static bool On => SwapperId != byte.MaxValue;

    private ShapeshiftMenuElement CNO;

    public override void SetupCustomOption()
    {
        Options.SetupSingleRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Swapper);

        SwapMax = new IntegerOptionItem(Id + 3, "SwapperMax", new(0, 20, 1), 1, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Swapper])
            .SetValueFormat(OptionFormat.Times);

        CanSwapSelf = new BooleanOptionItem(Id + 2, "CanSwapSelfVotes", false, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Swapper]);
        
        CanStartMeeting = new BooleanOptionItem(Id + 4, "JesterCanUseButton", true, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Swapper]);

        SwapperAbilityUseGainWithEachTaskCompleted = new FloatOptionItem(Id + 6, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 0.3f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Swapper])
            .SetValueFormat(OptionFormat.Times);

        AbilityChargesWhenFinishedTasks = new FloatOptionItem(Id + 7, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Swapper])
            .SetValueFormat(OptionFormat.Times);

        HideMsg = new BooleanOptionItem(Id + 5, "SwapperHideMsg", true, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Swapper]);
    }

    public override void Init()
    {
        SwapTargets = (byte.MaxValue, byte.MaxValue);
        SwapperId = byte.MaxValue;
    }

    public override void Add(byte playerId)
    {
        SwapperId = playerId;
        playerId.SetAbilityUseLimit(SwapMax.GetFloat());
        CNO = null;
    }

    public static bool SwapMsg(PlayerControl pc, string msg, bool isUI = false)
    {
        if (!AmongUsClient.Instance.AmHost || !GameStates.IsInGame || pc == null || pc.GetCustomRole() != CustomRoles.Swapper) return false;

        Logger.Info($"{pc.GetNameWithRole()} : {msg} (UI: {isUI})", "Swapper");

        int operate;
        msg = msg.ToLower().TrimStart().TrimEnd();

        if (GuessManager.CheckCommand(ref msg, "id|guesslist|gl编号|玩家编号|玩家id|id列表|玩家列表|列表|所有id|全部id", true, out bool spamRequired))
            operate = 1;
        else if (GuessManager.CheckCommand(ref msg, "sw|换票|换|swap|st", false, out spamRequired))
            operate = 2;
        else
            return false;

        switch (operate)
        {
            case 1:
                Utils.SendMessage(GuessManager.GetFormatString(), pc.PlayerId);
                break;
            case 2:
            {
                if (!pc.IsAlive())
                {
                    if (!isUI) Utils.SendMessage(GetString("SwapDead"), pc.PlayerId, importance: MessageImportance.Low);
                    pc.ShowPopUp(GetString("SwapDead"));
                    return true;
                }

                if (pc.GetAbilityUseLimit() < 1)
                {
                    if (!isUI) Utils.SendMessage(GetString("SwapperTrialMax"), pc.PlayerId);

                    pc.ShowPopUp(GetString("SwapperTrialMax"));
                    return true;
                }

                if (HideMsg.GetBool() && !isUI && !spamRequired)
                    Utils.SendMessage("\n", pc.PlayerId, GetString("NoSpamAnymoreUseCmd"));

                if (!byte.TryParse(msg.Replace(" ", string.Empty), out byte targetId))
                {
                    Utils.SendMessage(GetString("SwapHelp"), pc.PlayerId);
                    return true;
                }

                PlayerControl target = Utils.GetPlayerById(targetId);
                if (target == null) break;

                bool targetIsntSelected = SwapTargets.Item1 != target.PlayerId && SwapTargets.Item2 != target.PlayerId; // Whether the picked target isn't already being swapped

                bool vote1Empty = SwapTargets.Item1 == byte.MaxValue && targetIsntSelected; // Whether the first swapping slot is suitable to swap this target
                bool vote2Available = SwapTargets.Item1 != byte.MaxValue && SwapTargets.Item2 == byte.MaxValue && targetIsntSelected; // Whether the second swapping slot is suitable to swap this target
                bool selfCheck = CanSwapSelf.GetBool() || target.PlayerId != pc.PlayerId;

                if (vote1Empty && selfCheck) // Take the first slot
                {
                    SwapTargets.Item1 = target.PlayerId;

                    if (!isUI) Utils.SendMessage(GetString("Swap1"), pc.PlayerId, importance: MessageImportance.High);

                    Logger.Info($"{pc.GetNameWithRole().RemoveHtmlTags()} chose to swap {target.GetNameWithRole()} (first target)", "Swapper");
                }

                else if (vote2Available && selfCheck) // Take the second slot
                {
                    SwapTargets.Item2 = target.PlayerId;

                    if (!isUI) Utils.SendMessage(GetString("Swap2"), pc.PlayerId, importance: MessageImportance.High);

                    Logger.Info($"{pc.GetNameWithRole().RemoveHtmlTags()} chose to swap {target.GetNameWithRole()} (second target)", "Swapper");
                }

                else if (target.PlayerId == SwapTargets.Item1) // If this player is already chosen to be swapped in the first slot, cancel it
                {
                    SwapTargets.Item1 = byte.MaxValue;

                    if (!isUI) Utils.SendMessage(GetString("CancelSwap1"), pc.PlayerId, importance: MessageImportance.High);

                    Logger.Info($"{pc.GetNameWithRole().RemoveHtmlTags()} canceled swapping on {target.GetNameWithRole()} (first target)", "Swapper");
                }

                else if (target.PlayerId == SwapTargets.Item2) // If this player is already chosen to be swapped in the second slot, cancel it
                {
                    SwapTargets.Item2 = byte.MaxValue;

                    if (!isUI) Utils.SendMessage(GetString("CancelSwap2"), pc.PlayerId, importance: MessageImportance.High);

                    Logger.Info($"{pc.GetNameWithRole().RemoveHtmlTags()} canceled swapping on {target.GetNameWithRole()} (second target)", "Swapper");
                }

                else if (!selfCheck) // When the Swapper tries to swap themselves, but they aren't allowed to
                {
                    if (!isUI)
                        Utils.SendMessage(GetString("CantSwapSelf"), pc.PlayerId);
                    else
                        pc.ShowPopUp(GetString("CantSwapSelf"));
                }


                if (CustomRoles.MeetingManager.RoleExist())
                {
                    PlayerControl pc1 = SwapTargets.Item1.GetPlayer();
                    PlayerControl pc2 = SwapTargets.Item2.GetPlayer();

                    if (pc1 != null && pc2 != null)
                        MeetingManager.OnSwap(pc1, pc2);
                }

                break;
            }
        }

        return true;
    }

    public static void OnExileFinish()
    {
        if (SwapTargets != (byte.MaxValue, byte.MaxValue))
        {
            Utils.GetPlayerById(SwapperId).RpcRemoveAbilityUse();
            SwapTargets = (byte.MaxValue, byte.MaxValue);
        }
    }

    public static void ManipulateVotingResult(Dictionary<byte, int> votingData, MeetingHud.VoterState[] states)
    {
        try
        {
            if (SwapperId == byte.MaxValue || SwapTargets.Item1 == byte.MaxValue || SwapTargets.Item2 == byte.MaxValue) return;

            // Swap the number of votes received internally
            int count1 = votingData.GetValueOrDefault(SwapTargets.Item1, 0);
            int count2 = votingData.GetValueOrDefault(SwapTargets.Item2, 0);
            votingData[SwapTargets.Item1] = count2;
            votingData[SwapTargets.Item2] = count1;

            // Swap the votes visually
            List<byte> votedFor1 = states.Where(x => x.VotedForId == SwapTargets.Item1).Select(x => x.VoterId).ToList();
            List<byte> votedFor2 = states.Where(x => x.VotedForId == SwapTargets.Item2).Select(x => x.VoterId).ToList();

            for (var index = 0; index < states.Length; index++)
            {
                ref MeetingHud.VoterState state = ref states[index];
                
                if (votedFor1.Contains(state.VoterId))
                    state.VotedForId = SwapTargets.Item2;
                else if (votedFor2.Contains(state.VoterId))
                    state.VotedForId = SwapTargets.Item1;
            }

            Utils.SendMessage(string.Format(GetString("SwapVote"), SwapTargets.Item1.ColoredPlayerName(), SwapTargets.Item2.ColoredPlayerName()), title: Utils.ColorString(Utils.GetRoleColor(CustomRoles.Swapper), GetString("SwapTitle")), importance: MessageImportance.High);
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }

    public override void OnMeetingShapeshift(PlayerControl shapeshifter, PlayerControl target)
    {
        if (Starspawn.IsDayBreak) return;

        if (GameStates.CurrentServerType != GameStates.ServerType.Vanilla)
        {
            if (CNO == null) CNO = CanSwapSelf.GetBool() ? new ShapeshiftMenuElement(shapeshifter.PlayerId) : null;
            else if (CNO.playerControl.NetId == target.NetId) target = shapeshifter;
        }
        
        SwapMsg(shapeshifter, $"/sw {target.PlayerId}");
    }

    public override void OnReportDeadBody()
    {
        CNO?.Despawn();
        CNO = null;
    }

    private static void SendRPC(byte playerId)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetSwapperVotes, SendOption.Reliable, AmongUsClient.Instance.HostId);
        writer.Write(playerId);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader, PlayerControl pc)
    {
        byte playerId = reader.ReadByte();
        SwapMsg(pc, $"/sw {playerId}", true);
    }

    private static void SwapperOnClick(byte playerId, MeetingHud __instance)
    {
        Logger.Msg($"Click: ID {playerId}", "Swapper UI");

        PlayerControl pc = Utils.GetPlayerById(playerId);
        if (pc == null || !pc.IsAlive() || !GameStates.IsVoting || Starspawn.IsDayBreak) return;

        if (AmongUsClient.Instance.AmHost)
            SwapMsg(PlayerControl.LocalPlayer, $"/sw {playerId}", true);
        else
            SendRPC(playerId);

        if (PlayerControl.LocalPlayer.GetCustomRole() == CustomRoles.Swapper && PlayerControl.LocalPlayer.IsAlive())
        {
            __instance.playerStates.ToList().ForEach(x =>
            {
                Transform swapButton = x.transform.FindChild("ShootButton");
                if (swapButton != null) Object.Destroy(swapButton.gameObject);
            });

            CreateSwapperButton(__instance);
        }
    }

    private static void CreateSwapperButton(MeetingHud __instance)
    {
        foreach (PlayerVoteArea pva in __instance.playerStates)
        {
            PlayerControl pc = Utils.GetPlayerById(pva.TargetPlayerId);
            if (pc == null || !pc.IsAlive()) continue;

            GameObject template = pva.Buttons.transform.Find("CancelButton").gameObject;
            GameObject targetBox = Object.Instantiate(template, pva.transform);
            targetBox.name = "ShootButton";
            targetBox.transform.localPosition = new(-0.35f, 0.03f, -1.31f);
            var renderer = targetBox.GetComponent<SpriteRenderer>();

            if (pc.PlayerId == pva.TargetPlayerId && (SwapTargets.Item1 == pc.PlayerId || SwapTargets.Item2 == pc.PlayerId))
                renderer.sprite = CustomButton.Get("SwapYes");
            else
                renderer.sprite = CustomButton.Get("SwapNo");

            var button = targetBox.GetComponent<PassiveButton>();
            button.OnClick.RemoveAllListeners();
            button.OnClick.AddListener((Action)(() => SwapperOnClick(pva.TargetPlayerId, __instance)));
        }
    }

    //[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    public static class StartMeetingPatch
    {
        // ReSharper disable once UnusedMember.Local
        public static void Postfix(MeetingHud __instance)
        {
            if (PlayerControl.LocalPlayer.GetCustomRole() == CustomRoles.Swapper && PlayerControl.LocalPlayer.IsAlive())
                CreateSwapperButton(__instance);
        }
    }

    public override void ManipulateGameEndCheckCrew(PlayerState playerState, out bool keepGameGoing, out int countsAs)
    {
        if (playerState.IsDead)
        {
            base.ManipulateGameEndCheckCrew(playerState, out keepGameGoing, out countsAs);
            return;
        }

        keepGameGoing = true;
        countsAs = 1;
    }
}
