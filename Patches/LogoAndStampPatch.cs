using System.Collections;
using BepInEx.Unity.IL2CPP.Utils;
using HarmonyLib;
using UnityEngine;
using static EHR.Translator;

namespace EHR;

// Credit：https://github.com/ykundesu/SuperNewRoles/blob/master/SuperNewRoles/Patches/LogoAndStampPatch.cs
[HarmonyPatch]
public static class CredentialsPatch
{
    [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start))]
    public static class LogoPatch
    {
        private static string BoosterData = string.Empty;
        private static string SponsersData = string.Empty;
        private static string DevsData = string.Empty;
        private static string TransData = string.Empty;

        static IEnumerator ViewCredentialsCoro(MainMenuManager __instance)
        {
            while (true)
            {
                yield return new WaitForSeconds(1f);
                if (__instance != null)
                {
                    // ViewBoosterPatch(__instance);
                }

                break;
            }
        }

        public static void InitCredentialsData()
        {
            BoosterData = string.Empty;
            SponsersData = string.Empty;
            DevsData = string.Empty;
            TransData = string.Empty;

            DevsData += $"<color={Main.ModColor}>♥咔皮呆 & Gurge44</color> - <size=75%>{GetString("MainDev")}</size>";
            DevsData += $"\n<color={Main.ModColor}>♥IRIDESCENT</color> - <size=75%>{GetString("Art")}</size>";
            DevsData += $"\nNCSIMON - <size=75%>{GetString("RoleDev")}</size>";
            DevsData += $"\n天寸梦初 - <size=75%>{GetString("RoleDev")}&{GetString("TechSup")}</size>";
            DevsData += $"\nCommandf1 - <size=75%>{GetString("RoleDev")}&{GetString("FeatureDev")}</size>";
            DevsData += $"\n喜 - <size=75%>{GetString("RoleDev")}</size>";
            DevsData += $"\nSHAAARKY - <size=75%>{GetString("RoleDev")}</size>";
            DevsData += $"\nGurge44 - <size=75%>{GetString("FeatureDev")}</size>";

            TransData += $"Tommy-XL - <size=75%>{GetString("TranEN")}&{GetString("TranRU")}</size>";
            TransData += $"\nTem - <size=75%>{GetString("TranEN")}&{GetString("TranRU")}</size>";
            TransData += $"\n阿龍 - <size=75%>{GetString("TranCHT")}</size>";
            TransData += $"\nGurge44 - <size=75%>{GetString("TranEN")}</size>";
            TransData += $"\n法官 - <size=75%>{GetString("TranCHT")}</size>";
            TransData += $"\nSolarFlare - <size=75%>{GetString("TranEN")}</size>";
            TransData += $"\nchill_ultimated - <size=75%>{GetString("TranRU")}</size>";

            BoosterData += "bunny";
            BoosterData += "\nNamra";
            BoosterData += "\nKNIGHT";
            BoosterData += "\nSolarFlare";

            SponsersData += "罗寄";
            SponsersData += "\n鬼";
            SponsersData += "\n喜";
            SponsersData += "\n小叨院长";
            SponsersData += "\n波奇酱";
            SponsersData += "\n法师";
            SponsersData += "\n沐煊";
            SponsersData += "\nSolarFlare";
            SponsersData += "\n林林林";
            SponsersData += "\n撒币";
            SponsersData += "\n斯卡蒂Skadi";
            SponsersData += "\nltemten";
            SponsersData += $"\n\n<size=60%>({GetString("OnlyShowPart")})</size>";
        }

/*
        static void ViewBoosterPatch(MainMenuManager __instance)
        {
            var template = __instance.transform.FindChild("StatsPopup");
            var obj = Object.Instantiate(template, template.transform.parent).gameObject;
            Object.Destroy(obj.GetComponent<StatsPopup>());

            var devtitletext = obj.transform.FindChild("StatNumsText_TMP");
            devtitletext.GetComponent<TextMeshPro>().text = GetString("Developer");
            devtitletext.GetComponent<TextMeshPro>().alignment = TextAlignmentOptions.Center;
            devtitletext.localPosition = new(-2.4f, 1.65f, -2f);
            devtitletext.localScale = new(0.8f, 0.8f, 0.8f);

            var devtext = obj.transform.FindChild("StatsText_TMP");
            devtext.GetComponent<TextMeshPro>().text = DevsData;
            devtext.GetComponent<TextMeshPro>().alignment = TextAlignmentOptions.Capline;
            devtext.localPosition = new(-2.4f, 1.27f, -2f);
            devtext.localScale = new(0.5f, 0.5f, 1f);

            var transtitletext = Object.Instantiate(devtitletext, obj.transform);
            transtitletext.GetComponent<TextMeshPro>().text = GetString("Translator");
            transtitletext.GetComponent<TextMeshPro>().alignment = TextAlignmentOptions.Center;
            transtitletext.localPosition = new(0f, 1.65f, -2f);
            transtitletext.localScale = new(0.8f, 0.8f, 1f);

            var transtext = Object.Instantiate(devtext, obj.transform);
            transtext.GetComponent<TextMeshPro>().text = TransData;
            transtext.GetComponent<TextMeshPro>().alignment = TextAlignmentOptions.Capline;
            transtext.localPosition = new(0f, 1.27f, -2f);
            transtext.localScale = new(0.5f, 0.5f, 1f);

            var boostertitletext = Object.Instantiate(devtitletext, obj.transform);
            boostertitletext.GetComponent<TextMeshPro>().text = GetString("Booster");
            boostertitletext.GetComponent<TextMeshPro>().alignment = TextAlignmentOptions.Center;
            boostertitletext.localPosition = new(-2.4f, -1f, -2f);
            boostertitletext.localScale = new(0.8f, 0.8f, 1f);

            var boostertext = Object.Instantiate(devtext, obj.transform);
            boostertext.GetComponent<TextMeshPro>().text = BoosterData;
            boostertext.GetComponent<TextMeshPro>().alignment = TextAlignmentOptions.Capline;
            boostertext.localPosition = new(-2.4f, -1.38f, -2f);
            boostertext.localScale = new(0.5f, 0.5f, 1f);

            var sponsortitletext = Object.Instantiate(devtitletext, obj.transform);
            sponsortitletext.GetComponent<TextMeshPro>().text = GetString("Sponsor");
            sponsortitletext.GetComponent<TextMeshPro>().alignment = TextAlignmentOptions.Center;
            sponsortitletext.localPosition = new(2.4f, 1.65f, -2f);
            sponsortitletext.localScale = new(0.8f, 0.8f, 1f);

            var sponsortext = Object.Instantiate(devtext, obj.transform);
            sponsortext.GetComponent<TextMeshPro>().text = SponsersData;
            sponsortext.GetComponent<TextMeshPro>().alignment = TextAlignmentOptions.Capline;
            sponsortext.localPosition = new(2.4f, 1.27f, -2f);
            sponsortext.localScale = new(0.5f, 0.5f, 1f);

            var textobj = obj.transform.FindChild("Title_TMP");
            Object.Destroy(textobj.GetComponent<TextTranslatorTMP>());
            textobj.GetComponent<TextMeshPro>().text = GetString("DevAndSpnTitle");
            textobj.GetComponent<TextMeshPro>().alignment = TextAlignmentOptions.Center;
            textobj.localScale = new(1.2f, 1.2f, 1f);
            textobj.localPosition = new(0f, 2.2f, -2f);
            obj.transform.FindChild("Background").localScale = new(1.5f, 1f, 1f);
            obj.transform.FindChild("CloseButton").localPosition = new(-3.75f, 2.65f, 0);
        }
*/

        public static void Postfix(MainMenuManager __instance)
        {
            InitCredentialsData();
            AmongUsClient.Instance.StartCoroutine(ViewCredentialsCoro(__instance));
        }
    }
}