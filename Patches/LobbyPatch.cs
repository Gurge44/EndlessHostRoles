using System;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace EHR;

//[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.FixedUpdate))]
public static class LobbyFixedUpdatePatch
{
    private static GameObject Paint;
    private static SpriteRenderer LeftEngineSR;
    private static SpriteRenderer RightEngineSR;

    public static void Postfix()
    {
        try
        {
            if (!Paint)
            {
                GameObject leftBox = GameObject.Find("Leftbox");

                if (leftBox != null)
                {
                    Paint = Object.Instantiate(leftBox, leftBox.transform.parent.transform);
                    Paint.name = "Lobby Paint";
                    Paint.transform.localPosition = new(0.042f, -2.59f, -10.5f);
                    var renderer = Paint.GetComponent<SpriteRenderer>();
                    renderer.sprite = Utils.LoadSprite("EHR.Resources.Images.LobbyPaint.png", 290f);
                }
            }

            if (!LeftEngineSR || !RightEngineSR)
            {
                var leftEngine = GameObject.Find("LeftEngine");
                if (leftEngine != null) LeftEngineSR = leftEngine.GetComponent<SpriteRenderer>();

                var rightEngine = GameObject.Find("RightEngine");
                if (rightEngine != null) RightEngineSR = rightEngine.GetComponent<SpriteRenderer>();
            }
            else
            {
                LeftEngineSR.color = Color.cyan;
                RightEngineSR.color = Color.cyan;
            }
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }
}

[HarmonyPatch(typeof(HostInfoPanel), nameof(HostInfoPanel.SetUp))]
public static class HostInfoPanelSetUpPatch
{
    private static TextMeshPro HostText;

    public static bool Prefix(HostInfoPanel __instance)
    {
        return GameStates.IsLobby && __instance.player.ColorId != byte.MaxValue;
    }

    public static void Postfix(HostInfoPanel __instance)
    {
        try
        {
            if (HostText == null) HostText = __instance.content.transform.FindChild("Name").GetComponent<TextMeshPro>();

            string name = AmongUsClient.Instance.GetHost().PlayerName.Split('\n')[^1];
            if (name == string.Empty) return;

            string text = AmongUsClient.Instance.AmHost
                ? Translator.GetString("YouAreHostSuffix")
                : name;

            HostText.text = Utils.ColorString(Palette.PlayerColors[__instance.player.ColorId], text);
        }
        catch { }
    }
}

// https://github.com/SuperNewRoles/SuperNewRoles/blob/master/SuperNewRoles/Patches/LobbyBehaviourPatch.cs
//[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Update))]
internal static class LobbyBehaviourUpdatePatch
{
    public static void Postfix(LobbyBehaviour __instance)
    {
        // ReSharper disable once ConvertToLocalFunction
        Func<ISoundPlayer, bool> lobbybgm = x => x.Name.Equals("MapTheme");
        ISoundPlayer mapThemeSound = SoundManager.Instance.soundPlayers.Find(lobbybgm);

        if (!Main.LobbyMusic.Value)
        {
            if (mapThemeSound == null) return;
            SoundManager.Instance.StopNamedSound("MapTheme");
        }
        else
        {
            if (mapThemeSound != null) return;
            SoundManager.Instance.CrossFadeSound("MapTheme", __instance.MapTheme, 0.5f);
        }
    }
}
