﻿using System;
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
        int gameId = AmongUsClient.Instance.GameId;
        if (gameId == 32) return;

        string roomCode = GameCode.IntToGameName(gameId);
        if (roomCode == LastRoomCode || roomCode.IsNullOrWhiteSpace() || string.IsNullOrEmpty(roomCode)) return;
        LastRoomCode = roomCode;

        int modLanguage = Options.ModLanguage.GetValue();
        string language = modLanguage == 0 ? Translator.GetUserTrueLang().ToString() : ((Options.ModLanguages)modLanguage).ToString();

        string serverName = Utils.GetRegionName();
        string hostName = Main.AllPlayerNames[PlayerControl.LocalPlayer.PlayerId].RemoveHtmlTags();
        Main.Instance.StartCoroutine(SendLobbyCreatedRequest(roomCode, serverName, language, $"EHR v{Main.PluginDisplayVersion}", gameId, hostName));
    }

    private static IEnumerator SendLobbyCreatedRequest(string roomCode, string serverName, string language, string version, int gameId, string hostName)
    {
        long timeSinceLastRequest = Utils.TimeStamp - LastRequestTimeStamp;
        if (timeSinceLastRequest < BufferTime) yield return new WaitForSeconds(BufferTime);
        LastRequestTimeStamp = Utils.TimeStamp;

        var jsonData = $"{{\"roomCode\":\"{roomCode}\",\"serverName\":\"{serverName}\",\"language\":\"{language}\",\"version\":\"{version}\",\"gameId\":\"{gameId}\",\"hostName\":\"{hostName}\"}}";
        byte[] jsonToSend = new UTF8Encoding().GetBytes(jsonData);

        var request = new UnityWebRequest("https://gurge44.pythonanywhere.com/lobby_created", "POST")
        {
            uploadHandler = new UploadHandlerRaw(jsonToSend),
            downloadHandler = new DownloadHandlerBuffer()
        };

        request.SetRequestHeader("Content-Type", "application/json");
        yield return request.SendWebRequest();

        LastRequestTimeStamp = Utils.TimeStamp;
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
            LateTask.New(NotifyLobbyCreated, BufferTime);
            return;
        }

        StartMessageEdit();
        return;

        void StartMessageEdit() => Main.Instance.StartCoroutine(SendLobbyStatusChangedRequest(LastRoomCode, status.ToString().Replace('_', ' '), PlayerControl.AllPlayerControls.Count));
    }

    private static IEnumerator SendLobbyStatusChangedRequest(string roomCode, string newStatus, int players)
    {
        if (string.IsNullOrEmpty(Token)) yield break;

        long timeSinceLastRequest = Utils.TimeStamp - LastRequestTimeStamp;
        if (timeSinceLastRequest < BufferTime) yield return new WaitForSeconds(BufferTime);
        LastRequestTimeStamp = Utils.TimeStamp;

        var jsonData = $"{{\"roomCode\":\"{roomCode}\",\"token\":\"{Token}\",\"newStatus\":\"{newStatus}\",\"players\":\"{players}\"}}";
        byte[] jsonToSend = new UTF8Encoding().GetBytes(jsonData);

        var request = new UnityWebRequest("https://gurge44.pythonanywhere.com/update_status", "POST")
        {
            uploadHandler = new UploadHandlerRaw(jsonToSend),
            downloadHandler = new DownloadHandlerBuffer()
        };

        request.SetRequestHeader("Content-Type", "application/json");
        yield return request.SendWebRequest();

        LastRequestTimeStamp = Utils.TimeStamp;
        bool success = request.result == UnityWebRequest.Result.Success;
        Logger.Msg(success ? "Lobby status changed notification sent successfully." : $"Failed to send lobby status changed notification: {request.error}", "LobbyNotifierForDiscord.SendLobbyStatusChangedRequest");

        if (!success && request.responseCode == 404)
        {
            LastRoomCode = string.Empty;
            LateTask.New(NotifyLobbyCreated, BufferTime);
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
internal static class ExitGamePatch
{
    public static void Prefix()
    {
        if (SetUpRoleTextPatch.IsInIntro)
        {
            SetUpRoleTextPatch.IsInIntro = false;
            Utils.NotifyRoles(ForceLoop: true);
        }
    }

    public static void Postfix()
    {
        LobbySharingAPI.NotifyLobbyStatusChanged(LobbyStatus.Closed);

        GameEndChecker.LoadingEndScreen = false;
    }
}