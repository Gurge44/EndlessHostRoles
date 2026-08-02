using System.Linq;
using EHR.Modules;
using System;
using UnityEngine;
using static EHR.Translator;

namespace EHR.Roles;

public class Pawn : RoleBase
{
    public static bool On;

    public static OptionItem KeepsGameGoing;
    
    public CustomRoles ChosenRole;

    private byte PawnId;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(651500)
            .AutoSetupOption(ref KeepsGameGoing, true)
            .CreateOverrideTasksData();
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        ChosenRole = CustomRoles.NotAssigned;
        PawnId = playerId;
    }

    public override void OnTaskComplete(PlayerControl pc, int completedTaskCount, int totalTaskCount)
    {
        if (completedTaskCount + 1 < totalTaskCount || ChosenRole == CustomRoles.NotAssigned || ChosenRole.IsAdditionRole() || ChosenRole.IsForOtherGameMode()) return;

        pc.RpcSetCustomRole(ChosenRole);
        pc.RpcChangeRoleBasis(ChosenRole);
        
        if (pc.AmOwner && ChosenRole is CustomRoles.Crewmate or CustomRoles.CrewmateEHR)
            Achievements.Type.Why.Complete();
    }

    public override void AfterMeetingTasks()
    {
        var pc = PawnId.GetPlayer();
        if (pc == null || !pc.IsAlive()) return;

        if (!pc.AllTasksCompleted() || ChosenRole == CustomRoles.NotAssigned || ChosenRole.IsAdditionRole() || ChosenRole.IsForOtherGameMode()) return;

        pc.RpcSetCustomRole(ChosenRole);
        pc.RpcChangeRoleBasis(ChosenRole);

        if (pc.AmOwner && ChosenRole is CustomRoles.Crewmate or CustomRoles.CrewmateEHR)
            Achievements.Type.Why.Complete();
    }

    public static void CreatePawnButton(MeetingHud __instance)
    {
        PlayerVoteArea localPva = __instance.playerStates
            .FirstOrDefault(pva => pva.TargetPlayerId == PlayerControl.LocalPlayer.PlayerId);

        PlayerControl pc = Utils.GetPlayerById(localPva.TargetPlayerId);
        if (!pc || !pc.IsAlive()) return;

        GameObject template = localPva.Buttons.transform.Find("CancelButton").gameObject;
        GameObject targetBox = Object.Instantiate(template, localPva.transform);
        targetBox.name = "ShootButton";
        targetBox.transform.localPosition = new(-0.35f, 0.03f, -1.31f);
        var renderer = targetBox.GetComponent<SpriteRenderer>();
        renderer.sprite = Utils.LoadSprite("EHR.Resources.Images.Skills.PawnPromotion.png", 160f);
        var button = targetBox.GetComponent<PassiveButton>();
        button.OnClick.RemoveAllListeners();
        button.OnClick.AddListener((Action)(() => GuessManager.GuesserOnClick(localPva.TargetPlayerId, __instance, true)));
    }

    //[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    public static class StartMeetingPatch
    {
        public static void Postfix(MeetingHud __instance)
        {
            if (PlayerControl.LocalPlayer.Is(CustomRoles.Pawn) && PlayerControl.LocalPlayer.IsAlive())
                CreatePawnButton(__instance);
        }
    }

    public static void ProcessGuesserUI(CustomRoles role)
    {
        PlayerControl pc = PlayerControl.LocalPlayer;
        if (!pc || !pc.Is(CustomRoles.Pawn)) return;

        var command = $"/choose {GetString(role.ToString())}";

        if (AmongUsClient.Instance.AmHost)
            ChatCommands.ChooseCommand(PlayerControl.LocalPlayer, command, command.Split(' '));
        else
            ChatCommands.RequestCommandProcessingFromHost(command, "Choose");
    }
}