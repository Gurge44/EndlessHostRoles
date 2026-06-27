using System.Collections.Generic;
using System;
using UnityEngine;
using static EHR.Translator;

namespace EHR.Roles;

public class Forger : RoleBase
{
    public static bool On;

    public static Dictionary<byte, CustomRoles> Forges = [];

    private static OptionItem AbilityUseLimit;
    private static OptionItem AbilityUseGainWithEachKill;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(651700)
            .AutoSetupOption(ref AbilityUseLimit, 1f, new FloatValueRule(0, 20, 0.05f), OptionFormat.Times)
            .AutoSetupOption(ref AbilityUseGainWithEachKill, 0.4f, new FloatValueRule(0f, 5f, 0.1f), OptionFormat.Times);
    }

    public override void Init()
    {
        On = false;
        Forges = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetFloat());
    }

    public static void CreateForgerButton(MeetingHud __instance)
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
            renderer.sprite = Utils.LoadSprite("EHR.Resources.Images.Skills.AmnesiacKill.png", 160f);
            var button = targetBox.GetComponent<PassiveButton>();
            button.OnClick.RemoveAllListeners();
            button.OnClick.AddListener((Action)(() => GuessManager.GuesserOnClick(pva.TargetPlayerId, __instance, true)));
        }
    }

    //[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    public static class StartMeetingPatch
    {
        public static void Postfix(MeetingHud __instance)
        {
            if (PlayerControl.LocalPlayer.Is(CustomRoles.Forger) && PlayerControl.LocalPlayer.IsAlive())
                CreateForgerButton(__instance);
        }
    }

    public static void ProcessGuesserUI(byte playerId, CustomRoles role)
    {
        PlayerControl pc = Utils.GetPlayerById(playerId);
        if (pc == null || !pc.IsAlive() || Starspawn.IsDayBreak) return;

        var command = $"/forge {playerId} {GetString(role.ToString())}";

        if (AmongUsClient.Instance.AmHost)
            ChatCommands.ForgeCommand(PlayerControl.LocalPlayer, command, command.Split(' '));
        else
            ChatCommands.RequestCommandProcessingFromHost(command, "Forge");
    }
}