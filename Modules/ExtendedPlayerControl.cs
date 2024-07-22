using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using AmongUs.GameOptions;
using EHR.AddOns.Crewmate;
using EHR.AddOns.Impostor;
using EHR.Crewmate;
using EHR.Impostor;
using EHR.Modules;
using EHR.Neutral;
using Hazel;
using InnerNet;
using UnityEngine;
using static EHR.Translator;
using static EHR.Utils;

namespace EHR;

static class ExtendedPlayerControl
{
    public const MurderResultFlags ResultFlags = MurderResultFlags.Succeeded;

    public static void SetRole(this PlayerControl player, RoleTypes role, bool canOverride = false)
    {
        player.StartCoroutine(player.CoSetRole(role, canOverride));
    }

    public static void RpcSetCustomRole(this PlayerControl player, CustomRoles role, bool replaceAllAddons = false)
    {
        if (role < CustomRoles.NotAssigned)
        {
            Main.PlayerStates[player.PlayerId].SetMainRole(role);
        }
        else
        {
            if (!Cleanser.CleansedCanGetAddon.GetBool() && player.Is(CustomRoles.Cleansed)) return;
            Main.PlayerStates[player.PlayerId].SetSubRole(role, replaceAllAddons);
        }

        if (AmongUsClient.Instance.AmHost)
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetCustomRole, SendOption.Reliable);
            writer.Write(player.PlayerId);
            writer.WritePacked((int)role);
            writer.Write(replaceAllAddons);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
    }

    public static void RpcSetCustomRole(byte PlayerId, CustomRoles role)
    {
        if (AmongUsClient.Instance.AmHost)
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetCustomRole, SendOption.Reliable);
            writer.Write(PlayerId);
            writer.WritePacked((int)role);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
    }

    public static void SetChatVisible(this PlayerControl player) // Credit: NikoCat233 | Unused for now
    {
        if (!GameStates.IsInGame || !AmongUsClient.Instance.AmHost || GameStates.IsMeeting) return;

        if (player.AmOwner)
        {
            HudManager.Instance.Chat.SetVisible(true);
            return;
        }

        if (player.IsModClient())
        {
            var modsend = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.ShowChat, SendOption.Reliable, player.OwnerId);
            modsend.WritePacked(player.OwnerId);
            modsend.Write(true);
            AmongUsClient.Instance.FinishRpcImmediately(modsend);
            return;
        }

        var customNetId = AmongUsClient.Instance.NetIdCnt++;
        var vanillasend = MessageWriter.Get(SendOption.Reliable);
        vanillasend.StartMessage(6);
        vanillasend.Write(AmongUsClient.Instance.GameId);
        vanillasend.Write(player.OwnerId);

        vanillasend.StartMessage((byte)GameDataTag.SpawnFlag);
        vanillasend.WritePacked(1); // 1 Meeting Hud Spawn id
        vanillasend.WritePacked(-2); // Owned by host
        vanillasend.Write((byte)SpawnFlags.None);
        vanillasend.WritePacked(1);
        vanillasend.WritePacked(customNetId);

        vanillasend.StartMessage(1);
        vanillasend.WritePacked(0);
        vanillasend.EndMessage();

        vanillasend.EndMessage();
        vanillasend.EndMessage();

        vanillasend.StartMessage(6);
        vanillasend.Write(AmongUsClient.Instance.GameId);
        vanillasend.Write(player.OwnerId);
        vanillasend.StartMessage((byte)GameDataTag.RpcFlag);
        vanillasend.WritePacked(customNetId);
        vanillasend.Write((byte)RpcCalls.CloseMeeting);
        vanillasend.EndMessage();
        vanillasend.EndMessage();

        //vanillasend.StartMessage(6);
        //vanillasend.Write(AmongUsClient.Instance.GameId);
        //vanillasend.Write(player.OwnerId);
        //vanillasend.StartMessage((byte)GameDataTag.DespawnFlag);
        //vanillasend.WritePacked(customNetId);
        //vanillasend.EndMessage();
        //vanillasend.EndMessage();
        // Despawn here dont show chat button somehow

        AmongUsClient.Instance.SendOrDisconnect(vanillasend);
        vanillasend.Recycle();
    }

    public static void RpcExile(this PlayerControl player)
    {
        RPC.ExileAsync(player);
    }

    public static ClientData GetClient(this PlayerControl player)
    {
        try
        {
            return AmongUsClient.Instance.allClients.ToArray().FirstOrDefault(cd => cd.Character.PlayerId == player.PlayerId);
        }
        catch
        {
            return null;
        }
    }

    public static int GetClientId(this PlayerControl player)
    {
        if (player == null) return -1;
        var client = player.GetClient();
        return client?.Id ?? -1;
    }

    public static CustomRoles GetCustomRole(this NetworkedPlayerInfo player)
    {
        return player == null || player.Object == null ? CustomRoles.Crewmate : player.Object.GetCustomRole();
    }

    /// <summary>
    /// *Sub-roles cannot be obtained.
    /// </summary>
    public static CustomRoles GetCustomRole(this PlayerControl player)
    {
        if (player == null)
        {
            var callerMethod = new StackFrame(1, false).GetMethod();
            string callerMethodName = callerMethod?.Name;
            Logger.Warn(callerMethod?.DeclaringType?.FullName + "." + callerMethodName + " tried to get a CustomRole, but the target was null.", "GetCustomRole");
            return CustomRoles.Crewmate;
        }

        return Main.PlayerStates.TryGetValue(player.PlayerId, out var State) ? State.MainRole : CustomRoles.Crewmate;
    }

    public static List<CustomRoles> GetCustomSubRoles(this PlayerControl player)
    {
        if (GameStates.IsLobby) return [CustomRoles.NotAssigned];
        if (player == null)
        {
            Logger.Warn("The player is null", "GetCustomSubRoles");
            return [CustomRoles.NotAssigned];
        }

        return Main.PlayerStates.TryGetValue(player.PlayerId, out var state) ? state.SubRoles : [CustomRoles.NotAssigned];
    }

    public static CountTypes GetCountTypes(this PlayerControl player)
    {
        if (player == null)
        {
            var caller = new StackFrame(1, false);
            var callerMethod = caller.GetMethod();
            string callerMethodName = callerMethod?.Name;
            string callerClassName = callerMethod?.DeclaringType?.FullName;
            Logger.Warn(callerClassName + "." + callerMethodName + " tried to get a CountType, but the player was null", "GetCountTypes");
            return CountTypes.None;
        }

        return Main.PlayerStates.TryGetValue(player.PlayerId, out var State) ? State.countTypes : CountTypes.None;
    }

    public static void RpcSetNameEx(this PlayerControl player, string name)
    {
        foreach (PlayerControl seer in Main.AllPlayerControls)
        {
            Main.LastNotifyNames[(player.PlayerId, seer.PlayerId)] = name;
        }

        Logger.Info($"Set:{player?.Data?.PlayerName}:{name} for All", "RpcSetNameEx");
        player?.RpcSetName(name);
    }

    public static void RpcSetNamePrivate(this PlayerControl player, string name, PlayerControl seer = null, bool force = false)
    {
        if (player == null || name == null || !AmongUsClient.Instance.AmHost) return;
        if (seer == null) seer = player;
        if (!force && Main.LastNotifyNames[(player.PlayerId, seer.PlayerId)] == name) return;

        Main.LastNotifyNames[(player.PlayerId, seer.PlayerId)] = name;
        Logger.Info($"Set:{player.Data?.PlayerName}:{name} for {seer.GetNameWithRole().RemoveHtmlTags()}", "RpcSetNamePrivate");

        if (seer == null || player == null) return;

        var clientId = seer.GetClientId();

        var sender = CustomRpcSender.Create(name: "SetNamePrivate");
        sender.AutoStartRpc(player.NetId, (byte)RpcCalls.SetName, clientId)
            .Write(seer.Data.NetId)
            .Write(name)
            .EndRpc();
        sender.SendMessage();
    }

    public static void RpcSetRoleDesync(this PlayerControl player, RoleTypes role, bool canOverride, int clientId)
    {
        // player: Rename target

        if (player == null) return;
        if (AmongUsClient.Instance.ClientId == clientId)
        {
            player.SetRole(role, canOverride);
            return;
        }

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(player.NetId, (byte)RpcCalls.SetRole, SendOption.Reliable, clientId);
        writer.Write((ushort)role);
        writer.Write(canOverride);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void KillFlash(this PlayerControl player)
    {
        if (GameStates.IsLobby) return;

        //Kill flash (blackout + reactor flash) processing

        var systemtypes = (MapNames)Main.NormalOptions.MapId switch
        {
            MapNames.Polus => SystemTypes.Laboratory,
            MapNames.Airship => SystemTypes.HeliSabotage,
            _ => SystemTypes.Reactor
        };
        bool ReactorCheck = IsActive(systemtypes); //Checking whether the reactor sabotage is active

        var Duration = Options.KillFlashDuration.GetFloat();
        if (ReactorCheck) Duration += 0.2f; // Extend blackout during reactor

        // Execution
        Main.PlayerStates[player.PlayerId].IsBlackOut = true; // Blackout
        if (player.AmOwner)
        {
            FlashColor(new(1f, 0f, 0f, 0.3f));
            if (Constants.ShouldPlaySfx()) RPC.PlaySound(player.PlayerId, Sounds.KillSound);
        }
        else if (player.IsModClient())
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.KillFlash, SendOption.Reliable, player.GetClientId());
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        else if (!ReactorCheck) player.ReactorFlash(); // Reactor flash

        player.MarkDirtySettings();
        LateTask.New(() =>
        {
            Main.PlayerStates[player.PlayerId].IsBlackOut = false; // Cancel blackout
            player.MarkDirtySettings();
        }, Duration, "RemoveKillFlash");
    }

    public static void RpcGuardAndKill(this PlayerControl killer, PlayerControl target = null, int colorId = 0, bool forObserver = false, bool fromSetKCD = false)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            var caller = new StackFrame(1, false);
            var callerMethod = caller.GetMethod();
            string callerMethodName = callerMethod?.Name;
            string callerClassName = callerMethod?.DeclaringType?.FullName;
            Logger.Warn($"Modded non-host client activated RpcGuardAndKill from {callerClassName}.{callerMethodName}", "RpcGuardAndKill");
            return;
        }

        if (target == null) target = killer;

        // Check Observer
        if (!forObserver && !MeetingStates.FirstMeeting)
        {
            Main.AllPlayerControls.Where(x => x.Is(CustomRoles.Observer) && killer.PlayerId != x.PlayerId).Do(x => x.RpcGuardAndKill(target, colorId, true));
        }

        // Host
        if (killer.AmOwner)
        {
            killer.MurderPlayer(target, MurderResultFlags.FailedProtected);
        }

        // Other Clients
        if (!killer.IsHost())
        {
            var writer = AmongUsClient.Instance.StartRpcImmediately(killer.NetId, (byte)RpcCalls.MurderPlayer, SendOption.Reliable, killer.GetClientId());
            writer.WriteNetObject(target);
            writer.Write((int)MurderResultFlags.FailedProtected);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        if (!fromSetKCD) killer.AddKillTimerToDict(half: true);
    }

    public static void RpcSpecificVanish(this PlayerControl player, PlayerControl seer)
    {
        /*
         *  Unluckily the vanishing animation cannot be disabled
         *  For vanila client seer side, the player must be with Phantom Role behavior, or the rpc will do nothing
         */
        if (!AmongUsClient.Instance.AmHost) return;

        MessageWriter msg = AmongUsClient.Instance.StartRpcImmediately(player.NetId, (byte)RpcCalls.StartVanish, SendOption.None, seer.GetClientId());
        AmongUsClient.Instance.FinishRpcImmediately(msg);
    }

    public static void RpcSpecificAppear(this PlayerControl player, PlayerControl seer, bool shouldAnimate)
    {
        /*
         *  For vanila client seer side, the player must be with Phantom Role behavior, or the rpc will do nothing
         */
        if (!AmongUsClient.Instance.AmHost) return;

        MessageWriter msg = AmongUsClient.Instance.StartRpcImmediately(player.NetId, (byte)RpcCalls.StartAppear, SendOption.None, seer.GetClientId());
        msg.Write(shouldAnimate);
        AmongUsClient.Instance.FinishRpcImmediately(msg);
    }

    public static void AddKCDAsAbilityCD(this PlayerControl pc) => AddAbilityCD(pc, (int)Math.Round(Main.AllPlayerKillCooldown.TryGetValue(pc.PlayerId, out var KCD) ? KCD : Options.DefaultKillCooldown));
    public static void AddAbilityCD(this PlayerControl pc, bool includeDuration = true) => Utils.AddAbilityCD(pc.GetCustomRole(), pc.PlayerId, includeDuration);
    public static void AddAbilityCD(this PlayerControl pc, int CD) => Main.AbilityCD[pc.PlayerId] = (TimeStamp, CD);
    public static bool HasAbilityCD(this PlayerControl pc) => Main.AbilityCD.ContainsKey(pc.PlayerId);

    public static float GetAbilityUseLimit(this PlayerControl pc) => Main.AbilityUseLimit.GetValueOrDefault(pc.PlayerId, float.NaN);
    public static float GetAbilityUseLimit(this byte playerId) => Main.AbilityUseLimit.GetValueOrDefault(playerId, float.NaN);

    public static void RpcRemoveAbilityUse(this PlayerControl pc, bool log = true)
    {
        float current = pc.GetAbilityUseLimit();
        if (float.IsNaN(current) || current <= 0f) return;
        pc.SetAbilityUseLimit(current - 1, log: log);
    }

    public static void RpcIncreaseAbilityUseLimitBy(this PlayerControl pc, float get, bool log = true)
    {
        float current = pc.GetAbilityUseLimit();
        if (float.IsNaN(current)) return;
        pc.SetAbilityUseLimit(current + get, log: log);
    }

    public static void SetAbilityUseLimit(this PlayerControl pc, float limit, bool rpc = true, bool log = true) => pc.PlayerId.SetAbilityUseLimit(limit, rpc, log);

    public static void SetAbilityUseLimit(this byte playerId, float limit, bool rpc = true, bool log = true)
    {
        if (float.IsNaN(limit) || limit is < 0f or > 100f || (Main.AbilityUseLimit.TryGetValue(playerId, out var beforeLimit) && Math.Abs(beforeLimit - limit) < 0.01f)) return;

        Main.AbilityUseLimit[playerId] = limit;

        if (AmongUsClient.Instance.AmHost && playerId.IsPlayerModClient() && !playerId.IsHost() && rpc)
        {
            var writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncAbilityUseLimit, SendOption.Reliable);
            writer.Write(playerId);
            writer.Write(limit);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        var pc = GetPlayerById(playerId);
        NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        if (log) Logger.Info($" {pc.GetNameWithRole()} => {Math.Round(limit, 1)}", "SetAbilityUseLimit");
    }

    public static void Suicide(this PlayerControl pc, PlayerState.DeathReason deathReason = PlayerState.DeathReason.Suicide, PlayerControl realKiller = null)
    {
        Main.PlayerStates[pc.PlayerId].deathReason = deathReason;
        Main.PlayerStates[pc.PlayerId].SetDead();

        Medic.IsDead(pc);
        if (realKiller != null)
        {
            pc.SetRealKiller(realKiller);
            if (realKiller.Is(CustomRoles.Damocles)) Damocles.OnMurder(realKiller.PlayerId);
            IncreaseAbilityUseLimitOnKill(realKiller);
        }

        pc.Kill(pc);
    }

    public static void SetKillCooldown(this PlayerControl player, float time = -1f, PlayerControl target = null, bool forceAnime = false)
    {
        if (player == null) return;
        Logger.Info($"{player.GetNameWithRole()}'s KCD set to {(Math.Abs(time - (-1f)) < 0.5f ? Main.AllPlayerKillCooldown[player.PlayerId] : time)}s", "SetKCD");
        if (player.GetCustomRole().UsesPetInsteadOfKill())
        {
            if (Math.Abs(time - (-1f)) < 0.5f) player.AddKCDAsAbilityCD();
            else player.AddAbilityCD((int)Math.Round(time));
            if (player.GetCustomRole() is not CustomRoles.Necromancer and not CustomRoles.Deathknight) return;
        }

        if (!player.CanUseKillButton()) return;
        player.AddKillTimerToDict(CD: time);
        if (target == null) target = player;
        if (time >= 0f) Main.AllPlayerKillCooldown[player.PlayerId] = time * 2;
        else Main.AllPlayerKillCooldown[player.PlayerId] *= 2;
        if (player.Is(CustomRoles.Glitch) && Main.PlayerStates[player.PlayerId].Role is Glitch gc)
        {
            gc.LastKill = TimeStamp + ((int)(time / 2) - Glitch.KillCooldown.GetInt());
            gc.KCDTimer = (int)(time / 2);
        }
        else if (forceAnime || !player.IsModClient() || !Options.DisableShieldAnimations.GetBool())
        {
            player.SyncSettings();
            player.RpcGuardAndKill(target, 11, fromSetKCD: true);
        }
        else
        {
            time = Main.AllPlayerKillCooldown[player.PlayerId] / 2;
            if (player.AmOwner) PlayerControl.LocalPlayer.SetKillTimer(time);
            else
            {
                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetKillTimer, SendOption.Reliable, player.GetClientId());
                writer.Write(time);
                AmongUsClient.Instance.FinishRpcImmediately(writer);
            }

            Main.AllPlayerControls.Where(x => x.Is(CustomRoles.Observer) && target.PlayerId != x.PlayerId).Do(x => x.RpcGuardAndKill(target, 11, true, fromSetKCD: true));
        }

        if (player.GetCustomRole() is not CustomRoles.Inhibitor and not CustomRoles.Saboteur) player.ResetKillCooldown();
    }

    public static void RpcSpecificMurderPlayer(this PlayerControl killer, PlayerControl target = null)
    {
        if (target == null) target = killer;
        if (killer.AmOwner)
        {
            killer.MurderPlayer(target, ResultFlags);
        }
        else
        {
            MessageWriter messageWriter = AmongUsClient.Instance.StartRpcImmediately(killer.NetId, (byte)RpcCalls.MurderPlayer, SendOption.Reliable, killer.GetClientId());
            messageWriter.WriteNetObject(target);
            messageWriter.Write((byte)ResultFlags);
            AmongUsClient.Instance.FinishRpcImmediately(messageWriter);
        }
    }

    public static void RpcResetAbilityCooldown(this PlayerControl target)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        Logger.Info($"Reset Ability Cooldown for {target.name} (ID: {target.PlayerId})", "RpcResetAbilityCooldown");
        if (target.Is(CustomRoles.Glitch) && Main.PlayerStates[target.PlayerId].Role is Glitch gc)
        {
            gc.LastHack = TimeStamp;
            gc.LastMimic = TimeStamp;
            gc.MimicCDTimer = 10;
            gc.HackCDTimer = 10;
        }
        else if (PlayerControl.LocalPlayer == target)
        {
            // If target is host
            PlayerControl.LocalPlayer.Data.Role.SetCooldown();
        }
        else
        {
            // If target is not host
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(target.NetId, (byte)RpcCalls.ProtectPlayer, SendOption.None, target.GetClientId());
            writer.WriteNetObject(target);
            writer.Write(0);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        /*
            When a player guards someone, ability cooldowns are reset regardless of that player's role.
            Due to the addition of logs, it is no longer possible to guard no one, so it has been changed to the player guarding themselves for 0 seconds instead.
            This change disables Guardian Angel as a position.
            Reset host cooldown directly.
        */
    }

    public static void RpcDesyncRepairSystem(this PlayerControl target, SystemTypes systemType, int amount)
    {
        var messageWriter = AmongUsClient.Instance.StartRpcImmediately(ShipStatus.Instance.NetId, (byte)RpcCalls.UpdateSystem, SendOption.Reliable, target.GetClientId());
        messageWriter.Write((byte)systemType);
        messageWriter.WriteNetObject(target);
        messageWriter.Write((byte)amount);
        AmongUsClient.Instance.FinishRpcImmediately(messageWriter);
    }

    public static void MarkDirtySettings(this PlayerControl player)
    {
        PlayerGameOptionsSender.SetDirty(player.PlayerId);
    }

    public static void SyncSettings(this PlayerControl player)
    {
        PlayerGameOptionsSender.SetDirty(player.PlayerId);
        GameOptionsSender.SendAllGameOptions();
    }

    public static TaskState GetTaskState(this PlayerControl player) => Main.PlayerStates.TryGetValue(player.PlayerId, out var state) ? state.TaskState : new();

    public static bool IsNonHostModClient(this PlayerControl pc) => pc.IsModClient() && !pc.IsHost();

    public static string GetDisplayRoleName(this PlayerControl player, bool pure = false)
    {
        return Utils.GetDisplayRoleName(player.PlayerId, pure);
    }

    public static string GetSubRoleNames(this PlayerControl player, bool forUser = false)
    {
        var SubRoles = Main.PlayerStates[player.PlayerId].SubRoles;
        if (SubRoles.Count == 0) return string.Empty;
        var sb = new StringBuilder();
        foreach (CustomRoles role in SubRoles)
        {
            if (role == CustomRoles.NotAssigned) continue;
            sb.Append($"{ColorString(Color.white, "\n<size=1>")}{GetRoleName(role, forUser)}");
        }

        return sb.ToString();
    }

    public static string GetAllRoleName(this PlayerControl player, bool forUser = true)
    {
        if (!player) return null;
        var text = GetRoleName(player.GetCustomRole(), forUser);
        text += player.GetSubRoleNames(forUser);
        return text;
    }

    public static string GetNameWithRole(this PlayerControl player, bool forUser = false)
    {
        return $"{player?.Data?.PlayerName}" + (GameStates.IsInGame && Options.CurrentGameMode is not CustomGameMode.FFA and not CustomGameMode.MoveAndStop and not CustomGameMode.HotPotato and not CustomGameMode.Speedrun ? $" ({player?.GetAllRoleName(forUser).RemoveHtmlTags().Replace('\n', ' ')})" : string.Empty);
    }

    public static string GetRoleColorCode(this PlayerControl player)
    {
        return Utils.GetRoleColorCode(player.GetCustomRole());
    }

    public static Color GetRoleColor(this PlayerControl player)
    {
        return Utils.GetRoleColor(player.GetCustomRole());
    }

    public static void ResetPlayerCam(this PlayerControl pc, float delay = 0f)
    {
        if (pc == null || !AmongUsClient.Instance.AmHost || pc.AmOwner) return;

        var systemtypes = (MapNames)Main.NormalOptions.MapId switch
        {
            MapNames.Polus => SystemTypes.Laboratory,
            MapNames.Airship => SystemTypes.HeliSabotage,
            _ => SystemTypes.Reactor
        };

        LateTask.New(() => { pc.RpcDesyncRepairSystem(systemtypes, 128); }, 0f + delay, "Reactor Desync");

        LateTask.New(() => { pc.RpcSpecificMurderPlayer(); }, 0.2f + delay, "Murder To Reset Cam");

        LateTask.New(() =>
        {
            pc.RpcDesyncRepairSystem(systemtypes, 16);
            if (Main.NormalOptions.MapId == 4) // Airship only
                pc.RpcDesyncRepairSystem(systemtypes, 17);
        }, 0.4f + delay, "Fix Desync Reactor");
    }

    public static void ReactorFlash(this PlayerControl pc, float delay = 0f)
    {
        if (pc == null) return;

        Logger.Info($"Reactor Flash for {pc.GetNameWithRole()}", "ReactorFlash");

        var systemtypes = (MapNames)Main.NormalOptions.MapId switch
        {
            MapNames.Polus => SystemTypes.Laboratory,
            MapNames.Airship => SystemTypes.HeliSabotage,
            _ => SystemTypes.Reactor
        };

        float FlashDuration = Options.KillFlashDuration.GetFloat();

        pc.RpcDesyncRepairSystem(systemtypes, 128);

        LateTask.New(() =>
        {
            pc.RpcDesyncRepairSystem(systemtypes, 16);

            if (Main.NormalOptions.MapId == 4) // on Airship
                pc.RpcDesyncRepairSystem(systemtypes, 17);
        }, FlashDuration + delay, "Fix Desync Reactor");
    }

    public static string GetRealName(this PlayerControl player, bool isMeeting = false)
    {
        try
        {
            return isMeeting ? player.Data.PlayerName : player.name;
        }
        catch (NullReferenceException nullReferenceException)
        {
            Logger.Error($"{nullReferenceException.Message} - player is null? {player == null}", "GetRealName");
            return string.Empty;
        }
        catch (Exception exception)
        {
            ThrowException(exception);
            return string.Empty;
        }
    }

    public static bool IsRoleBlocked(this PlayerControl pc) => RoleBlockManager.RoleBlockedPlayers.ContainsKey(pc.PlayerId);
    public static bool IsPlayerRoleBlocked(this byte id) => RoleBlockManager.RoleBlockedPlayers.ContainsKey(id);
    public static void BlockRole(this PlayerControl pc, float duration) => RoleBlockManager.AddRoleBlock(pc, duration);

    public static bool IsHost(this InnerNetObject ino) => ino.OwnerId == AmongUsClient.Instance.HostId;
    public static bool IsHost(this byte id) => GetPlayerById(id)?.OwnerId == AmongUsClient.Instance.HostId;

    public static void RpcShowScanAnimation(this PlayerControl target, PlayerControl seer, bool IsActive)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        byte cnt = ++PlayerControl.LocalPlayer.scannerCount;

        MessageWriter messageWriter = AmongUsClient.Instance.StartRpcImmediately(target.NetId, (byte)RpcCalls.SetScanner, SendOption.Reliable, seer.GetClientId());
        messageWriter.Write(IsActive);
        messageWriter.Write(cnt);
        AmongUsClient.Instance.FinishRpcImmediately(messageWriter);
    }

    public static bool HasKillButton(this PlayerControl pc)
    {
        CustomRoles role = pc.GetCustomRole();
        if (!pc.IsAlive() || pc.Data.Role.Role == RoleTypes.GuardianAngel || Pelican.IsEaten(pc.PlayerId)) return false;
        if (role.GetDYRole() == RoleTypes.Impostor || role.GetVNRole() is CustomRoles.Impostor or CustomRoles.Shapeshifter) return true;
        return pc.Is(CustomRoleTypes.Impostor) || pc.IsNeutralKiller() || role.IsTasklessCrewmate();
    }

    public static bool CanUseKillButton(this PlayerControl pc)
    {
        if (!pc.IsAlive()) return false;
        if (Mastermind.ManipulatedPlayers.ContainsKey(pc.PlayerId)) return true;
        if (Penguin.IsVictim(pc)) return false;
        if (Options.CurrentGameMode is CustomGameMode.HotPotato or CustomGameMode.MoveAndStop) return false;
        if (Options.CurrentGameMode == CustomGameMode.Speedrun && !SpeedrunManager.CanKill.Contains(pc.PlayerId)) return false;
        if (Pelican.IsEaten(pc.PlayerId)) return false;
        if (pc.Data.Role.Role == RoleTypes.GuardianAngel) return false;

        return pc.GetCustomRole() switch
        {
            // SoloKombat
            CustomRoles.KB_Normal => pc.SoloAlive(),
            // FFA
            CustomRoles.Killer => pc.IsAlive(),
            // Move And Stop
            CustomRoles.Tasker => false,
            // Hot Potato
            CustomRoles.Potato => false,
            // Speedrun
            CustomRoles.Runner => SpeedrunManager.CanKill.Contains(pc.PlayerId),
            // Hide And Seek
            CustomRoles.Seeker => true,
            CustomRoles.Hider => false,
            CustomRoles.Troll => false,
            CustomRoles.Fox => false,
            CustomRoles.Jumper => false,
            CustomRoles.Detector => false,
            CustomRoles.Jet => false,
            CustomRoles.Dasher => true,
            CustomRoles.Locator => true,
            CustomRoles.Venter => true,
            CustomRoles.Agent => true,
            CustomRoles.Taskinator => false,

            _ => Main.PlayerStates.TryGetValue(pc.PlayerId, out var state) && state.Role.CanUseKillButton(pc)
        };
    }

    public static bool CanUseImpostorVentButton(this PlayerControl pc)
    {
        if (!pc.IsAlive() || pc.Data.Role.Role == RoleTypes.GuardianAngel || Penguin.IsVictim(pc)) return false;
        if (pc.GetRoleTypes() == RoleTypes.Engineer) return false;
        if (CopyCat.Instances.Any(x => x.CopyCatPC.PlayerId == pc.PlayerId)) return true;

        if (pc.Is(CustomRoles.Nimble) || Options.EveryoneCanVent.GetBool()) return true;
        if (pc.Is(CustomRoles.Bloodlust) || pc.Is(CustomRoles.Refugee)) return true;

        return pc.GetCustomRole() switch
        {
            // SoloKombat
            CustomRoles.KB_Normal => true,
            // FFA
            CustomRoles.Killer => true,
            // Move And Stop
            CustomRoles.Tasker => false,
            // Hot Potato
            CustomRoles.Potato => false,
            // Speedrun
            CustomRoles.Runner => false,

            _ => Main.PlayerStates.TryGetValue(pc.PlayerId, out var state) && state.Role.CanUseImpostorVentButton(pc)
        };
    }

    public static bool CanUseSabotage(this PlayerControl pc)
    {
        if (!pc.IsAlive() || pc.Data.Role.Role == RoleTypes.GuardianAngel) return false;

        return Main.PlayerStates.TryGetValue(pc.PlayerId, out var state) && state.Role.CanUseSabotage(pc);
    }

    public static Vector2 Pos(this PlayerControl pc) => new(pc.transform.position.x, pc.transform.position.y);

    public static void AddKillTimerToDict(this PlayerControl pc, bool half = false, float CD = -1f)
    {
        float resultKCD;
        if (Math.Abs(CD - (-1f)) < 0.5f)
        {
            resultKCD = Main.AllPlayerKillCooldown.GetValueOrDefault(pc.PlayerId, 0f);

            if (half)
            {
                resultKCD /= 2f;
            }
        }
        else
        {
            resultKCD = CD;
        }

        if (pc.GetCustomRole().UsesPetInsteadOfKill() && resultKCD > 0f)
        {
            pc.AddAbilityCD((int)Math.Round(resultKCD));
        }

        if (Main.KillTimers.TryGetValue(pc.PlayerId, out var timer) && timer > resultKCD) return;
        Main.KillTimers[pc.PlayerId] = resultKCD;
    }

    public static bool IsDousedPlayer(this PlayerControl arsonist, PlayerControl target)
    {
        if (arsonist == null || target == null || Arsonist.IsDoused == null) return false;
        Arsonist.IsDoused.TryGetValue((arsonist.PlayerId, target.PlayerId), out bool isDoused);
        return isDoused;
    }

    public static bool IsDrawPlayer(this PlayerControl arsonist, PlayerControl target)
    {
        if (arsonist == null || target == null || Revolutionist.IsDraw == null) return false;
        Revolutionist.IsDraw.TryGetValue((arsonist.PlayerId, target.PlayerId), out bool isDraw);
        return isDraw;
    }

    public static bool IsRevealedPlayer(this PlayerControl player, PlayerControl target)
    {
        if (player == null || target == null || Farseer.IsRevealed == null) return false;
        Farseer.IsRevealed.TryGetValue((player.PlayerId, target.PlayerId), out bool isDoused);
        return isDoused;
    }

    public static void RpcSetDousedPlayer(this PlayerControl player, PlayerControl target, bool isDoused)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetDousedPlayer, SendOption.Reliable); //RPCによる同期
        writer.Write(player.PlayerId);
        writer.Write(target.PlayerId);
        writer.Write(isDoused);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void RpcSetDrawPlayer(this PlayerControl player, PlayerControl target, bool isDoused)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetDrawPlayer, SendOption.Reliable); //RPCによる同期
        writer.Write(player.PlayerId);
        writer.Write(target.PlayerId);
        writer.Write(isDoused);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void RpcSetRevealtPlayer(this PlayerControl player, PlayerControl target, bool isDoused)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetRevealedPlayer, SendOption.Reliable); //RPCによる同期
        writer.Write(player.PlayerId);
        writer.Write(target.PlayerId);
        writer.Write(isDoused);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static bool IsShifted(this PlayerControl pc) => Main.CheckShapeshift.TryGetValue(pc.PlayerId, out var shifted) && shifted;
    public static bool IsPlayerShifted(this byte id) => Main.CheckShapeshift.TryGetValue(id, out var shifted) && shifted;
    public static bool HasSubRole(this PlayerControl pc) => Main.PlayerStates[pc.PlayerId].SubRoles.Count > 0;
    public static bool HasEvilAddon(this PlayerControl pc) => Main.PlayerStates[pc.PlayerId].SubRoles.Any(x => x.IsEvilAddon());

    public static void ResetKillCooldown(this PlayerControl player)
    {
        Main.PlayerStates[player.PlayerId].Role.SetKillCooldown(player.PlayerId);

        Main.AllPlayerKillCooldown[player.PlayerId] = player.GetCustomRole() switch
        {
            CustomRoles.KB_Normal => SoloKombatManager.KB_ATKCooldown.GetFloat(),
            CustomRoles.Killer => FFAManager.FFAKcd.GetFloat(),
            _ => Main.AllPlayerKillCooldown[player.PlayerId]
        };
        if (player.PlayerId == LastImpostor.currentId)
            LastImpostor.SetKillCooldown();
        if (player.Is(CustomRoles.Mare))
            if (IsActive(SystemTypes.Electrical)) Main.AllPlayerKillCooldown[player.PlayerId] = Options.MareKillCD.GetFloat();
            else Main.AllPlayerKillCooldown[player.PlayerId] = Options.MareKillCDNormally.GetFloat();
        if (player.Is(CustomRoles.Bloodlust))
            Main.AllPlayerKillCooldown[player.PlayerId] = Bloodlust.KCD.GetFloat();

        if (Main.KilledDiseased.TryGetValue(player.PlayerId, out int value))
        {
            Main.AllPlayerKillCooldown[player.PlayerId] += value * Options.DiseasedCDOpt.GetFloat();
            Logger.Info($"KCD of player set to {Main.AllPlayerKillCooldown[player.PlayerId]}", "Diseased");
        }

        if (Main.KilledAntidote.TryGetValue(player.PlayerId, out int value1))
        {
            var kcd = Main.AllPlayerKillCooldown[player.PlayerId] - value1 * Options.AntidoteCDOpt.GetFloat();
            if (kcd < 0) kcd = 0;
            Main.AllPlayerKillCooldown[player.PlayerId] = kcd;
            Logger.Info($"KCD of player set to {Main.AllPlayerKillCooldown[player.PlayerId]}", "Antidote");
        }
    }

    public static void TrapperKilled(this PlayerControl killer, PlayerControl target)
    {
        Logger.Info($"{target?.Data?.PlayerName} was Trapper", "Trapper");
        var tmpSpeed = Main.AllPlayerSpeed[killer.PlayerId];
        Main.AllPlayerSpeed[killer.PlayerId] = Main.MinSpeed;
        ReportDeadBodyPatch.CanReport[killer.PlayerId] = false;
        killer.MarkDirtySettings();
        LateTask.New(() =>
        {
            Main.AllPlayerSpeed[killer.PlayerId] = tmpSpeed;
            ReportDeadBodyPatch.CanReport[killer.PlayerId] = true;
            killer.MarkDirtySettings();
            RPC.PlaySoundRPC(killer.PlayerId, Sounds.TaskComplete);
        }, Options.TrapperBlockMoveTime.GetFloat(), "Trapper BlockMove");
    }

    public static bool IsDouseDone(this PlayerControl player)
    {
        if (!player.Is(CustomRoles.Arsonist)) return false;
        var count = GetDousedPlayerCount(player.PlayerId);
        return count.Item1 >= count.Item2;
    }

    public static bool IsDrawDone(this PlayerControl player) // Determine whether the conditions to win are met
    {
        if (!player.Is(CustomRoles.Revolutionist)) return false;
        var count = GetDrawPlayerCount(player.PlayerId, out _);
        return count.Item1 >= count.Item2;
    }

    public static void RpcExileV2(this PlayerControl player)
    {
        player.Exiled();
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(player.NetId, (byte)RpcCalls.Exiled, SendOption.None);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static (Vector2 Location, string RoomName) GetPositionInfo(this PlayerControl pc)
    {
        PlainShipRoom room = pc.GetPlainShipRoom();
        string roomName = room == null ? "Outside" : $"@{GetString($"{room.RoomId}")}";
        Vector2 pos = pc.Pos();

        return (pos, roomName);
    }

    public static bool TP(this PlayerControl pc, PlayerControl target, bool log = true)
    {
        return Utils.TP(pc.NetTransform, target.Pos(), log);
    }

    public static bool TP(this PlayerControl pc, Vector2 location, bool log = true)
    {
        return Utils.TP(pc.NetTransform, location, log);
    }

    // ReSharper disable once InconsistentNaming
    public static bool TPtoRndVent(this PlayerControl pc, bool log = true)
    {
        return Utils.TPtoRndVent(pc.NetTransform, log);
    }

    public static void Kill(this PlayerControl killer, PlayerControl target)
    {
        if (Options.CurrentGameMode == CustomGameMode.SoloKombat) return;
        if (target == null) target = killer;

        CheckAndSpawnAdditionalRefugee(target.Data);

        if (target.GetTeam() is Team.Impostor or Team.Neutral) Stressed.OnNonCrewmateDead();

        if (killer.Is(CustomRoles.Damocles)) Damocles.OnMurder(killer.PlayerId);
        else if (killer.Is(Team.Impostor)) Damocles.OnOtherImpostorMurder();
        else if (target.Is(Team.Impostor)) Damocles.OnImpostorDeath();

        if (killer.Is(CustomRoles.Bloodlust)) FixedUpdatePatch.AddExtraAbilityUsesOnFinishedTasks(killer);
        else IncreaseAbilityUseLimitOnKill(killer);

        target.SetRealKiller(killer, NotOverRide: true);

        switch (killer.PlayerId == target.PlayerId)
        {
            case true when killer.shapeshifting:
                LateTask.New(() => { killer.RpcMurderPlayer(target, true); }, 1.5f, "Shapeshifting Suicide Delay");
                return;
            case false when !killer.Is(CustomRoles.Pestilence) && Main.PlayerStates[target.PlayerId].Role is SchrodingersCat cat:
                cat.OnCheckMurderAsTarget(killer, target);
                return;
            default:
                killer.RpcMurderPlayer(target, true);
                break;
        }
    }

    public static bool RpcCheckAndMurder(this PlayerControl killer, PlayerControl target, bool check = false) => CheckMurderPatch.RpcCheckAndMurder(killer, target, check);

    public static void NoCheckStartMeeting(this PlayerControl reporter, NetworkedPlayerInfo target, bool force = false)
    {
        if (Options.DisableMeeting.GetBool() && !force) return;
        ReportDeadBodyPatch.AfterReportTasks(reporter, target);
        MeetingRoomManager.Instance.AssignSelf(reporter, target);
        DestroyableSingleton<HudManager>.Instance.OpenMeetingRoom(reporter);
        reporter.RpcStartMeeting(target);
    }

    public static bool IsModClient(this PlayerControl player) => Main.PlayerVersion.ContainsKey(player.PlayerId);

    public static List<PlayerControl> GetPlayersInAbilityRangeSorted(this PlayerControl player, bool ignoreColliders = false) => GetPlayersInAbilityRangeSorted(player, _ => true, ignoreColliders);

    public static List<PlayerControl> GetPlayersInAbilityRangeSorted(this PlayerControl player, Predicate<PlayerControl> predicate, bool ignoreColliders = false)
    {
        var rangePlayersIL = RoleBehaviour.GetTempPlayerList();
        List<PlayerControl> rangePlayers = [];
        player.Data.Role.GetPlayersInAbilityRangeSorted(rangePlayersIL, ignoreColliders);
        foreach (var pc in rangePlayersIL)
        {
            if (predicate(pc)) rangePlayers.Add(pc);
        }

        return rangePlayers;
    }

    public static bool IsNeutralKiller(this PlayerControl player) => player.Is(CustomRoles.Bloodlust) || player.GetCustomRole().IsNK();
    public static bool IsNeutralBenign(this PlayerControl player) => player.GetCustomRole().IsNB();
    public static bool IsNeutralEvil(this PlayerControl player) => player.GetCustomRole().IsNE();
    public static bool IsNeutralChaos(this PlayerControl player) => player.GetCustomRole().IsNC();
    public static bool IsSnitchTarget(this PlayerControl player) => player.Is(CustomRoles.Bloodlust) || player.GetCustomRole().IsSnitchTarget();
    public static bool IsMadmate(this PlayerControl player) => player.Is(CustomRoles.Madmate) || player.GetCustomRole().IsMadmate();

    public static bool HasGhostRole(this PlayerControl player) => GhostRolesManager.AssignedGhostRoles.ContainsKey(player.PlayerId) || Main.PlayerStates.TryGetValue(player.PlayerId, out var state) && state.SubRoles.Any(x => x.IsGhostRole());

    public static bool KnowDeathReason(this PlayerControl seer, PlayerControl target)
        => ((seer.Is(CustomRoles.Doctor) || seer.Is(CustomRoles.Autopsy)
                                         || (seer.Data.IsDead && Options.GhostCanSeeDeathReason.GetBool()))
            && target.Data.IsDead) || (target.Is(CustomRoles.Gravestone) && target.Data.IsDead);

    public static string GetRoleInfo(this PlayerControl player, bool InfoLong = false)
    {
        var role = player.GetCustomRole();
        if (role is CustomRoles.Crewmate or CustomRoles.Impostor)
            InfoLong = false;

        var text = role.ToString();

        var Prefix = string.Empty;
        if (!InfoLong)
        {
            Prefix = role switch
            {
                CustomRoles.Mafia => CanMafiaKill() ? "After" : "Before",
                _ => Prefix
            };
        }

        var Info = (role.IsVanilla() ? "Blurb" : "Info") + (InfoLong ? "Long" : string.Empty);
        return GetString($"{Prefix}{text}{Info}");
    }

    public static void SetRealKiller(this PlayerControl target, PlayerControl killer, bool NotOverRide = false)
    {
        if (target == null)
        {
            Logger.Info("target is null", "SetRealKiller");
            return;
        }

        var State = Main.PlayerStates[target.PlayerId];
        if (State.RealKiller.TIMESTAMP != DateTime.MinValue && NotOverRide) return; // Do not overwrite if value already exists
        byte killerId = killer == null ? byte.MaxValue : killer.PlayerId;
        RPC.SetRealKiller(target.PlayerId, killerId);
    }

    public static PlayerControl GetRealKiller(this PlayerControl target)
    {
        var killerId = Main.PlayerStates[target.PlayerId].GetRealKiller();
        return killerId == byte.MaxValue ? null : GetPlayerById(killerId);
    }

    public static PlainShipRoom GetPlainShipRoom(this PlayerControl pc)
    {
        if (!pc.IsAlive() || Pelican.IsEaten(pc.PlayerId)) return null;
        var Rooms = ShipStatus.Instance.AllRooms;
        return Rooms.Where(room => room.roomArea).FirstOrDefault(room => pc.Collider.IsTouching(room.roomArea));
    }

    public static bool IsImpostor(this PlayerControl pc) => !pc.Is(CustomRoles.Bloodlust) && pc.GetCustomRole().IsImpostor();
    public static bool IsCrewmate(this PlayerControl pc) => !pc.Is(CustomRoles.Bloodlust) && pc.GetCustomRole().IsCrewmate();

    public static CustomRoleTypes GetCustomRoleTypes(this PlayerControl pc) => pc.Is(CustomRoles.Bloodlust) ? CustomRoleTypes.Neutral : pc.GetCustomRole().GetCustomRoleTypes();

    public static RoleTypes GetRoleTypes(this PlayerControl pc) => pc.GetCustomSubRoles() switch
    {
        { } x when x.Contains(CustomRoles.Bloodlust) => RoleTypes.Impostor,
        { } x when x.Contains(CustomRoles.Nimble) => RoleTypes.Engineer,
        { } x when x.Contains(CustomRoles.Physicist) => RoleTypes.Scientist,
        { } x when x.Contains(CustomRoles.Finder) => RoleTypes.Tracker,
        { } x when x.Contains(CustomRoles.Noisy) => RoleTypes.Noisemaker,
        _ => pc.GetCustomRole().GetRoleTypes()
    };

    public static bool Is(this PlayerControl target, CustomRoles role) =>
        role > CustomRoles.NotAssigned ? target.GetCustomSubRoles().Contains(role) : target.GetCustomRole() == role;

    public static bool Is(this PlayerControl target, CustomRoleTypes type) => target.GetCustomRoleTypes() == type;
    public static bool Is(this PlayerControl target, RoleTypes type) => (target.Is(CustomRoles.Bloodlust) && type == RoleTypes.Impostor) || target.GetCustomRole().GetRoleTypes() == type;
    public static bool Is(this PlayerControl target, CountTypes type) => target.GetCountTypes() == type;

    public static bool Is(this PlayerControl target, Team team) => team switch
    {
        Team.Impostor => target.GetCustomRole().IsImpostorTeamV2() && !target.Is(CustomRoles.Bloodlust),
        Team.Neutral => target.GetCustomRole().IsNeutralTeamV2() || target.Is(CustomRoles.Bloodlust),
        Team.Crewmate => target.GetCustomRole().IsCrewmateTeamV2(),
        Team.None => target.Is(CustomRoles.GM) || target.Is(CountTypes.None) || target.Is(CountTypes.OutOfGame),
        _ => false
    };

    public static Team GetTeam(this PlayerControl target)
    {
        var subRoles = target.GetCustomSubRoles();
        if (target.Is(CustomRoles.Bloodlust) || subRoles.Any(x => x.IsConverted())) return Team.Neutral;
        if (subRoles.Contains(CustomRoles.Madmate)) return Team.Impostor;
        var role = target.GetCustomRole();
        if (role.IsImpostorTeamV2()) return Team.Impostor;
        if (role.IsNeutralTeamV2()) return Team.Neutral;
        return role.IsCrewmateTeamV2() ? Team.Crewmate : Team.None;
    }

    public static bool IsAlive(this PlayerControl target)
    {
        // If target is not null and cannot be obtained, it is assumed to be alive because it has not been registered
        if (target == null || target.Is(CustomRoles.GM)) return false;
        return GameStates.IsLobby || !Main.PlayerStates.TryGetValue(target.PlayerId, out var ps) || !ps.IsDead;
    }

    ///<summary>Is the player currently protected</summary>
    public static bool IsProtected(this PlayerControl self) => self.protectedByGuardianId > -1;
}