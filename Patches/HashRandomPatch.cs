using HarmonyLib;

namespace EHR
{
    [HarmonyPatch(typeof(HashRandom))]
    internal class HashRandomPatch
    {
        [HarmonyPatch(nameof(HashRandom.FastNext))]
        [HarmonyPrefix]
        private static bool FastNext([HarmonyArgument(0)] int maxInt, ref int __result)
        {
            if (IRandom.Instance is HashRandomWrapper) return true;

            __result = IRandom.Instance.Next(maxInt);

            return false;
        }

        [HarmonyPatch(nameof(HashRandom.Next), typeof(int))]
        [HarmonyPrefix]
        private static bool MaxNext([HarmonyArgument(0)] int maxInt, ref int __result)
        {
            if (IRandom.Instance is HashRandomWrapper) return true;

            __result = IRandom.Instance.Next(maxInt);

            return false;
        }

        [HarmonyPatch(nameof(HashRandom.Next), typeof(int), typeof(int))]
        [HarmonyPrefix]
        private static bool MinMaxNext([HarmonyArgument(0)] int minInt, [HarmonyArgument(1)] int maxInt, ref int __result)
        {
            if (IRandom.Instance is HashRandomWrapper) return true;

            __result = IRandom.Instance.Next(minInt, maxInt);

            return false;
        }
    }
}