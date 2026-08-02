using System.Collections.Generic;
using System;
using Hazel;
using UnityEngine;

namespace EHR.Roles;

internal class Markseeker : RoleBase
{
    private const int Id = 643550;
    public static List<byte> PlayerIdList;
    public static bool On;

    public byte MarkedId;
    public bool TargetRevealed;
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Markseeker);
    }

    public override void Add(byte playerId)
    {
        On = true;
        MarkedId = byte.MaxValue;
        TargetRevealed = false;
        PlayerIdList ??= [];
        PlayerIdList.Add(playerId);
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList?.Remove(playerId);
    }

    public override void Init()
    {
        On = false;
        PlayerIdList = null;
    }

    public void ReceiveRPC(MessageReader reader)
    {
        MarkedId = reader.ReadByte();
    }

/*
    public override bool OnVote(PlayerControl player, PlayerControl target)
    {
        if (Starspawn.IsDayBreak) return false;
        if (player == null || target == null || player.PlayerId == target.PlayerId || MarkedId != byte.MaxValue || Main.DontCancelVoteList.Contains(player.PlayerId)) return false;

        MarkedId = target.PlayerId;

        Main.DontCancelVoteList.Add(player.PlayerId);
        return true;
    }
*/

    public override void OnMeetingShapeshift(PlayerControl shapeshifter, PlayerControl target)
    {
        var command = $"/mark {target.PlayerId}";
        ChatCommands.MarkCommand(shapeshifter, command, command.Split(' '));
    }

    public static void OnDeath(PlayerControl player)
    {
        if (Main.PlayerStates[player.PlayerId].Role is not Markseeker { IsEnable: true } ms || ms.MarkedId == byte.MaxValue) return;

        ms.TargetRevealed = true;
    }

    private static void MarkseekerOnClick(byte playerId /*, MeetingHud __instance*/)
    {
        Logger.Msg($"Click: ID {playerId}", "Markseeker UI");
        PlayerControl pc = Utils.GetPlayerById(playerId);
        if (pc == null || !pc.IsAlive() || !GameStates.IsVoting || Starspawn.IsDayBreak) return;

        var command = $"/mark {playerId}";

        if (AmongUsClient.Instance.AmHost)
            ChatCommands.MarkCommand(PlayerControl.LocalPlayer, command, command.Split(' '));
        else
            ChatCommands.RequestCommandProcessingFromHost(command, "Mark");
    }

    public static void CreateMarkseekerButton(MeetingHud __instance)
    {
        foreach (PlayerVoteArea pva in __instance.playerStates)
        {
            PlayerControl pc = Utils.GetPlayerById(pva.TargetPlayerId);
            if (!pc || !pc.IsAlive()) continue;

            GameObject template = pva.Buttons.transform.Find("CancelButton").gameObject;
            GameObject targetBox = Object.Instantiate(template, pva.transform);
            targetBox.name = "ShootButton";
            targetBox.transform.localPosition = new(-0.35f, 0.03f, -1.31f);
            var renderer = targetBox.GetComponent<SpriteRenderer>();
            renderer.sprite = Utils.LoadSprite("EHR.Resources.Images.Skills.prophecies.png", 160f);
            var button = targetBox.GetComponent<PassiveButton>();
            button.OnClick.RemoveAllListeners();
            button.OnClick.AddListener((Action)(() => MarkseekerOnClick(pva.TargetPlayerId)));
        }
    }

    //[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    public static class StartMeetingPatch
    {
        public static void Postfix(MeetingHud __instance)
        {
            if (PlayerControl.LocalPlayer.Is(CustomRoles.Markseeker) && PlayerControl.LocalPlayer.IsAlive())
                CreateMarkseekerButton(__instance);
        }
    }
}