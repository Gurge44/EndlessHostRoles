using System;
using System.Linq;
using EHR.Modules;
using UnityEngine;
using static EHR.Translator;

namespace EHR.Roles;

public class Loner : RoleBase
{
    public static bool On;

    public override bool IsEnable => On;

    public byte PickedPlayer;
    public CustomRoles PickedRole;
    public bool Done;

    public override void SetupCustomOption()
    {
        StartSetup(655400);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        PickedPlayer = byte.MaxValue;
        PickedRole = CustomRoles.Crewmate;
        Done = false;
    }

    public override void OnMeetingShapeshift(PlayerControl shapeshifter, PlayerControl target)
    {
        if (Starspawn.IsDayBreak) return;
        if (shapeshifter == null || target == null || shapeshifter.PlayerId == target.PlayerId || Done) return;

        PickedPlayer = target.PlayerId;
        PickedRole = Main.CustomRoleValues.Where(x => x.IsImpostor() && !x.IsVanilla() && !CustomRoleSelector.RoleResult.ContainsValue(x) && x.GetMode() != 0).RandomElement();
        if (PickedRole == CustomRoles.Crewmate) PickedRole = CustomRoles.ImpostorEHR;

        Utils.SendMessage("\n", shapeshifter.PlayerId, string.Format(Translator.GetString("Loner.Picked"), PickedPlayer.ColoredPlayerName(), PickedRole.ToColoredString()), importance: MessageImportance.High);
    }

    public override void AfterMeetingTasks()
    {
        var pc = PickedPlayer.GetPlayer();

        if (!Done && PickedPlayer != byte.MaxValue && PickedRole != CustomRoles.Crewmate && pc != null)
        {
            Done = true;
            pc.RpcSetCustomRole(PickedRole);
            pc.RpcChangeRoleBasis(PickedRole);
        }
    }

    public static void CreateLonerButton(MeetingHud __instance)
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
            renderer.sprite = Utils.LoadSprite("EHR.Resources.Images.Skills.Sidekick.png", 160f);
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
            if (PlayerControl.LocalPlayer.Is(CustomRoles.Loner) && PlayerControl.LocalPlayer.IsAlive())
                CreateLonerButton(__instance);
        }
    }

    public static void ProcessGuesserUI(byte playerId, CustomRoles role)
    {
        PlayerControl pc = Utils.GetPlayerById(playerId);
        if (!pc || !pc.IsAlive() || !PlayerControl.LocalPlayer.Is(CustomRoles.Loner) || Starspawn.IsDayBreak) return;

        var command = $"/select {playerId} {GetString(role.ToString())}";

        if (AmongUsClient.Instance.AmHost)
            ChatCommands.SelectCommand(PlayerControl.LocalPlayer, command, command.Split(' '));
        else
            ChatCommands.RequestCommandProcessingFromHost(command, "Select");
    }
}