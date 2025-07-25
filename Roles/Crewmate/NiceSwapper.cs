﻿using System;
using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using EHR.Patches;
using Hazel;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using static EHR.Translator;


namespace EHR.Crewmate;

public class NiceSwapper : RoleBase
{
    private const int Id = 642680;

    public static OptionItem SwapMax;
    public static OptionItem HideMsg;
    public static OptionItem CanSwapSelf;
    public static OptionItem CanStartMeeting;
    public static OptionItem NiceSwapperAbilityUseGainWithEachTaskCompleted;
    public static OptionItem AbilityChargesWhenFinishedTasks;

    private static (byte, byte) SwapTargets = (byte.MaxValue, byte.MaxValue);
    private static byte NiceSwapperId = byte.MaxValue;

    public override bool IsEnable => NiceSwapperId != byte.MaxValue;
    public static bool On => NiceSwapperId != byte.MaxValue;

    public override void SetupCustomOption()
    {
        Options.SetupSingleRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.NiceSwapper);

        SwapMax = new IntegerOptionItem(Id + 3, "NiceSwapperMax", new(0, 20, 1), 1, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.NiceSwapper])
            .SetValueFormat(OptionFormat.Times);

        CanSwapSelf = new BooleanOptionItem(Id + 2, "CanSwapSelfVotes", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.NiceSwapper]);
        CanStartMeeting = new BooleanOptionItem(Id + 4, "JesterCanUseButton", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.NiceSwapper]);

        NiceSwapperAbilityUseGainWithEachTaskCompleted = new FloatOptionItem(Id + 6, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 0.3f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.NiceSwapper])
            .SetValueFormat(OptionFormat.Times);

        AbilityChargesWhenFinishedTasks = new FloatOptionItem(Id + 7, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.NiceSwapper])
            .SetValueFormat(OptionFormat.Times);

        HideMsg = new BooleanOptionItem(Id + 5, "SwapperHideMsg", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.NiceSwapper]);
    }

    public override void Init()
    {
        SwapTargets = (byte.MaxValue, byte.MaxValue);
        NiceSwapperId = byte.MaxValue;
    }

    public override void Add(byte playerId)
    {
        NiceSwapperId = playerId;
        playerId.SetAbilityUseLimit(SwapMax.GetFloat());
    }

    public static bool SwapMsg(PlayerControl pc, string msg, bool isUI = false)
    {
        string originMsg = msg;

        if (!AmongUsClient.Instance.AmHost || !GameStates.IsInGame || pc == null || pc.GetCustomRole() != CustomRoles.NiceSwapper) return false;

        Logger.Info($"{pc.GetNameWithRole()} : {msg} (UI: {isUI})", "Swapper");

        int operate;
        msg = msg.ToLower().TrimStart().TrimEnd();

        if (CheckCommand(ref msg, "id|guesslist|gl编号|玩家编号|玩家id|id列表|玩家列表|列表|所有id|全部id"))
            operate = 1;
        else if (CheckCommand(ref msg, "sw|换票|换|swap|st", false))
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
                    if (!isUI) Utils.SendMessage(GetString("SwapDead"), pc.PlayerId, sendOption: SendOption.None);
                    pc.ShowPopUp(GetString("SwapDead"));
                    return true;
                }

                if (pc.GetAbilityUseLimit() < 1)
                {
                    if (!isUI) Utils.SendMessage(GetString("NiceSwapperTrialMax"), pc.PlayerId);

                    pc.ShowPopUp(GetString("NiceSwapperTrialMax"));
                    return true;
                }

                if (HideMsg.GetBool() && !isUI)
                    ChatManager.SendPreviousMessagesToAll();
                else if (pc.AmOwner && !isUI) Utils.SendMessage(originMsg, 255, pc.GetRealName());

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

                    if (!isUI) Utils.SendMessage(GetString("Swap1"), pc.PlayerId);

                    Logger.Info($"{pc.GetNameWithRole().RemoveHtmlTags()} chose to swap {target.GetNameWithRole()} (first target)", "Swapper");
                }

                else if (vote2Available && selfCheck) // Take the second slot
                {
                    SwapTargets.Item2 = target.PlayerId;

                    if (!isUI) Utils.SendMessage(GetString("Swap2"), pc.PlayerId);

                    Logger.Info($"{pc.GetNameWithRole().RemoveHtmlTags()} chose to swap {target.GetNameWithRole()} (second target)", "Swapper");
                }

                else if (target.PlayerId == SwapTargets.Item1) // If this player is already chosen to be swapped in the first slot, cancel it
                {
                    SwapTargets.Item1 = byte.MaxValue;

                    if (!isUI) Utils.SendMessage(GetString("CancelSwap1"), pc.PlayerId);

                    Logger.Info($"{pc.GetNameWithRole().RemoveHtmlTags()} canceled swapping on {target.GetNameWithRole()} (first target)", "Swapper");
                }

                else if (target.PlayerId == SwapTargets.Item2) // If this player is already chosen to be swapped in the second slot, cancel it
                {
                    SwapTargets.Item2 = byte.MaxValue;

                    if (!isUI) Utils.SendMessage(GetString("CancelSwap2"), pc.PlayerId);

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
            Utils.GetPlayerById(NiceSwapperId).RpcRemoveAbilityUse();
            SwapTargets = (byte.MaxValue, byte.MaxValue);
        }
    }

    public static void OnCheckForEndVoting()
    {
        if (NiceSwapperId == byte.MaxValue || SwapTargets.Item1 == byte.MaxValue || SwapTargets.Item2 == byte.MaxValue) return;

        CheckForEndVotingPatch.RunRoleCode = false;

        try
        {
            MeetingHud meetingHud = MeetingHud.Instance;
            Il2CppReferenceArray<PlayerVoteArea> playerStates = meetingHud.playerStates;
            List<PlayerVoteArea> votedFor2 = playerStates.Where(x => x.VotedFor == SwapTargets.Item2).ToList();

            playerStates.DoIf(x => x.VotedFor == SwapTargets.Item1, x =>
            {
                x.UnsetVote();
                meetingHud.SetDirtyBit(1U);
                AmongUsClient.Instance.SendAllStreamedObjects();
                meetingHud.RpcClearVote(x.TargetPlayerId.GetPlayer().OwnerId);
                meetingHud.SetDirtyBit(1U);
                AmongUsClient.Instance.SendAllStreamedObjects();
                meetingHud.CastVote(x.TargetPlayerId, SwapTargets.Item2);
                x.VotedFor = SwapTargets.Item2;
            });

            votedFor2.ForEach(x =>
            {
                x.UnsetVote();
                meetingHud.SetDirtyBit(1U);
                AmongUsClient.Instance.SendAllStreamedObjects();
                meetingHud.RpcClearVote(x.TargetPlayerId.GetPlayer().OwnerId);
                meetingHud.SetDirtyBit(1U);
                AmongUsClient.Instance.SendAllStreamedObjects();
                meetingHud.CastVote(x.TargetPlayerId, SwapTargets.Item1);
                x.VotedFor = SwapTargets.Item1;
            });

            PlayerControl target1 = Utils.GetPlayerById(SwapTargets.Item1);
            PlayerControl target2 = Utils.GetPlayerById(SwapTargets.Item2);
            if (target1 == null || target2 == null) return;

            Utils.SendMessage(string.Format(GetString("SwapVote"), target1.GetRealName(), target2.GetRealName()), 255, Utils.ColorString(Utils.GetRoleColor(CustomRoles.NiceSwapper), GetString("SwapTitle")));
        }
        finally { CheckForEndVotingPatch.RunRoleCode = true; }
    }

    private static bool CheckCommand(ref string msg, string command, bool exact = true)
    {
        string[] comList = command.Split('|');

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
        if (!Utils.DoRPC) return;

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetNiceSwapperVotes, SendOption.Reliable);
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
        Logger.Msg($"Click: ID {playerId}", "NiceSwapper UI");

        PlayerControl pc = Utils.GetPlayerById(playerId);
        if (pc == null || !pc.IsAlive() || !GameStates.IsVoting) return;

        if (AmongUsClient.Instance.AmHost)
            SwapMsg(PlayerControl.LocalPlayer, $"/sw {playerId}", true);
        else
            SendRPC(playerId);

        if (PlayerControl.LocalPlayer.GetCustomRole() == CustomRoles.NiceSwapper && PlayerControl.LocalPlayer.IsAlive())
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
            if (PlayerControl.LocalPlayer.GetCustomRole() == CustomRoles.NiceSwapper && PlayerControl.LocalPlayer.IsAlive())
                CreateSwapperButton(__instance);
        }
    }
}