using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AmongUs.Data;
using AmongUs.GameOptions;
using AmongUs.InnerNet.GameDataMessages;
using EHR.AddOns.Common;
using EHR.Crewmate;
using EHR.Modules;
using EHR.Neutral;
using EHR.Patches;
using HarmonyLib;
using Hazel;
using InnerNet;
using UnityEngine;
using static EHR.Translator;

namespace EHR;

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameJoined))]
internal static class OnGameJoinedPatch
{
    public static bool JoiningGame;
    public static bool ClearedLogs;

    public static void Postfix(AmongUsClient __instance)
    {
        JoiningGame = true;

        while (!Options.IsLoaded) Task.Delay(1);

        Logger.Info($"{__instance.GameId} joined lobby", "OnGameJoined");

        SetUpRoleTextPatch.IsInIntro = false;

        Main.PlayerVersion = [];
        RPC.RpcVersionCheck();
        SoundManager.Instance?.ChangeAmbienceVolume(DataManager.Settings.Audio.AmbienceVolume);
        Options.LoadUserData();

        GameStates.InGame = false;
        ErrorText.Instance?.Clear();
        ChatCommands.VotedToStart = [];
        Main.GameTimer = 0f;

        Utils.DirtyName = [];

        LateTask.New(Achievements.ShowWaitingAchievements, 8f, log: false);
        
        if (!ClearedLogs)
        {
            LateTask.New(() =>
            {
                if (!HudManager.InstanceExists || AmongUsClient.Instance.IsGameStarted) return;
            
                var result = CleanOldItems();
            
                if (result.Files > 0 || result.Folders > 0)
                {
                    Prompt.Show(string.Format(GetString("Promt.DeleteOldLogs"), result.Files, result.Folders), () =>
                    {
                        LateTask.New(() =>
                        {
                            result = CleanOldItems(dryRun: false);
                            HudManager.Instance.ShowPopUp(string.Format(GetString("LogDeletionResults"), result.Files, result.Folders));
                        }, 0.5f, log: false);
                    }, () => { });
                }
                
                ClearedLogs = true;
            }, 5f, log: false);
        }

        if (AmongUsClient.Instance.AmHost)
        {
            GameStartManagerPatch.GameStartManagerUpdatePatch.ExitTimer = -1;
            Main.DoBlockNameChange = false;
            EAC.DeNum = 0;
            Main.AllPlayerNames = [];
            Main.AllClientRealNames = [];

            if (Main.NormalOptions?.KillCooldown == 0f) Main.NormalOptions.KillCooldown = Main.LastKillCooldown.Value;

            AURoleOptions.SetOpt(Main.NormalOptions?.CastFast<IGameOptions>());
            if (AURoleOptions.ShapeshifterCooldown == 0f) AURoleOptions.ShapeshifterCooldown = Main.LastShapeshifterCooldown.Value;

            LateTask.New(() =>
            {
                JoiningGame = false;

                if (BanManager.CheckEACList(PlayerControl.LocalPlayer.FriendCode, PlayerControl.LocalPlayer.GetClient().GetHashedPuid()) && GameStates.IsOnlineGame)
                {
                    AmongUsClient.Instance.ExitGame(DisconnectReasons.Banned);
                    SceneChanger.ChangeScene("MainMenu");
                }

                ClientData client = PlayerControl.LocalPlayer.GetClient();
                Logger.Info($"{client.PlayerName.RemoveHtmlTags()} (ClientID: {client.Id} / FriendCode: {client.FriendCode} / HashPuid: {client.GetHashedPuid()} / Platform: {client.PlatformData.Platform}) Hosted room (Server: {Utils.GetRegionName()})", "Session");

                Main.Instance.StartCoroutine(OptionShower.GetText());
            }, 1f, "OnGameJoinedPatch");
            
            LateTask.New(() =>
            {
                if (Main.NormalOptions != null && Mathf.Approximately(Main.NormalOptions.KillCooldown, 25f))
                    Main.NormalOptions.KillCooldown = Options.FallBackKillCooldownValue?.GetFloat() ?? 25f;
            }, 5f, log: false);

            Main.SetRoles = [];
            Main.SetAddOns = [];
            ChatCommands.DraftResult = [];
            ChatCommands.DraftRoles = [];

            LateTask.New(() =>
            {
                if (GameStates.CurrentServerType is not GameStates.ServerType.Custom and not GameStates.ServerType.Local)
                {
                    try
                    {
                        LobbySharingAPI.NotifyLobbyStatusChanged(LobbyStatus.In_Lobby);
                        if (GameStates.InGame) LateTask.New(() => LobbySharingAPI.NotifyLobbyStatusChanged(LobbyStatus.In_Game), 5f, "NotifyLobbyStatusChanged Immediately");
                    }
                    catch (Exception e) { Utils.ThrowException(e); }
                }
                else Logger.Info($"Not sending lobby status to the server because the server type is {GameStates.CurrentServerType}", "OnGameJoinedPatch");
            }, 5f, "NotifyLobbyCreated");

            if (Options.AutoGMPollCommandAfterJoin.GetBool() && !Options.AutoGMRotationEnabled)
            {
                Main.Instance.StartCoroutine(CoRoutine());

                IEnumerator CoRoutine()
                {
                    float timer = Options.AutoGMPollCommandCooldown.GetInt();

                    while (timer > 0)
                    {
                        if (!GameStates.IsLobby) yield break;
                        timer -= Time.deltaTime;
                        yield return null;
                    }

                    if (Options.AutoGMPollCommandAfterJoin.GetBool() && !Options.AutoGMRotationEnabled)
                        ChatCommands.GameModePollCommand(PlayerControl.LocalPlayer, "/gmpoll", ["/gmpoll"]);
                }
            }

            if (Options.AutoDraftStartCommandAfterJoin.GetBool())
            {
                Main.Instance.StartCoroutine(CoRoutine());

                IEnumerator CoRoutine()
                {
                    float timer = Options.AutoDraftStartCommandCooldown.GetInt();

                    while (timer > 0)
                    {
                        if (!GameStates.IsLobby) yield break;
                        timer -= Time.deltaTime;
                        yield return null;
                    }

                    if (Options.AutoDraftStartCommandAfterJoin.GetBool())
                        ChatCommands.DraftStartCommand(PlayerControl.LocalPlayer, "/draftstart", ["/draftstart"]);
                }
            }

            if (Options.AutoGMRotationEnabled)
            {
                Main.Instance.StartCoroutine(CoRoutine());

                IEnumerator CoRoutine()
                {
                    yield return new WaitForSeconds(10f);

                    try { Utils.SendMessage(HudManagerPatch.BuildAutoGMRotationStatusText(true), title: GetString("AutoGMRotationStatusText")); }
                    catch (Exception e) { Utils.ThrowException(e); }

                    CustomGameMode nextGM = Options.AutoGMRotationCompiled[Options.AutoGMRotationIndex];
                    
                    float timer;
                    if (nextGM != CustomGameMode.All) timer = 0f;
                    else if (Options.AutoGMPollCommandAfterJoin.GetBool()) timer = Options.AutoGMPollCommandCooldown.GetInt() - 10;
                    else if (Main.AutoStart.Value) timer = (Options.MinWaitAutoStart.GetFloat() * 60) - 65;
                    else timer = 30f;

                    Logger.Info($"Auto GM Rotation timer: {timer}", "Auto GM Rotation");

                    HudManagerPatch.AutoGMRotationCooldownTimerEndTS = Utils.TimeStamp + (int)timer;

                    while (timer > 0)
                    {
                        if (!GameStates.IsLobby) yield break;
                        timer -= Time.deltaTime;
                        yield return null;
                    }

                    if (Options.AutoGMRotationEnabled)
                    {
                        if (nextGM == CustomGameMode.All) ChatCommands.GameModePollCommand(PlayerControl.LocalPlayer, "/gmpoll", ["/gmpoll"]);
                        else Options.GameMode.SetValue((int)nextGM - 1);

                        Logger.Info($"Auto GM Rotation: Next Game Mode = {nextGM}", "Auto GM Rotation");
                    }
                }
            }
        }
        else
            LateTask.New(() => Main.Instance.StartCoroutine(OptionShower.GetText()), 10f, "OptionShower.GetText on client");
    }

    // Written with AI because I don't want it to delete the wrong files
    /// <summary>
    /// Cleans files and folders older than `days` in the EHR_Logs folder.
    /// Default: dryRun = true (shows what would be deleted).
    /// </summary>
    private static (int Files, int Folders) CleanOldItems(bool dryRun = true, int days = 7)
    {
#if ANDROID
        return (0, 0); // Not supported on Android
#endif
        string path;
        try
        {
            var f = $"{Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)}/EHR_Logs";
            // Normalize separators
            path = Path.GetFullPath(f);
        }
        catch (Exception ex)
        {
            Logger.Error($"Could not determine path: {ex.Message}", "CleanOldItems");
            return (0, 0);
        }

        if (!Path.IsPathRooted(path))
        {
            Logger.Error("Target path is not rooted. Aborting for safety.", "CleanOldItems");
            return (0, 0);
        }

        // Safety checks: ensure target folder name is exactly "EHR_Logs"
        var folderName = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (!string.Equals(folderName, "EHR_Logs", StringComparison.OrdinalIgnoreCase))
        {
            Logger.Error($"[ERROR] Target folder name is '{folderName}' (expected 'EHR_Logs'). Aborting for safety.", "CleanOldItems");
            return (0, 0);
        }

        if (!Directory.Exists(path))
        {
            Logger.Msg("[INFO] Target directory does not exist. Nothing to clean.", "CleanOldItems");
            return (0, 0);
        }

        var threshold = DateTime.Now - TimeSpan.FromDays(days);
        Logger.Msg($"Threshold: delete items last written before {threshold:O}", "CleanOldItems");
        Logger.Msg(dryRun
            ? "Running in dry run mode — no files or folders will be deleted."
            : "Running for real — files and folders will be deleted.", "CleanOldItems");

        int filesDeleted = 0;
        int foldersDeleted = 0;
        var failedDeletes = new List<string>();

        // 1) Delete files older than threshold (walk all files recursively)
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    DateTime lastWrite = File.GetLastWriteTime(file);
                    if (lastWrite < threshold)
                    {
                        Logger.Warn(dryRun
                            ? $"[DRY] Would delete file: {file} (LastWrite: {lastWrite:O})"
                            : $"[DEL] Deleting file: {file} (LastWrite: {lastWrite:O})", "CleanOldItems");

                        if (!dryRun)
                        {
                            File.SetAttributes(file, FileAttributes.Normal); // remove read-only to avoid exceptions
                            File.Delete(file);
                        }
                        filesDeleted++;
                    }
                }
                catch (Exception exFile)
                {
                    string msg = $"Failed to delete file '{file}': {exFile.Message}";
                    Logger.Error(msg, "CleanOldItems");
                    failedDeletes.Add(msg);
                    // continue with other files
                }
            }
        }
        catch (Exception exEnum)
        {
            Logger.Error($"Failed enumerating files: {exEnum.Message}", "CleanOldItems");
        }

        // 2) Attempt to delete directories that are empty AND older than threshold.
        //    We process directories from deepest to shallowest so we can remove empty parent dirs.
        try
        {
            var allDirectories = Directory
                .EnumerateDirectories(path, "*", SearchOption.AllDirectories)
                // order by path depth descending
                .OrderByDescending(d => d.Count(c => c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar))
                .ToList();

            foreach (var dir in allDirectories)
            {
                try
                {
                    // Skip if directory now contains any entries
                    if (Directory.EnumerateFileSystemEntries(dir).Any())
                        continue;

                    DateTime lastWrite = Directory.GetLastWriteTime(dir);
                    DateTime creation = Directory.GetCreationTime(dir);

                    // Only delete if the directory is older than threshold by last write OR creation time (safer)
                    if (lastWrite < threshold || creation < threshold)
                    {
                        Logger.Warn(dryRun
                            ? $"[DRY] Would delete empty folder: {dir} (LastWrite: {lastWrite:O}, Creation: {creation:O})"
                            : $"[DEL] Deleting empty folder: {dir} (LastWrite: {lastWrite:O}, Creation: {creation:O})", "CleanOldItems");

                        if (!dryRun)
                        {
                            Directory.Delete(dir, false); // false -> directory must be empty
                        }
                        foldersDeleted++;
                    }
                }
                catch (Exception exDir)
                {
                    string msg = $"Failed to delete directory '{dir}': {exDir.Message}";
                    Logger.Error(msg, "CleanOldItems");
                    failedDeletes.Add(msg);
                }
            }

            // Optionally, check the root target folder itself: if empty and old, you might want to delete it.
            // Here we will NOT delete the root EHR_Logs folder itself to be extra safe.
        }
        catch (Exception exEnumDirs)
        {
            Logger.Error($"Failed enumerating directories: {exEnumDirs.Message}", "CleanOldItems");
        }

        // Summary
        Logger.Msg("=== Summary ===", "CleanOldItems");
        Logger.Msg($"Files matched and processed: {filesDeleted}", "CleanOldItems");
        Logger.Msg($"Folders matched and processed: {foldersDeleted}", "CleanOldItems");
        if (failedDeletes.Count > 0)
        {
            Logger.Msg($"Failures ({failedDeletes.Count}):", "CleanOldItems");
            foreach (var f in failedDeletes) Logger.Msg(f, "CleanOldItems");
        }
        else
        {
            Logger.Msg("No failures reported.", "CleanOldItems");
        }

        if (dryRun)
        {
            Logger.Msg("Dry run complete.", "CleanOldItems");
        }
        
        return (filesDeleted, foldersDeleted);
    }
}

[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.DisconnectInternal))]
internal static class DisconnectInternalPatch
{
    public static void Prefix( /*InnerNetClient __instance,*/ DisconnectReasons reason, string stringReason)
    {
        //ShowDisconnectPopupPatch.Reason = reason;
        //ShowDisconnectPopupPatch.StringReason = stringReason;
        ErrorText.Instance.CheatDetected = false;
        ErrorText.Instance.SBDetected = false;
        ErrorText.Instance.Clear();
    }
}

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnPlayerJoined))]
internal static class OnPlayerJoinedPatch
{
    public static bool IsDisconnected(this ClientData client)
    {
        foreach (ClientData clientData in AmongUsClient.Instance.allClients)
        {
            if (clientData.Id == client.Id)
                return false;
        }

        return true;
    }

    public static void Postfix( /*AmongUsClient __instance,*/ [HarmonyArgument(0)] ClientData client)
    {
        Logger.Info($"{client.PlayerName} (ClientID: {client.Id} / FriendCode: {client.FriendCode} / Hashed PUID: {client.GetHashedPuid()}) joined the lobby", "Session");

        LateTask.New(() =>
        {
            try
            {
                if (!AmongUsClient.Instance.AmHost) return;

                Utils.DirtyName.Add(PlayerControl.LocalPlayer.PlayerId);

                if (Options.KickSlowJoiningPlayers.GetBool() && ((!client.IsDisconnected() && client.Character.Data.IsIncomplete) || ((client.Character.Data.DefaultOutfit.ColorId < 0 || Palette.PlayerColors.Length <= client.Character.Data.DefaultOutfit.ColorId) && Main.AllPlayerControls.Length <= 15)))
                {
                    Logger.SendInGame(GetString("Error.InvalidColor") + $" {client.Id}/{client.PlayerName}", Color.yellow);
                    AmongUsClient.Instance.KickPlayer(client.Id, false);
                    Logger.Info($"Kicked client {client.Id}/{client.PlayerName} since its PlayerControl was not spawned in time.", "OnPlayerJoinedPatchPostfix");
                    return;
                }

                if (!Main.PlayerVersion.ContainsKey(client.Character.PlayerId))
                {
                    MessageWriter retry = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.RequestRetryVersionCheck, SendOption.None, client.Id);
                    AmongUsClient.Instance.FinishRpcImmediately(retry);
                }

                if (client.Character != null && client.Character.Data != null && (client.Character.Data.DefaultOutfit.ColorId < 0 || Palette.PlayerColors.Length <= client.Character.Data.DefaultOutfit.ColorId) && Main.AllPlayerControls.Length >= 17)
                    Disco.ChangeColor(client.Character);
            }
            catch { }
        }, 4.5f, "green bean kick late task", false);

        if (AmongUsClient.Instance.AmHost && client.FriendCode == "" && Options.KickPlayerFriendCodeNotExist.GetBool() && GameStates.CurrentServerType is not GameStates.ServerType.Modded and not GameStates.ServerType.Niko and not GameStates.ServerType.Local)
        {
            if (!BanManager.TempBanWhiteList.Contains(client.GetHashedPuid())) BanManager.TempBanWhiteList.Add(client.GetHashedPuid());

            AmongUsClient.Instance.KickPlayer(client.Id, false);
            Logger.SendInGame(string.Format(GetString("Message.KickedByNoFriendCode"), client.PlayerName), Color.yellow);
            Logger.Info($"TempBanned a player {client.PlayerName} without a friend code", "Temp Ban");
        }

        if (AmongUsClient.Instance.AmHost && client.PlatformData.Platform is Platforms.Android or Platforms.IPhone && Options.KickAndroidPlayer.GetBool())
        {
            AmongUsClient.Instance.KickPlayer(client.Id, false);
            string msg = string.Format(GetString("KickAndriodPlayer"), client.PlayerName);
            Logger.SendInGame(msg, Color.yellow);
            Logger.Info(msg, "Android Kick");
        }

        if (FastDestroyableSingleton<FriendsListManager>.Instance.IsPlayerBlockedUsername(client.FriendCode) && AmongUsClient.Instance.AmHost && GameStates.CurrentServerType is not GameStates.ServerType.Modded and not GameStates.ServerType.Niko and not GameStates.ServerType.Local)
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
internal static class OnPlayerLeftPatch
{
    public static void Postfix(AmongUsClient __instance, [HarmonyArgument(0)] ClientData data, [HarmonyArgument(1)] DisconnectReasons reason)
    {
        try
        {
            if (GameStates.IsInGame && data != null && data.Character != null)
            {
                if (Options.CurrentGameMode == CustomGameMode.HideAndSeek) CustomHnS.PlayerRoles.Remove(data.Character.PlayerId);

                if (data.Character.Is(CustomRoles.Lovers) && data.Character.IsAlive())
                {
                    foreach (PlayerControl lovers in Main.LoversPlayers)
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
                    case CustomRoles.Jackal:
                        Jackal.Instances.Do(x => x.PromoteSidekick());
                        break;
                }

                if (Executioner.Target.ContainsValue(data.Character.PlayerId)) Executioner.ChangeRoleByTarget(data.Character);

                if (Lawyer.Target.ContainsValue(data.Character.PlayerId)) Lawyer.ChangeRoleByTarget(data.Character);

                if (Spiritualist.SpiritualistTarget == data.Character.PlayerId) Spiritualist.RemoveTarget();

                Postman.CheckAndResetTargets(data.Character);
                GhostRolesManager.AssignedGhostRoles.Remove(data.Character.PlayerId);

                PlayerState state = Main.PlayerStates[data.Character.PlayerId];
                if (state.deathReason == PlayerState.DeathReason.etc) state.deathReason = PlayerState.DeathReason.Disconnected;

                if (!state.IsDead) state.SetDead();

                Utils.AfterPlayerDeathTasks(data.Character, GameStates.IsMeeting, true);

                NameNotifyManager.Notifies.Remove(data.Character.PlayerId);
                data.Character.RpcSetName(data.Character.GetRealName(true));
                PlayerGameOptionsSender.RemoveSender(data.Character);
            }

            // Additional description of the reason for disconnection
            switch (reason)
            {
                case DisconnectReasons.Hacking:
                    Logger.SendInGame(string.Format(GetString("PlayerLeftByAU-Anticheat"), data?.PlayerName), Color.yellow);
                    break;
            }

            Logger.Info($"{data?.PlayerName} - (ClientID: {data?.Id} / FriendCode: {data?.FriendCode}) - Disconnected: {reason}, Ping: ({AmongUsClient.Instance.Ping})", "Session");

            if (AmongUsClient.Instance.AmHost)
            {
                Main.SayStartTimes.Remove(__instance.ClientId);
                Main.SayBanwordsTimes.Remove(__instance.ClientId);
                Main.PlayerVersion.Remove(data?.Character?.PlayerId ?? byte.MaxValue);

                if (data != null && data.Character != null)
                {
                    uint netid = data.Character.NetId;

                    LateTask.New(() =>
                    {
                        if (GameStates.IsOnlineGame)
                        {
                            var message = new DespawnGameDataMessage(netid);
                            AmongUsClient.Instance.LateBroadcastReliableMessage(message.CastFast<IGameDataMessage>());
                        }

                        if (GameStates.IsLobby)
                            Utils.DirtyName.Add(PlayerControl.LocalPlayer.PlayerId);
                    }, 2.5f, "Repeat Despawn", false);
                }
            }

            Utils.CountAlivePlayers(true);
        }
        catch (NullReferenceException) { }
        catch (Exception ex) { Logger.Error(ex.ToString(), "OnPlayerLeftPatch.Postfix"); }
        finally
        {
            if (!GameStates.IsLobby && GameStates.IsInTask && !ExileController.Instance)
                Utils.NotifyRoles(ForceLoop: true);
        }
    }
}

[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.Spawn))]
internal static class InnerNetClientSpawnPatch
{
    public static void Postfix([HarmonyArgument(1)] int ownerId, [HarmonyArgument(2)] SpawnFlags flags)
    {
        if (!AmongUsClient.Instance.AmHost || flags != SpawnFlags.IsClientCharacter) return;

        ClientData client = Utils.GetClientById(ownerId);

        Logger.Msg($"Spawn player data: ID {client?.Character?.PlayerId}: {client?.PlayerName}", "CreatePlayer");

        if (client == null || client.Character == null // client is null
                           || client.ColorId < 0 || Palette.PlayerColors.Length <= client.ColorId) // invalid client color
            Logger.Warn("client is null or client have invalid color", "TrySyncAndSendMessage");
        else
        {
            LateTask.New(() => OptionItem.SyncAllOptions(client.Id), 3f, "Sync All Options For New Player");

            LateTask.New(() =>
            {
                if (Main.OverrideWelcomeMsg != "")
                    Utils.SendMessage(Main.OverrideWelcomeMsg, client.Character.PlayerId, sendOption: SendOption.None);
                else
                    TemplateManager.SendTemplate("welcome", client.Character.PlayerId, true);
            }, GameStates.CurrentServerType == GameStates.ServerType.Niko ? 7f : 3f, "Welcome Message");

            LateTask.New(() =>
            {
                if (client.Character == null) return;

                MessageWriter sender = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.RequestRetryVersionCheck, SendOption.Reliable, client.Character.OwnerId);
                AmongUsClient.Instance.FinishRpcImmediately(sender);
            }, 3f, "RPC Request Retry Version Check");

            if (GameStates.IsOnlineGame && !client.Character.IsHost())
            {
                LateTask.New(() =>
                {
                    if (GameStates.IsLobby && client.Character != null && LobbyBehaviour.Instance != null && GameStates.CurrentServerType == GameStates.ServerType.Vanilla)
                    {
                        // Only for vanilla
                        if (!client.Character.IsModdedClient())
                        {
                            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(LobbyBehaviour.Instance.NetId, (byte)RpcCalls.LobbyTimeExpiring, SendOption.None, client.Id);
                            writer.WritePacked((int)GameStartManagerPatch.Timer);
                            writer.Write(false);
                            AmongUsClient.Instance.FinishRpcImmediately(writer);
                        }
                        // Non-host modded client
                        else
                        {
                            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncLobbyTimer, SendOption.Reliable, client.Id);
                            writer.Write(GameStartManagerPatch.TimerStartTS.ToString());
                            AmongUsClient.Instance.FinishRpcImmediately(writer);
                        }
                    }
                }, IRandom.Instance.Next(7, 13), "Sync Lobby Timer RPC");
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

            // if (PlayerControl.LocalPlayer.FriendCode.GetDevUser().Up && Options.EnableUpMode.GetBool())
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
internal static class PlayerControlCheckNamePatch
{
    public static void Postfix(PlayerControl __instance, ref string playerName)
    {
        if (!AmongUsClient.Instance.AmHost || !GameStates.IsLobby) return;

        if (BanManager.CheckDenyNamePlayer(__instance, playerName)) return;

        if (Main.AllClientRealNames.TryAdd(__instance.OwnerId, playerName)) RPC.SyncAllClientRealNames();

        string name = playerName;

        if (Options.FormatNameMode.GetInt() == 2 && __instance.Data.ClientId != AmongUsClient.Instance.ClientId)
            name = Main.Get_TName_Snacks;
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
            if (__instance != null && !__instance.Data.Disconnected && !__instance.IsModdedClient())
            {
                MessageWriter sender = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.RequestRetryVersionCheck, SendOption.Reliable, __instance.OwnerId);
                AmongUsClient.Instance.FinishRpcImmediately(sender);
            }
        }, 0.6f, "Retry Version Check", false);
    }
}

//[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.FixedUpdate))]
internal static class InnerNetClientFixedUpdatePatch
{
    private static float Timer;

    public static void Postfix()
    {
        try
        {
            if (GameStates.IsLocalGame || !GameStates.IsLobby || !Options.KickNotJoinedPlayersRegularly.GetBool() || Main.AllPlayerControls.Length < 7) return;

            Timer += Time.fixedDeltaTime;
            if (Timer < 25f) return;
            Timer = 0f;

            AmongUsClient.Instance.KickNotJoinedPlayers();
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.SetColor))]
internal static class SetColorPatch
{
    public static void Postfix(PlayerControl __instance, byte bodyColor)
    {
        if (Main.IntroDestroyed || __instance == null) return;

        string colorName = Palette.GetColorName(bodyColor);
        Logger.Info($"{__instance.GetRealName()}'s color is {colorName}", "RpcSetColor");

        if (colorName == "???")
        {
            LateTask.New(() =>
            {
                if (Options.KickSlowJoiningPlayers.GetBool() && __instance != null && !Main.PlayerColors.ContainsKey(__instance.PlayerId))
                {
                    ClientData client = __instance.GetClient();

                    if (client != null)
                    {
                        Logger.SendInGame(GetString("Error.InvalidColor") + $" {client.Id}/{client.PlayerName}", Color.yellow);
                        AmongUsClient.Instance.KickPlayer(client.Id, false);
                    }
                }
            }, 5f, "fortegreen bean color kick");
        }

        if (bodyColor == 255) return;

        Main.PlayerColors[__instance.PlayerId] = Palette.PlayerColors[bodyColor];
    }
}

// Next 2: from https://github.com/EnhancedNetwork/TownofHost-Enhanced/blob/main/Patches/LobbyPatch.cs

[HarmonyPatch(typeof(PlayerMaterial), nameof(PlayerMaterial.SetColors), typeof(int), typeof(Material))]
static class PlayerMaterialPatch
{
    public static void Prefix([HarmonyArgument(0)] ref int colorId)
    {
        if (colorId < 0 || colorId >= Palette.PlayerColors.Length)
            colorId = 0;
    }
}

[HarmonyPatch(typeof(NetworkedPlayerInfo), nameof(NetworkedPlayerInfo.Init))]
static class NetworkedPlayerInfoInitPatch
{
    public static void Postfix(NetworkedPlayerInfo __instance)
    {
        foreach (var outfit in __instance.Outfits)
        {
            if (outfit.Value != null)
                if (outfit.Value.ColorId < 0 || outfit.Value.ColorId >= Palette.PlayerColors.Length)
                    outfit.Value.ColorId = 0;
        }
    }
}