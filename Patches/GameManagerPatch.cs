using HarmonyLib;
using Hazel;

namespace EHR;

[HarmonyPatch(typeof(GameManager), nameof(GameManager.Serialize))]
internal static class GameManagerSerializeFix
{
    public static bool InitialState = true;

    public static bool Prefix(GameManager __instance, [HarmonyArgument(0)] MessageWriter writer, [HarmonyArgument(1)] bool initialState, ref bool __result)
    {
        InitialState = initialState;

        var flag = false;

        for (var index = 0; index < __instance.LogicComponents.Count; ++index)
        {
            GameLogicComponent logicComponent = __instance.LogicComponents[index];

            if (initialState || logicComponent.IsDirty)
            {
                writer.StartMessage((byte)index);
                bool hasBody = logicComponent.Serialize(writer);

                if (hasBody)
                {
                    flag = true;
                    writer.EndMessage();
                }
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

[HarmonyPatch(typeof(LogicOptions), nameof(LogicOptions.Serialize))] // Only called by the patch above
internal static class LogicOptionsSerializePatch
{
    public static bool Prefix(ref bool __result)
    {
        // Block all but the first time and synchronize only with CustomSyncSettings
        if (!GameManagerSerializeFix.InitialState)
        {
            __result = false;
            return false;
        }

        return true;
    }
}