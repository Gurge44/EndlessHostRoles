using HarmonyLib;

namespace TOHE.Patches;

[HarmonyPatch(typeof(TextBoxTMP), nameof(TextBoxTMP.IsCharAllowed))]
class TextBoxTMPCharAllowedPatch
{
    public static bool Prefix(TextBoxTMP __instance, [HarmonyArgument(0)] char i, ref bool __result)
    {
        if (__instance.IpMode ? i is >= '0' and <= '9' or '.' : i == ' ' || i is >= 'A' and <= 'Z' || i is >= 'a' and <= 'z' || i is >= '0' and <= '9' || i is >= 'À' and <= 'ÿ' || i is >= 'Ѐ' and <= 'џ' || i is >= '\u3040' and <= '㆟' || i is >= 'ⱡ' and <= '힣' || __instance.AllowSymbols && TextBoxTMP.SymbolChars.Contains(i) || __instance.AllowEmail && TextBoxTMP.EmailChars.Contains(i)) return true;
        if (i is not ('+' or '<' or '>' or '"' or '*' or '#' or '@' or '$' or '%' or '^' or '&' or '(' or ')' or '-' or '=' or '_' or '{' or '}' or '[' or ']' or ':' or ';' or ',' or '.' or '?' or '/' or '|' or '\\' or '`' or '~' or 'Н' or 'Г' or 'З' or 'В' or 'А' or 'П' or 'О' or 'Л' or 'Д' or 'Ж' or 'М' or 'И' or 'Б' or 'å' or 'ø' or 'æ' or 'ñ' or 'ä' or 'ö' or 'ü' or 'ß')) return true;

        __result = true;
        return false;
    }
}

/* Originally by KARPED1EM. Reference: https://github.com/KARPED1EM/TownOfNext/blob/TONX/TONX/Patches/TextBoxPatch.cs */
[HarmonyPatch(typeof(TextBoxTMP))]
public class TextBoxPatch
{
    [HarmonyPatch(nameof(TextBoxTMP.SetText)), HarmonyPrefix]
    public static void ModifyCharacterLimit(TextBoxTMP __instance/*, [HarmonyArgument(0)] string input, [HarmonyArgument(1)] string inputCompo = ""*/)
    {
        __instance.characterLimit = AmongUsClient.Instance.AmHost ? 2000 : 300;
    }
}
