using System;
using AmongUs.GameOptions;
using HarmonyLib;

namespace EHR.Patches;

[HarmonyPatch(typeof(GameOptionsManager), nameof(GameOptionsManager.SwitchGameMode))]
static class SwitchGameModePatch
{
    private static bool Warned;

    public static bool Prefix(GameModes gameMode)
    {
        if (!AmongUsClient.Instance.AmHost || gameMode != GameModes.HideNSeek || Warned) return true;
        HudManager.Instance.ShowPopUp(Translator.GetString("HnSUnloadWarning"));
        return false;
    }

    public static void Postfix(GameModes gameMode)
    {
        if (!AmongUsClient.Instance.AmHost || gameMode != GameModes.HideNSeek) return;

        if (!Warned)
        {
            Warned = true;
            return;
        }

        ErrorText.Instance.HnSFlag = true;
        ErrorText.Instance.AddError(ErrorCode.HnsUnload);
        Zoom.SetZoomSize(reset: true);
        Harmony.UnpatchAll();
        Main.Instance.Unload();
    }
}

[HarmonyPatch(typeof(NormalGameOptionsV08), nameof(NormalGameOptionsV08.SetRecommendations), typeof(int), typeof(bool), typeof(RulesPresets))]
[HarmonyPatch(typeof(NormalGameOptionsV08), nameof(NormalGameOptionsV08.SetRecommendations), typeof(int), typeof(bool))]
static class SetRecommendationsPatch
{
    public static void Postfix(NormalGameOptionsV08 __instance,
        [HarmonyArgument(0)] int numPlayers,
        [HarmonyArgument(1)] bool isOnline)
    {
        numPlayers = Math.Clamp(numPlayers, 4, 15);
        __instance.PlayerSpeedMod = __instance.MapId == 4 ? 1.5f : 1.25f;
        __instance.CrewLightMod = 0.5f;
        __instance.ImpostorLightMod = 1.25f;
        __instance.KillCooldown = 27.5f;
        __instance.NumCommonTasks = 1;
        __instance.NumLongTasks = 3;
        __instance.NumShortTasks = 4;
        __instance.NumEmergencyMeetings = 1;
        __instance.NumImpostors = GetRecommendedImpostors();
        __instance.KillDistance = 1;
        __instance.DiscussionTime = 0;
        __instance.VotingTime = 120;
        __instance.IsDefaults = true;
        __instance.ConfirmImpostor = false;
        __instance.VisualTasks = false;

        __instance.roleOptions.SetRoleRate(RoleTypes.Shapeshifter, 0, 0);
        __instance.roleOptions.SetRoleRate(RoleTypes.Scientist, 0, 0);
        __instance.roleOptions.SetRoleRate(RoleTypes.GuardianAngel, 0, 0);
        __instance.roleOptions.SetRoleRate(RoleTypes.Engineer, 0, 0);
        __instance.roleOptions.SetRoleRecommended(RoleTypes.Shapeshifter);
        __instance.roleOptions.SetRoleRecommended(RoleTypes.Scientist);
        __instance.roleOptions.SetRoleRecommended(RoleTypes.GuardianAngel);
        __instance.roleOptions.SetRoleRecommended(RoleTypes.Engineer);

        switch (Options.CurrentGameMode)
        {
            case CustomGameMode.SoloKombat:
            case CustomGameMode.FFA:
            case CustomGameMode.RoomRush:
            case CustomGameMode.NaturalDisasters:
            case CustomGameMode.HotPotato:
            case CustomGameMode.CaptureTheFlag:
                __instance.CrewLightMod = __instance.ImpostorLightMod = 1.25f;
                __instance.NumImpostors = 3;
                __instance.NumCommonTasks = 0;
                __instance.NumLongTasks = 0;
                __instance.NumShortTasks = 0;
                __instance.KillCooldown = 0f;
                __instance.NumEmergencyMeetings = 0;
                __instance.KillDistance = 0;
                break;
            case CustomGameMode.Speedrun:
            case CustomGameMode.MoveAndStop:
                __instance.CrewLightMod = 1.25f;
                __instance.ImpostorLightMod = 1.25f;
                __instance.KillCooldown = 60f;
                __instance.NumCommonTasks = 2;
                __instance.NumLongTasks = 3;
                __instance.NumShortTasks = 5;
                __instance.NumEmergencyMeetings = 0;
                __instance.VisualTasks = true;
                break;
            case CustomGameMode.HideAndSeek:
                __instance.CrewLightMod = 1.25f;
                __instance.ImpostorLightMod = 0.5f;
                __instance.KillCooldown = 10f;
                __instance.NumCommonTasks = 2;
                __instance.NumLongTasks = 3;
                __instance.NumShortTasks = 5;
                __instance.NumEmergencyMeetings = 0;
                __instance.VisualTasks = true;
                break;
        }

        return;

        int GetRecommendedImpostors() => numPlayers switch
        {
            > 13 => 3,
            > 8 => 2,
            _ => 1
        };
    }
}