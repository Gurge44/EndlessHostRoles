using System;
using EHR.Modules;
using UnityEngine;
using static EHR.Translator;

namespace EHR.Roles;

public class Inquirer : RoleBase
{
    public static OptionItem FailChance;
    private static OptionItem AbilityUseLimit;
    private static OptionItem AbilityUseGainWithEachTaskCompleted;
    private static OptionItem AbilityChargesWhenFinishedTasks;

    public override bool IsEnable => false;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(649710, TabGroup.CrewmateRoles, CustomRoles.Inquirer);

        FailChance = new IntegerOptionItem(649712, "Inquirer.FailChance", new(0, 100, 5), 20, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Inquirer])
            .SetValueFormat(OptionFormat.Percent);

        AbilityUseLimit = new FloatOptionItem(649713, "AbilityUseLimit", new(0, 20, 0.05f), 1, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Inquirer])
            .SetValueFormat(OptionFormat.Times);

        AbilityUseGainWithEachTaskCompleted = new FloatOptionItem(649714, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 0.8f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Inquirer])
            .SetValueFormat(OptionFormat.Times);

        AbilityChargesWhenFinishedTasks = new FloatOptionItem(649715, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Inquirer])
            .SetValueFormat(OptionFormat.Times);
    }

    public override void Init() { }

    public override void Add(byte playerId)
    {
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetFloat());
    }

    public static void CreateInquirerButton(MeetingHud __instance)
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
            renderer.sprite = Utils.LoadSprite("EHR.Resources.Images.Skills.InspectorIcon.png", 160f);
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
            if (PlayerControl.LocalPlayer.Is(CustomRoles.Inquirer) && PlayerControl.LocalPlayer.IsAlive())
                CreateInquirerButton(__instance);
        }
    }

    public static void ProcessGuesserUI(byte playerId, CustomRoles role)
    {
        PlayerControl pc = Utils.GetPlayerById(playerId);
        if (pc == null || !pc.IsAlive() || Starspawn.IsDayBreak) return;

        var command = $"/check {playerId} {GetString(role.ToString())}";

        if (AmongUsClient.Instance.AmHost)
            ChatCommands.CheckCommand(PlayerControl.LocalPlayer, command, command.Split(' '));
        else
            ChatCommands.RequestCommandProcessingFromHost(command, "Check");
    }
}