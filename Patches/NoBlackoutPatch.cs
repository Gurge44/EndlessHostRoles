using HarmonyLib;

namespace EHR
{
    [HarmonyPatch(typeof(LogicGameFlowNormal), nameof(LogicGameFlowNormal.IsGameOverDueToDeath))]
    internal class DontBlackoutPatch
    {
        public static void Postfix(ref bool __result)
        {
            __result = false;
        }
    }
}