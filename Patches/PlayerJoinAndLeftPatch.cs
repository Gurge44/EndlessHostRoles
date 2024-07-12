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
using static EHR.Translator;

namespace EHR;

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameJoined))]
class OnGameJoinedPatch
{
    public static void Postfix(AmongUsClient __instance)
    {
        while (!Options.IsLoaded) Task.Delay(1);
        Logger.Info($"{__instance.GameId} joined lobby", "OnGameJoined");
        Main.PlayerVersion = [];
        if (!Main.VersionCheat.Value) RPC.RpcVersionCheck();
        SoundManager.Instance?.ChangeAmbienceVolume(DataManager.Settings.Audio.AmbienceVolume);

        if (GameStates.IsModHost)
            Main.HostClientId = Utils.GetPlayerById(0)?.GetClientId() ?? -1;

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
class OnPlayerJoinedPatch
{
    public static void Postfix( /*AmongUsClient __instance,*/ [HarmonyArgument(0)] ClientData client)
    {
        Logger.Info($"{client.PlayerName} (ClientID: {client.Id} / FriendCode: {client.FriendCode} / Hashed PUID: {client.GetHashedPuid()}) joined the lobby", "Session");

        LateTask.New(() =>
        {
            try
            {
                if (AmongUsClient.Instance.AmHost)
                {
                    if (!client.Character.Data.Disconnected && client.Character.Data.IsIncomplete)
                    {
                        Logger.SendInGame(GetString("Error.InvalidColor") + $" {client.Id}/{client.PlayerName}");
                        AmongUsClient.Instance.KickPlayer(client.Id, false);
                        Logger.Info($"Kicked client {client.Id}/{client.PlayerName} because PlayerControl is not spawned in time.", "OnPlayerJoinedPatchPostfix");
                        return;
                    }

                    if (!Main.PlayerVersion.TryGetValue(client.Character.PlayerId, out _))
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
            Logger.Info($"Ban Player ー {client.PlayerName}({client.FriendCode}) has been banned.", "BAN");
        }

        BanManager.CheckBanPlayer(client);
        BanManager.CheckDenyNamePlayer(client);
        RPC.RpcVersionCheck();

        if (AmongUsClient.Instance.AmHost)
        {
            if (Main.SayStartTimes.ContainsKey(client.Id)) Main.SayStartTimes.Remove(client.Id);
            if (Main.SayBanwordsTimes.ContainsKey(client.Id)) Main.SayBanwordsTimes.Remove(client.Id);
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
                    foreach (var lovers in Main.LoversPlayers.ToArray())
                    {
                        Main.IsLoversDead = true;
                        Main.LoversPlayers.Remove(lovers);
                        Main.PlayerStates[lovers.PlayerId].RemoveSubRole(CustomRoles.Lovers);
                    }
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

            if (Main.HostClientId == __instance.ClientId)
            {
                const int clientId = -1;
                var player = PlayerControl.LocalPlayer;
                var title = "<color=#aaaaff>" + GetString("DefaultSystemMessageTitle") + "</color>";
                var name = player?.Data?.PlayerName;
                var msg = string.Empty;
                if (GameStates.IsInGame)
                {
                    Utils.ErrorEnd("Host Left the Game");
                    msg = GetString("Message.HostLeftGameInGame");
                }
                else if (GameStates.IsLobby)
                    msg = GetString("Message.HostLeftGameInLobby");

                player?.SetName(title);
                DestroyableSingleton<HudManager>.Instance.Chat.AddChat(player, msg);
                player?.SetName(name);

                if (player != null && player.Data != null)
                {
                    var writer = CustomRpcSender.Create("MessagesToSend");
                    writer.StartMessage(clientId);
                    writer.StartRpc(player.NetId, (byte)RpcCalls.SetName)
                        .Write(player.Data.NetId)
                        .Write(title)
                        .EndRpc();
                    writer.StartRpc(player.NetId, (byte)RpcCalls.SendChat)
                        .Write(msg)
                        .EndRpc();
                    writer.StartRpc(player.NetId, (byte)RpcCalls.SetName)
                        .Write(player.Data.NetId)
                        .Write(player.Data.PlayerName)
                        .EndRpc();
                    writer.EndMessage();
                    writer.SendMessage();
                }
            }

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
        if (client == null) return;

        Logger.Msg($"Spawn player data: ID {client.Character.PlayerId}: {client.PlayerName}", "CreatePlayer");

        LateTask.New(() =>
        {
            if (client.Character == null || !GameStates.IsLobby) return;
            OptionItem.SyncAllOptions(client.Id);
        }, 3f, "Sync All Options For New Player");

        Main.GuessNumber[client.Character.PlayerId] = [-1, 7];

        LateTask.New(() =>
        {
            if (client.Character == null) return;
            if (Main.OverrideWelcomeMsg != string.Empty) Utils.SendMessage(Main.OverrideWelcomeMsg, client.Character.PlayerId);
            else TemplateManager.SendTemplate("welcome", client.Character.PlayerId, true);
        }, 3f, "Welcome Message");
        if (Main.OverrideWelcomeMsg == string.Empty && Main.PlayerStates.Count > 0 && Main.ClientIdList.Contains(client.Id))
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

            if (PlayerControl.LocalPlayer.FriendCode.GetDevUser().IsUp && Options.EnableUpMode.GetBool())
            {
                LateTask.New(() =>
                {
                    if (!AmongUsClient.Instance.IsGameStarted && client.Character != null)
                    {
                        Main.IsChatCommand = true;
                        //     Utils.SendMessage($"{GetString("Message.YTPlanNotice")} {PlayerControl.LocalPlayer.FriendCode.GetDevUser().UpName}", client.Character.PlayerId);
                    }
                }, 1.4f, "DisplayUpWarnning");
            }
        }
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckName))]
class PlayerControlCheckNamePatch
{
    public static void Postfix(PlayerControl __instance, ref string playerName)
    {
        if (!AmongUsClient.Instance.AmHost || !GameStates.IsLobby) return;

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

        if (__instance != null && !name.Equals(playerName))
        {
            Logger.Warn($"Standard nickname: {playerName} => {name}", "Name Format");
            playerName = name;
        }
    }
}