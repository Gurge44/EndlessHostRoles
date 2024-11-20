using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using HarmonyLib;
using InnerNet;
using UnityEngine;
using UnityEngine.Networking;

namespace EHR.Modules
{
    public static class LobbyNotifierForDiscord
    {
        private const float BufferTime = 5;
        public static long LastRequestTimeStamp;
        public static string LastRoomCode = string.Empty;
        private static string Token = string.Empty;

        private static string WebhookUrl
        {
            get
            {
                const string path = "EHR.Resources.Config.URL.txt";
                Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(path)!;
                stream.Position = 0;
                using StreamReader reader = new(stream, Encoding.UTF8);
                return reader.ReadToEnd();
            }
        }

        private static void NotifyLobbyCreated()
        {
            var roomCode = GameCode.IntToGameName(AmongUsClient.Instance.GameId);
            if (roomCode == LastRoomCode) return;
            LastRoomCode = roomCode;

            var serverName = Utils.GetRegionName();
            var language = Translator.GetUserTrueLang().ToString();
            Main.Instance.StartCoroutine(SendLobbyCreatedRequest(roomCode, serverName, language, $"EHR v{Main.PluginDisplayVersion}"));
        }

        private static IEnumerator SendLobbyCreatedRequest(string roomCode, string serverName, string language, string version)
        {
            var timeSinceLastRequest = Utils.TimeStamp - LastRequestTimeStamp;

            if (timeSinceLastRequest < BufferTime)
            {
                yield return new WaitForSeconds(BufferTime - timeSinceLastRequest);
            }

            LastRequestTimeStamp = Utils.TimeStamp;

            var jsonData = $"{{\"roomCode\":\"{roomCode}\",\"serverName\":\"{serverName}\",\"language\":\"{language}\",\"version\":\"{version}\"}}";
            byte[] jsonToSend = new UTF8Encoding().GetBytes(jsonData);

            UnityWebRequest request = new UnityWebRequest(WebhookUrl, "POST")
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
                catch (Exception ex)
                {
                    Logger.Msg($"Failed to parse token from response: {ex.Message}", "LobbyNotifierForDiscord.SendLobbyCreatedRequest");
                }

                Utils.SendMessage("\n", PlayerControl.LocalPlayer.PlayerId, Translator.GetString("Message.LobbyCodeSent"));
            }
        }

        public static void NotifyLobbyStatusChanged(LobbyStatus status)
        {
            if (!Options.PostLobbyCodeToEHRDiscordServer.GetBool()) return;

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

            if (timeSinceLastRequest < BufferTime)
            {
                yield return new WaitForSeconds(BufferTime - timeSinceLastRequest);
            }

            LastRequestTimeStamp = Utils.TimeStamp;

            var jsonData = $"{{\"roomCode\":\"{roomCode}\",\"token\":\"{Token}\",\"newStatus\":\"{newStatus}\"}}";
            byte[] jsonToSend = new UTF8Encoding().GetBytes(jsonData);

            UnityWebRequest request = new UnityWebRequest(WebhookUrl.Replace("lobby_created", "update_status"), "POST")
            {
                uploadHandler = new UploadHandlerRaw(jsonToSend),
                downloadHandler = new DownloadHandlerBuffer()
            };

            request.SetRequestHeader("Content-Type", "application/json");
            yield return request.SendWebRequest();

            bool success = request.result == UnityWebRequest.Result.Success;
            Logger.Msg(success ? "Lobby status changed notification sent successfully." : $"Failed to send lobby status changed notification: {request.error}", "LobbyNotifierForDiscord.SendLobbyStatusChangedRequest");
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
        public static void Postfix()
        {
            LobbyNotifierForDiscord.NotifyLobbyStatusChanged(LobbyStatus.Closed);
        }
    }
}