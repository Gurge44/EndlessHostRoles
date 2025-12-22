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
    private static ClientOptionItem HorseMode;
    private static ClientOptionItem LongMode;
    private static ClientOptionItem ShowPlayerInfoInLobby;
    private static ClientOptionItem LobbyMusic;
    private static ClientOptionItem EnableCommandHelper;
    private static ClientOptionItem ShowModdedClientText;
    private static ClientOptionItem AutoHaunt;
    private static ClientOptionItem ButtonCooldownInDecimalUnder10s;
    private static ClientOptionItem CancelPetAnimation;
#if !ANDROID
    private static ClientOptionItem TryFixStuttering;
#endif
#if DEBUG
    private static ClientOptionItem GodMode;
#endif

    public static void Postfix(OptionsMenuBehaviour __instance)
    {
        if (__instance.DisableMouseMovement == null) return;

        Main.SwitchVanilla.Value = false;

        if (Main.ResetOptions || !DebugModeManager.AmDebugger)
        {
            Main.ResetOptions = false;
            Main.GodMode.Value = false;
        }

        if (GM == null || GM.ToggleButton == null)
        {
            GM = ClientOptionItem.Create("GM", Main.GM, __instance, GMButtonToggle);

            static void GMButtonToggle()
            {
                if (Main.GM.Value) HudManager.Instance.ShowPopUp(Translator.GetString("EnabledGMWarning"));
            }
        }

        if (UnlockFPS == null || UnlockFPS.ToggleButton == null)
        {
            UnlockFPS = ClientOptionItem.Create("UnlockFPS", Main.UnlockFps, __instance, UnlockFPSButtonToggle);

            static void UnlockFPSButtonToggle()
            {
                Application.targetFrameRate = Main.UnlockFps.Value ? 120 : 60;
                Logger.SendInGame(string.Format(Translator.GetString("FPSSetTo"), Application.targetFrameRate));
            }
        }

        if (ShowFPS == null || ShowFPS.ToggleButton == null)
            ShowFPS = ClientOptionItem.Create("ShowFPS", Main.ShowFps, __instance);

        if (AutoStart == null || AutoStart.ToggleButton == null)
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

        if (ForceOwnLanguage == null || ForceOwnLanguage.ToggleButton == null)
            ForceOwnLanguage = ClientOptionItem.Create("ForceOwnLanguage", Main.ForceOwnLanguage, __instance);

        if (ForceOwnLanguageRoleName == null || ForceOwnLanguageRoleName.ToggleButton == null)
            ForceOwnLanguageRoleName = ClientOptionItem.Create("ForceOwnLanguageRoleName", Main.ForceOwnLanguageRoleName, __instance);

        if (EnableCustomButton == null || EnableCustomButton.ToggleButton == null)
            EnableCustomButton = ClientOptionItem.Create("EnableCustomButton", Main.EnableCustomButton, __instance);

        if (EnableCustomSoundEffect == null || EnableCustomSoundEffect.ToggleButton == null)
            EnableCustomSoundEffect = ClientOptionItem.Create("EnableCustomSoundEffect", Main.EnableCustomSoundEffect, __instance);

        if (SwitchVanilla == null || SwitchVanilla.ToggleButton == null)
        {
            SwitchVanilla = ClientOptionItem.Create("SwitchVanilla", Main.SwitchVanilla, __instance, SwitchVanillaButtonToggle);

            static void SwitchVanillaButtonToggle()
            {
                if (PlayerControl.LocalPlayer != null)
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
                    MainMenuManagerPatch.ShowRightPanelImmediately();

                    Harmony.UnpatchAll();
                    Main.Instance.Unload();
                }
            }
        }

        if (DarkTheme == null || DarkTheme.ToggleButton == null)
            DarkTheme = ClientOptionItem.Create("EnableDarkTheme", Main.DarkTheme, __instance);
        
        if (DarkThemeForMeetingUI == null || DarkThemeForMeetingUI.ToggleButton == null)
            DarkThemeForMeetingUI = ClientOptionItem.Create("DarkThemeForMeetingUI", Main.DarkThemeForMeetingUI, __instance);

        if (HorseMode == null || HorseMode.ToggleButton == null)
        {
            HorseMode = ClientOptionItem.Create("HorseMode", Main.HorseMode, __instance, SwitchHorseMode);

            static void SwitchHorseMode()
            {
                Main.LongMode.Value = false;
                HorseMode.UpdateToggle();
                LongMode.UpdateToggle();

                foreach (PlayerControl pc in Main.AllPlayerControls)
                {
                    pc.MyPhysics.SetBodyType(pc.BodyType);
                    if (pc.BodyType == PlayerBodyTypes.Normal) pc.cosmetics.currentBodySprite.BodySprite.transform.localScale = new(0.5f, 0.5f, 1f);
                }
            }
        }

        if (LongMode == null || LongMode.ToggleButton == null)
        {
            LongMode = ClientOptionItem.Create("LongMode", Main.LongMode, __instance, SwitchLongMode);

            static void SwitchLongMode()
            {
                Main.HorseMode.Value = false;
                HorseMode.UpdateToggle();
                LongMode.UpdateToggle();

                foreach (PlayerControl pc in Main.AllPlayerControls)
                {
                    pc.MyPhysics.SetBodyType(pc.BodyType);
                    if (pc.BodyType == PlayerBodyTypes.Normal) pc.cosmetics.currentBodySprite.BodySprite.transform.localScale = new(0.5f, 0.5f, 1f);
                }
            }
        }

        if (ShowPlayerInfoInLobby == null || ShowPlayerInfoInLobby.ToggleButton == null)
        {
            ShowPlayerInfoInLobby = ClientOptionItem.Create("ShowPlayerInfoInLobby", Main.ShowPlayerInfoInLobby, __instance, ShowPlayerInfoInLobbyButtonToggle);

            static void ShowPlayerInfoInLobbyButtonToggle() => Utils.DirtyName.UnionWith(Main.AllPlayerControls.Select(x => x.PlayerId));
        }

        if (LobbyMusic == null || LobbyMusic.ToggleButton == null)
            LobbyMusic = ClientOptionItem.Create("LobbyMusic", Main.LobbyMusic, __instance);

        if (EnableCommandHelper == null || EnableCommandHelper.ToggleButton == null)
            EnableCommandHelper = ClientOptionItem.Create("EnableCommandHelper", Main.EnableCommandHelper, __instance);

        if (ShowModdedClientText == null || ShowModdedClientText.ToggleButton == null)
            ShowModdedClientText = ClientOptionItem.Create("ShowModdedClientText", Main.ShowModdedClientText, __instance);

        if (AutoHaunt == null || AutoHaunt.ToggleButton == null)
        {
            AutoHaunt = ClientOptionItem.Create("AutoHaunt", Main.AutoHaunt, __instance, AutoHauntButtonToggle);

            static void AutoHauntButtonToggle()
            {
                if (Main.AutoHaunt.Value)
                    Modules.AutoHaunt.Start();
            }
        }
        
        if (ButtonCooldownInDecimalUnder10s == null || ButtonCooldownInDecimalUnder10s.ToggleButton == null)
            ButtonCooldownInDecimalUnder10s = ClientOptionItem.Create("ButtonCooldownInDecimalUnder10s", Main.ButtonCooldownInDecimalUnder10s, __instance);

        if (CancelPetAnimation == null || CancelPetAnimation.ToggleButton == null)
            CancelPetAnimation = ClientOptionItem.Create("CancelPetAnimation", Main.CancelPetAnimation, __instance);
        
#if !ANDROID
        if (TryFixStuttering == null || TryFixStuttering.ToggleButton == null)
        {
            TryFixStuttering = ClientOptionItem.Create("TryFixStuttering", Main.TryFixStuttering, __instance, TryFixStutteringButtonToggle);

            [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
            static void TryFixStutteringButtonToggle()
            {
                if (Main.TryFixStuttering.Value)
                {
                    if (Application.platform == RuntimePlatform.WindowsPlayer && Environment.ProcessorCount >= 4)
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
#endif

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