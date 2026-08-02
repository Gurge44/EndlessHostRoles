using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace EHR.Patches;

// Entirely from: https://github.com/AU-Avengers/LocalizeUs/blob/main/LocalizeUs/Patches/TmpAwakePatch.cs along with all resources

[HarmonyPatch]
public static class TmpAwakePatch
{
    private static TMP_FontAsset LibSansRegTmp;
    private static TMP_FontAsset LibSansBoldTmp;
    private static TMP_FontAsset LibSansItalicTmp;
    private static TMP_FontAsset LibSansBoldItalicTmp;
    private static TMP_FontAsset VcrRegTmp;
    private static TMP_FontAsset BarlowRegTmp;
    private static TMP_FontAsset BarlowBoldTmp;
    private static TMP_FontAsset BarlowSemiBoldTmp;
    private static TMP_FontAsset BarlowBoldItalicTmp;
    private static TMP_FontAsset NotoSansArabicTmp;
    private static bool FallbackRegistered;

    [HarmonyPatch(typeof(TextMeshPro), nameof(TextMeshPro.Awake))]
    [HarmonyPostfix]
    public static void TmpAwakePostfix(TextMeshPro __instance)
    {
        if (!LibSansRegTmp)
        {
            LibSansRegTmp = LoadFontFromResources("EHR.Resources.Fonts.LiberationSans-Regular.ttf");
            LibSansRegTmp.name = "LiberationSans Regular (Custom)";
        }
        if (!LibSansBoldTmp)
        {
            LibSansBoldTmp = LoadFontFromResources("EHR.Resources.Fonts.LiberationSans-Bold.ttf");
            LibSansBoldTmp.name = "LiberationSans Bold (Custom)";
        }
        if (!LibSansItalicTmp)
        {
            LibSansItalicTmp = LoadFontFromResources("EHR.Resources.Fonts.LiberationSans-Italic.ttf");
            LibSansItalicTmp.name = "LiberationSans Italic (Custom)";
        }
        if (!LibSansBoldItalicTmp)
        {
            LibSansBoldItalicTmp = LoadFontFromResources("EHR.Resources.Fonts.LiberationSans-BoldItalic.ttf");
            LibSansBoldItalicTmp.name = "LiberationSans Bold Italic (Custom)";
        }
        if (!NotoSansArabicTmp)
        {
            NotoSansArabicTmp = LoadFontFromResources("EHR.Resources.Fonts.NotoSans-Arabic.ttf");
            NotoSansArabicTmp.name = "NotoSans Arabic (Custom)";
        }
        if (!VcrRegTmp)
        {
            VcrRegTmp = LoadFontFromResources("EHR.Resources.Fonts.marisas-vcr-osd-mono-faithful-32x.ttf");
            VcrRegTmp.name = "Marisa's VCR OSD Mono 32x (Custom)";
        }
        if (!BarlowRegTmp)
        {
            BarlowRegTmp = LoadFontFromResources("EHR.Resources.Fonts.Barlow-Regular.ttf");
            BarlowRegTmp.name = "Barlow Regular (Custom)";
        }
        if (!BarlowSemiBoldTmp)
        {
            BarlowSemiBoldTmp = LoadFontFromResources("EHR.Resources.Fonts.Barlow-SemiBold.ttf");
            BarlowSemiBoldTmp.name = "Barlow Semi Bold (Custom)";
        }
        if (!BarlowBoldItalicTmp)
        {
            BarlowBoldItalicTmp = LoadFontFromResources("EHR.Resources.Fonts.Barlow-BoldItalic.ttf");
            BarlowBoldItalicTmp.name = "Barlow Bold Italic (Custom)";
        }
        if (!BarlowBoldTmp)
        {
            BarlowBoldTmp = LoadFontFromResources("EHR.Resources.Fonts.Barlow-Bold.ttf", 40);
            BarlowBoldTmp.name = "Barlow Bold (Custom)";
        }
        
        // Instead of replacing the font entirely (which causes rendering
        // differences in weight, outline, and breaks other asset-bundle
        // fonts), register the extended font as a fallback on the original
        // LiberationSans. The original font renders Latin text exactly as
        // the base game does; the extended font only kicks in for glyphs
        // that are missing from the original (e.g. ĄČĘĖĮŠŲŪŽ).
        
        switch (__instance.font.name)
        {
            case "LiberationSans SDF" when !FallbackRegistered:
                LibSansRegTmp.fontWeightTable = __instance.font.fontWeightTable;
                var regMat = LibSansRegTmp.material;
                regMat.SetFloat(ShaderUtilities.ID_OutlineWidth, 10f);
                regMat.SetFloat(ShaderUtilities.ID_FaceDilate, 1f);
                RegisterFallback(__instance.font, LibSansRegTmp);
                RegisterFallback(__instance.font, NotoSansArabicTmp);
                NotoSansArabicTmp.fontWeightTable = __instance.font.fontWeightTable;
                FallbackRegistered = true;
                __instance.UpdateMeshPadding();
                break;
            case "VCR SDF":
                RegisterFallback(__instance.font, VcrRegTmp);
                VcrRegTmp.fontWeightTable = __instance.font.fontWeightTable;
                break;
            case "Barlow-BoldItalic Masked":
            case "Barlow-BoldItalic SDF":
                RegisterFallback(__instance.font, BarlowBoldItalicTmp);
                BarlowBoldItalicTmp.fontWeightTable = __instance.font.fontWeightTable;
                break;
            case "Barlow-SemiBold Masked":
            case "Barlow-SemiBold SDF":
                RegisterFallback(__instance.font, BarlowSemiBoldTmp);
                BarlowSemiBoldTmp.fontWeightTable = __instance.font.fontWeightTable;
                break;
            case "Barlow-Regular Masked":
            case "Barlow-Regular SDF":
                RegisterFallback(__instance.font, BarlowRegTmp);
                BarlowRegTmp.fontWeightTable = __instance.font.fontWeightTable;
                break;
            case "Barlow-Bold Masked":
            case "Barlow-Bold SDF":
                RegisterFallback(__instance.font, BarlowBoldTmp);
                BarlowBoldTmp.fontWeightTable = __instance.font.fontWeightTable;
                break;
        }
    }

    private static void RegisterFallback(TMP_FontAsset mainFont, TMP_FontAsset fallbackFont)
    {
        var fallbacks = mainFont.fallbackFontAssetTable;

        // Check whether the fallback is already registered.
        if (fallbacks != null)
        {
            foreach (var f in fallbacks)
            {
                if (f == fallbackFont)
                    return;
            }
        }

        // Create or extend the fallback list.
        var newList = new Il2CppSystem.Collections.Generic.List<TMP_FontAsset>();
        if (fallbacks != null)
        {
            foreach (var f in fallbacks)
                newList.Add(f);
        }
        newList.Add(fallbackFont);
        mainFont.fallbackFontAssetTable = newList;
    }

    private static TMP_FontAsset LoadFontFromResources(string resourcePath, int padding = 18)
    {
        try
        {
            Assembly assembly = Assembly.GetExecutingAssembly();

            using Stream stream = assembly.GetManifestResourceStream(resourcePath);
            if (stream == null)
            {
                Logger.Error($"Font resource not found: {resourcePath}", nameof(LoadFontFromResources));
                return null;
            }

            string tempFileName = $"{Path.GetFileNameWithoutExtension(resourcePath)}_{Guid.NewGuid()}{Path.GetExtension(resourcePath)}";
            string tempPath = Path.Combine(Application.temporaryCachePath, tempFileName);

            using (FileStream fileStream = File.Create(tempPath))
            {
                stream.CopyTo(fileStream);
            }

            Font newFont = new(tempPath);
            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(newFont,
                90,
                padding,
                UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                2048,
                2048);
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            File.Delete(tempPath);

            return fontAsset;
        }
        catch (Exception ex)
        {
            Utils.ThrowException(ex);
            return null;
        }
    }
}