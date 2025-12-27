using System;
using System.Linq;
﻿using EHR.Modules;
using EHR.Neutral;
using Hazel;
using UnityEngine;

namespace EHR.Impostor;

public class Ventriloquist : RoleBase
{
    public static bool On;

    private static OptionItem UseLimit;
    private static OptionItem VentriloquistAbilityUseGainWithEachKill;

    public byte Target;
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(649650, TabGroup.ImpostorRoles, CustomRoles.Ventriloquist);

        UseLimit = new FloatOptionItem(649652, "AbilityUseLimit", new(0, 20, 0.05f), 0, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Ventriloquist])
            .SetValueFormat(OptionFormat.Times);

        VentriloquistAbilityUseGainWithEachKill = new FloatOptionItem(649653, "AbilityUseGainWithEachKill", new(0f, 5f, 0.1f), 2f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Ventriloquist])
            .SetValueFormat(OptionFormat.Times);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        Target = byte.MaxValue;
        playerId.SetAbilityUseLimit(UseLimit.GetFloat());
    }

    public override void OnMeetingShapeshift(PlayerControl shapeshifter, PlayerControl target)
    {
        var command = $"/target {target.PlayerId}";
        ChatCommands.TargetCommand(shapeshifter, "Command.Target", command, command.Split(' '));
    }

    public static void ReceiveRPC(MessageReader reader, PlayerControl pc)
    {
        int playerId = reader.ReadByte();
        var command = $"/target {playerId}";
        ChatCommands.TargetCommand(pc, "Command.Target", command, command.Split(' '));
    }

    private static void VentriloquisttOnClick(byte playerId /*, MeetingHud __instance*/)
    {
        Logger.Msg($"Click: ID {playerId}", "Ventriloquist UI");
        PlayerControl pc = Utils.GetPlayerById(playerId);
        if (pc == null || !pc.IsAlive() || !GameStates.IsVoting || Starspawn.IsDayBreak) return;

        if (AmongUsClient.Instance.AmHost)
        {
            var command = $"/target {playerId}";
            ChatCommands.TargetCommand(PlayerControl.LocalPlayer, "Command.Target", command, command.Split(' '));
        }
        else
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.VentriloquistClick, SendOption.Reliable, AmongUsClient.Instance.HostId);
            writer.Write(playerId);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
    }

    public static void CreateVentriloquistButton(MeetingHud __instance)
    {
        foreach (PlayerVoteArea pva in __instance.playerStates.ToArray())
        {
            PlayerControl pc = Utils.GetPlayerById(pva.TargetPlayerId);
            if (pc == null || !pc.IsAlive()) continue;

            GameObject template = pva.Buttons.transform.Find("CancelButton").gameObject;
            GameObject targetBox = Object.Instantiate(template, pva.transform);
            targetBox.name = "ShootButton";
            targetBox.transform.localPosition = new(-0.35f, 0.03f, -1.31f);
            var renderer = targetBox.GetComponent<SpriteRenderer>();
            renderer.sprite = Utils.LoadSprite("EHR.Resources.Images.Skills.Hack.png", 160f);
            var button = targetBox.GetComponent<PassiveButton>();
            button.OnClick.RemoveAllListeners();
            button.OnClick.AddListener((Action)(() => VentriloquisttOnClick(pva.TargetPlayerId)));
        }
    }

    //[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    public static class StartMeetingPatch
    {
        public static void Postfix(MeetingHud __instance)
        {
            if (PlayerControl.LocalPlayer.Is(CustomRoles.Ventriloquist) && PlayerControl.LocalPlayer.IsAlive())
                CreateVentriloquistButton(__instance);
        }
    }
}
