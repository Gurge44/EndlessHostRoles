using System;
using System.Linq;
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
            GameObject LeftBox = GameObject.Find("Leftbox");

            if (LeftBox != null)
            {
                Paint = Object.Instantiate(LeftBox, LeftBox.transform.parent.transform);
                Paint.name = "Lobby Paint";
                Paint.transform.localPosition = new(0.042f, -2.59f, -10.5f);
                var renderer = Paint.GetComponent<SpriteRenderer>();
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
        try
        {
            if (HostText == null) HostText = __instance.content.transform.FindChild("Name").GetComponent<TextMeshPro>();

            var name = AmongUsClient.Instance.GetHost().PlayerName;
            var split = name.Split('\n');
            name = Options.GetSuffixMode() == SuffixModes.None ? split[^1] : split[^2];
            if (name == string.Empty) return;

            string text = AmongUsClient.Instance.AmHost
                ? Translator.GetString("YouAreHostSuffix")
                : name;

            HostText.text = Utils.ColorString(Palette.PlayerColors[__instance.player.ColorId], text);
        }
        catch { }
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

// https://github.com/SuperNewRoles/SuperNewRoles/blob/master/SuperNewRoles/Patches/LobbyBehaviourPatch.cs
[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Update))]
static class LobbyBehaviourUpdatePatch
{
    public static void Postfix(LobbyBehaviour __instance)
    {
        // ReSharper disable once ConvertToLocalFunction
        Func<ISoundPlayer, bool> lobbybgm = x => x.Name.Equals("MapTheme");
        ISoundPlayer MapThemeSound = SoundManager.Instance.soundPlayers.Find(lobbybgm);

        if (!Main.LobbyMusic.Value)
        {
            if (MapThemeSound == null) return;
            SoundManager.Instance.StopNamedSound("MapTheme");
        }
        else
        {
            if (MapThemeSound != null) return;
            SoundManager.Instance.CrossFadeSound("MapTheme", __instance.MapTheme, 0.5f);
        }
    }
}