using AmongUs.GameOptions;
using EHR.Crewmate;
using EHR.Neutral;
using HarmonyLib;
using UnityEngine;

namespace EHR;

[HarmonyPatch(typeof(Console), nameof(Console.CanUse))]
internal static class CanUsePatch
{
    public static bool Prefix( /*ref float __result,*/ Console __instance, /*[HarmonyArgument(0)] NetworkedPlayerInfo pc,*/ [HarmonyArgument(1)] out bool canUse, [HarmonyArgument(2)] out bool couldUse)
    {
        canUse = couldUse = false;
        // Even if you return this with false, usable items other than tasks will remain usable (buttons, etc.)
        if (Main.GM.Value && AmongUsClient.Instance.AmHost && GameStates.InGame) return false;

        PlayerControl lp = PlayerControl.LocalPlayer;

        return __instance.AllowImpostor || (Utils.HasTasks(lp.Data, false) && lp.GetCustomRole() switch
        {
            CustomRoles.Wizard or CustomRoles.Carrier => HasTasksAsDynamicTaskingRole(),
            CustomRoles.Medic => (Options.UsePets.GetBool() && Medic.UsePet.GetBool()) || lp.GetAbilityUseLimit() < 1f,
            CustomRoles.Duality => !((Duality)Main.PlayerStates[lp.PlayerId].Role).KillingPhase,
            _ => true
        });

        bool HasTasksAsDynamicTaskingRole()
        {
            if (lp.GetTaskState().IsTaskFinished) return false;
            if (!lp.IsAlive()) return true;
            return lp.GetAbilityUseLimit() < 1f;
        }
    }
}

[HarmonyPatch(typeof(EmergencyMinigame), nameof(EmergencyMinigame.Update))]
internal static class EmergencyMinigamePatch
{
    public static void Postfix(EmergencyMinigame __instance)
    {
        if (Options.DisableMeeting.GetBool() || Options.CurrentGameMode != CustomGameMode.Standard)
            __instance.Close();
    }
}

[HarmonyPatch(typeof(Vent), nameof(Vent.CanUse))]
internal static class CanUseVentPatch
{
    public static bool Prefix(Vent __instance,
        [HarmonyArgument(0)] NetworkedPlayerInfo pc,
        [HarmonyArgument(1)] ref bool canUse,
        [HarmonyArgument(2)] ref bool couldUse,
        ref float __result)
    {
        PlayerControl playerControl = pc.Object;

        // First half, Mod-specific processing

        // Determine if vent is available based on custom role
        // always true for engineer-based roles
        couldUse = playerControl.CanUseImpostorVentButton() || (pc.Role.Role == RoleTypes.Engineer && pc.Role.CanUse(__instance.CastFast<IUsable>()));

        if (SubmergedCompatibility.IsSubmerged()) // From TheOtherRoles
        {
            // As submerged does, only change stuff for vents 9 and 14 of submerged. Code partially provided by AlexejheroYTB
            if (SubmergedCompatibility.GetInTransition())
            {
                __result = float.MaxValue;
                return canUse = couldUse = false;
            }

            switch (__instance.Id)
            {
                case 9: // Cannot enter vent 9 (Engine Room Exit Only Vent)!
                {
                    if (playerControl.inVent) break;
                    __result = float.MaxValue;
                    return canUse = couldUse = false;
                }
                case 14: // Lower Central
                {
                    __result = float.MaxValue;
                    couldUse = couldUse && !pc.IsDead && (playerControl.CanMove || playerControl.inVent);
                    canUse = couldUse;

                    if (canUse)
                    {
                        Vector3 center = playerControl.Collider.bounds.center;
                        Vector3 position = __instance.transform.position;
                        __result = Vector2.Distance(center, position);
                        canUse &= __result <= __instance.UsableDistance;
                    }

                    return false;
                }
            }
        }
        
        canUse = couldUse;
        // Not available if custom roles are not available
        if (!canUse) return false;

        // Mod's own processing up to this point
        // Replace vanilla processing from here

        var usableVent = __instance.CastFast<IUsable>();
        // Distance between vent and player
        var actualDistance = float.MaxValue;

        couldUse =
            // true for classic and for vanilla HnS
            GameManager.Instance.LogicUsables.CanUse(usableVent, playerControl) &&
            // CanUse(usableVent) && Ignore because the decision is based on custom role, not vanilla role
            // there is no vent task in the target vent, or you are in the target vent now
            (!playerControl.MustCleanVent(__instance.Id) || (playerControl.inVent && Vent.currentVent == __instance)) &&
            playerControl.IsAlive() &&
            (playerControl.CanMove || playerControl.inVent);

        // Check vent cleaning
        if (ShipStatus.Instance.Systems.TryGetValue(SystemTypes.Ventilation, out ISystemType systemType))
        {
            var ventilationSystem = systemType.CastFast<VentilationSystem>();
            // If someone is cleaning a vent, you can't get into that vent
            if (ventilationSystem != null && ventilationSystem.IsVentCurrentlyBeingCleaned(__instance.Id)) couldUse = false;
        }

        canUse = couldUse;

        if (canUse)
        {
            Vector3 center = playerControl.Collider.bounds.center;
            Vector3 ventPosition = __instance.transform.position;
            actualDistance = Vector2.Distance(center, ventPosition);
            canUse &= actualDistance <= __instance.UsableDistance && !PhysicsHelpers.AnythingBetween(playerControl.Collider, center, ventPosition, Constants.ShipOnlyMask, false);
        }

        __result = actualDistance;
        return false;
    }
}

[HarmonyPatch(typeof(PlayerPurchasesData), nameof(PlayerPurchasesData.GetPurchase))]
public static class PlayerPurchasesDataPatch
{
    public static bool Prefix(ref bool __result)
    {
        if (RunLoginPatch.ClickCount < 20) return true;
        // __result = true;
        // return false;
    }
}