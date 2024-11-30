using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;

namespace EHR
{
    [HarmonyPatch(typeof(JoinGameButton), nameof(JoinGameButton.OnClick))]
    internal class JoinGameButtonPatch
    {
        public static void Prefix(JoinGameButton __instance)
        {
            if (__instance.GameIdText == null) return;

            if (__instance.GameIdText.text == "" && Regex.IsMatch(GUIUtility.systemCopyBuffer.Trim('\r', '\n'), @"^[A-Z]{6}$"))
            {
                Logger.Info($"{GUIUtility.systemCopyBuffer}", "ClipBoard");
                __instance.GameIdText.SetText(GUIUtility.systemCopyBuffer.Trim('\r', '\n'));
            }
        }
    }
}