using System;
using System.Collections;
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
    private static bool IsRestarting;
    private static Coroutine CountdownCoroutine;

    public static void Postfix(EndGameManager __instance)
    {
        GameEndChecker.LoadingEndScreen = false;
        if (!Main.AutoPlayAgain.Value) return;

        CleanupText();
        IsRestarting = true;
        int time = AmongUsClient.Instance.AmHost ? Options.AutoPlayAgainCountdown.GetInt() : 10;
        CountdownCoroutine = Main.Instance.StartCoroutine(AutoPlayAgainCountdown(__instance, time));
    }

    private static IEnumerator AutoPlayAgainCountdown(EndGameManager endGameManager, int seconds)
    {
        float initialDelay = AmongUsClient.Instance.AmHost ? 0.5f : 1.5f;
        yield return new WaitForSecondsRealtime(initialDelay);

        Logger.Msg("Beginning Auto Play Again Countdown!", "AutoPlayAgain");

        TextMeshPro tmp = null;
        string format = GetString("CountdownText");

        while (IsRestarting)
        {
            if (!endGameManager || !endGameManager.Navigation)
            {
                CleanupText();
                yield break;
            }

            if (!CountdownText)
            {
                CountdownText = new GameObject("CountdownText")
                {
                    transform =
                    {
                        position = new Vector3(0f, 2.5f, 10f)
                    }
                };

                tmp = CountdownText.AddComponent<TextMeshPro>();
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontSize = 3f;
            }
            else if (!tmp)
            {
                tmp = CountdownText.GetComponent<TextMeshPro>();
            }

            tmp.text = string.Format(format, seconds);

            if (seconds <= 0)
            {
                if (AmongUsClient.Instance.AmHost)
                    endGameManager.Navigation.NextGame();
                else
                    ClickPlayAgain();

                CleanupText();
                yield break;
            }

            seconds--;
            yield return new WaitForSecondsRealtime(1f);
        }
    }

    private static void ClickPlayAgain()
    {
        var btn = Object.FindObjectsOfType<PassiveButton>().FirstOrDefault(x => x.gameObject.name == "PlayAgainButton");

        if (!btn)
        {
            Logger.Warn("PlayAgainButton not found.", "AutoPlayAgain");
            return;
        }

        try
        {
            btn.OnClick.Invoke();
            Logger.Info("Successfully invoked PlayAgainButton click event.", "AutoPlayAgain");
        }
        catch (Exception ex)
        {
            Logger.Warn($"Direct click invocation failed: {ex.Message}. Attempting mechanical click fallback.", "AutoPlayAgain");

            try
            {
                btn.ReceiveClickDown();
                btn.ReceiveClickUp();
            }
            catch { }
        }
    }

    public static void CleanupText()
    {
        IsRestarting = false;

        if (CountdownCoroutine != null)
        {
            Main.Instance.StopCoroutine(CountdownCoroutine);
            CountdownCoroutine = null;
        }

        if (CountdownText)
        {
            Object.Destroy(CountdownText);
            CountdownText = null;
        }
    }
}

[HarmonyPatch(typeof(EndGameNavigation), nameof(EndGameNavigation.NextGame))]
internal static class EndGameNavigationNextGamePatch
{
    public static void Postfix()
    {
        EndGameManagerPatch.CleanupText();
        
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