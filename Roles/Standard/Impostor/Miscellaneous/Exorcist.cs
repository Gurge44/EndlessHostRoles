using System;
using TMPro;
using UnityEngine;

namespace EHR.Roles;

public class Exorcist : RoleBase
{
    public static bool On;

    public static OptionItem AbilityDuration;
    private static OptionItem AbilityUseLimit;
    private static OptionItem AbilityUseGainWithEachKill;

    public static long AbilityEndTS;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(658500)
            .AutoSetupOption(ref AbilityDuration, 15, new IntegerValueRule(1, 120, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref AbilityUseLimit, 1f, new FloatValueRule(0, 20, 0.05f), OptionFormat.Times)
            .AutoSetupOption(ref AbilityUseGainWithEachKill, 0.4f, new FloatValueRule(0f, 5f, 0.25f), OptionFormat.Times);
    }

    public override void Init()
    {
        On = false;
        AbilityEndTS = 0;
    }

    public override void Add(byte playerId)
    {
        On = true;
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetFloat());
    }

    public override void OnMeetingShapeshift(PlayerControl shapeshifter, PlayerControl target)
    {
        const string command = "/exo";
        ChatCommands.ExoCommand(shapeshifter, command, command.Split(' '));
    }

    private static void ExorcistOnClick(GameObject exorcistButton)
    {
        Logger.Msg($"Exorcist Click: ID {PlayerControl.LocalPlayer.PlayerId}", "Exorcist UI");
        if (PlayerControl.LocalPlayer == null || !PlayerControl.LocalPlayer.IsAlive() || !GameStates.IsVoting) return;

        const string command = "/exo";

        if (AmongUsClient.Instance.AmHost)
            ChatCommands.ExoCommand(PlayerControl.LocalPlayer, command, command.Split(' '));
        else
            ChatCommands.RequestCommandProcessingFromHost(command, "Exo");

        exorcistButton.SetActive(false);
        LateTask.New(() => exorcistButton.SetActive(true), 1f, "ExorcistButton");
    }

    private static void CreateExorcistButton(MeetingHud __instance)
    {
        GameObject existingButton = GameObject.Find("ExorcistButton");
        if (existingButton) Object.Destroy(existingButton.gameObject);
        if (!PlayerControl.LocalPlayer || !PlayerControl.LocalPlayer.IsAlive()) return;

        GameObject parent = GameObject.Find("Main Camera").transform.Find("Hud").Find("ChatUi").Find("ChatScreenRoot").Find("ChatScreenContainer").gameObject;
        GameObject template = __instance.transform.Find("MeetingContents").Find("ButtonStuff").Find("button_skipVoting").gameObject;
        GameObject exorcistButton = Object.Instantiate(template, parent.transform);
        exorcistButton.name = "ExorcistButton";
        exorcistButton.transform.localPosition = new(3.46f, 0f, 45f);
        exorcistButton.SetActive(true);
        var renderer = exorcistButton.GetComponent<SpriteRenderer>();
        renderer.sprite = CustomButton.Get("MeetingKillButton");
        renderer.color = Color.white;
        GameObject Text_TMP = exorcistButton.GetComponentInChildren<TextMeshPro>().gameObject;
        Text_TMP.SetActive(false);
        var button = exorcistButton.GetComponent<PassiveButton>();
        button.OnClick.RemoveAllListeners();
        button.OnClick.AddListener((Action)(() => ExorcistOnClick(exorcistButton)));
        GameObject ControllerHighlight = exorcistButton.transform.Find("ControllerHighlight").gameObject;
        ControllerHighlight.transform.localScale = new(0.5f, 2f, 0.5f);
    }

    //[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    public static class StartMeetingPatch
    {
        public static void Postfix(MeetingHud __instance)
        {
            if (PlayerControl.LocalPlayer.IsAlive() && PlayerControl.LocalPlayer.Is(CustomRoles.Exorcist))
                CreateExorcistButton(__instance);
        }
    }
}