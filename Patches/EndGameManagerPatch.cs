using System;
using System.Linq;
using HarmonyLib;
using InnerNet;
using TMPro;
using UnityEngine;
using static EHR.Translator;

namespace EHR.Patches;

[HarmonyPatch(typeof(EndGameManager), nameof(EndGameManager.ShowButtons))]
public static class EndGameManagerPatch 
{
    private static GameObject CountdownText;
    private static bool IsRestarting { get; set; }

    public static void Postfix(EndGameManager __instance) 
    {
        GameEndChecker.LoadingEndScreen = false;
        if (!Main.AutoPlayAgain.Value) return;

        if (CountdownText != null) GameObject.Destroy(CountdownText);
        IsRestarting = true;
        
        int startSeconds = AmongUsClient.Instance.AmHost ? Options.AutoPlayAgainCountdown.GetInt() : 10;
        
        LateTask.New(() => {
            Logger.Msg("Beginning Auto Play Again Countdown!", "AutoPlayAgain");
            BeginAutoPlayAgainCountdown(__instance, startSeconds);
        }, AmongUsClient.Instance.AmHost ? 0.5f : 1.5f, "Auto Play Again");
    }

    private static void BeginAutoPlayAgainCountdown(EndGameManager endGameManager, int seconds) 
    {
        if (!IsRestarting || endGameManager == null || endGameManager.Navigation == null) { CleanupText(); return; }

        if (CountdownText == null) 
        {
            CountdownText = new GameObject("CountdownText") { transform = { position = new Vector3(0f, 2.5f, 10f) } };
            var tmp = CountdownText.AddComponent<TextMeshPro>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 3f;
        }

        CountdownText.GetComponent<TextMeshPro>().text = string.Format(GetString("CountdownText"), seconds);

        if (seconds <= 0) 
        {
            if (AmongUsClient.Instance.AmHost) endGameManager.Navigation.NextGame(); 
            else ClickPlayAgain();
            
            CleanupText();
        } 
        else LateTask.New(() => BeginAutoPlayAgainCountdown(endGameManager, seconds - 1), 1f, log: false);
    }

    private static void ClickPlayAgain()
    {
        var btn = Object.FindObjectsOfType<PassiveButton>().FirstOrDefault(x => x.gameObject.name == "PlayAgainButton");
        if (btn == null) { Logger.Warn("PlayAgainButton not found.", "AutoPlayAgain"); return; }

        try 
        { 
            btn.OnClick.Invoke(); 
            Logger.Info("Successfully invoked PlayAgainButton click event.", "AutoPlayAgain");
        }
        catch (Exception ex)
        {
            Logger.Warn($"Direct click invocation failed: {ex.Message}. Attempting mechanical click fallback.", "AutoPlayAgain");
            try { btn.ReceiveClickDown(); btn.ReceiveClickUp(); } catch { }
        }
    }

    private static void CleanupText()
    {
        IsRestarting = false;
        if (CountdownText == null) return;
        GameObject.Destroy(CountdownText);
        CountdownText = null;
    }
}

[HarmonyPatch(typeof(EndGameNavigation), nameof(EndGameNavigation.NextGame))]
internal static class EndGameNavigationNextGamePatch
{
    public static void Postfix()
    {
        if (!AmongUsClient.Instance.AmHost || !Options.KickSlowJoiningPlayers.GetBool()) return;

        LateTask.New(() =>
        {
            foreach (ClientData client in AmongUsClient.Instance.allClients)
            {
                if ((!client.IsDisconnected() && client.Character.Data.IsIncomplete) || client.Character.Data.DefaultOutfit.ColorId < 0 || Palette.PlayerColors.Length <= client.Character.Data.DefaultOutfit.ColorId)
                {
                    Logger.SendInGame(GetString("Error.InvalidColor") + $" {client.Id}/{client.PlayerName}", Color.yellow);
                    AmongUsClient.Instance.KickPlayer(client.Id, false);
                    Logger.Info($"Kicked client {client.Id}/{client.PlayerName} since its PlayerControl was not spawned in time.", "OnPlayerJoinedPatchPostfix");
                    return;
                }
            }
        }, 5f, "Kick Fortegreen Beans After Play-Again");
    }
}