using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using HarmonyLib;
using InnerNet;
using UnityEngine;
using UnityEngine.Networking;

namespace EHR.Modules;

public static class LobbySharingAPI
{
    private const float BufferTime = 5;
    public static long LastRequestTimeStamp;
    public static string LastRoomCode = string.Empty;
    private static string Token = string.Empty;

    private static void NotifyLobbyCreated()
    {
        var gameId = AmongUsClient.Instance.GameId;
        if (gameId == 32) return;

        var roomCode = GameCode.IntToGameName(gameId);
        if (roomCode == LastRoomCode) return;
        LastRoomCode = roomCode;

        var serverName = Utils.GetRegionName();
        var language = Translator.GetUserTrueLang().ToString();
        Main.Instance.StartCoroutine(SendLobbyCreatedRequest(roomCode, serverName, language, $"EHR v{Main.PluginDisplayVersion}", gameId));
    }

    private static IEnumerator SendLobbyCreatedRequest(string roomCode, string serverName, string language, string version, int gameId)
    {
        var timeSinceLastRequest = Utils.TimeStamp - LastRequestTimeStamp;
        if (timeSinceLastRequest < BufferTime) yield return new WaitForSeconds(BufferTime - timeSinceLastRequest);
        LastRequestTimeStamp = Utils.TimeStamp;

        var jsonData = $"{{\"roomCode\":\"{roomCode}\",\"serverName\":\"{serverName}\",\"language\":\"{language}\",\"version\":\"{version}\",\"gameId\":\"{gameId}\"}}";
        byte[] jsonToSend = new UTF8Encoding().GetBytes(jsonData);

        UnityWebRequest request = new UnityWebRequest("https://gurge44.pythonanywhere.com/lobby_created", "POST")
        {
            uploadHandler = new UploadHandlerRaw(jsonToSend),
            downloadHandler = new DownloadHandlerBuffer()
        };

        request.SetRequestHeader("Content-Type", "application/json");
        yield return request.SendWebRequest();

        bool success = request.result == UnityWebRequest.Result.Success;
        Logger.Msg(success ? "Lobby created notification sent successfully." : $"Failed to send lobby created notification: {request.error}", "LobbyNotifierForDiscord.SendLobbyCreatedRequest");

        if (success)
        {
            try
            {
                string responseText = request.downloadHandler.text;
                Logger.Msg("Response from server: " + responseText, "LobbyNotifierForDiscord.SendLobbyCreatedRequest");

                using JsonDocument doc = JsonDocument.Parse(responseText);
                Token = doc.RootElement.GetProperty("token").GetString();

                Logger.Msg($"Token for room {roomCode}: {Token}", "LobbyNotifierForDiscord.SendLobbyCreatedRequest");
            }
            catch (Exception ex) { Logger.Msg($"Failed to parse token from response: {ex.Message}", "LobbyNotifierForDiscord.SendLobbyCreatedRequest"); }

            Utils.SendMessage("\n", PlayerControl.LocalPlayer.PlayerId, Translator.GetString("Message.LobbyCodeSent"));
        }
        else Utils.SendMessage("\n", PlayerControl.LocalPlayer.PlayerId, string.Format(Translator.GetString("Message.LobbyCodeSendError"), request.error));
    }

    public static void NotifyLobbyStatusChanged(LobbyStatus status)
    {
        if (!Options.PostLobbyCodeToEHRWebsite.GetBool() || !AmongUsClient.Instance.AmHost) return;

        if (GameCode.IntToGameName(AmongUsClient.Instance.GameId) != LastRoomCode)
        {
            status = LobbyStatus.Closed;
            StartMessageEdit();
            NotifyLobbyCreated();
            return;
        }

        StartMessageEdit();
        return;

        void StartMessageEdit()
        {
            Main.Instance.StartCoroutine(SendLobbyStatusChangedRequest(LastRoomCode, status.ToString().Replace('_', ' ')));
        }
    }

    private static IEnumerator SendLobbyStatusChangedRequest(string roomCode, string newStatus)
    {
        if (string.IsNullOrEmpty(Token)) yield break;

        var timeSinceLastRequest = Utils.TimeStamp - LastRequestTimeStamp;
        if (timeSinceLastRequest < BufferTime) yield return new WaitForSeconds(BufferTime - timeSinceLastRequest);
        LastRequestTimeStamp = Utils.TimeStamp;

        var jsonData = $"{{\"roomCode\":\"{roomCode}\",\"token\":\"{Token}\",\"newStatus\":\"{newStatus}\"}}";
        byte[] jsonToSend = new UTF8Encoding().GetBytes(jsonData);

        UnityWebRequest request = new UnityWebRequest("https://gurge44.pythonanywhere.com/update_status", "POST")
        {
            uploadHandler = new UploadHandlerRaw(jsonToSend),
            downloadHandler = new DownloadHandlerBuffer()
        };

        request.SetRequestHeader("Content-Type", "application/json");
        yield return request.SendWebRequest();

        bool success = request.result == UnityWebRequest.Result.Success;
        Logger.Msg(success ? "Lobby status changed notification sent successfully." : $"Failed to send lobby status changed notification: {request.error}", "LobbyNotifierForDiscord.SendLobbyStatusChangedRequest");

        if (!success && request.responseCode == 404)
        {
            LastRoomCode = string.Empty;
            NotifyLobbyCreated();
            Logger.Msg("Room code not found, re-sending lobby created notification.", "LobbyNotifierForDiscord.SendLobbyStatusChangedRequest");
        }
    }
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
public enum LobbyStatus
{
    In_Lobby,
    In_Game,
    Ended,
    Closed
}

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.ExitGame))]
static class ExitGamePatch
{
    public static void Prefix()
    {
        if (SetUpRoleTextPatch.IsInIntro)
        {
            SetUpRoleTextPatch.IsInIntro = false;
            Utils.NotifyRoles(NoCache: true);
        }
    }

    public static void Postfix()
    {
        LobbySharingAPI.NotifyLobbyStatusChanged(LobbyStatus.Closed);
    }
}