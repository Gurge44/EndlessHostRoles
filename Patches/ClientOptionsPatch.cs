using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace EHR;

//��Դ��https://github.com/tukasa0001/TownOfHost/pull/1265
[HarmonyPatch(typeof(OptionsMenuBehaviour), nameof(OptionsMenuBehaviour.Start))]
public static class OptionsMenuBehaviourStartPatch
{
    private static ClientOptionItem GM;
    private static ClientOptionItem UnlockFPS;
    private static ClientOptionItem ShowFPS;
    private static ClientOptionItem AutoStart;
    private static ClientOptionItem ForceOwnLanguage;
    private static ClientOptionItem ForceOwnLanguageRoleName;
    private static ClientOptionItem EnableCustomButton;
    private static ClientOptionItem EnableCustomSoundEffect;
    private static ClientOptionItem SwitchVanilla;
    private static ClientOptionItem DarkTheme;
    private static ClientOptionItem DarkThemeForMeetingUI;
    private static ClientOptionItem ShowPlayerInfoInLobby;
    private static ClientOptionItem HorseMode;
    private static ClientOptionItem LongMode;
    private static ClientOptionItem ClassicMode;
    private static ClientOptionItem LobbyMusic;
    private static ClientOptionItem EnableCommandHelper;
    private static ClientOptionItem ShowModdedClientText;
    private static ClientOptionItem AutoHaunt;
    private static ClientOptionItem ButtonCooldownInDecimalUnder10s;
    private static ClientOptionItem CancelPetAnimation;
    private static ClientOptionItem TryFixStuttering;
    private static ClientOptionItem ShowClientControlGUI;
#if DEBUG
    private static ClientOptionItem GodMode;
#endif

    public static void Postfix(OptionsMenuBehaviour __instance)
    {
        if (!__instance.DisableMouseMovement) return;

        Main.SwitchVanilla.Value = false;

        if (Main.ResetOptions || !DebugModeManager.AmDebugger)
        {
            Main.ResetOptions = false;
            Main.GodMode.Value = false;
        }

        if (GM == null || !GM.ToggleButton)
        {
            GM = ClientOptionItem.Create("GM", Main.GM, __instance, GMButtonToggle);

            static void GMButtonToggle()
            {
                if (Main.GM.Value) HudManager.Instance.ShowPopUp(Translator.GetString("EnabledGMWarning"));
            }
        }

        if (UnlockFPS == null || !UnlockFPS.ToggleButton)
        {
            UnlockFPS = ClientOptionItem.Create("UnlockFPS", Main.UnlockFps, __instance, UnlockFPSButtonToggle);

            static void UnlockFPSButtonToggle()
            {
                Application.targetFrameRate = Main.UnlockFps.Value ? 120 : 60;
                Logger.SendInGame(string.Format(Translator.GetString("FPSSetTo"), Application.targetFrameRate));
            }
        }

        if (ShowFPS == null || !ShowFPS.ToggleButton)
            ShowFPS = ClientOptionItem.Create("ShowFPS", Main.ShowFps, __instance);

        if (AutoStart == null || !AutoStart.ToggleButton)
        {
            AutoStart = ClientOptionItem.Create("AutoStart", Main.AutoStart, __instance, AutoStartButtonToggle);

            static void AutoStartButtonToggle()
            {
                if (!Main.AutoStart.Value && GameStates.IsCountDown)
                {
                    GameStartManager.Instance.ResetStartState();
                    Logger.SendInGame(Translator.GetString("CancelStartCountDown"));
                }
            }
        }

        if (ForceOwnLanguage == null || !ForceOwnLanguage.ToggleButton)
            ForceOwnLanguage = ClientOptionItem.Create("ForceOwnLanguage", Main.ForceOwnLanguage, __instance);

        if (ForceOwnLanguageRoleName == null || !ForceOwnLanguageRoleName.ToggleButton)
            ForceOwnLanguageRoleName = ClientOptionItem.Create("ForceOwnLanguageRoleName", Main.ForceOwnLanguageRoleName, __instance);

        if (EnableCustomButton == null || !EnableCustomButton.ToggleButton)
            EnableCustomButton = ClientOptionItem.Create("EnableCustomButton", Main.EnableCustomButton, __instance);

        if (EnableCustomSoundEffect == null || !EnableCustomSoundEffect.ToggleButton)
            EnableCustomSoundEffect = ClientOptionItem.Create("EnableCustomSoundEffect", Main.EnableCustomSoundEffect, __instance);

        if (SwitchVanilla == null || !SwitchVanilla.ToggleButton)
        {
            SwitchVanilla = ClientOptionItem.Create("SwitchVanilla", Main.SwitchVanilla, __instance, SwitchVanillaButtonToggle);

            static void SwitchVanillaButtonToggle()
            {
                if (PlayerControl.LocalPlayer)
                {
                    Zoom.SetZoomSize(reset: true);
                    AmongUsClient.Instance.ExitGame(DisconnectReasons.ExitGame);
                    SceneChanger.ChangeScene("MainMenu");
                    LateTask.New(() => HudManager.Instance.ShowPopUp(Translator.GetString("RejoinRequiredDueToVanillaSwitch")), 1.9f, log: false);
                    LateTask.New(Unload, 2f, log: false);
                }
                else
                    Unload();

                return;

                static void Unload()
                {
                    if (ClientControlGUI.Instance) Object.Destroy(ClientControlGUI.Instance);
                    MainMenuManagerPatch.ShowRightPanelImmediately();

                    Main.Instance.Harmony.UnpatchSelf();
                    Main.Instance.Unload();
                }
            }
        }

        if (DarkTheme == null || !DarkTheme.ToggleButton)
            DarkTheme = ClientOptionItem.Create("EnableDarkTheme", Main.DarkTheme, __instance);
        
        if (DarkThemeForMeetingUI == null || !DarkThemeForMeetingUI.ToggleButton)
            DarkThemeForMeetingUI = ClientOptionItem.Create("DarkThemeForMeetingUI", Main.DarkThemeForMeetingUI, __instance);

        if (ShowPlayerInfoInLobby == null || !ShowPlayerInfoInLobby.ToggleButton)
        {
            ShowPlayerInfoInLobby = ClientOptionItem.Create("ShowPlayerInfoInLobby", Main.ShowPlayerInfoInLobby, __instance, ShowPlayerInfoInLobbyButtonToggle);

            static void ShowPlayerInfoInLobbyButtonToggle() => Utils.DirtyName.UnionWith(Main.EnumeratePlayerControls().Select(x => x.PlayerId));
        }

        if (HorseMode == null || !HorseMode.ToggleButton)
        {
            HorseMode = ClientOptionItem.Create("HorseMode", Main.HorseMode, __instance, SwitchHorseMode);

            static void SwitchHorseMode()
            {
                Main.LongMode.Value = false;
                Main.ClassicMode.Value = false;
                HorseMode.UpdateToggle();
                LongMode.UpdateToggle();
                ClassicMode.UpdateToggle();

                foreach (PlayerControl pc in Main.EnumeratePlayerControls())
                {
                    pc.MyPhysics.SetBodyType(pc.BodyType);
                    if (pc.BodyType == PlayerBodyTypes.Normal) pc.cosmetics.currentBodySprite.BodySprite.transform.localScale = new(0.5f, 0.5f, 1f);
                }
            }
        }

        if (LongMode == null || !LongMode.ToggleButton)
        {
            LongMode = ClientOptionItem.Create("LongMode", Main.LongMode, __instance, SwitchLongMode);

            static void SwitchLongMode()
            {
                Main.HorseMode.Value = false;
                Main.ClassicMode.Value = false;
                HorseMode.UpdateToggle();
                LongMode.UpdateToggle();
                ClassicMode.UpdateToggle();

                foreach (PlayerControl pc in Main.EnumeratePlayerControls())
                {
                    pc.MyPhysics.SetBodyType(pc.BodyType);
                    if (pc.BodyType == PlayerBodyTypes.Normal) pc.cosmetics.currentBodySprite.BodySprite.transform.localScale = new(0.5f, 0.5f, 1f);
                }
            }
        }

        if (ClassicMode == null || !ClassicMode.ToggleButton)
        {
            ClassicMode = ClientOptionItem.Create("ClassicMode", Main.ClassicMode, __instance, SwitchClassicMode);

            static void SwitchClassicMode()
            {
                Main.HorseMode.Value = false;
                Main.LongMode.Value = false;
                HorseMode.UpdateToggle();
                LongMode.UpdateToggle();
                ClassicMode.UpdateToggle();

                foreach (PlayerControl pc in Main.EnumeratePlayerControls())
                {
                    pc.MyPhysics.SetBodyType(pc.BodyType);
                    if (pc.BodyType == PlayerBodyTypes.Normal) pc.cosmetics.currentBodySprite.BodySprite.transform.localScale = new(0.5f, 0.5f, 1f);
                }
            }
        }

        if (LobbyMusic == null || !LobbyMusic.ToggleButton)
            LobbyMusic = ClientOptionItem.Create("LobbyMusic", Main.LobbyMusic, __instance);

        if (EnableCommandHelper == null || !EnableCommandHelper.ToggleButton)
            EnableCommandHelper = ClientOptionItem.Create("EnableCommandHelper", Main.EnableCommandHelper, __instance);

        if (ShowModdedClientText == null || !ShowModdedClientText.ToggleButton)
            ShowModdedClientText = ClientOptionItem.Create("ShowModdedClientText", Main.ShowModdedClientText, __instance);

        if (AutoHaunt == null || !AutoHaunt.ToggleButton)
        {
            AutoHaunt = ClientOptionItem.Create("AutoHaunt", Main.AutoHaunt, __instance, AutoHauntButtonToggle);

            static void AutoHauntButtonToggle()
            {
                if (Main.AutoHaunt.Value)
                    Modules.AutoHaunt.Start();
            }
        }
        
        if (ButtonCooldownInDecimalUnder10s == null || !ButtonCooldownInDecimalUnder10s.ToggleButton)
            ButtonCooldownInDecimalUnder10s = ClientOptionItem.Create("ButtonCooldownInDecimalUnder10s", Main.ButtonCooldownInDecimalUnder10s, __instance);

        if (CancelPetAnimation == null || !CancelPetAnimation.ToggleButton)
            CancelPetAnimation = ClientOptionItem.Create("CancelPetAnimation", Main.CancelPetAnimation, __instance);

        if (OperatingSystem.IsWindows() && (TryFixStuttering == null || !TryFixStuttering.ToggleButton))
        {
            TryFixStuttering = ClientOptionItem.Create("TryFixStuttering", Main.TryFixStuttering, __instance, TryFixStutteringButtonToggle);

            [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
            static void TryFixStutteringButtonToggle()
            {
                if (!OperatingSystem.IsWindows()) return;
                
                if (Main.TryFixStuttering.Value)
                {
                    if (Environment.ProcessorCount >= 4)
                    {
                        var process = Process.GetCurrentProcess();
                        Main.OriginalAffinity = process.ProcessorAffinity;
                        process.ProcessorAffinity = (IntPtr)((1 << 2) | (1 << 3));
                    }
                }
                else
                {
                    if (Main.OriginalAffinity.HasValue)
                    {
                        var proc = Process.GetCurrentProcess();
                        proc.ProcessorAffinity = Main.OriginalAffinity.Value;
                        Main.OriginalAffinity = null;
                    }
                }
            }
        }
        
        if (ShowClientControlGUI == null || !ShowClientControlGUI.ToggleButton)
        {
            ShowClientControlGUI = ClientOptionItem.Create("ShowClientControlGUI", Main.ShowClientControlGUI, __instance, ShowClientControlGUIButtonToggle);

            static void ShowClientControlGUIButtonToggle()
            {
                switch (Main.ShowClientControlGUI.Value)
                {
                    case true when !ClientControlGUI.Instance:
                        Main.Instance.AddComponent<ClientControlGUI>();
                        break;
                    case false when ClientControlGUI.Instance:
                        Object.Destroy(ClientControlGUI.Instance);
                        break;
                }
            }
        }

#if DEBUG
        if ((GodMode == null || GodMode.ToggleButton == null) && DebugModeManager.AmDebugger)
        {
            GodMode = ClientOptionItem.Create("GodMode", Main.GodMode, __instance);
        }
#endif
    }
}

[HarmonyPatch(typeof(OptionsMenuBehaviour), nameof(OptionsMenuBehaviour.Close))]
public static class OptionsMenuBehaviourClosePatch
{
    public static void Postfix()
    {
        ClientOptionItem.CustomBackground?.gameObject.SetActive(false);
    }
}