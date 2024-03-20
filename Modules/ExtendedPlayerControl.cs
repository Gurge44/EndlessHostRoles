using AmongUs.GameOptions;
using HarmonyLib;
using Hazel;
using InnerNet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using TOHE.Modules;
using TOHE.Patches;
using TOHE.Roles.AddOns.Crewmate;
using TOHE.Roles.AddOns.Impostor;
using TOHE.Roles.Crewmate;
using TOHE.Roles.Impostor;
using TOHE.Roles.Neutral;
using UnityEngine;
using static TOHE.Translator;
using static TOHE.Utils;

namespace TOHE;

static class ExtendedPlayerControl
{
    public static void RpcSetCustomRole(this PlayerControl player, CustomRoles role, bool isRoleForced = false)
    {
        if (role < CustomRoles.NotAssigned || isRoleForced)
        {
            Main.PlayerStates[player.PlayerId].SetMainRole(role);
        }
        else
        {
            if (!Cleanser.CleansedCanGetAddon.GetBool() && player.Is(CustomRoles.Cleansed)) return;
            Main.PlayerStates[player.PlayerId].SetSubRole(role);
        }

        if (AmongUsClient.Instance.AmHost)
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetCustomRole, SendOption.Reliable);
            writer.Write(player.PlayerId);
            writer.WritePacked((int)role);
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

    public static void RpcExile(this PlayerControl player)
    {
        RPC.ExileAsync(player);
    }
    public static ClientData GetClient(this PlayerControl player)
    {
        try
        {
            var client = AmongUsClient.Instance.allClients.ToArray().FirstOrDefault(cd => cd.Character.PlayerId == player.PlayerId);
            return client;
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
    public static CustomRoles GetCustomRole(this GameData.PlayerInfo player)
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
            Logger.Warn("CustomSubRoleを取得しようとしましたが、対象がnullでした。", "getCustomSubRole");
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
        HudManagerPatch.LastSetNameDesyncCount++;

        Logger.Info($"Set:{player?.Data?.PlayerName}:{name} for All", "RpcSetNameEx");
        player?.RpcSetName(name);
    }

    public static void RpcSetNamePrivate(this PlayerControl player, string name, bool DontShowOnModdedClient = false, PlayerControl seer = null, bool force = false)
    {
        if (player == null || name == null || !AmongUsClient.Instance.AmHost) return;
        if (seer == null) seer = player;
        if (!force && Main.LastNotifyNames[(player.PlayerId, seer.PlayerId)] == name) return;

        Main.LastNotifyNames[(player.PlayerId, seer.PlayerId)] = name;
        HudManagerPatch.LastSetNameDesyncCount++;
        Logger.Info($"Set:{player.Data?.PlayerName}:{name} for {seer.GetNameWithRole().RemoveHtmlTags()}", "RpcSetNamePrivate");

        var clientId = seer.GetClientId();
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(player.NetId, (byte)RpcCalls.SetName, SendOption.Reliable, clientId);
        writer.Write(name);
        writer.Write(DontShowOnModdedClient);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void RpcSetRoleDesync(this PlayerControl player, RoleTypes role, int clientId)
    {
        //player: Rename target

        if (player == null) return;
        if (AmongUsClient.Instance.ClientId == clientId)
        {
            player.SetRole(role);
            return;
        }
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(player.NetId, (byte)RpcCalls.SetRole, SendOption.Reliable, clientId);
        writer.Write((ushort)role);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
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
        if (killer.PlayerId != 0)
        {
            var writer = AmongUsClient.Instance.StartRpcImmediately(killer.NetId, (byte)RpcCalls.MurderPlayer, SendOption.Reliable);
            writer.WriteNetObject(target);
            writer.Write((int)MurderResultFlags.FailedProtected);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        if (!fromSetKCD) killer.AddKillTimerToDict(half: true);
    }
    //public static void SetKillCooldownV2(this PlayerControl player, float time = -1f)
    //{
    //    if (player == null) return;
    //    if (!player.CanUseKillButton()) return;
    //    if (time >= 0f) Main.AllPlayerKillCooldown[player.PlayerId] = time * 2;
    //    else Main.AllPlayerKillCooldown[player.PlayerId] *= 2;
    //    player.SyncSettings();
    //    player.RpcGuardAndKill();
    //    player.ResetKillCooldown();
    //}

    public static void AddKCDAsAbilityCD(this PlayerControl pc) => AddAbilityCD(pc, (int)Math.Round(Main.AllPlayerKillCooldown.TryGetValue(pc.PlayerId, out var KCD) ? KCD : Options.DefaultKillCooldown));
    public static void AddAbilityCD(this PlayerControl pc, bool includeDuration = true) => Utils.AddAbilityCD(pc.GetCustomRole(), pc.PlayerId, includeDuration);
    public static void AddAbilityCD(this PlayerControl pc, int CD) => Main.AbilityCD[pc.PlayerId] = (TimeStamp, CD);
    public static bool HasAbilityCD(this PlayerControl pc) => Main.AbilityCD.ContainsKey(pc.PlayerId);

    public static float GetAbilityUseLimit(this PlayerControl pc) => Main.AbilityUseLimit.GetValueOrDefault(pc.PlayerId, float.NaN);
    public static float GetAbilityUseLimit(this byte playerId) => Main.AbilityUseLimit.GetValueOrDefault(playerId, float.NaN);

    public static void RpcRemoveAbilityUse(this PlayerControl pc)
    {
        float current = pc.GetAbilityUseLimit();
        if (float.IsNaN(current) || current <= 0f) return;
        pc.SetAbilityUseLimit(current - 1);
    }
    public static void RpcIncreaseAbilityUseLimitBy(this PlayerControl pc, float get)
    {
        float current = pc.GetAbilityUseLimit();
        if (float.IsNaN(current)) return;
        pc.SetAbilityUseLimit(current + get);
    }

    public static void SetAbilityUseLimit(this PlayerControl pc, float limit, bool rpc = true) => pc.PlayerId.SetAbilityUseLimit(limit, rpc);

    public static void SetAbilityUseLimit(this byte playerId, float limit, bool rpc = true)
    {
        if (float.IsNaN(limit) || limit is < 0f or > 100f || (Main.AbilityUseLimit.TryGetValue(playerId, out var beforeLimit) && Math.Abs(beforeLimit - limit) < 0.01f)) return;

        Main.AbilityUseLimit[playerId] = limit;

        if (AmongUsClient.Instance.AmHost && playerId.IsPlayerModClient() && playerId != 0 && rpc)
        {
            var writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncAbilityUseLimit, SendOption.Reliable);
            writer.Write(playerId);
            writer.Write(limit);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        var pc = GetPlayerById(playerId);
        NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        Logger.Info($" {pc.GetNameWithRole()} => {Math.Round(limit, 1)}", "SetAbilityUseLimit");
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
            return;
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
    //public static void SetKillCooldownV3(this PlayerControl player, float time = -1f, PlayerControl target = null, bool forceAnime = false)
    //{
    //    if (player == null) return;
    //    if (!player.CanUseKillButton()) return;
    //    if (target == null) target = player;
    //    if (time >= 0f) Main.AllPlayerKillCooldown[player.PlayerId] = time * 2;
    //    else Main.AllPlayerKillCooldown[player.PlayerId] *= 2;
    //    if (forceAnime || !player.IsModClient() || !Options.DisableShieldAnimations.GetBool())
    //    {
    //        player.SyncSettings();
    //        player.RpcGuardAndKill(target, 11);
    //    }
    //    else
    //    {
    //        time = Main.AllPlayerKillCooldown[player.PlayerId] / 2;
    //        if (player.AmOwner) PlayerControl.LocalPlayer.SetKillTimer(time);
    //        else
    //        {
    //            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetKillTimer, SendOption.Reliable, player.GetClientId());
    //            writer.Write(time);
    //            AmongUsClient.Instance.FinishRpcImmediately(writer);
    //        }
    //        Main.AllPlayerControls.Where(x => x.Is(CustomRoles.Observer) && target.PlayerId != x.PlayerId).Do(x => x.RpcGuardAndKill(target, 11, true));
    //    }
    //    player.ResetKillCooldown();
    //}
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

    /*
        [Obsolete]
        public static void RpcSpecificProtectPlayer(this PlayerControl killer, PlayerControl target = null, int colorId = 0)
        {
            if (AmongUsClient.Instance.AmClient)
            {
                killer.ProtectPlayer(target, colorId);
            }
            MessageWriter messageWriter = AmongUsClient.Instance.StartRpcImmediately(killer.NetId, (byte)RpcCalls.ProtectPlayer, SendOption.Reliable, killer.GetClientId());
            messageWriter.WriteNetObject(target);
            messageWriter.Write(colorId);
            AmongUsClient.Instance.FinishRpcImmediately(messageWriter);
        }
    */
    public static void RpcResetAbilityCooldown(this PlayerControl target)
    {
        if (!AmongUsClient.Instance.AmHost) return; //ホスト以外が実行しても何も起こさない
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

    /*public static void RpcBeKilled(this PlayerControl player, PlayerControl KilledBy = null) {
        if(!AmongUsClient.Instance.AmHost) return;
        byte KilledById;
        if(KilledBy == null)
            KilledById = byte.MaxValue;
        else
            KilledById = KilledBy.PlayerId;

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(player.NetId, (byte)CustomRPC.BeKilled, Hazel.SendOption.Reliable, -1);
        writer.Write(player.PlayerId);
        writer.Write(KilledById);
        AmongUsClient.Instance.FinishRpcImmediately(writer);

        RPC.BeKilled(player.PlayerId, KilledById);
    }*/
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

    /*public static GameOptionsData DeepCopy(this GameOptionsData opt)
    {
        var optByte = opt.ToBytes(5);
        return GameOptionsData.FromBytes(optByte);
    }*/

    public static bool IsNonHostModClient(this PlayerControl pc) => pc.IsModClient() && pc.PlayerId != 0;

    public static string GetDisplayRoleName(this PlayerControl player, bool pure = false)
    {
        return Utils.GetDisplayRoleName(player.PlayerId, pure);
    }
    public static string GetSubRoleName(this PlayerControl player, bool forUser = false)
    {
        var SubRoles = Main.PlayerStates[player.PlayerId].SubRoles.ToArray();
        if (SubRoles.Length == 0) return string.Empty;
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
        text += player.GetSubRoleName(forUser);
        return text;
    }
    public static string GetNameWithRole(this PlayerControl player, bool forUser = false)
    {
        return $"{player?.Data?.PlayerName}" + (GameStates.IsInGame && Options.CurrentGameMode is not CustomGameMode.FFA and not CustomGameMode.MoveAndStop and not CustomGameMode.HotPotato ? $" ({player?.GetAllRoleName(forUser).RemoveHtmlTags().Replace('\n', ' ')})" : string.Empty);
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
            _ => SystemTypes.Reactor,
        };

        _ = new LateTask(() =>
        {
            pc.RpcDesyncRepairSystem(systemtypes, 128);
        }, 0f + delay, "Reactor Desync");

        _ = new LateTask(() =>
        {
            pc.RpcSpecificMurderPlayer();
        }, 0.2f + delay, "Murder To Reset Cam");

        _ = new LateTask(() =>
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
            _ => SystemTypes.Reactor,
        };

        float FlashDuration = Options.KillFlashDuration.GetFloat();

        pc.RpcDesyncRepairSystem(systemtypes, 128);

        _ = new LateTask(() =>
        {
            pc.RpcDesyncRepairSystem(systemtypes, 16);

            if (Main.NormalOptions.MapId == 4) // on Airship
                pc.RpcDesyncRepairSystem(systemtypes, 17);
        }, FlashDuration + delay, "Fix Desync Reactor");
    }

    public static string GetRealName(this PlayerControl player, bool isMeeting = false)
    {
        return isMeeting ? player?.Data?.PlayerName : player?.name;
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
        if (Pelican.IsEaten(pc.PlayerId)) return false;
        if (pc.Data.Role.Role == RoleTypes.GuardianAngel) return false;

        return pc.GetCustomRole() switch
        {
            //SoloKombat
            CustomRoles.KB_Normal => pc.SoloAlive(),
            //FFA
            CustomRoles.Killer => pc.IsAlive(),
            //Move And Stop
            CustomRoles.Tasker => false,
            //Hot Potato
            CustomRoles.Potato => false,
            //Hide And Seek
            CustomRoles.Seeker => true,
            CustomRoles.Hider => false,
            CustomRoles.Troll => false,
            CustomRoles.Fox => false,

            _ => Main.PlayerStates.TryGetValue(pc.PlayerId, out var state) && state.Role.CanUseKillButton(pc)
        };
    }

    public static bool CanUseImpostorVentButton(this PlayerControl pc)
    {
        if (!pc.IsAlive() || pc.Data.Role.Role == RoleTypes.GuardianAngel || Penguin.IsVictim(pc)) return false;
        if (CopyCat.playerIdList.Contains(pc.PlayerId)) return true;

        if ((pc.Is(CustomRoles.Nimble) || Options.EveryoneCanVent.GetBool()) && pc.GetCustomRole().GetVNRole() != CustomRoles.Engineer) return true;
        if (pc.Is(CustomRoles.Bloodlust)) return true;

        return pc.GetCustomRole() switch
        {
            //SoloKombat
            CustomRoles.KB_Normal => true,
            //FFA
            CustomRoles.Killer => true,
            //Move And Stop
            CustomRoles.Tasker => false,
            //Hot Potato
            CustomRoles.Potato => false,

            _ => Main.PlayerStates.TryGetValue(pc.PlayerId, out var state) && state.Role.CanUseImpostorVentButton(pc),
        };
    }
    public static bool CanUseSabotage(this PlayerControl pc) // NOTE: THIS IS FOR THE HUD FOR MODDED CLIENTS, THIS DOES NOT DETERMINE WHETHER A ROLE CAN SABOTAGE
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
        if (arsonist == null || target == null || Arsonist.isDoused == null) return false;
        Arsonist.isDoused.TryGetValue((arsonist.PlayerId, target.PlayerId), out bool isDoused);
        return isDoused;
    }
    public static bool IsDrawPlayer(this PlayerControl arsonist, PlayerControl target)
    {
        if (arsonist == null || target == null || Revolutionist.isDraw == null) return false;
        Revolutionist.isDraw.TryGetValue((arsonist.PlayerId, target.PlayerId), out bool isDraw);
        return isDraw;
    }
    public static bool IsRevealedPlayer(this PlayerControl player, PlayerControl target)
    {
        if (player == null || target == null || Farseer.isRevealed == null) return false;
        Farseer.isRevealed.TryGetValue((player.PlayerId, target.PlayerId), out bool isDoused);
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
            CustomRoles.Killer => FFAManager.FFA_KCD.GetFloat(),
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
            Logger.Info($"kill cd of player set to {Main.AllPlayerKillCooldown[player.PlayerId]}", "Diseased");
        }
        if (Main.KilledAntidote.TryGetValue(player.PlayerId, out int value1))
        {
            var kcd = Main.AllPlayerKillCooldown[player.PlayerId] - value1 * Options.AntidoteCDOpt.GetFloat();
            if (kcd < 0) kcd = 0;
            Main.AllPlayerKillCooldown[player.PlayerId] = kcd;
            Logger.Info($"kill cd of player set to {Main.AllPlayerKillCooldown[player.PlayerId]}", "Antidote");
        }
    }
    public static void TrapperKilled(this PlayerControl killer, PlayerControl target)
    {
        Logger.Info($"{target?.Data?.PlayerName}はTrapperだった", "Trapper");
        var tmpSpeed = Main.AllPlayerSpeed[killer.PlayerId];
        Main.AllPlayerSpeed[killer.PlayerId] = Main.MinSpeed;    //tmpSpeedで後ほど値を戻すので代入しています。
        ReportDeadBodyPatch.CanReport[killer.PlayerId] = false;
        killer.MarkDirtySettings();
        _ = new LateTask(() =>
        {
            Main.AllPlayerSpeed[killer.PlayerId] = Main.AllPlayerSpeed[killer.PlayerId] - Main.MinSpeed + tmpSpeed;
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
    public static (Vector2 LOCATION, string ROOM_NAME) GetPositionInfo(this PlayerControl pc)
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

        if (target.GetTeam() is Team.Impostor or Team.Neutral) Stressed.OnNonCrewmateDead();

        if (killer.Is(CustomRoles.Damocles)) Damocles.OnMurder(killer.PlayerId);
        else if (killer.Is(Team.Impostor)) Damocles.OnOtherImpostorMurder();
        else if (target.Is(Team.Impostor)) Damocles.OnImpostorDeath();

        if (killer.Is(CustomRoles.Bloodlust)) FixedUpdatePatch.AddExtraAbilityUsesOnFinishedTasks(killer);
        else IncreaseAbilityUseLimitOnKill(killer);

        target.SetRealKiller(killer, NotOverRide: true);

        if (killer.PlayerId == target.PlayerId && killer.shapeshifting)
        {
            _ = new LateTask(() => { killer.RpcMurderPlayer(target, true); }, 1.5f, "Shapeshifting Suicide Delay");
            return;
        }

        killer.RpcMurderPlayer(target, true);
    }

    /*
        public static void RpcMurderPlayerV2(this PlayerControl killer, PlayerControl target)
        {
            target.RemoveProtection();
            MessageWriter messageWriter = AmongUsClient.Instance.StartRpcImmediately(target.NetId, (byte)RpcCalls.MurderPlayer, SendOption.Reliable);
            messageWriter.WriteNetObject(target);
            messageWriter.Write((int)MurderResultFlags.FailedProtected);
            AmongUsClient.Instance.FinishRpcImmediately(messageWriter);

            if (killer.AmOwner)
            {
                killer.RpcMurderPlayer(target, true);
            }
            else
            {
                killer.MurderPlayer(target, ResultFlags);
                MessageWriter messageWriter2 = AmongUsClient.Instance.StartRpcImmediately(killer.NetId, (byte)RpcCalls.MurderPlayer, SendOption.Reliable);
                messageWriter2.WriteNetObject(target);
                messageWriter2.Write((int)ResultFlags);
                AmongUsClient.Instance.FinishRpcImmediately(messageWriter2);
            }
            target.Data.IsDead = true;

            NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
            NotifyRoles(SpecifySeer: target);
        }
    */
    public static bool RpcCheckAndMurder(this PlayerControl killer, PlayerControl target, bool check = false) => CheckMurderPatch.RpcCheckAndMurder(killer, target, check);

    public static void NoCheckStartMeeting(this PlayerControl reporter, GameData.PlayerInfo target, bool force = false)
    {
        if (Options.DisableMeeting.GetBool() && !force) return;
        ReportDeadBodyPatch.AfterReportTasks(reporter, target);
        MeetingRoomManager.Instance.AssignSelf(reporter, target);
        DestroyableSingleton<HudManager>.Instance.OpenMeetingRoom(reporter);
        reporter.RpcStartMeeting(target);
    }
    public static bool IsModClient(this PlayerControl player) => Main.playerVersion.ContainsKey(player.PlayerId);

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

    public static bool IsCrewmate(this PlayerControl pc) => !pc.Is(CustomRoles.Bloodlust) && pc.GetCustomRole().IsCrewmate();
    public static CustomRoleTypes GetCustomRoleTypes(this PlayerControl pc) => pc.Is(CustomRoles.Bloodlust) ? CustomRoleTypes.Neutral : pc.GetCustomRole().GetCustomRoleTypes();
    public static RoleTypes GetRoleTypes(this PlayerControl pc) => pc.Is(CustomRoles.Bloodlust) ? RoleTypes.Impostor : pc.GetCustomRole().GetRoleTypes();

    public static bool Is(this PlayerControl target, CustomRoles role) =>
        role > CustomRoles.NotAssigned ? target.GetCustomSubRoles().Contains(role) : target.GetCustomRole() == role;

    public static bool Is(this PlayerControl target, CustomRoleTypes type) => target.GetCustomRoleTypes() == type;
    public static bool Is(this PlayerControl target, RoleTypes type) => target.GetCustomRole().GetRoleTypes() == type;
    public static bool Is(this PlayerControl target, CountTypes type) => target.GetCountTypes() == type;
    public static bool Is(this PlayerControl target, Team team) => team switch
    {
        Team.Impostor => target.GetCustomRole().IsImpostorTeamV3(),
        Team.Neutral => target.GetCustomRole().IsNeutralTeamV2(),
        Team.Crewmate => target.GetCustomRole().IsCrewmateTeamV2(),
        Team.None => target.Is(CustomRoles.GM) || target.Is(CountTypes.None) || target.Is(CountTypes.OutOfGame),
        _ => false,
    };
    public static Team GetTeam(this PlayerControl target)
    {
        var role = target.GetCustomRole();
        if (role.IsImpostorTeamV3()) return Team.Impostor;
        if (role.IsNeutralTeamV2()) return Team.Neutral;
        return role.IsCrewmateTeamV2() ? Team.Crewmate : Team.None;
    }
    public static bool IsAlive(this PlayerControl target)
    {
        //ロビーなら生きている
        //targetがnullならば切断者なので生きていない
        //targetがnullでなく取得できない場合は登録前なので生きているとする
        if (target == null || target.Is(CustomRoles.GM)) return false;
        return GameStates.IsLobby || !Main.PlayerStates.TryGetValue(target.PlayerId, out var ps) || !ps.IsDead;
    }

    ///<summary>Is the player currently protected</summary>
    public static bool IsProtected(this PlayerControl self) => self.protectedByGuardianId > -1;


    public const MurderResultFlags ResultFlags = MurderResultFlags.Succeeded;
}