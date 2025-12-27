using System;
using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using EHR.Neutral;
using Hazel;
using UnityEngine;

namespace EHR.Crewmate;

public class Imitator : RoleBase
{
    public static bool On;
    public static List<byte> PlayerIdList = [];
    public static Dictionary<byte, CustomRoles> ImitatingRole = [];

    public override bool IsEnable => On;


    public override void SetupCustomOption()
    {
        StartSetup(653190);
    }

    public override void Init()
    {
        if (GameStates.InGame && !Main.HasJustStarted) return;
        On = false;
        PlayerIdList = [];
        ImitatingRole = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        ImitatingRole[playerId] = CustomRoles.Imitator;
        PlayerIdList.Add(playerId);
    }

    public static void SetRoles()
    {
        foreach (byte id in PlayerIdList)
        {
            PlayerControl pc = id.GetPlayer();

            if (pc != null && pc.IsAlive() && ImitatingRole.TryGetValue(id, out CustomRoles role) && !pc.Is(role))
            {
                Main.AbilityUseLimit.Remove(pc.PlayerId);
                Utils.SendRPC(CustomRPC.RemoveAbilityUseLimit, pc.PlayerId);
                pc.RpcSetCustomRole(role);
                pc.RpcChangeRoleBasis(role);
            }
        }
    }

    private static void SendRPC(byte playerId)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.ImitatorClick, SendOption.Reliable, AmongUsClient.Instance.HostId);
        writer.Write(playerId);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader, PlayerControl pc)
    {
        int playerId = reader.ReadByte();
        var command = $"/imitate {playerId}";
        ChatCommands.ImitateCommand(pc, "Command.Imitate", command, command.Split(' '));
    }

    private static void ImitatorOnClick(byte playerId /*, MeetingHud __instance*/)
    {
        Logger.Msg($"Click: ID {playerId}", "Imitator UI");
        PlayerControl pc = Utils.GetPlayerById(playerId);
        if (pc == null || pc.IsAlive() || !GameStates.IsVoting || Starspawn.IsDayBreak) return;

        if (AmongUsClient.Instance.AmHost)
        {
            var command = $"/imitate {playerId}";
            ChatCommands.ImitateCommand(PlayerControl.LocalPlayer, "Command.Imitate", command, command.Split(' '));

            if (ImitatingRole.ContainsKey(PlayerControl.LocalPlayer.PlayerId))
            {
                foreach (PlayerVoteArea pva in MeetingHud.Instance.playerStates)
                {
                    Transform button = pva.transform.FindChild("ImitatorButton");
                    if (button != null) Object.Destroy(button.gameObject);
                }
            }
        }
        else
            SendRPC(playerId);
    }

    private static void CreateImitatorButton(MeetingHud __instance)
    {
        foreach (PlayerVoteArea pva in __instance.playerStates.ToArray())
        {
            PlayerControl pc = Utils.GetPlayerById(pva.TargetPlayerId);
            if (pc == null || pc.IsAlive()) continue;

            GameObject template = pva.Buttons.transform.Find("CancelButton").gameObject;
            GameObject targetBox = Object.Instantiate(template, pva.transform);
            targetBox.name = "ImitatorButton";
            targetBox.transform.localPosition = new(-0.35f, 0.03f, -1.31f);
            var renderer = targetBox.GetComponent<SpriteRenderer>();
            renderer.sprite = Utils.LoadSprite("EHR.Resources.Images.Skills.TargetIcon.png", 130f);
            var button = targetBox.GetComponent<PassiveButton>();
            button.OnClick.RemoveAllListeners();
            button.OnClick.AddListener((Action)(() => ImitatorOnClick(pva.TargetPlayerId)));
        }
    }

    //[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    public static class StartMeetingPatch
    {
        public static void Postfix(MeetingHud __instance)
        {
            if (PlayerIdList.Contains(PlayerControl.LocalPlayer.PlayerId) && PlayerControl.LocalPlayer.IsAlive())
                CreateImitatorButton(__instance);
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
