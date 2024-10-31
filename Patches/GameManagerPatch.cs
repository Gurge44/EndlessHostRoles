using HarmonyLib;
using Hazel;

namespace EHR
{
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.Serialize))]
    internal class GameManagerSerializeFix
    {
        public static bool Prefix(GameManager __instance, [HarmonyArgument(0)] MessageWriter writer, [HarmonyArgument(1)] bool initialState, ref bool __result)
        {
            var flag = false;

            for (var index = 0; index < __instance.LogicComponents.Count; ++index)
            {
                GameLogicComponent logicComponent = __instance.LogicComponents[index];

                if (initialState || logicComponent.IsDirty)
                {
                    flag = true;
                    writer.StartMessage((byte)index);
                    bool hasBody = logicComponent.Serialize(writer, initialState);

                    if (hasBody)
                        writer.EndMessage();
                    else
                        writer.CancelMessage();

                    logicComponent.ClearDirtyFlag();
                }
            }

            __instance.ClearDirtyBits();
            __result = flag;
            return false;
        }
    }

    [HarmonyPatch(typeof(LogicOptions), nameof(LogicOptions.Serialize))]
    internal class LogicOptionsSerializePatch
    {
        public static bool Prefix(ref bool __result, /*MessageWriter writer,*/ bool initialState)
        {
            // Block all but the first time and synchronize only with CustomSyncSettings
            if (!initialState)
            {
                __result = false;
                return false;
            }

            return true;
        }
    }
}