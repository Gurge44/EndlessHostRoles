using HarmonyLib;
using UnityEngine;

namespace TOHE.Patches;

/* Originally by Kap. Reference: https://github.com/KARPED1EM/TownOfNext/blob/TONX/TONX/Patches/TextBoxPatch.cs */
[HarmonyPatch(typeof(TextBoxTMP))]
public class TextBoxPatch
{
    [HarmonyPatch(nameof(TextBoxTMP.SetText)), HarmonyPrefix]
    public static void ModifyCharacterLimit(TextBoxTMP __instance/*, [HarmonyArgument(0)] string input, [HarmonyArgument(1)] string inputCompo = ""*/)
    {
        __instance.characterLimit = AmongUsClient.Instance.AmHost ? 2000 : 300;
    }
}
