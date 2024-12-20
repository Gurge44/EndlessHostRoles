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
        if (!AmongUsClient.Instance.AmHost || !Options.AutoPlayAgain.GetBool()) return;

        IsRestarting = false;

        LateTask.New(() =>
        {
            Logger.Msg("Beginning Auto Play Again Countdown!", "AutoPlayAgain");
            IsRestarting = true;
            BeginAutoPlayAgainCountdown(__instance, Options.AutoPlayAgainCountdown.GetInt());
        }, 0.5f, "Auto Play Again");
    }

    private static void BeginAutoPlayAgainCountdown(EndGameManager endGameManager, int seconds)
    {
        if (!IsRestarting) return;

        if (endGameManager == null) return;

        EndGameNavigation navigation = endGameManager.Navigation;
        if (navigation == null) return;

        if (seconds == Options.AutoPlayAgainCountdown.GetInt())
        {
            CountdownText = new("CountdownText")
            {
                transform =
                {
                    position = new(0f, 2.5f, 10f)
                }
            };

            var countdownTextTMP = CountdownText.AddComponent<TextMeshPro>();
            countdownTextTMP.text = string.Format(GetString("CountdownText"), seconds);
            countdownTextTMP.alignment = TextAlignmentOptions.Center;
            countdownTextTMP.fontSize = 3f;
        }
        else
        {
            var countdownTextTMP = CountdownText.GetComponent<TextMeshPro>();
            countdownTextTMP.text = string.Format(GetString("CountdownText"), seconds);
        }

        if (seconds == 0)
        {
            navigation.NextGame();
            CountdownText.transform.DestroyChildren();
        }
        else LateTask.New(() => { BeginAutoPlayAgainCountdown(endGameManager, seconds - 1); }, 1f, log: false);
    }
}

[HarmonyPatch(typeof(EndGameNavigation), nameof(EndGameNavigation.NextGame))]
static class EndGameNavigationNextGamePatch
{
    public static void Postfix()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        LateTask.New(() =>
        {
            foreach (ClientData client in AmongUsClient.Instance.allClients)
            {
                if ((!client.IsDisconnected() && client.Character.Data.IsIncomplete) || client.Character.Data.DefaultOutfit.ColorId < 0 || Palette.PlayerColors.Length <= client.Character.Data.DefaultOutfit.ColorId)
                {
                    Logger.SendInGame(GetString("Error.InvalidColor") + $" {client.Id}/{client.PlayerName}");
                    AmongUsClient.Instance.KickPlayer(client.Id, false);
                    Logger.Info($"Kicked client {client.Id}/{client.PlayerName} since its PlayerControl was not spawned in time.", "OnPlayerJoinedPatchPostfix");
                    return;
                }
            }
        }, 5f, "Kick Fortegreen Beans After Play-Again");
    }
}