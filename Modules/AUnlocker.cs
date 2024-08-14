using HarmonyLib;

namespace EHR.Modules
{
    // Credit: https://github.com/astra1dev/AUnlocker
    public static class AUnlocker
    {
        // Some of the below patches are from https://github.com/scp222thj/MalumMenu/blob/main/src/Passive/UnlockFeaturesPatch.cs

        // Remove minor status
        [HarmonyPatch(typeof(EOSManager), nameof(EOSManager.IsMinorOrWaiting))]
        public static class RemoveMinorStatus
        {
            public static void Postfix(ref bool __result)
            {
                __result = false;
            }
        }

        // Unlock custom name
        [HarmonyPatch(typeof(FullAccount), nameof(FullAccount.CanSetCustomName))]
        public static class CustomNameEnabled
        {
            public static void Prefix(ref bool canSetName)
            {
                canSetName = true;
            }
        }

        [HarmonyPatch(typeof(HatManager), nameof(HatManager.Initialize))]
        public static class UnlockCosmetics
        {
            // loop through all cosmetics and set them free
            // Source: https://github.com/scp222thj/MalumMenu/blob/main/src/Passive/FreeCosmeticsPatch.cs
            public static void Postfix(HatManager __instance)
            {
                foreach (var bundle in __instance.allBundles) bundle.Free = true;
                foreach (var featuredBundle in __instance.allFeaturedBundles) featuredBundle.Free = true;
                foreach (var featuredCube in __instance.allFeaturedCubes) featuredCube.Free = true;
                foreach (var featuredItem in __instance.allFeaturedItems) featuredItem.Free = true;
                foreach (var hat in __instance.allHats) hat.Free = true;
                foreach (var nameplate in __instance.allNamePlates) nameplate.Free = true;
                foreach (var pet in __instance.allPets) pet.Free = true;
                foreach (var skin in __instance.allSkins) skin.Free = true;
                foreach (var starBundle in __instance.allStarBundles) starBundle.price = 0;
                foreach (var visor in __instance.allVisors) visor.Free = true;
            }
        }
    }
}