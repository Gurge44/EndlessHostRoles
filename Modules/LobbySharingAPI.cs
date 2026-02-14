using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
        if (roomCode == LastRoomCode || string.IsNullOrWhiteSpace(roomCode)) return;
        LastRoomCode = roomCode;

        int modLanguage = Options.ModLanguage.GetValue();
        string language = modLanguage == 0 ? Translator.GetUserTrueLang().ToString() : ((Options.ModLanguages)modLanguage).ToString();

        string serverName = Utils.GetRegionName();
        string hostName = Main.AllPlayerNames[PlayerControl.LocalPlayer.PlayerId].RemoveHtmlTags();
        string map = Options.RandomMapsMode.GetBool() ? "Random" : SubmergedCompatibility.Loaded && Main.NormalOptions.MapId == 6 ? "Submerged" : Main.CurrentMap.ToString();
        string gameMode = Options.EnableAutoGMRotation.GetBool() ? "Rotating" : Translator.GetString(Options.CurrentGameMode.ToString(), SupportedLangs.English).RemoveHtmlTags().ToUpper();
        string hostHashedPuid = Options.SendHashedPuidToUseLinkedAccount.GetBool() ? PlayerControl.LocalPlayer.GetClient().GetHashedPuid() : string.Empty;
        const string version = $"EHR v{Main.PluginDisplayVersion}";
        Main.Instance.StartCoroutine(SendLobbyCreatedRequest(roomCode, serverName, language, version, gameId, hostName, map, gameMode, hostHashedPuid));
    }

    private static IEnumerator SendLobbyCreatedRequest(string roomCode, string serverName, string language, string version, int gameId, string hostName, string map, string gameMode, string hostHashedPuid)
    {
        long timeSinceLastRequest = Utils.TimeStamp - LastRequestTimeStamp;
        if (timeSinceLastRequest < BufferTime) yield return new WaitForSecondsRealtime(BufferTime);
        LastRequestTimeStamp = Utils.TimeStamp;

        var jsonData = $"{{\"roomCode\":\"{roomCode}\",\"serverName\":\"{serverName}\",\"language\":\"{language}\",\"version\":\"{version}\",\"gameId\":\"{gameId}\",\"hostName\":\"{hostName}\",\"map\":\"{map}\",\"gameMode\":\"{gameMode}\",\"hostHashedPuid\":\"{hostHashedPuid}\"}}";
        byte[] jsonToSend = new UTF8Encoding().GetBytes(jsonData);

        var request = new UnityWebRequest("https://gurge44.pythonanywhere.com/lobby_created", "POST")
        {
            uploadHandler = new UploadHandlerRaw(jsonToSend),
            downloadHandler = new DownloadHandlerBuffer()
        };

        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("User-Agent", $"{Main.ModName} v{Main.PluginVersion} - {Regex.Replace(hostName, @"[^\x20-\x7E]", "")}");
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

        if (status != LobbyStatus.Closed && GameCode.IntToGameName(AmongUsClient.Instance.GameId) != LastRoomCode)
        {
            status = LobbyStatus.Closed;
            StartMessageEdit();
            LateTask.New(NotifyLobbyCreated, BufferTime * 2);
            return;
        }

        StartMessageEdit();
        return;

        void StartMessageEdit()
        {
            string map = Options.RandomMapsMode.GetBool() ? "Random" : SubmergedCompatibility.Loaded && Main.NormalOptions.MapId == 6 ? "Submerged" : Main.CurrentMap.ToString();
            string gameMode = Options.EnableAutoGMRotation.GetBool() ? "Rotating" : Translator.GetString(Options.CurrentGameMode.ToString(), SupportedLangs.English).ToUpper();
            Main.Instance.StartCoroutine(SendLobbyStatusChangedRequest(LastRoomCode, status.ToString().Replace('_', ' '), PlayerControl.AllPlayerControls.Count, map, gameMode));
        }
    }

    private static IEnumerator SendLobbyStatusChangedRequest(string roomCode, string newStatus, int players, string map, string gameMode)
    {
        if (string.IsNullOrWhiteSpace(Token)) yield break;

        long timeSinceLastRequest = Utils.TimeStamp - LastRequestTimeStamp;
        if (timeSinceLastRequest < BufferTime) yield return new WaitForSecondsRealtime(BufferTime);
        LastRequestTimeStamp = Utils.TimeStamp;

        var jsonData = $"{{\"roomCode\":\"{roomCode}\",\"token\":\"{Token}\",\"newStatus\":\"{newStatus}\",\"players\":\"{players}\",\"map\":\"{map}\",\"gameMode\":\"{gameMode}\"}}";
        byte[] jsonToSend = new UTF8Encoding().GetBytes(jsonData);

        var request = new UnityWebRequest("https://gurge44.pythonanywhere.com/update_status", "POST")
        {
            uploadHandler = new UploadHandlerRaw(jsonToSend),
            downloadHandler = new DownloadHandlerBuffer()
        };

        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("User-Agent", $"{Main.ModName} v{Main.PluginVersion}");
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

[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.DisconnectInternal))]
internal static class ExitGamePatch
{
    public static void Prefix(InnerNetClient __instance)
    {
        if (__instance is not AmongUsClient) return;
        
        Logger.Msg("Exiting game", "ExitGamePatch.Prefix");

        GameStates.InGame = false;
        Main.RealOptionsData?.Restore(GameOptionsManager.Instance.CurrentGameOptions);
        
        if (SetUpRoleTextPatch.IsInIntro)
        {
            SetUpRoleTextPatch.IsInIntro = false;
            Utils.NotifyRoles(ForceLoop: true);
        }
    }

    public static void Postfix(InnerNetClient __instance)
    {
        if (__instance is not AmongUsClient) return;
        
        LobbySharingAPI.NotifyLobbyStatusChanged(LobbyStatus.Closed);

        GameEndChecker.LoadingEndScreen = false;
    }
}