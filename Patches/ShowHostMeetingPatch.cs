using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace EHR.Patches;

// Thanks TOU-R: https://github.com/eDonnes124/Town-Of-Us-R/blob/master/source/Patches/ShowHostMeetingPatch.cs

[HarmonyPatch]
public static class ShowHostMeetingPatch
{
    private static PlayerControl HostControl;
    private static string HostName = string.Empty;
    private static int HostColor = int.MaxValue;

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.OnDestroy))]
    [HarmonyPostfix]
    public static void OnDestroy_Postfix()
    {
        try
        {
            if (GameStates.IsInGame && HostControl == null)
            {
                PlayerControl host = AmongUsClient.Instance.GetHost().Character;
                NetworkedPlayerInfo.PlayerOutfit outfit = Main.PlayerStates[host.Data.PlayerId].NormalOutfit;

                HostControl = host;
                HostName = Main.AllPlayerNames.GetValueOrDefault(host.PlayerId, outfit.PlayerName);
                HostColor = outfit.ColorId;
            }
        }
        catch { }
    }

    // [HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.ShowRole))]
    // [HarmonyPostfix]
    public static void ShowRole_Postfix()
    {
        PlayerControl host = AmongUsClient.Instance.GetHost().Character;

        HostControl = host;
        HostName = Main.AllPlayerNames.GetValueOrDefault(host.PlayerId, host.CurrentOutfit.PlayerName);
        HostColor = host.CurrentOutfit.ColorId;
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Update))]
    [HarmonyPostfix]
    public static void Update_Postfix(MeetingHud __instance)
    {
        // Don't display it in local games, because it would be impossible to end meetings
        if (!GameStates.IsOnlineGame) return;

        PlayerMaterial.SetColors(HostColor, __instance.HostIcon);
        __instance.ProceedButton.gameObject.GetComponentInChildren<TextMeshPro>().text = string.Format(Translator.GetString("HostIconInMeeting"), HostName);
    }

    //[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    //[HarmonyPostfix]
    public static void Setup_Postfix(MeetingHud __instance)
    {
        if (!GameStates.IsOnlineGame) return;

        __instance.ProceedButton.gameObject.transform.localPosition = new(-2.5f, 2.2f, 0);
        __instance.ProceedButton.gameObject.GetComponent<SpriteRenderer>().enabled = false;
        __instance.ProceedButton.GetComponent<PassiveButton>().enabled = false;
        __instance.HostIcon.enabled = true;
        __instance.HostIcon.gameObject.SetActive(true);
        __instance.ProceedButton.gameObject.SetActive(true);
    }
}