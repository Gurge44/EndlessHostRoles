using HarmonyLib;
using TMPro;
using UnityEngine;

namespace EHR;

[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.FixedUpdate))]
public static class LobbyFixedUpdatePatch
{
    private static GameObject Paint;

    public static void Postfix()
    {
        if (Paint == null)
        {
            var LeftBox = GameObject.Find("Leftbox");
            if (LeftBox != null)
            {
                Paint = Object.Instantiate(LeftBox, LeftBox.transform.parent.transform);
                Paint.name = "Lobby Paint";
                Paint.transform.localPosition = new(0.042f, -2.59f, -10.5f);
                SpriteRenderer renderer = Paint.GetComponent<SpriteRenderer>();
                renderer.sprite = Utils.LoadSprite("EHR.Resources.Images.LobbyPaint.png", 290f);
            }
        }
    }
}

[HarmonyPatch(typeof(HostInfoPanel), nameof(HostInfoPanel.SetUp))]
public static class HostInfoPanelUpdatePatch
{
    private static TextMeshPro HostText;

    public static void Postfix(HostInfoPanel __instance)
    {
        if (HostText == null) HostText = __instance.content.transform.FindChild("Name").GetComponent<TextMeshPro>();
        var text = Main.HostRealName + (AmongUsClient.Instance.AmHost ? Translator.GetString("YouAreHostSuffix") : string.Empty);
        HostText.text = Utils.ColorString(Palette.PlayerColors[__instance.player.ColorId], text);
    }
}