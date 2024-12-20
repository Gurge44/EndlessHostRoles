using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using AmongUs.GameOptions;
using HarmonyLib;
using Il2CppInterop.Runtime.Attributes;
using TMPro;
using UnityEngine;

namespace EHR.Patches;

// https://github.com/CrowdedMods/CrowdedMod/blob/master/src/CrowdedMod
// Niko adjusted mono behavior patches to fit into non-reactor mods

internal static class Crowded
{
    private static CreateOptionsPicker Instance;
    public static readonly int MaxImpostors = GameOptionsManager.Instance.currentHostOptions.MaxPlayers / 2;
    private static int MaxPlayers => GameStates.IsVanillaServer ? 15 : 127;

    [HarmonyPatch(typeof(CreateOptionsPicker), nameof(CreateOptionsPicker.Awake))]
    public static class CreateOptionsPickerAwake
    {
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public static void Prefix(CreateOptionsPicker __instance)
        {
            Instance = __instance;

            if (GameStates.IsVanillaServer)
            {
                if (GameOptionsManager.Instance.GameHostOptions != null)
                {
                    if (GameOptionsManager.Instance.GameHostOptions.MaxPlayers > 15) { GameOptionsManager.Instance.GameHostOptions.SetInt(Int32OptionNames.MaxPlayers, 15); }
                }
            }
        }

        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public static void Postfix(CreateOptionsPicker __instance)
        {
            if (__instance.mode != SettingsMode.Host) return;

            {
                var firstButtonRenderer = __instance.MaxPlayerButtons[0];
                firstButtonRenderer.GetComponentInChildren<TextMeshPro>().text = "-";
                firstButtonRenderer.enabled = false;
                var firstButtonButton = firstButtonRenderer.GetComponent<PassiveButton>();
                firstButtonButton.OnClick.RemoveAllListeners();

                firstButtonButton.OnClick.AddListener((Action)(() =>
                {
                    for (var i = 1; i < 11; i++)
                    {
                        var playerButton = __instance.MaxPlayerButtons[i];
                        var tmp = playerButton.GetComponentInChildren<TextMeshPro>();
                        var newValue = Mathf.Max(byte.Parse(tmp.text) - 10, byte.Parse(playerButton.name) - 2);
                        tmp.text = newValue.ToString();
                    }

                    __instance.UpdateMaxPlayersButtons(__instance.GetTargetOptions());
                }));

                Object.Destroy(firstButtonRenderer);
                var lastButtonRenderer = __instance.MaxPlayerButtons[^1];
                lastButtonRenderer.GetComponentInChildren<TextMeshPro>().text = "+"; // False error
                lastButtonRenderer.enabled = false; // False error
                var lastButtonButton = lastButtonRenderer.GetComponent<PassiveButton>(); // False error
                lastButtonButton.OnClick.RemoveAllListeners();

                lastButtonButton.OnClick.AddListener((Action)(() =>
                {
                    for (var i = 1; i < 11; i++)
                    {
                        var playerButton = __instance.MaxPlayerButtons[i];
                        var tmp = playerButton.GetComponentInChildren<TextMeshPro>();

                        var newValue = Mathf.Min(byte.Parse(tmp.text) + 10,
                            MaxPlayers - 14 + byte.Parse(playerButton.name));

                        tmp.text = newValue.ToString();
                    }

                    __instance.UpdateMaxPlayersButtons(__instance.GetTargetOptions());
                }));

                Object.Destroy(lastButtonRenderer); // False error

                for (var i = 1; i < 11; i++)
                {
                    var playerButton = __instance.MaxPlayerButtons[i].GetComponent<PassiveButton>();
                    var text = playerButton.GetComponentInChildren<TextMeshPro>();
                    playerButton.OnClick.RemoveAllListeners();

                    playerButton.OnClick.AddListener((Action)(() =>
                    {
                        var maxPlayers = byte.Parse(text.text);
                        var maxImp = Mathf.Min(__instance.GetTargetOptions().NumImpostors, maxPlayers / 2);
                        __instance.GetTargetOptions().SetInt(Int32OptionNames.NumImpostors, maxImp);
                        __instance.ImpostorButtons[1].TextMesh.text = maxImp.ToString();
                        __instance.SetMaxPlayersButtons(maxPlayers);
                    }));
                }

                foreach (var button in __instance.MaxPlayerButtons) { button.enabled = button.GetComponentInChildren<TextMeshPro>().text == __instance.GetTargetOptions().MaxPlayers.ToString(); }
            }

            {
                var secondButton = __instance.ImpostorButtons[1];
                secondButton.SpriteRenderer.enabled = false;
                Object.Destroy(secondButton.transform.FindChild("ConsoleHighlight").gameObject);
                Object.Destroy(secondButton.PassiveButton);
                Object.Destroy(secondButton.BoxCollider);
                var secondButtonText = secondButton.TextMesh;
                secondButtonText.text = __instance.GetTargetOptions().NumImpostors.ToString();
                var firstButton = __instance.ImpostorButtons[0];
                firstButton.SpriteRenderer.enabled = false;
                firstButton.TextMesh.text = "-";
                var firstPassiveButton = firstButton.PassiveButton;
                firstPassiveButton.OnClick.RemoveAllListeners();

                firstPassiveButton.OnClick.AddListener((Action)(() =>
                {
                    var newVal = Mathf.Clamp(
                        byte.Parse(secondButtonText.text) - 1,
                        1,
                        __instance.GetTargetOptions().MaxPlayers / 2
                    );

                    __instance.SetImpostorButtons(newVal);
                    secondButtonText.text = newVal.ToString();
                }));

                var thirdButton = __instance.ImpostorButtons[2];
                thirdButton.SpriteRenderer.enabled = false;
                thirdButton.TextMesh.text = "+";
                var thirdPassiveButton = thirdButton.PassiveButton;
                thirdPassiveButton.OnClick.RemoveAllListeners();

                thirdPassiveButton.OnClick.AddListener((Action)(() =>
                {
                    var newVal = Mathf.Clamp(
                        byte.Parse(secondButtonText.text) + 1,
                        1,
                        __instance.GetTargetOptions().MaxPlayers / 2
                    );

                    __instance.SetImpostorButtons(newVal);
                    secondButtonText.text = newVal.ToString();
                }));
            }
        }
    }

    [HarmonyPatch(typeof(ServerManager), nameof(ServerManager.SetRegion))]
    public static class ServerManager_SetRegion
    {
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public static void Postfix()
        {
            if (GameStates.IsVanillaServer)
            {
                if (GameOptionsManager.Instance.GameHostOptions != null && GameOptionsManager.Instance.GameHostOptions.MaxPlayers > 15)
                    GameOptionsManager.Instance.GameHostOptions.SetInt(Int32OptionNames.MaxPlayers, 15);

                if (Instance)
                {
                    for (var i = 1; i < 11; i++)
                    {
                        var playerButton = Instance.MaxPlayerButtons[i];
                        var tmp = playerButton.GetComponentInChildren<TextMeshPro>();

                        var newValue = Mathf.Min(byte.Parse(tmp.text) + 10,
                            MaxPlayers - 14 + byte.Parse(playerButton.name));

                        tmp.text = newValue.ToString();
                    }

                    Instance.UpdateMaxPlayersButtons(Instance.GetTargetOptions());
                }
            }
        }
    }

    [HarmonyPatch(typeof(CreateOptionsPicker), nameof(CreateOptionsPicker.UpdateMaxPlayersButtons))]
    public static class CreateOptionsPickerUpdateMaxPlayersButtons
    {
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public static bool Prefix(CreateOptionsPicker __instance, [HarmonyArgument(0)] IGameOptions opts)
        {
            if (__instance.mode != SettingsMode.Host) return true;

            if (__instance.CrewArea) { __instance.CrewArea.SetCrewSize(opts.MaxPlayers, opts.NumImpostors); }

            var selectedAsString = opts.MaxPlayers.ToString();

            for (var i = 1; i < __instance.MaxPlayerButtons.Count - 1; i++)
            {
                __instance.MaxPlayerButtons[i].enabled = __instance.MaxPlayerButtons[i].GetComponentInChildren<TextMeshPro>().text == selectedAsString; // False errors
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(CreateOptionsPicker), nameof(CreateOptionsPicker.UpdateImpostorsButtons))]
    public static class CreateOptionsPickerUpdateImpostorsButtons
    {
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public static bool Prefix(CreateOptionsPicker __instance)
        {
            return __instance.mode != SettingsMode.Host;
        }
    }

    [HarmonyPatch(typeof(CreateOptionsPicker), nameof(CreateOptionsPicker.SetImpostorButtons))]
    public static class CreateOptionsPickerSetImpostorButtons
    {
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public static bool Prefix(CreateOptionsPicker __instance, int numImpostors)
        {
            if (__instance.mode != SettingsMode.Host) return true;
            IGameOptions targetOptions = __instance.GetTargetOptions();
            targetOptions.SetInt(Int32OptionNames.NumImpostors, numImpostors);
            __instance.SetTargetOptions(targetOptions);
            __instance.UpdateImpostorsButtons(numImpostors);
            return false;
        }
    }

    [HarmonyPatch(typeof(CreateOptionsPicker), nameof(CreateOptionsPicker.SetMaxPlayersButtons))]
    public static class CreateOptionsPickerSetMaxPlayersButtons
    {
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public static bool Prefix(CreateOptionsPicker __instance, int maxPlayers)
        {
            if (DestroyableSingleton<FindAGameManager>.InstanceExists || __instance.mode != SettingsMode.Host) { return true; }

            IGameOptions targetOptions = __instance.GetTargetOptions();
            targetOptions.SetInt(Int32OptionNames.MaxPlayers, maxPlayers);
            __instance.SetTargetOptions(targetOptions);
            __instance.UpdateMaxPlayersButtons(targetOptions);
            return false;
        }
    }

    [HarmonyPatch(typeof(GameOptionsData), nameof(GameOptionsData.AreInvalid))]
    public static class InvalidOptionsPatches
    {
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public static bool Prefix(GameOptionsData __instance, [HarmonyArgument(0)] int maxExpectedPlayers)
        {
            return __instance.MaxPlayers > maxExpectedPlayers ||
                   __instance.NumImpostors < 1 ||
                   __instance.NumImpostors + 1 > maxExpectedPlayers / 2 ||
                   __instance.KillDistance is < 0 or > 2 ||
                   __instance.PlayerSpeedMod is <= 0f or > 3f;
        }
    }

    [HarmonyPatch(typeof(SecurityLogger), nameof(SecurityLogger.Awake))]
    public static class SecurityLoggerPatch
    {
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public static void Postfix(ref SecurityLogger __instance)
        {
            __instance.Timers = new float[127];
        }
    }

    [HarmonyPatch(typeof(PlayerTab), nameof(PlayerTab.Update))]
    public static class PlayerTabIsSelectedItemEquippedPatch
    {
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public static void Postfix(PlayerTab __instance)
        {
            if (GameOptionsManager.Instance.CurrentGameOptions.MaxPlayers > 15) { __instance.currentColorIsEquipped = false; }
        }
    }

    [HarmonyPatch(typeof(PlayerTab), nameof(PlayerTab.UpdateAvailableColors))]
    public static class PlayerTabUpdateAvailableColorsPatch
    {
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public static bool Prefix(PlayerTab __instance)
        {
            if (GameOptionsManager.Instance.CurrentGameOptions.MaxPlayers <= 15) { return true; }

            __instance.AvailableColors.Clear();

            for (var i = 0; i < Palette.PlayerColors.Count; i++)
            {
                if (!PlayerControl.LocalPlayer || PlayerControl.LocalPlayer.CurrentOutfit.ColorId != i) { __instance.AvailableColors.Add(i); }
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    public static class MeetingHudStartPatch
    {
        public static void Postfix(MeetingHud __instance)
        {
            if (Main.NormalOptions.MaxPlayers <= 15) return;
            __instance.gameObject.AddComponent<MeetingHudPagingBehaviour>().meetingHud = __instance;
        }
    }

    [HarmonyPatch(typeof(ShapeshifterMinigame), nameof(ShapeshifterMinigame.Begin))]
    public static class ShapeshifterMinigameBeginPatch
    {
        public static void Postfix(ShapeshifterMinigame __instance)
        {
            if (Main.NormalOptions.MaxPlayers <= 15) return;
            __instance.gameObject.AddComponent<ShapeShifterPagingBehaviour>().shapeshifterMinigame = __instance;
        }
    }

    [HarmonyPatch(typeof(VitalsMinigame), nameof(VitalsMinigame.Begin))]
    public static class VitalsMinigameBeginPatch
    {
        public static void Postfix(VitalsMinigame __instance)
        {
            if (Main.NormalOptions.MaxPlayers <= 15) return;
            __instance.gameObject.AddComponent<VitalsPagingBehaviour>().vitalsMinigame = __instance;
        }
    }
}

public class AbstractPagingBehaviour(IntPtr ptr) : MonoBehaviour(ptr)
{
    protected const string PageIndexGameObjectName = "CrowdedMod_PageIndex";

    // ReSharper disable once ReplaceWithFieldKeyword
    private int _page;

    protected static int MaxPerPage => 15;

    public virtual int PageIndex
    {
        get => _page;
        set
        {
            _page = value;
            OnPageChanged();
        }
    }

    protected virtual int MaxPageIndex => throw new("MaxPageIndex must be overridden");
    public virtual void Start() => OnPageChanged();

    public virtual void Update()
    {
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.LeftArrow) || Input.mouseScrollDelta.y > 0f)
            Cycle(false);
        else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.RightArrow) || Input.mouseScrollDelta.y < 0f)
            Cycle(true);
    }

    public virtual void OnPageChanged() => throw new("OnPageChanged must be overridden");

    /// <summary>
    /// Loops around if you go over the limits.<br/>
    /// Attempting to go up a page while on the first page will take you to the last page and vice versa.
    /// </summary>
    public virtual void Cycle(bool increment)
    {
        var change = increment ? 1 : -1;
        PageIndex = Mathf.Clamp(PageIndex + change, 0, MaxPageIndex);
    }
}

public class MeetingHudPagingBehaviour(IntPtr ptr) : AbstractPagingBehaviour(ptr)
{
    internal MeetingHud meetingHud = null!;
    [HideFromIl2Cpp] private IEnumerable<PlayerVoteArea> Targets => meetingHud.playerStates.OrderBy(p => p.AmDead);

    protected override int MaxPageIndex => (Targets.Count() - 1) / MaxPerPage;
    public override void Start() => OnPageChanged();

    public override void Update()
    {
        base.Update();
        // Sometimes the timer text is spammed with the page counter for some eccentric reason, so this is just a Band-Aid fix for it
        if (meetingHud.state is MeetingHud.VoteStates.Animating or MeetingHud.VoteStates.Proceeding || meetingHud.TimerText.text.Contains($" ({PageIndex + 1}/{MaxPageIndex + 1})")) return; // TimerText does not update there
        meetingHud.TimerText.text += $" ({PageIndex + 1}/{MaxPageIndex + 1})";
    }

    public override void OnPageChanged()
    {
        var i = 0;

        foreach (var button in Targets)
        {
            if (i >= PageIndex * MaxPerPage && i < (PageIndex + 1) * MaxPerPage)
            {
                button.gameObject.SetActive(true);
                var relativeIndex = i % MaxPerPage;
                var row = relativeIndex / 3;
                var col = relativeIndex % 3;
                var buttonTransform = button.transform;

                buttonTransform.localPosition = meetingHud.VoteOrigin +
                                                new Vector3(
                                                    meetingHud.VoteButtonOffsets.x * col,
                                                    meetingHud.VoteButtonOffsets.y * row,
                                                    buttonTransform.localPosition.z
                                                );
            }
            else button.gameObject.SetActive(false);

            i++;
        }
    }
}

public class ShapeShifterPagingBehaviour(IntPtr ptr) : AbstractPagingBehaviour(ptr)
{
    public ShapeshifterMinigame shapeshifterMinigame = null!;
    private TextMeshPro PageText = null!;
    [HideFromIl2Cpp] private IEnumerable<ShapeshifterPanel> Targets => shapeshifterMinigame.potentialVictims.ToArray();

    protected override int MaxPageIndex => (Targets.Count() - 1) / MaxPerPage;

    public override void Start()
    {
        PageText = Instantiate(HudManager.Instance.KillButton.cooldownTimerText, shapeshifterMinigame.transform);
        PageText.name = PageIndexGameObjectName;
        PageText.enableWordWrapping = false;
        PageText.gameObject.SetActive(true);
        PageText.transform.localPosition = new(4.1f, -2.36f, -1f);
        PageText.transform.localScale *= 0.5f;
        OnPageChanged();
    }

    public override void OnPageChanged()
    {
        PageText.text = $"({PageIndex + 1}/{MaxPageIndex + 1})";
        var i = 0;

        foreach (var panel in Targets)
        {
            if (i >= PageIndex * MaxPerPage && i < (PageIndex + 1) * MaxPerPage)
            {
                panel.gameObject.SetActive(true);
                var relativeIndex = i % MaxPerPage;
                var row = relativeIndex / 3;
                var col = relativeIndex % 3;
                var buttonTransform = panel.transform;

                buttonTransform.localPosition =
                    new(
                        shapeshifterMinigame.XStart + shapeshifterMinigame.XOffset * col,
                        shapeshifterMinigame.YStart + shapeshifterMinigame.YOffset * row,
                        buttonTransform.localPosition.z
                    );
            }
            else panel.gameObject.SetActive(false);

            i++;
        }
    }
}

public class VitalsPagingBehaviour(IntPtr ptr) : AbstractPagingBehaviour(ptr)
{
    public VitalsMinigame vitalsMinigame = null!;
    private TextMeshPro PageText = null!;
    [HideFromIl2Cpp] private IEnumerable<VitalsPanel> Targets => vitalsMinigame.vitals.ToArray();

    protected override int MaxPageIndex => (Targets.Count() - 1) / MaxPerPage;

    public override void Start()
    {
        PageText = Instantiate(HudManager.Instance.KillButton.cooldownTimerText, vitalsMinigame.transform);
        PageText.name = PageIndexGameObjectName;
        PageText.enableWordWrapping = false;
        PageText.gameObject.SetActive(true);
        PageText.transform.localPosition = new(2.7f, -2f, -1f);
        PageText.transform.localScale *= 0.5f;
        OnPageChanged();
    }

    public override void OnPageChanged()
    {
        if (PlayerTask.PlayerHasTaskOfType<HudOverrideTask>(PlayerControl.LocalPlayer))
            return;

        PageText.text = $"({PageIndex + 1}/{MaxPageIndex + 1})";
        var i = 0;

        foreach (var panel in Targets)
        {
            if (i >= PageIndex * MaxPerPage && i < (PageIndex + 1) * MaxPerPage)
            {
                panel.gameObject.SetActive(true);
                var relativeIndex = i % MaxPerPage;
                var row = relativeIndex / 3;
                var col = relativeIndex % 3;
                var panelTransform = panel.transform;

                panelTransform.localPosition =
                    new(
                        vitalsMinigame.XStart + vitalsMinigame.XOffset * col,
                        vitalsMinigame.YStart + vitalsMinigame.YOffset * row,
                        panelTransform.localPosition.z
                    );
            }
            else panel.gameObject.SetActive(false);

            i++;
        }
    }
}