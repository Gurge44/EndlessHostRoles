using System;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using UnityEngine;

namespace EHR.Crewmate;

public class Retributionist : RoleBase
{
    public static bool On;

    public override bool IsEnable => On;

    public static OptionItem ResetCampedPlayerAfterEveryMeeting;
    public static OptionItem UsePet;

    public byte Camping;
    public bool Notified;
    private PlayerControl RetributionistPC;

    public override void SetupCustomOption()
    {
        StartSetup(653200)
            .AutoSetupOption(ref ResetCampedPlayerAfterEveryMeeting, false)
            .CreatePetUseSetting(ref UsePet);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        Camping = byte.MaxValue;
        Notified = false;
        RetributionistPC = playerId.GetPlayer();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        opt.SetVision(false);
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return pc.GetRoleMap().CustomRole == CustomRoles.Retributionist;
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = 1f;
    }

    public override void AfterMeetingTasks()
    {
        if (RetributionistPC == null || !RetributionistPC.IsAlive() || CanUseKillButton(RetributionistPC)) return;

        PlayerControl campTarget = Camping.GetPlayer();

        if (ResetCampedPlayerAfterEveryMeeting.GetBool() || campTarget == null || !campTarget.IsAlive())
        {
            Notified = false;
            Camping = byte.MaxValue;
            Utils.SendRPC(CustomRPC.SyncRoleData, RetributionistPC.PlayerId, Camping);
            RetributionistPC.RpcChangeRoleBasis(CustomRoles.Retributionist);
        }
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        Camping = target.PlayerId;
        Utils.SendRPC(CustomRPC.SyncRoleData, RetributionistPC.PlayerId, Camping);
        Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
        RetributionistPC.RpcChangeRoleBasis(CustomRoles.CrewmateEHR);
        return false;
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (Camping == byte.MaxValue || Notified || !pc.IsAlive()) return;

        PlayerControl campTarget = Camping.GetPlayer();

        if (campTarget == null)
        {
            if (!Notified)
            {
                Camping = byte.MaxValue;
                Utils.SendRPC(CustomRPC.SyncRoleData, RetributionistPC.PlayerId, Camping);
            }

            return;
        }

        if (!campTarget.IsAlive())
        {
            pc.ReactorFlash();
            pc.Notify(Translator.GetString("Retributionist.TargetDead"), 15f);
            Notified = true;
        }
    }

    public override void OnMeetingShapeshift(PlayerControl shapeshifter, PlayerControl target)
    {
        var command = $"/retribute {target.PlayerId}";
        ChatCommands.RetributeCommand(shapeshifter, "Command.Retribute", command, command.Split(' '));
    }

    public void ReceiveRPC(MessageReader reader)
    {
        Camping = reader.ReadByte();
    }

    private static void RetributionistOnClick(byte playerId /*, MeetingHud __instance*/)
    {
        Logger.Msg($"Click: ID {playerId}", "Retributionist UI");
        PlayerControl pc = Utils.GetPlayerById(playerId);
        if (pc == null || !pc.IsAlive() || !GameStates.IsVoting || Starspawn.IsDayBreak) return;

        if (AmongUsClient.Instance.AmHost)
        {
            var command = $"/retribute {playerId}";
            ChatCommands.RetributeCommand(PlayerControl.LocalPlayer, "Command.Retribute", command, command.Split(' '));
        }
        else
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.RetributionistClick, SendOption.Reliable, AmongUsClient.Instance.HostId);
            writer.Write(playerId);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
    }

    public static void CreateRetributionistButton(MeetingHud __instance)
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
            renderer.sprite = Utils.LoadSprite("EHR.Resources.Images.Skills.MeetingKillButton.png", 140f);
            var button = targetBox.GetComponent<PassiveButton>();
            button.OnClick.RemoveAllListeners();
            button.OnClick.AddListener((Action)(() => RetributionistOnClick(pva.TargetPlayerId)));
        }
    }

    //[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    public static class StartMeetingPatch
    {
        public static void Postfix(MeetingHud __instance)
        {
            if (PlayerControl.LocalPlayer.Is(CustomRoles.Retributionist) && PlayerControl.LocalPlayer.IsAlive())
                CreateRetributionistButton(__instance);
        }
    }

    public override void ManipulateGameEndCheckCrew(PlayerState playerState, out bool keepGameGoing, out int countsAs)
    {
        if (playerState.IsDead)
        {
            base.ManipulateGameEndCheckCrew(playerState, out keepGameGoing, out countsAs);
            return;
        }

        keepGameGoing = GameStates.IsInTask || Notified;
        countsAs = 1;
    }
}
