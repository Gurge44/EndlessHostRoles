using HarmonyLib;
using UnityEngine;

namespace EHR.Patches;

[HarmonyPatch(typeof(LightSource), nameof(LightSource.Update))]
internal static class LightSourceUpdatePatch
{
    private static readonly int PlayerRadius = Shader.PropertyToID("_PlayerRadius");
    private static readonly int LightRadius = Shader.PropertyToID("_LightRadius");
    private static readonly int LightOffset = Shader.PropertyToID("_LightOffset");
    private static readonly int FlashlightSize = Shader.PropertyToID("_FlashlightSize");
    private static readonly int FlashlightAngle = Shader.PropertyToID("_FlashlightAngle");

    public static bool Prefix(LightSource __instance)
    {
        Vector3 position = __instance.transform.position;
        position.z -= 7f;
        __instance.UpdateFlashlightAngle();
        __instance.LightCutawayMaterial.SetFloat(PlayerRadius, __instance.PlayerRadius);
        __instance.LightCutawayMaterial.SetFloat(LightRadius, __instance.ViewDistance);
        __instance.LightCutawayMaterial.SetVector(LightOffset, __instance.LightOffset);
        __instance.LightCutawayMaterial.SetFloat(FlashlightSize, __instance.FlashlightSize);
        __instance.LightCutawayMaterial.SetFloat(FlashlightAngle, PlayerControl.LocalPlayer.FlashlightAngle);
        __instance.lightChild.transform.position = position;
        __instance.renderer.Render(position);
        return false;
    }
}

[HarmonyPatch(typeof(LightSourceGpuRenderer), nameof(LightSourceGpuRenderer.DrawOcclusion))]
internal static class LightSourceGpuRendererPatch
{
    private static readonly int DepthCompressionValue = Shader.PropertyToID("_DepthCompressionValue");
    private static readonly int ShmapTexture = Shader.PropertyToID("_ShmapTexture");
    private static readonly int Radius = Shader.PropertyToID("_Radius");
    private static readonly int Column = Shader.PropertyToID("_Column");
    private static readonly int LightPosition = Shader.PropertyToID("_LightPosition");
    private static readonly int TexelSize = Shader.PropertyToID("_TexelSize");

    public static bool Prefix(LightSourceGpuRenderer __instance, [HarmonyArgument(0)] float effectiveRadius)
    {
        if (!__instance.shadowTexture || !__instance.shadowCasterMaterial) return false;
        float width = __instance.shadowTexture.width;
        __instance.shadowCasterMaterial.SetFloat(DepthCompressionValue, effectiveRadius);
        __instance.cb.Clear();
        __instance.cb.SetRenderTarget(__instance.shadowTexture);
        __instance.cb.ClearRenderTarget(true, true, new Color(1f, 1f, 1f, 1f));
        __instance.cb.SetGlobalTexture(ShmapTexture, __instance.shadowTexture);
        __instance.cb.SetGlobalFloat(Radius, __instance.lightSource.ViewDistance);
        __instance.cb.SetGlobalFloat(Column, 0.0f);
        __instance.cb.SetGlobalVector(LightPosition, __instance.lightSource.transform.position);
        __instance.cb.SetGlobalVector(TexelSize, new Vector4(1f / width, 1f / width, width, width));
        __instance.cb.SetGlobalFloat(DepthCompressionValue, effectiveRadius);
        __instance.cb.DrawMesh(__instance.occluderMesh, Matrix4x4.identity, __instance.shadowCasterMaterial);
        Graphics.ExecuteCommandBuffer(__instance.cb);
        return false;
    }
}