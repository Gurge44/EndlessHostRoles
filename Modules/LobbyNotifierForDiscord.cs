using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using InnerNet;
using UnityEngine.Networking;

namespace EHR.Modules
{
    public static class LobbyNotifierForDiscord
    {
        private static long LastLobbyCreatedNotificationSentTimeStamp;

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

        public static bool NotifyLobbyCreated()
        {
            long currentTimeStamp = Utils.TimeStamp;
            if (currentTimeStamp - LastLobbyCreatedNotificationSentTimeStamp < 600) return false;
            LastLobbyCreatedNotificationSentTimeStamp = currentTimeStamp;

            var roomCode = GameCode.IntToGameName(AmongUsClient.Instance.GameId);
            var serverName = Utils.GetRegionName();
            var language = Translator.GetUserTrueLang().ToString();
            var hostName = PlayerControl.LocalPlayer.FriendCode.GetDevUser().IsUp ? Main.AllPlayerNames.GetValueOrDefault(PlayerControl.LocalPlayer.PlayerId, "?Unknown") : "?Untrusted";
            Main.Instance.StartCoroutine(SendLobbyCreatedRequest(roomCode, serverName, language, $"EHR v{Main.PluginDisplayVersion}", hostName));
            return true;
        }

        private static IEnumerator SendLobbyCreatedRequest(string roomCode, string serverName, string language, string version, string hostName)
        {
            var jsonData = $"{{\"roomCode\":\"{roomCode}\",\"serverName\":\"{serverName}\",\"language\":\"{language}\",\"version\":\"{version}\",\"hostName\":\"{hostName}\"}}";
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
            if (success) Utils.SendMessage("\n", PlayerControl.LocalPlayer.PlayerId, Translator.GetString("Message.LobbyCodeSent"));
        }
    }
}