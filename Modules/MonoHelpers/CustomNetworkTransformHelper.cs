using HarmonyLib;
using Hazel;

namespace EHR;

// There is no efficient way to get the last element of a Queue.
// So instead we keep track of the most recent incoming network transform data manually.

[HarmonyPatch(typeof(CustomNetworkTransform))]
public static class CustomNetworkTransformHelper
{
    public static readonly Vector2[] CurrentPosition = new Vector2[256];
    
    [HarmonyPatch(nameof(CustomNetworkTransform.Awake)), HarmonyPostfix]
    public static void Awake(CustomNetworkTransform __instance)
    {
        CurrentPosition[__instance.myPlayer.PlayerId] = __instance.transform.position;
    }

    [HarmonyPatch(nameof(CustomNetworkTransform.Deserialize)), HarmonyPrefix]
    public static void Deserialize(CustomNetworkTransform __instance, MessageReader reader, bool initialState)
    {
        if (__instance.isPaused) return;
        
        MessageReader subReader = MessageReader.Get(reader);

        try
        {
            if (initialState)
            {
                subReader.ReadUInt16();
                CurrentPosition[__instance.myPlayer.PlayerId] = NetHelpers.ReadVector2(subReader);
            }
            else
            {
                if (__instance.AmOwner) return;
                
                ushort num1 = subReader.ReadUInt16();
                int num2 = subReader.ReadPackedInt32();

                for (int index = 0; index < num2; ++index)
                {
                    ushort newSid = (ushort)(num1 + (uint) index);
                    Vector2 pt = NetHelpers.ReadVector2(subReader);
                    
                    if (NetHelpers.SidGreaterThan(newSid, __instance.lastSequenceId))
                        CurrentPosition[__instance.myPlayer.PlayerId] = pt;
                }
            }
        }
        finally
        {
            subReader.Recycle();
        }
    }
}