using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AmongUs.Data;
using AmongUs.GameOptions;
using EHR.Crewmate;
using EHR.Modules;
using EHR.Neutral;
using HarmonyLib;
using Hazel;
using InnerNet;
using UnityEngine;
using static EHR.Translator;

namespace EHR;

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameJoined))]
class OnGameJoinedPatch
{
    public static void Postfix(AmongUsClient __instance)
    {
        while (!Options.IsLoaded) Task.Delay(1);
        Logger.Info($"{__instance.GameId} joined lobby", "OnGameJoined");
        if (AmongUsClient.Instance.AmHost) Main.HostClientId = __instance.ClientId;
        Main.PlayerVersion = [];
        RPC.RpcVersionCheck();
        SoundManager.Instance?.ChangeAmbienceVolume(DataManager.Settings.Audio.AmbienceVolume);

        ChatUpdatePatch.DoBlockChat = false;
        GameStates.InGame = false;
        ErrorText.Instance?.Clear();

        if (AmongUsClient.Instance.AmHost)
        {
            GameStartManagerPatch.GameStartManagerUpdatePatch.ExitTimer = -1;
            Main.DoBlockNameChange = false;
            Main.NewLobby = true;
            EAC.DeNum = new();
            Main.AllPlayerNames = [];
            Main.AllClientRealNames = [];

            if (Main.NormalOptions?.KillCooldown == 0f)
                Main.NormalOptions.KillCooldown = Main.LastKillCooldown.Value;

            AURoleOptions.SetOpt(Main.NormalOptions?.Cast<IGameOptions>());
            if (AURoleOptions.ShapeshifterCooldown == 0f)
                AURoleOptions.ShapeshifterCooldown = Main.LastShapeshifterCooldown.Value;

            LateTask.New(() =>
            {
                if (BanManager.CheckEACList(PlayerControl.LocalPlayer.FriendCode, PlayerControl.LocalPlayer.GetClient().GetHashedPuid()) && GameStates.IsOnlineGame)
                {
                    AmongUsClient.Instance.ExitGame(DisconnectReasons.Banned);
                    SceneChanger.ChangeScene("MainMenu");
                }

                var client = PlayerControl.LocalPlayer.GetClient();
                Logger.Info($"{client.PlayerName.RemoveHtmlTags()} (ClientID: {client.Id} / FriendCode: {client.FriendCode} / HashPuid: {client.GetHashedPuid()} / Platform: {client.PlatformData.Platform}) Hosted room", "Session");
            }, 1f, "OnGameJoinedPatch");

            Main.SetRoles = [];
            Main.SetAddOns = [];
        }
    }
}

[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.DisconnectInternal))]
class DisconnectInternalPatch
{
    public static void Prefix( /*InnerNetClient __instance,*/ DisconnectReasons reason, string stringReason)
    {
        ShowDisconnectPopupPatch.Reason = reason;
        ShowDisconnectPopupPatch.StringReason = stringReason;
        ErrorText.Instance.CheatDetected = false;
        ErrorText.Instance.SBDetected = false;
        ErrorText.Instance.Clear();
        Cloud.StopConnect();
    }
}

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnPlayerJoined))]
static class OnPlayerJoinedPatch
{
    static bool IsDisconnected(this ClientData client)
    {
        var __instance = AmongUsClient.Instance;
        for (int i = 0; i < __instance.allClients.Count; i++)
        {
            ClientData clientData = __instance.allClients[i];
            if (clientData.Id == client.Id)
            {
                return true;
            }
        }

        return false;
    }

    public static void Postfix( /*AmongUsClient __instance,*/ [HarmonyArgument(0)] ClientData client)
    {
        Logger.Info($"{client.PlayerName} (ClientID: {client.Id} / FriendCode: {client.FriendCode} / Hashed PUID: {client.GetHashedPuid()}) joined the lobby", "Session");

        LateTask.New(() =>
        {
            try
            {
                if (AmongUsClient.Instance.AmHost)
                {
                    if (!client.IsDisconnected() && client.Character.Data.IsIncomplete)
                    {
                        Logger.SendInGame(GetString("Error.InvalidColor") + $" {client.Id}/{client.PlayerName}");
                        AmongUsClient.Instance.KickPlayer(client.Id, false);
                        Logger.Info($"Kicked client {client.Id}/{client.PlayerName} since its PlayerControl was not spawned in time.", "OnPlayerJoinedPatchPostfix");
                        return;
                    }

                    if (!Main.PlayerVersion.ContainsKey(client.Character.PlayerId))
                    {
                        var retry = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.RequestRetryVersionCheck, SendOption.None, client.Id);
                        AmongUsClient.Instance.FinishRpcImmediately(retry);
                    }
                }
            }
            catch
            {
            }
        }, 3f, "green bean kick late task", false);

        if (AmongUsClient.Instance.AmHost && client.FriendCode == "" && Options.KickPlayerFriendCodeNotExist.GetBool() && !GameStates.IsLocalGame)
        {
            if (!BanManager.TempBanWhiteList.Contains(client.GetHashedPuid()))
                BanManager.TempBanWhiteList.Add(client.GetHashedPuid());
            AmongUsClient.Instance.KickPlayer(client.Id, true);
            Logger.SendInGame(string.Format(GetString("Message.KickedByNoFriendCode"), client.PlayerName));
            Logger.Info($"TempBanned a player {client.PlayerName} without a friend code", "Temp Ban");
        }

        if (AmongUsClient.Instance.AmHost && client.PlatformData.Platform == (Platforms.Android | Platforms.IPhone) && Options.KickAndroidPlayer.GetBool())
        {
            AmongUsClient.Instance.KickPlayer(client.Id, false);
            string msg = string.Format(GetString("KickAndriodPlayer"), client.PlayerName);
            Logger.SendInGame(msg);
            Logger.Info(msg, "Android Kick");
        }

        if (AmongUsClient.Instance.AmHost && (client.PlayerName.EndsWith("cm", StringComparison.OrdinalIgnoreCase) || client.PlayerName.EndsWith("sm", StringComparison.OrdinalIgnoreCase)) && (client.PlayerName.Length == 4 || client.PlayerName.Count(x => x is 'i' or 'I') >= 2))
        {
            AmongUsClient.Instance.KickPlayer(client.Id, false);
            Logger.SendInGame("They were probably hacking tbh");
        }

        if (DestroyableSingleton<FriendsListManager>.Instance.IsPlayerBlockedUsername(client.FriendCode) && AmongUsClient.Instance.AmHost)
        {
            AmongUsClient.Instance.KickPlayer(client.Id, true);
            Logger.Info($"Blocked Player {client.PlayerName}({client.FriendCode}) has been banned.", "BAN");
        }

        BanManager.CheckBanPlayer(client);
        RPC.RpcVersionCheck();

        if (AmongUsClient.Instance.AmHost)
        {
            Main.SayStartTimes.Remove(client.Id);
            Main.SayBanwordsTimes.Remove(client.Id);
        }
    }
}

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnPlayerLeft))]
class OnPlayerLeftPatch
{
    public static void Postfix(AmongUsClient __instance, [HarmonyArgument(0)] ClientData data, [HarmonyArgument(1)] DisconnectReasons reason)
    {
        try
        {
            if (GameStates.IsInGame)
            {
                if (Options.CurrentGameMode == CustomGameMode.HideAndSeek) HnSManager.PlayerRoles.Remove(data.Character.PlayerId);

                if (data.Character.Is(CustomRoles.Lovers) && !data.Character.Data.IsDead)
                {
                    foreach (var lovers in Main.LoversPlayers)
                    {
                        Main.IsLoversDead = true;
                        Main.PlayerStates[lovers.PlayerId].RemoveSubRole(CustomRoles.Lovers);
                    }

                    Main.LoversPlayers.RemoveAll(x => x.PlayerId == data.Character.PlayerId);
                }

                switch (data.Character.GetCustomRole())
                {
                    case CustomRoles.Pelican:
                        Pelican.OnPelicanDied(data.Character.PlayerId);
                        break;
                    case CustomRoles.Markseeker:
                        Markseeker.OnDeath(data.Character);
                        break;
                }

                if (Executioner.Target.ContainsValue(data.Character.PlayerId)) Executioner.ChangeRoleByTarget(data.Character);
                if (Lawyer.Target.ContainsValue(data.Character.PlayerId)) Lawyer.ChangeRoleByTarget(data.Character);
                if (Spiritualist.SpiritualistTarget == data.Character.PlayerId) Spiritualist.RemoveTarget();
                Postman.CheckAndResetTargets(data.Character);
                GhostRolesManager.AssignedGhostRoles.Remove(data.Character.PlayerId);

                Utils.AfterPlayerDeathTasks(data.Character, GameStates.IsMeeting);

                PlayerState state = Main.PlayerStates[data.Character.PlayerId];
                if (state.deathReason == PlayerState.DeathReason.etc) state.deathReason = PlayerState.DeathReason.Disconnected;
                if (!state.IsDead) state.SetDead();

                NameNotifyManager.Notice.Remove(data.Character.PlayerId);
                data.Character.RpcSetName(data.Character.GetRealName(isMeeting: true));
                AntiBlackout.OnDisconnect(data.Character.Data);
                PlayerGameOptionsSender.RemoveSender(data.Character);
            }

            // if (Main.HostClientId == __instance.ClientId)
            // {
            //     const int clientId = -1;
            //     var player = PlayerControl.LocalPlayer;
            //     var title = "<color=#aaaaff>" + GetString("DefaultSystemMessageTitle") + "</color>";
            //     var name = player?.Data?.PlayerName;
            //     var msg = string.Empty;
            //     if (GameStates.IsInGame)
            //     {
            //         Utils.ErrorEnd("Host Left the Game");
            //         msg = GetString("Message.HostLeftGameInGame");
            //     }
            //     else if (GameStates.IsLobby)
            //         msg = GetString("Message.HostLeftGameInLobby");
            //
            //     player?.SetName(title);
            //     DestroyableSingleton<HudManager>.Instance.Chat.AddChat(player, msg);
            //     player?.SetName(name);
            //
            //     if (player != null && player.Data != null)
            //     {
            //         var writer = CustomRpcSender.Create("MessagesToSend");
            //         writer.StartMessage(clientId);
            //         writer.StartRpc(player.NetId, (byte)RpcCalls.SetName)
            //             .Write(player.Data.NetId)
            //             .Write(title)
            //             .EndRpc();
            //         writer.StartRpc(player.NetId, (byte)RpcCalls.SendChat)
            //             .Write(msg)
            //             .EndRpc();
            //         writer.StartRpc(player.NetId, (byte)RpcCalls.SetName)
            //             .Write(player.Data.NetId)
            //             .Write(player.Data.PlayerName)
            //             .EndRpc();
            //         writer.EndMessage();
            //         writer.SendMessage();
            //     }
            // }

            // Additional description of the reason for disconnection
            switch (reason)
            {
                case DisconnectReasons.Hacking:
                    Logger.SendInGame(string.Format(GetString("PlayerLeftByAU-Anticheat"), data?.PlayerName));
                    break;
                case DisconnectReasons.Error:
                    Logger.SendInGame(string.Format(GetString("PlayerLeftByError"), data?.PlayerName));
                    LateTask.New(() =>
                    {
                        CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Error);
                        GameManager.Instance.enabled = false;
                        GameManager.Instance?.RpcEndGame(GameOverReason.ImpostorDisconnect, false);
                    }, 3f, "Disconnect Error Auto-end");

                    break;
            }

            Logger.Info($"{data?.PlayerName} - (ClientID: {data?.Id} / FriendCode: {data?.FriendCode}) - Disconnected: {reason}，Ping: ({AmongUsClient.Instance.Ping})", "Session");

            if (AmongUsClient.Instance.AmHost)
            {
                Main.SayStartTimes.Remove(__instance.ClientId);
                Main.SayBanwordsTimes.Remove(__instance.ClientId);
                Main.PlayerVersion.Remove(data?.Character?.PlayerId ?? byte.MaxValue);
                Main.MessagesToSend.RemoveAll(x => x.RECEIVER_ID == data?.Character.PlayerId);

                if (data != null && data.Character != null)
                {
                    var netid = data.Character.NetId;
                    LateTask.New(() =>
                    {
                        if (GameStates.IsOnlineGame)
                        {
                            MessageWriter messageWriter = AmongUsClient.Instance.Streams[1];
                            messageWriter.StartMessage(5);
                            messageWriter.WritePacked(netid);
                            messageWriter.EndMessage();
                        }
                    }, 2.5f, "Repeat Despawn", false);
                }
            }

            Utils.CountAlivePlayers(true);

            if (data != null && data.Character != null) data.Character.Data.Disconnected = true;
        }
        catch (NullReferenceException)
        {
        }
        catch (Exception ex)
        {
            Logger.Error(ex.ToString(), "OnPlayerLeftPatch.Postfix");
        }
        finally
        {
            Utils.NotifyRoles(NoCache: true);
        }
    }
}

[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.Spawn))]
class InnerNetClientSpawnPatch
{
    public static void Postfix([HarmonyArgument(1)] int ownerId, [HarmonyArgument(2)] SpawnFlags flags)
    {
        if (!AmongUsClient.Instance.AmHost || flags != SpawnFlags.IsClientCharacter) return;

        var client = Utils.GetClientById(ownerId);

        Logger.Msg($"Spawn player data: ID {client?.Character?.PlayerId}: {client?.PlayerName}", "CreatePlayer");

        if (client == null || client.Character == null // client is null
                           || client.ColorId < 0 || Palette.PlayerColors.Length <= client.ColorId) // invalid client color
        {
            Logger.Warn("client is null or client have invalid color", "TrySyncAndSendMessage");
        }
        else
        {
            LateTask.New(() => { OptionItem.SyncAllOptions(client.Id); }, 3f, "Sync All Options For New Player");

            LateTask.New(() =>
            {
                if (Main.OverrideWelcomeMsg != "") Utils.SendMessage(Main.OverrideWelcomeMsg, client.Character.PlayerId);
                else TemplateManager.SendTemplate("welcome", client.Character.PlayerId, true);
            }, 3f, "Welcome Message");

            LateTask.New(() =>
            {
                if (client.Character == null) return;
                var sender = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.RequestRetryVersionCheck, SendOption.Reliable, client.Character.OwnerId);
                AmongUsClient.Instance.FinishRpcImmediately(sender);
            }, 3f, "RPC Request Retry Version Check");

            if (GameStates.IsOnlineGame)
            {
                LateTask.New(() =>
                {
                    if (GameStates.IsLobby && client.Character != null && LobbyBehaviour.Instance != null && GameStates.IsVanillaServer)
                    {
                        // Only for vanilla
                        if (!client.Character.IsHost() && !client.Character.IsModClient())
                        {
                            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(LobbyBehaviour.Instance.NetId, (byte)RpcCalls.LobbyTimeExpiring, SendOption.None, client.Id);
                            writer.WritePacked((int)GameStartManagerPatch.Timer);
                            writer.Write(false);
                            AmongUsClient.Instance.FinishRpcImmediately(writer);
                        }
                        // Non-host modded client
                        else if (!client.Character.IsHost() && client.Character.IsModClient())
                        {
                            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncLobbyTimer, SendOption.Reliable, client.Id);
                            writer.WritePacked((int)GameStartManagerPatch.Timer);
                            AmongUsClient.Instance.FinishRpcImmediately(writer);
                        }
                    }
                }, 3f, "Send RPC or Sync Lobby Timer");
            }
        }

        if (client != null && client.Character != null) Main.GuessNumber[client.Character.PlayerId] = [-1, 7];

        if (Main.OverrideWelcomeMsg == string.Empty && Main.PlayerStates.Count > 0 && client != null && Main.ClientIdList.Contains(client.Id))
        {
            if (Options.AutoDisplayKillLog.GetBool())
            {
                LateTask.New(() =>
                {
                    if (!AmongUsClient.Instance.IsGameStarted && client.Character != null)
                    {
                        Main.IsChatCommand = true;
                        Utils.ShowKillLog(client.Character.PlayerId);
                    }
                }, 1f, "DisplayKillLog");
            }

            if (Options.AutoDisplayLastAddOns.GetBool())
            {
                LateTask.New(() =>
                {
                    if (!AmongUsClient.Instance.IsGameStarted && client.Character != null)
                    {
                        Main.IsChatCommand = true;
                        Utils.ShowLastAddOns(client.Character.PlayerId);
                    }
                }, 1.1f, "DisplayLastAddOns");
            }

            if (Options.AutoDisplayLastRoles.GetBool())
            {
                LateTask.New(() =>
                {
                    if (!AmongUsClient.Instance.IsGameStarted && client.Character != null)
                    {
                        Main.IsChatCommand = true;
                        Utils.ShowLastRoles(client.Character.PlayerId);
                    }
                }, 1.2f, "DisplayLastRoles");
            }

            if (Options.AutoDisplayLastResult.GetBool())
            {
                LateTask.New(() =>
                {
                    if (!AmongUsClient.Instance.IsGameStarted && client.Character != null)
                    {
                        Main.IsChatCommand = true;
                        Utils.ShowLastResult(client.Character.PlayerId);
                    }
                }, 1.3f, "DisplayLastResult");
            }

            // if (PlayerControl.LocalPlayer.FriendCode.GetDevUser().IsUp && Options.EnableUpMode.GetBool())
            // {
            //     LateTask.New(() =>
            //     {
            //         if (!AmongUsClient.Instance.IsGameStarted && client.Character != null)
            //         {
            //             Main.IsChatCommand = true;
            //             Utils.SendMessage($"{GetString("Message.YTPlanNotice")} {PlayerControl.LocalPlayer.FriendCode.GetDevUser().UpName}", client.Character.PlayerId);
            //         }
            //     }, 1.4f, "DisplayUpWarnning");
            // }
        }
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckName))]
class PlayerControlCheckNamePatch
{
    public static void Postfix(PlayerControl __instance, ref string playerName)
    {
        if (!AmongUsClient.Instance.AmHost || !GameStates.IsLobby) return;

        if (BanManager.CheckDenyNamePlayer(__instance, playerName)) return;

        if (Main.AllClientRealNames.TryAdd(__instance.OwnerId, playerName))
            RPC.SyncAllClientRealNames();

        var name = playerName;

        if (Options.FormatNameMode.GetInt() == 2 && __instance.Data.ClientId != AmongUsClient.Instance.ClientId)
        {
            name = Main.Get_TName_Snacks;
        }
        else
        {
            name = name.RemoveHtmlTags().Replace(@"\", string.Empty).Replace("/", string.Empty).Replace("\n", string.Empty).Replace("\r", string.Empty).Replace("<", string.Empty).Replace(">", string.Empty);
            if (name.Length > 10) name = name[..10];
            if (Options.DisableEmojiName.GetBool()) name = Regex.Replace(name, @"\p{Cs}", string.Empty);
            if (Regex.Replace(Regex.Replace(name, @"\s", string.Empty), @"[\x01-\x1F,\x7F]", string.Empty).Length < 1) name = Main.Get_TName_Snacks;
        }

        Main.AllPlayerNames[__instance.PlayerId] = name;
        Logger.Info($"PlayerId: {__instance.PlayerId} - playerName: {playerName} - name: {name}", "Name player");
        RPC.SyncAllPlayerNames();

        if (__instance != null && !name.Equals(playerName))
        {
            Logger.Warn($"Standard nickname: {playerName} => {name}", "Name Format");
            playerName = name;
        }

        LateTask.New(() =>
        {
            if (__instance != null && !__instance.Data.Disconnected && !__instance.IsModClient())
            {
                var sender = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.RequestRetryVersionCheck, SendOption.Reliable, __instance.OwnerId);
                AmongUsClient.Instance.FinishRpcImmediately(sender);
            }
        }, 0.6f, "Retry Version Check", false);
    }
}

[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.FixedUpdate))]
static class InnerNetClientFixedUpdatePatch
{
    private static float Timer;

    public static void Postfix()
    {
        if (GameStates.IsLocalGame || Main.AllPlayerControls.Length < 7) return;

        Timer += Time.fixedDeltaTime;
        if (Timer < 25f) return;
        Timer = 0f;

        AmongUsClient.Instance.KickNotJoinedPlayers();
    }
}