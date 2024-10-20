using HarmonyLib;
using static EHR.Translator;

namespace EHR;

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnDisconnected))]
internal class OnDisconnectedPatch
{
    public static void Postfix( /*AmongUsClient __instance*/)
    {
        Main.VisibleTasksCount = false;
    }
}

[HarmonyPatch(typeof(DisconnectPopup), nameof(DisconnectPopup.DoShow))]
internal class ShowDisconnectPopupPatch
{
    public static DisconnectReasons Reason;
    public static string StringReason;

    public static void Postfix(DisconnectPopup __instance)
    {
        LateTask.New(() =>
        {
            if (__instance != null)
            {
                try
                {
                    switch (Reason)
                    {
                        case DisconnectReasons.Hacking:
                            __instance.SetText(GetString("DCNotify.Hacking"));
                            break;
                        case DisconnectReasons.Banned:
                            __instance.SetText(GetString("DCNotify.Banned"));
                            break;
                        case DisconnectReasons.Kicked:
                            __instance.SetText(GetString("DCNotify.Kicked"));
                            break;
                        case DisconnectReasons.GameNotFound:
                            __instance.SetText(GetString("DCNotify.GameNotFound"));
                            break;
                        case DisconnectReasons.GameStarted:
                            __instance.SetText(GetString("DCNotify.GameStarted"));
                            break;
                        case DisconnectReasons.GameFull:
                            __instance.SetText(GetString("DCNotify.GameFull"));
                            break;
                        case DisconnectReasons.IncorrectVersion:
                            __instance.SetText(GetString("DCNotify.IncorrectVersion"));
                            break;
                        case DisconnectReasons.LobbyInactivity:
                            __instance.SetText(GetString("DCNotify.Inactivity"));
                            break;
                        case DisconnectReasons.NotAuthorized:
                            __instance.SetText(GetString("DCNotify.Auth"));
                            break;
                        case DisconnectReasons.DuplicateConnectionDetected:
                            __instance.SetText(GetString("DCNotify.DupeLogin"));
                            break;
                        case DisconnectReasons.InvalidGameOptions:
                            __instance.SetText(GetString("DCNotify.InvalidSettings"));
                            break;
                        case DisconnectReasons.Error:
                            //if (StringReason.Contains("Couldn't find self")) __instance.SetText(GetString("DCNotify.DCFromServer"));
                            if (StringReason.Contains("Failed to send message")) __instance.SetText(GetString("DCNotify.DCFromServer"));
                            break;
                        case DisconnectReasons.Custom:
                            if (StringReason.Contains("Reliable packet")) __instance.SetText(GetString("DCNotify.DCFromServer"));
                            else if (StringReason.Contains("remote has not responded to")) __instance.SetText(GetString("DCNotify.DCFromServer"));
                            break;
                        case DisconnectReasons.ExitGame:
                            break;
                        case DisconnectReasons.InvalidName:
                            break;
                        case DisconnectReasons.ConnectionLimit:
                            break;
                        case DisconnectReasons.Destroy:
                            break;
                        case DisconnectReasons.IncorrectGame:
                            break;
                        case DisconnectReasons.ServerRequest:
                            break;
                        case DisconnectReasons.ServerFull:
                            break;
                        case DisconnectReasons.InternalPlayerMissing:
                            break;
                        case DisconnectReasons.InternalNonceFailure:
                            break;
                        case DisconnectReasons.InternalConnectionToken:
                            break;
                        case DisconnectReasons.PlatformLock:
                            break;
                        case DisconnectReasons.MatchmakerInactivity:
                            break;
                        case DisconnectReasons.NoServersAvailable:
                            break;
                        case DisconnectReasons.QuickmatchDisabled:
                            break;
                        case DisconnectReasons.TooManyGames:
                            break;
                        case DisconnectReasons.QuickchatLock:
                            break;
                        case DisconnectReasons.MatchmakerFull:
                            break;
                        case DisconnectReasons.Sanctions:
                            break;
                        case DisconnectReasons.ServerError:
                            break;
                        case DisconnectReasons.SelfPlatformLock:
                            break;
                        case DisconnectReasons.TooManyRequests:
                            break;
                        case DisconnectReasons.IntentionalLeaving:
                            break;
                        case DisconnectReasons.FocusLostBackground:
                            break;
                        case DisconnectReasons.FocusLost:
                            break;
                        case DisconnectReasons.NewConnection:
                            break;
                        case DisconnectReasons.PlatformParentalControlsBlock:
                            break;
                        case DisconnectReasons.PlatformUserBlock:
                            break;
                        case DisconnectReasons.PlatformFailedToGetUserBlock:
                            break;
                        case DisconnectReasons.ServerNotFound:
                            break;
                        case DisconnectReasons.ClientTimeout:
                            break;
                        case DisconnectReasons.Unknown:
                            break;
                    }
                }
                catch
                {
                }
            }
        }, 0.01f, "Override Disconnect Text");
    }
}