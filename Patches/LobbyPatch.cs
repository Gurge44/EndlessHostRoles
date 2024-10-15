using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using InnerNet;
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
public static class HostInfoPanelSetUpPatch
{
    private static TextMeshPro HostText;

    public static void Postfix(HostInfoPanel __instance)
    {
        if (HostText == null) HostText = __instance.content.transform.FindChild("Name").GetComponent<TextMeshPro>();
        var icon = Translator.GetString("Icon");
        var name = GameData.Instance?.GetHost()?.PlayerName?.RemoveHtmlTags()?.Split('\n').FirstOrDefault(x => x.Contains(icon))?.Split(icon)[^1] ?? string.Empty;
        if (name == string.Empty) return;
        var text = AmongUsClient.Instance.AmHost
            ? Translator.GetString("YouAreHostSuffix")
            : name;
        HostText.text = Utils.ColorString(Palette.PlayerColors[__instance.player.ColorId], text);
    }
}

public static class LobbyPatch
{
    public static bool IsGlitchedRoomCode()
    {
        string roomCode = GameCode.IntToGameName(AmongUsClient.Instance.GameId).ToUpper();
        string[] badEndings = ["IJPG", "SZAF", "LDQG", "ALGG", "UMPG", "GFXG", "JTFG", "PATG", "WMPG", "FUGG", "YTHG", "UFLG", "FBGG", "ZCQG", "RGGG", "ZHLG", "PJDG", "KJQG", "VDXG", "LCAF"];
        return badEndings.Any(roomCode.EndsWith);
    }
}