using System;
using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using Hazel;
using TMPro;
using UnityEngine;

namespace EHR.Neutral;

public class Starspawn : RoleBase
{
    public static bool On;
    private static List<Starspawn> Instances = [];

    private static OptionItem AbilityUseLimit;
    private static OptionItem AbilityCooldown;

    public static bool IsDayBreak;
    public bool HasUsedDayBreak;

    public HashSet<byte> IsolatedPlayers = [];

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(645700)
            .AutoSetupOption(ref AbilityUseLimit, 2f, new FloatValueRule(0, 20, 0.05f), OptionFormat.Times)
            .AutoSetupOption(ref AbilityCooldown, 30f, new FloatValueRule(0.5f, 120f, 0.5f), OptionFormat.Seconds);
    }

    public override void Init()
    {
        On = false;
        Instances = [];
        IsDayBreak = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        Instances.Add(this);
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetFloat());
        IsolatedPlayers = [];
        HasUsedDayBreak = false;
    }

    public override void Remove(byte playerId)
    {
        Instances.Remove(this);
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return pc.IsAlive();
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = AbilityCooldown.GetFloat();
    }

    public override void AfterMeetingTasks()
    {
        IsDayBreak = false;
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        IsolatedPlayers.Add(target.PlayerId);
        Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
        Utils.SendRPC(CustomRPC.SyncRoleData, killer.PlayerId, target.PlayerId);
        return false;
    }

    public override void OnMeetingShapeshift(PlayerControl shapeshifter, PlayerControl target)
    {
        const string command = "/daybreak";
        ChatCommands.DayBreakCommand(shapeshifter, "Command.Daybreak", command, command.Split(' '));
    }

    public void ReceiveRPC(MessageReader reader)
    {
        IsolatedPlayers.Add(reader.ReadByte());
    }

    private static void StarspawnOnClick(GameObject starspawnButton)
    {
        Logger.Msg($"Starspawn Click: ID {PlayerControl.LocalPlayer.PlayerId}", "Starspawn UI");
        if (PlayerControl.LocalPlayer == null || !PlayerControl.LocalPlayer.IsAlive() || !GameStates.IsVoting) return;

        if (AmongUsClient.Instance.AmHost)
        {
            var command = $"/daybreak";
            ChatCommands.DayBreakCommand(PlayerControl.LocalPlayer, "Command.Daybreak", command, command.Split(' '));
        }
        else
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.StarspawnClick, SendOption.Reliable, AmongUsClient.Instance.HostId);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        starspawnButton.SetActive(false);
        LateTask.New(() => starspawnButton.SetActive(true), 1f, "StarspawnButton");
    }

    public static void CreateStarspawnButton(MeetingHud __instance)
    {
        if (GameObject.Find("StarspawnButton") != null) Object.Destroy(GameObject.Find("StarspawnButton").gameObject);
        if (PlayerControl.LocalPlayer == null || !PlayerControl.LocalPlayer.IsAlive()) return;
        
        GameObject parent = GameObject.Find("Main Camera").transform.Find("Hud").Find("ChatUi").Find("ChatScreenRoot").Find("ChatScreenContainer").gameObject;
        GameObject template = __instance.transform.Find("MeetingContents").Find("ButtonStuff").Find("button_skipVoting").gameObject;
        GameObject starspawnButton = Object.Instantiate(template, parent.transform);
        starspawnButton.name = "StarspawnButton";
        starspawnButton.transform.localPosition = new(3.46f, 0f, 45f);
        starspawnButton.SetActive(true);
        var renderer = starspawnButton.GetComponent<SpriteRenderer>();
        renderer.sprite = CustomButton.Get("StarlightIcon");
        renderer.color = Color.white;
        GameObject Text_TMP = starspawnButton.GetComponentInChildren<TextMeshPro>().gameObject;
        Text_TMP.SetActive(false);
        var button = starspawnButton.GetComponent<PassiveButton>();
        button.OnClick.RemoveAllListeners();
        button.OnClick.AddListener((Action)(() => StarspawnOnClick(starspawnButton)));
        GameObject ControllerHighlight = starspawnButton.transform.Find("ControllerHighlight").gameObject;
        ControllerHighlight.transform.localScale = new (0.5f, 2f, 0.5f);
    }

    //[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    public static class StartMeetingPatch
    {
        public static void Postfix(MeetingHud __instance)
        {
            if (PlayerControl.LocalPlayer.Is(CustomRoles.Starspawn) && PlayerControl.LocalPlayer.IsAlive())
                CreateStarspawnButton(__instance);
        }
    }

    public static bool CheckInteraction(PlayerControl killer, PlayerControl target)
    {
        return !killer.Is(Team.Crewmate) || !Instances.Any(x => x.IsolatedPlayers.Contains(target.PlayerId));
    }
}
