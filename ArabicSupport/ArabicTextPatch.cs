using HarmonyLib;
using TMPro;

namespace EHR.ArabicSupport
{
    [HarmonyPatch(typeof(TMP_Text))]
    public static class ArabicTextPatch
    {
        [HarmonyPatch("text", MethodType.Setter)]
        [HarmonyPrefix]
        public static void Prefix(TMP_Text __instance, ref string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            if (!ContainsArabic(value)) return;

            string wrapped = WrapText(__instance, value);
            value = ArabicFixer.Fix(wrapped, true);
        }

        private static bool ContainsArabic(string s)
        {
            foreach (char c in s)
            {
                if (c is >= '\u0600' and <= '\u06FF' or >= '\uFB50' and <= '\uFEFF')
                    return true;
            }
            return false;
        }

        private static string WrapText(TMP_Text tmp, string text)
        {
            if (tmp.rectTransform == null) return text;
            float maxWidth = tmp.rectTransform.rect.width;
            if (maxWidth <= 0) return text;

            string[] paragraphs = text.Split('\n');
            var sb = new StringBuilder();

            for (int p = 0; p < paragraphs.Length; p++)
            {
                string[] words = paragraphs[p].Split(' ');
                string line = "";

                foreach (string word in words)
                {
                    string test = line.Length == 0 ? word : line + " " + word;
                    float width = tmp.GetPreferredValues(test).x;

                    if (width > maxWidth && line.Length > 0)
                    {
                        sb.Append(line).Append('\n');
                        line = word;
                    }
                    else
                        line = test;
                }

                sb.Append(line);
                if (p < paragraphs.Length - 1) sb.Append('\n');
            }

            return sb.ToString();
        }
    }
}