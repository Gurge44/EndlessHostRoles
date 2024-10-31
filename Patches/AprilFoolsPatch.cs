using HarmonyLib;
using Il2CppSystem;
using static CosmeticsLayer;
using Action = Il2CppSystem.Action;

namespace EHR.Patches
{
    [HarmonyPatch(typeof(AprilFoolsMode), nameof(AprilFoolsMode.ShouldShowAprilFoolsToggle))]
    public static class ShouldShowTogglePatch
    {
        public static void Postfix(ref bool __result)
        {
            __result = false;
        }
    }

    [HarmonyPatch(typeof(NormalGameManager), nameof(NormalGameManager.GetBodyType))]
    public static class GetNormalBodyTypePatch
    {
        public static void Postfix(ref PlayerBodyTypes __result)
        {
            if (Main.HorseMode.Value)
            {
                __result = PlayerBodyTypes.Horse;
                return;
            }

            if (Main.LongMode.Value)
            {
                __result = PlayerBodyTypes.Long;
                return;
            }

            __result = PlayerBodyTypes.Normal;
        }
    }

    [HarmonyPatch(typeof(HideAndSeekManager), nameof(HideAndSeekManager.GetBodyType))]
    public static class GetHnsBodyTypePatch
    {
        public static void Postfix(ref PlayerBodyTypes __result, [HarmonyArgument(0)] PlayerControl player)
        {
            try
            {
                if (player == null || player.Data == null || player.Data.Role == null)
                {
                    if (Main.HorseMode.Value)
                    {
                        __result = PlayerBodyTypes.Horse;
                        return;
                    }

                    if (Main.LongMode.Value)
                    {
                        __result = PlayerBodyTypes.Long;
                        return;
                    }

                    __result = PlayerBodyTypes.Normal;
                }
                else if (Main.HorseMode.Value)
                {
                    if (player.Data.Role.IsImpostor)
                    {
                        __result = PlayerBodyTypes.Normal;
                        return;
                    }

                    __result = PlayerBodyTypes.Horse;
                }
                else if (Main.LongMode.Value)
                {
                    if (player.Data.Role.IsImpostor)
                    {
                        __result = PlayerBodyTypes.LongSeeker;
                        return;
                    }

                    __result = PlayerBodyTypes.Long;
                }
                else
                {
                    if (player.Data.Role.IsImpostor)
                    {
                        __result = PlayerBodyTypes.Seeker;
                        return;
                    }

                    __result = PlayerBodyTypes.Normal;
                }
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(LongBoiPlayerBody))]
    public static class LongBoiPatches
    {
        [HarmonyPatch(nameof(LongBoiPlayerBody.Awake))]
        [HarmonyPrefix]
        public static bool LongBoyAwake_Patch(LongBoiPlayerBody __instance)
        {
            try
            {
                __instance.cosmeticLayer.OnSetBodyAsGhost += (Action)__instance.SetPoolableGhost;
                __instance.cosmeticLayer.OnColorChange += (Action<int>)__instance.SetHeightFromColor;
                __instance.cosmeticLayer.OnCosmeticSet += (Action<string, int, CosmeticKind>)__instance.OnCosmeticSet;
                __instance.gameObject.layer = 8;
            }
            catch { }

            return false;
        }

        [HarmonyPatch(nameof(LongBoiPlayerBody.Start))]
        [HarmonyPrefix]
        public static bool LongBoyStart_Patch(LongBoiPlayerBody __instance)
        {
            try
            {
                __instance.ShouldLongAround = true;
                if (__instance.hideCosmeticsQC) __instance.cosmeticLayer.SetHatVisorVisible(false);

                __instance.SetupNeckGrowth();

                if (__instance.isExiledPlayer)
                {
                    var instance = ShipStatus.Instance;
                    if (instance == null || instance.Type != ShipStatus.MapType.Fungle) __instance.cosmeticLayer.AdjustCosmeticRotations(-17.75f);
                }

                if (!__instance.isPoolablePlayer) __instance.cosmeticLayer.ValidateCosmetics();

                if (__instance.myPlayerControl)
                {
                    __instance.StopAllCoroutines();
                    __instance.SetHeightFromColor(__instance.myPlayerControl.Data.DefaultOutfit.ColorId);
                }
            }
            catch { }

            return false;
        }

        [HarmonyPatch(nameof(LongBoiPlayerBody.SetHeighFromDistanceHnS))]
        [HarmonyPrefix]
        public static bool LongBoyNeckSize_Patch(LongBoiPlayerBody __instance, ref float distance)
        {
            try
            {
                __instance.targetHeight = (distance / 10f) + 0.5f;
                __instance.SetupNeckGrowth(true);
            }
            catch { }

            return false;
        }

        [HarmonyPatch(typeof(HatManager), nameof(HatManager.CheckLongModeValidCosmetic))]
        [HarmonyPrefix]
        public static bool CheckLongMode_Patch(HatManager __instance, out bool __result, ref string cosmeticID, ref bool ignoreLongMode)
        {
            if (AprilFoolsMode.ShouldHorseAround() || AprilFoolsMode.ShouldLongAround())
            {
                __result = true;
                return false;
            }

            if (string.Equals("skin_rhm", cosmeticID))
            {
                __result = false;
                return false;
            }

            __result = true;
            return false;
        }
    }
}