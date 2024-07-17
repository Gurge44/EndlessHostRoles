using HarmonyLib;
using UnityEngine;

namespace EHR;

//��Դ��https://github.com/tukasa0001/TownOfHost/pull/1265
[HarmonyPatch(typeof(OptionsMenuBehaviour), nameof(OptionsMenuBehaviour.Start))]
public static class OptionsMenuBehaviourStartPatch
{
    private static ClientOptionItem GM;
    private static ClientOptionItem UnlockFPS;
    private static ClientOptionItem AutoStart;
    private static ClientOptionItem ForceOwnLanguage;
    private static ClientOptionItem ForceOwnLanguageRoleName;
    private static ClientOptionItem EnableCustomButton;
    private static ClientOptionItem EnableCustomSoundEffect;
    private static ClientOptionItem SwitchVanilla;
    private static ClientOptionItem DarkTheme;
    private static ClientOptionItem HorseMode;
    private static ClientOptionItem LongMode;
    private static ClientOptionItem ShowPlayerInfoInLobby;
    private static ClientOptionItem LobbyMusic;

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
            GM = ClientOptionItem.Create("GM", Main.GM, __instance);
        }

        if (UnlockFPS == null || UnlockFPS.ToggleButton == null)
        {
            UnlockFPS = ClientOptionItem.Create("UnlockFPS", Main.UnlockFps, __instance, UnlockFPSButtonToggle);

            static void UnlockFPSButtonToggle()
            {
                Application.targetFrameRate = Main.UnlockFps.Value ? 165 : 60;
                Logger.SendInGame(string.Format(Translator.GetString("FPSSetTo"), Application.targetFrameRate));
            }
        }

        if (AutoStart == null || AutoStart.ToggleButton == null)
        {
            AutoStart = ClientOptionItem.Create("AutoStart", Main.AutoStart, __instance, AutoStartButtonToggle);

            static void AutoStartButtonToggle()
            {
                if (Main.AutoStart.Value == false && GameStates.IsCountDown)
                {
                    GameStartManager.Instance.ResetStartState();
                    Logger.SendInGame(Translator.GetString("CancelStartCountDown"));
                }
            }
        }

        if (ForceOwnLanguage == null || ForceOwnLanguage.ToggleButton == null)
        {
            ForceOwnLanguage = ClientOptionItem.Create("ForceOwnLanguage", Main.ForceOwnLanguage, __instance);
        }

        if (ForceOwnLanguageRoleName == null || ForceOwnLanguageRoleName.ToggleButton == null)
        {
            ForceOwnLanguageRoleName = ClientOptionItem.Create("ForceOwnLanguageRoleName", Main.ForceOwnLanguageRoleName, __instance);
        }

        if (EnableCustomButton == null || EnableCustomButton.ToggleButton == null)
        {
            EnableCustomButton = ClientOptionItem.Create("EnableCustomButton", Main.EnableCustomButton, __instance);
        }

        if (EnableCustomSoundEffect == null || EnableCustomSoundEffect.ToggleButton == null)
        {
            EnableCustomSoundEffect = ClientOptionItem.Create("EnableCustomSoundEffect", Main.EnableCustomSoundEffect, __instance);
        }

        if (SwitchVanilla == null || SwitchVanilla.ToggleButton == null)
        {
            SwitchVanilla = ClientOptionItem.Create("SwitchVanilla", Main.SwitchVanilla, __instance, SwitchVanillaButtonToggle);

            static void SwitchVanillaButtonToggle()
            {
                if (PlayerControl.LocalPlayer == null) MainMenuManagerPatch.ShowRightPanelImmediately();
                Harmony.UnpatchAll();
                Main.Instance.Unload();
            }
        }

        if (DarkTheme == null || DarkTheme.ToggleButton == null)
        {
            DarkTheme = ClientOptionItem.Create("EnableDarkTheme", Main.DarkTheme, __instance);
        }

        if (HorseMode == null || HorseMode.ToggleButton == null)
        {
            HorseMode = ClientOptionItem.Create("HorseMode", Main.HorseMode, __instance, SwitchHorseMode);

            static void SwitchHorseMode()
            {
                Main.LongMode.Value = false;
                HorseMode.UpdateToggle();
                LongMode.UpdateToggle();
                foreach (var pc in Main.AllPlayerControls)
                {
                    pc.MyPhysics.SetBodyType(pc.BodyType);
                    if (pc.BodyType == PlayerBodyTypes.Normal)
                    {
                        pc.cosmetics.currentBodySprite.BodySprite.transform.localScale = new(0.5f, 0.5f, 1f);
                    }
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
                foreach (var pc in Main.AllPlayerControls)
                {
                    pc.MyPhysics.SetBodyType(pc.BodyType);
                    if (pc.BodyType == PlayerBodyTypes.Normal)
                    {
                        pc.cosmetics.currentBodySprite.BodySprite.transform.localScale = new(0.5f, 0.5f, 1f);
                    }
                }
            }
        }

        if (ShowPlayerInfoInLobby == null || ShowPlayerInfoInLobby.ToggleButton == null)
        {
            ShowPlayerInfoInLobby = ClientOptionItem.Create("ShowPlayerInfoInLobby", Main.ShowPlayerInfoInLobby, __instance);
        }

        if (LobbyMusic == null || LobbyMusic.ToggleButton == null)
        {
            LobbyMusic = ClientOptionItem.Create("LobbyMusic", Main.LobbyMusic, __instance, LobbyMusicButtonToggle);

            void LobbyMusicButtonToggle()
            {
                if (!Main.LobbyMusic.Value && GameStates.IsLobby)
                {
                    SoundManager.Instance.StopAllSound();
                    LateTask.New(() =>
                    {
                        Main.LobbyMusic.Value = true;
                        LobbyMusic.UpdateToggle();
                    }, 5f, log: false);
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
#if DEBUG
    private static ClientOptionItem GodMode;
#endif
}

[HarmonyPatch(typeof(OptionsMenuBehaviour), nameof(OptionsMenuBehaviour.Close))]
public static class OptionsMenuBehaviourClosePatch
{
    public static void Postfix()
    {
        ClientOptionItem.CustomBackground?.gameObject.SetActive(false);

        if (GameStates.InGame && GameStates.IsVoting && !DestroyableSingleton<HudManager>.Instance.Chat.IsOpenOrOpening)
            GuessManager.CreateIDLabels(MeetingHud.Instance);
    }
}