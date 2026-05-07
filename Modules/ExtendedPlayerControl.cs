using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using AmongUs.GameOptions;
using EHR.Gamemodes;
using EHR.Modules;
using EHR.Modules.Extensions;
using EHR.Roles;
using Hazel;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using InnerNet;
using UnityEngine;
using static EHR.Translator;
using static EHR.Utils;

namespace EHR;

internal static class ExtendedPlayerControl
{
    public static readonly HashSet<byte> BlackScreenWaitingPlayers = [];
    public static readonly HashSet<byte> CancelBlackScreenFix = [];
    private static readonly List<Vent> ResultBuffer = [];
    public static readonly HashSet<byte> TempExiled = [];

    extension(PlayerControl player)
    {
        public void SetRole(RoleTypes role, bool canOverride = true)
        {
            player.StartCoroutine(player.CoSetRole(role, canOverride));
        }

        public void RpcSetCustomRole(CustomRoles role, bool replaceAllAddons = false)
        {
            if (role < CustomRoles.NotAssigned)
                Main.PlayerStates[player.PlayerId].SetMainRole(role);
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

        public bool UsesMeetingShapeshift()
        {
            CustomRoles role = player.GetCustomRole();
            if (player.IsModdedClient() && role is CustomRoles.Councillor or CustomRoles.Inspector or CustomRoles.Judge or CustomRoles.Retributionist or CustomRoles.Starspawn or CustomRoles.Swapper or CustomRoles.Ventriloquist) return false;
            return role.UsesMeetingShapeshift();
        }

        public bool CanUseVent()
        {
            try { return player.CanUseVent(player.GetClosestVent()?.Id ?? int.MaxValue); }
            catch (Exception e)
            {
                ThrowException(e);
                return true;
            }
        }

        public bool CanUseVent(int ventId)
        {
            int? closestVentId = player.GetClosestVent()?.Id;
            if (player.inVent && closestVentId == ventId) return true;
        
            switch (Options.CurrentGameMode)
            {
                case CustomGameMode.RoomRush:
                    return true;
                case CustomGameMode.Standard when Options.DisableVentingOn1v1.GetBool() && Main.AllAlivePlayerControls.Count == 2 && player.GetRoleTypes() != RoleTypes.Engineer:
                    return false;
                case CustomGameMode.StopAndGo:
                    return StopAndGo.IsEventActive && StopAndGo.Event.Type == StopAndGo.Events.VentAccess;
                case CustomGameMode.Deathrace:
                    return Deathrace.CanUseVent(player, ventId);
            }

            if (player.Is(CustomRoles.Trainee) && MeetingStates.FirstMeeting) return false;
            if (player.Is(CustomRoles.Blocked) && closestVentId != ventId) return false;
            if (!GameStates.IsInTask || ExileController.Instance || AntiBlackout.SkipTasks || Main.Invisible.Contains(player.PlayerId)) return false;
            return (player.CanUseImpostorVentButton() || player.GetRoleTypes() == RoleTypes.Engineer) && Main.PlayerStates.Values.All(x => x.Role.CanUseVent(player, ventId));
        }

        // Next 5: From MoreGamemodes by Rabek009 (https://github.com/Rabek009/MoreGamemodes)

        public Vent GetClosestVent()
        {
            if (ShipStatus.Instance?.AllVents == null) return null;

            Vector2 pos = player.Pos();
            Vent closest = null;

            foreach (var vent in ShipStatus.Instance.AllVents)
            {
                if (!closest || Vector2.Distance(pos, vent.transform.position) < Vector2.Distance(pos, closest.transform.position))
                    closest = vent;
            }

            return closest;
        }

        public List<Vent> GetVentsFromClosest()
        {
            var allVents = ShipStatus.Instance?.AllVents;
            if (allVents == null) return [];

            ResultBuffer.Clear();
            ResultBuffer.AddRange(allVents);

            Vector2 playerpos = player.Pos();
            ResultBuffer.Sort((v1, v2) => 
                Vector2.Distance(playerpos, v1.transform.position)
                    .CompareTo(Vector2.Distance(playerpos, v2.transform.position)));

            if ((player.walkingToVent || player.inVent) && ResultBuffer.Count > 0 && ResultBuffer[0])
            {
                var nearbyVents = ResultBuffer[0].NearbyVents;
                if (nearbyVents != null)
                {
                    for (int i = nearbyVents.Length - 1; i >= 0; i--)
                    {
                        var v = nearbyVents[i];
                        if (v)
                        {
                            ResultBuffer.Remove(v);
                            ResultBuffer.Insert(0, v);
                        }
                    }
                }
            }

            return ResultBuffer;
        }

        public void RevertFreeze(Vector2 realPosition)
        {
            if (!AmongUsClient.Instance.AmHost) return;
            player.NetTransform.SnapTo(realPosition, (ushort)(player.NetTransform.lastSequenceId + 128));
            CustomRpcSender sender = CustomRpcSender.Create($"Revert SnapTo Freeze ({player.GetNameWithRole()})", SendOption.Reliable);
            sender.StartMessage();
            sender.StartRpc(player.NetTransform.NetId, (byte)RpcCalls.SnapTo)
                .WriteVector2(player.transform.position)
                .Write((ushort)(player.NetTransform.lastSequenceId + 32767))
                .EndRpc();
            sender.StartRpc(player.NetTransform.NetId, (byte)RpcCalls.SnapTo)
                .WriteVector2(player.transform.position)
                .Write((ushort)(player.NetTransform.lastSequenceId + 32767 + 16383))
                .EndRpc();
            sender.StartRpc(player.NetTransform.NetId, (byte)RpcCalls.SnapTo)
                .WriteVector2(player.transform.position)
                .Write(player.NetTransform.lastSequenceId)
                .EndRpc();
            sender.EndMessage();
            sender.SendMessage();
            NumSnapToCallsThisRound += 3;
            player.Visible = true;
        }

        public void FreezeForOthers()
        {
            if (!AmongUsClient.Instance.AmHost) return;
        
            foreach (PlayerControl pc in Main.EnumerateAlivePlayerControls())
            {
                if (pc == player || pc.AmOwner) continue;
                CustomRpcSender sender = CustomRpcSender.Create($"SnapTo Freeze ({player.GetNameWithRole()})", SendOption.Reliable);
                sender.StartMessage(pc.OwnerId);
                sender.StartRpc(player.NetTransform.NetId, (byte)RpcCalls.SnapTo)
                    .WriteVector2(player.transform.position)
                    .Write(player.NetTransform.lastSequenceId)
                    .EndRpc();
                sender.StartRpc(player.NetTransform.NetId, (byte)RpcCalls.SnapTo)
                    .WriteVector2(player.transform.position)
                    .Write((ushort)(player.NetTransform.lastSequenceId + 16383))
                    .EndRpc();
                sender.EndMessage();
                sender.SendMessage();
                NumSnapToCallsThisRound += 2;
            }

            player.Visible = false;
        }

        public void SetChatVisible(bool visible)
        {
            if (!AmongUsClient.Instance.AmHost) return;
        
            Logger.Info($"Setting the chat {(visible ? "visible" : "hidden")} for {player.GetNameWithRole()}", "SetChatVisible");

            if (player.AmOwner)
            {
                HudManager.Instance.Chat.SetVisible(visible);
                HudManager.Instance.Chat.HideBanButton();
                return;
            }

            if (player.IsModdedClient())
            {
                var msg = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetChatVisible, SendOption.Reliable, player.OwnerId);
                msg.Write(visible);
                AmongUsClient.Instance.FinishRpcImmediately(msg);
                return;
            }

            DataFlagRateLimiter.Enqueue(() =>
            {
                bool dead = player.Data.IsDead;
                MessageWriter writer = MessageWriter.Get(SendOption.Reliable);
                writer.StartMessage(6);
                writer.Write(AmongUsClient.Instance.GameId);
                writer.WritePacked(player.OwnerId);
                writer.StartMessage(4);
                writer.WritePacked(HudManager.Instance.MeetingPrefab.SpawnId);
                writer.WritePacked(-2);
                writer.Write((byte)SpawnFlags.None);
                writer.WritePacked(1);
                uint netIdCnt = AmongUsClient.Instance.NetIdCnt;
                AmongUsClient.Instance.NetIdCnt = netIdCnt + 1U;
                writer.WritePacked(netIdCnt);
                writer.StartMessage(1);
                writer.WritePacked(0);
                writer.EndMessage();
                writer.EndMessage();
                player.Data.IsDead = visible;
                writer.StartMessage(1);
                writer.WritePacked(player.Data.NetId);
                player.Data.Serialize(writer, true);
                writer.EndMessage();
                writer.StartMessage(2);
                writer.WritePacked(netIdCnt);
                writer.Write((byte)RpcCalls.CloseMeeting);
                writer.EndMessage();
                player.Data.IsDead = dead;
                writer.StartMessage(1);
                writer.WritePacked(player.Data.NetId);
                player.Data.Serialize(writer, true);
                writer.EndMessage();
                writer.StartMessage(5);
                writer.WritePacked(netIdCnt);
                writer.EndMessage();
                writer.EndMessage();
                AmongUsClient.Instance.SendOrDisconnect(writer);
                writer.Recycle();
            }, calls: 3);
        }

        public ClientData GetClient()
        {
            try { return AmongUsClient.Instance.GetClientFromCharacter(player); }
            catch { return null; }
        }

        /// <summary>
        ///     *Add-ons cannot be obtained.
        /// </summary>
        public CustomRoles GetCustomRole()
        {
            if (!player)
            {
                MethodBase callerMethod = new StackFrame(1, false).GetMethod();
                string callerMethodName = callerMethod?.Name;
                Logger.Warn($"{callerMethod?.DeclaringType?.FullName}.{callerMethodName} tried to get a CustomRole, but the target was null.", "GetCustomRole");
                return CustomRoles.Crewmate;
            }

            return Main.PlayerStates.TryGetValue(player.PlayerId, out PlayerState state) ? state.MainRole : CustomRoles.Crewmate;
        }

        public List<CustomRoles> GetCustomSubRoles()
        {
            if (GameStates.IsLobby) return [];

            if (!player)
            {
                Logger.Warn("The player is null", "GetCustomSubRoles");
                return [];
            }

            return Main.PlayerStates.TryGetValue(player.PlayerId, out PlayerState state) ? state.SubRoles : [];
        }

        public CountTypes GetCountTypes()
        {
            if (!player)
            {
                StackFrame caller = new(1, false);
                MethodBase callerMethod = caller.GetMethod();
                string callerMethodName = callerMethod?.Name;
                string callerClassName = callerMethod?.DeclaringType?.FullName;
                Logger.Warn($"{callerClassName}.{callerMethodName} tried to get a CountType, but the player was null", "GetCountTypes");
                return CountTypes.None;
            }

            if (!Main.PlayerStates.TryGetValue(player.PlayerId, out PlayerState state)) return CountTypes.None;

            if (!player.IsConverted() && state.SubRoles.Contains(CustomRoles.Bloodlust)) return CountTypes.Bloodlust;

            return state.countTypes;
        }

        public void RpcResetTasks(bool init = true)
        {
            if (!AmongUsClient.Instance.AmHost || !GameStates.IsInGame || !player) return;

            player.Data.RpcSetTasks(new Il2CppStructArray<byte>(0));
            if (init) Main.PlayerStates[player.PlayerId].InitTask(player);
        }

        public void ExileTemporarily() // Only used in game modes
        {
            if (!AmongUsClient.Instance.AmHost) return;
            if (!TempExiled.Add(player.PlayerId)) return;
        
            player.RpcSetRoleGlobal(RoleTypes.GuardianAngel);
            LateTask.New(player.SyncSettings, 0.1f, log: false);
            LateTask.New(player.RpcResetAbilityCooldown, 0.2f, log: false);
        
            Main.PlayerStates[player.PlayerId].SetDead();
        }

        // Saves some RPC calls for vanilla servers to make innersloth's rate limit happy
        public void ReviveFromTemporaryExile() // Only used in game modes
        {
            if (!AmongUsClient.Instance.AmHost) return;
        
            if (GameStates.CurrentServerType != GameStates.ServerType.Vanilla)
            {
                player.RpcRevive();
                return;
            }

            PlayerState state = Main.PlayerStates[player.PlayerId];
            state.IsDead = false;
            state.deathReason = PlayerState.DeathReason.etc;

            player.RpcSetRoleGlobal(RoleTypes.Crewmate);

            TempExiled.Remove(player.PlayerId);

            LateTask.New(() =>
            {
                var sender = CustomRpcSender.Create("ReviveFromTemporaryExile", SendOption.Reliable);
                var hasValue = false;

                RoleTypes newRoleType = state.MainRole.GetRoleTypes();
                CustomGameMode gameMode = Options.CurrentGameMode;

                if (gameMode is CustomGameMode.SoloPVP or CustomGameMode.FFA or CustomGameMode.CaptureTheFlag or CustomGameMode.KingOfTheZones or CustomGameMode.BedWars or CustomGameMode.Snowdown)
                    hasValue |= sender.RpcSetRole(player, newRoleType, player.OwnerId);

                player.ResetKillCooldown();

                switch (gameMode)
                {
                    case CustomGameMode.CaptureTheFlag:
                        LateTask.New(() => player.SetKillCooldownNonSync(CaptureTheFlag.KCD), 0.2f);
                        break;
                    case CustomGameMode.Snowdown:
                        LateTask.New(() => player.SetKillCooldownNonSync(5f), 0.2f);
                        break;
                    default:
                        LateTask.New(() => player.SetKillCooldown(), 0.2f);
                        break;
                }
            
                if (newRoleType is not (RoleTypes.Crewmate or RoleTypes.Impostor or RoleTypes.Noisemaker))
                    hasValue |= sender.RpcResetAbilityCooldown(player);

                if (DoRPC)
                {
                    sender.SyncGeneralOptions(player);
                    hasValue = true;
                }

                sender.SendMessage(!hasValue);
            }, 1f, log: false);
        }

        public void RpcSetRoleGlobal(RoleTypes roleTypes, bool setRoleMap = false)
        {
            try
            {
                if (!AmongUsClient.Instance.AmHost) return;
                if (AmongUsClient.Instance.AmClient) try { player.SetRole(roleTypes); } catch { }
                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(player.NetId, (byte)RpcCalls.SetRole, SendOption.Reliable);
                writer.Write((ushort)roleTypes);
                writer.Write(true);
                AmongUsClient.Instance.FinishRpcImmediately(writer);
                Logger.Info($" {player.GetNameWithRole()} => {roleTypes}", "RpcSetRoleGlobal");

                if (setRoleMap)
                {
                    foreach ((byte seerID, byte targetID) in StartGameHostPatch.RpcSetRoleReplacer.RoleMap.Keys.ToArray())
                    {
                        if (targetID == player.PlayerId)
                        {
                            var value = StartGameHostPatch.RpcSetRoleReplacer.RoleMap[(seerID, targetID)];
                            value.RoleType = roleTypes;
                            StartGameHostPatch.RpcSetRoleReplacer.RoleMap[(seerID, targetID)] = value;
                        }
                    }
                }
            }
            catch (Exception e) { ThrowException(e); }
        }

        public void RpcSetRoleDesync(RoleTypes role, int clientId, bool setRoleMap = false)
        {
            if (!AmongUsClient.Instance.AmHost) return;
            if (!player) return;

            if (setRoleMap)
            {
                try
                {
                    (byte, byte) key = (GetClientById(clientId).Character.PlayerId, player.PlayerId);

                    if (StartGameHostPatch.RpcSetRoleReplacer.RoleMap.TryGetValue(key, out (RoleTypes RoleType, CustomRoles CustomRole) pair))
                    {
                        pair.RoleType = role;
                        StartGameHostPatch.RpcSetRoleReplacer.RoleMap[key] = pair;
                    }
                }
                catch (Exception e) { ThrowException(e); }
            }

            try { Logger.Info($" {player.GetNameWithRole()} => {role} - for {GetClientById(clientId)?.Character?.GetNameWithRole() ?? "Someone"}", "RpcSetRoleDesync"); }
            catch (Exception e) { ThrowException(e); }

            if (AmongUsClient.Instance.ClientId == clientId)
            {
                player.SetRole(role);
                return;
            }

            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(player.NetId, (byte)RpcCalls.SetRole, SendOption.Reliable, clientId);
            writer.Write((ushort)role);
            writer.Write(true);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public (RoleTypes RoleType, CustomRoles CustomRole) GetRoleMap(byte targetId = byte.MaxValue)
        {
            return Utils.GetRoleMap(player.PlayerId, targetId);
        }

        public void RpcRevive()
        {
            if (!player) return;

            if (!player.Data.IsDead)
            {
                Logger.Warn($"Invalid Revive for {player.GetRealName()} / Player was already alive? {!player.Data.IsDead}", "RpcRevive");
                return;
            }
        
            if (!Main.PlayerStates.TryGetValue(player.PlayerId, out var state)) return;

            RPC.PlaySoundRPC(player.PlayerId, Sounds.SpawnSound);
            GhostRolesManager.RemoveGhostRole(player.PlayerId);
            ReportDeadBodyPatch.AlreadyReportedBodies.Remove(player.PlayerId);
            state.RealKiller = (DateTime.MinValue, byte.MaxValue);
            state.IsDead = false;
            state.deathReason = PlayerState.DeathReason.etc;
            TempExiled.Remove(player.PlayerId);
            if (Options.CurrentGameMode == CustomGameMode.Standard) state.Role.OnRevived(player);
            var sender = CustomRpcSender.Create("RpcRevive", SendOption.Reliable);
            player.RpcChangeRoleBasis(player.GetCustomRole());
            player.ResetKillCooldown();
            sender.RpcResetAbilityCooldown(player);
            sender.SyncGeneralOptions(player);

            LateTask.New(() => player.SetKillCooldown(), 0.2f, log: false);

            if (Options.AnonymousBodies.GetBool() && Main.AllPlayerNames.TryGetValue(player.PlayerId, out string name))
                RpcChangeSkin(player, new NetworkedPlayerInfo.PlayerOutfit().Set(name, 15, "", "", "", "", ""), sender);

            Camouflage.RpcSetSkin(player, sender: sender);
            sender.SyncSettings(player);

            sender.SendMessage();

            NotifyRoles(SpecifySeer: player);
            NotifyRoles(SpecifyTarget: player);
        }

        public void RpcChangeRoleBasis(CustomRoles newCustomRole, bool loggerRoleMap = false, bool forced = false)
        {
            if (!forced)
            {
                if (!AmongUsClient.Instance.AmHost || !GameStates.IsInGame || !player || !player.IsAlive()) return;

                if (AntiBlackout.SkipTasks || ExileController.Instance)
                {
                    StackTrace stackTrace = new(1, true);
                    MethodBase callerMethod = stackTrace.GetFrame(0)?.GetMethod();
                    string callerMethodName = callerMethod?.Name;
                    string callerClassName = callerMethod?.DeclaringType?.FullName;
                    Logger.Msg($"{callerClassName}.{callerMethodName} tried to change the role basis of {player.GetNameWithRole()} during anti-blackout processing or ejection screen showing, delaying the code to run after these tasks are complete", "RpcChangeRoleBasis");
                    Main.Instance.StartCoroutine(DelayBasisChange());
                    return;

                    IEnumerator DelayBasisChange()
                    {
                        while (AntiBlackout.SkipTasks || ExileController.Instance) yield return null;
                        yield return new WaitForSecondsRealtime(1f);
                        Logger.Msg($"Now that the anti-blackout processing or ejection screen showing is complete, the role basis of {player.GetNameWithRole()} will be changed", "RpcChangeRoleBasis");
                        player.RpcChangeRoleBasis(newCustomRole, loggerRoleMap);
                    }
                }
            }

            CustomRoles playerRole = Utils.GetRoleMap(player.PlayerId).CustomRole;
            RoleTypes newRoleType = newCustomRole.GetRoleTypes();
            RoleTypes rememberRoleType;

            if (!forced)
            {
                newRoleType = Options.CurrentGameMode switch
                {
                    CustomGameMode.Speedrun when newCustomRole == CustomRoles.Runner => Speedrun.CanKill.Contains(player.PlayerId) ? RoleTypes.Impostor : RoleTypes.Crewmate,
                    CustomGameMode.Standard when StartGameHostPatch.BasisChangingAddons.FindFirst(x => x.Value.Contains(player.PlayerId), out KeyValuePair<CustomRoles, List<byte>> kvp) => kvp.Key switch
                    {
                        CustomRoles.Bloodlust when newRoleType is RoleTypes.Crewmate or RoleTypes.Engineer or RoleTypes.Scientist or RoleTypes.Tracker or RoleTypes.Noisemaker => RoleTypes.Impostor,
                        CustomRoles.Nimble when newRoleType is RoleTypes.Crewmate or RoleTypes.Scientist or RoleTypes.Noisemaker or RoleTypes.Tracker => RoleTypes.Engineer,
                        CustomRoles.Physicist when newRoleType == RoleTypes.Crewmate => RoleTypes.Scientist,
                        CustomRoles.Finder when newRoleType is RoleTypes.Crewmate or RoleTypes.Scientist or RoleTypes.Noisemaker => RoleTypes.Tracker,
                        CustomRoles.Noisy when newRoleType == RoleTypes.Crewmate => RoleTypes.Noisemaker,
                        CustomRoles.Examiner when newRoleType is RoleTypes.Crewmate or RoleTypes.Scientist or RoleTypes.Noisemaker => RoleTypes.Detective,
                        CustomRoles.Venom when newRoleType == RoleTypes.Impostor => RoleTypes.Viper,
                        _ => newRoleType
                    },
                    _ => newRoleType
                };
            }

            bool oldRoleIsDesync = playerRole.IsDesyncRole();
            bool newRoleIsDesync = newCustomRole.IsDesyncRole() || player.Is(CustomRoles.Bloodlust);

            CustomRoles newRoleVN = newCustomRole.GetVNRole();
            RoleTypes newRoleDY = newCustomRole.GetDYRole();

            switch (oldRoleIsDesync, newRoleIsDesync)
            {
                // Desync role to normal role
                case (true, false):
                {
                    foreach (PlayerControl seer in Main.EnumeratePlayerControls())
                    {
                        int seerClientId = seer.OwnerId;
                        if (seerClientId == -1) continue;

                        bool seerIsHost = seer.IsHost();
                        bool self = player.PlayerId == seer.PlayerId;

                        if (!self && seer.HasDesyncRole() && !seerIsHost)
                            rememberRoleType = newRoleVN is CustomRoles.Noisemaker ? RoleTypes.Noisemaker : RoleTypes.Scientist;
                        else
                            rememberRoleType = newRoleType;

                        // Set role type for seer
                        StartGameHostPatch.RpcSetRoleReplacer.RoleMap[(seer.PlayerId, player.PlayerId)] = (rememberRoleType, newCustomRole);
                        player.RpcSetRoleDesync(rememberRoleType, seerClientId);

                        if (self) continue;

                        (RoleTypes seerRoleType, CustomRoles seerCustomRole) = seer.GetRoleMap();

                        if (seer.IsAlive())
                        {
                            if (seerCustomRole.IsDesyncRole())
                                rememberRoleType = seerIsHost ? RoleTypes.Crewmate : RoleTypes.Scientist;
                            else
                                rememberRoleType = seerRoleType;
                        }
                        else
                        {
                            bool playerIsKiller = playerRole.IsImpostor();

                            rememberRoleType = RoleTypes.CrewmateGhost;
                            if (!playerIsKiller && seer.Is(Team.Impostor)) rememberRoleType = RoleTypes.ImpostorGhost;

                            StartGameHostPatch.RpcSetRoleReplacer.RoleMap[(player.PlayerId, seer.PlayerId)] = (seerCustomRole.IsDesyncRole() ? seerIsHost ? RoleTypes.Crewmate : RoleTypes.Scientist : seerRoleType, seerCustomRole);
                            seer.RpcSetRoleDesync(rememberRoleType, player.OwnerId);
                            continue;
                        }

                        // Set role type for player
                        StartGameHostPatch.RpcSetRoleReplacer.RoleMap[(player.PlayerId, seer.PlayerId)] = (rememberRoleType, seerCustomRole);
                        seer.RpcSetRoleDesync(rememberRoleType, player.OwnerId);
                    }

                    break;
                }
                // Normal role to desync role
                case (false, true):
                {
                    foreach (PlayerControl seer in Main.EnumeratePlayerControls())
                    {
                        int seerClientId = seer.OwnerId;
                        if (seerClientId == -1) continue;

                        bool self = player.PlayerId == seer.PlayerId;

                        if (self)
                        {
                            rememberRoleType = player.IsHost() ? RoleTypes.Crewmate : RoleTypes.Impostor;

                            // For Desync Shapeshifter
                            if (newRoleDY is RoleTypes.Shapeshifter or RoleTypes.Phantom)
                                rememberRoleType = newRoleDY;
                        }
                        else
                            rememberRoleType = newRoleVN is CustomRoles.Noisemaker ? RoleTypes.Noisemaker : RoleTypes.Scientist;

                        StartGameHostPatch.RpcSetRoleReplacer.RoleMap[(seer.PlayerId, player.PlayerId)] = (rememberRoleType, newCustomRole);
                        player.RpcSetRoleDesync(rememberRoleType, seerClientId);

                        if (self) continue;

                        CustomRoles seerCustomRole = seer.GetRoleMap().CustomRole;

                        if (seer.IsAlive())
                            rememberRoleType = newRoleVN is CustomRoles.Noisemaker ? RoleTypes.Noisemaker : RoleTypes.Scientist;
                        else
                        {
                            rememberRoleType = RoleTypes.CrewmateGhost;

                            StartGameHostPatch.RpcSetRoleReplacer.RoleMap[(player.PlayerId, seer.PlayerId)] = (seerCustomRole.GetVNRole() is CustomRoles.Noisemaker ? RoleTypes.Noisemaker : RoleTypes.Scientist, seerCustomRole);
                            seer.RpcSetRoleDesync(rememberRoleType, player.OwnerId);
                            continue;
                        }

                        // Set role type for player
                        StartGameHostPatch.RpcSetRoleReplacer.RoleMap[(player.PlayerId, seer.PlayerId)] = (rememberRoleType, seerCustomRole);
                        seer.RpcSetRoleDesync(rememberRoleType, player.OwnerId);
                    }

                    break;
                }
                // Desync role to desync role
                // Normal role to normal role
                default:
                {
                    bool playerIsDesync = player.HasDesyncRole();

                    foreach (PlayerControl seer in Main.EnumeratePlayerControls())
                    {
                        int seerClientId = seer.OwnerId;
                        if (seerClientId == -1) continue;

                        if ((playerIsDesync || seer.HasDesyncRole()) && seer.PlayerId != player.PlayerId)
                            rememberRoleType = Utils.GetRoleMap(seer.PlayerId, player.PlayerId).RoleType;
                        else
                            rememberRoleType = newRoleType;

                        StartGameHostPatch.RpcSetRoleReplacer.RoleMap[(seer.PlayerId, player.PlayerId)] = (rememberRoleType, newCustomRole);
                        player.RpcSetRoleDesync(rememberRoleType, seerClientId);
                    }

                    break;
                }
            }

            if (loggerRoleMap)
            {
                foreach (PlayerControl seer in Main.EnumeratePlayerControls())
                {
                    NetworkedPlayerInfo seerData = seer.Data;

                    foreach (PlayerControl target in Main.EnumeratePlayerControls())
                    {
                        NetworkedPlayerInfo targetData = target.Data;
                        (RoleTypes roleType, CustomRoles customRole) = seer.GetRoleMap(targetData.PlayerId);
                        Logger.Info($"seer {seerData?.PlayerName}-{seerData?.PlayerId}, target {targetData.PlayerName}-{targetData.PlayerId} => {roleType}, {customRole}", "Role Map");
                    }
                }
            }

            if (!forced) Logger.Info($"{player.GetNameWithRole()}'s role basis was changed to {newRoleType} ({newCustomRole}) (from role: {playerRole}) - oldRoleIsDesync: {oldRoleIsDesync}, newRoleIsDesync: {newRoleIsDesync}", "RpcChangeRoleBasis");
        }

        public void RpcShapeshiftDesync(PlayerControl target, PlayerControl seer, bool animate)
        {
            try
            {
                int clientId = seer.OwnerId;

                if (AmongUsClient.Instance.ClientId == clientId)
                {
                    try { player.Shapeshift(target, animate); }
                    catch (Exception e) { ThrowException(e); }
                    return;
                }

                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(player.NetId, (byte)RpcCalls.Shapeshift, SendOption.Reliable, clientId);
                writer.WriteNetObject(target);
                writer.Write(animate);
                AmongUsClient.Instance.FinishRpcImmediately(writer);
            }
            catch (Exception e) { ThrowException(e); }
        }

        public bool SyncGeneralOptions()
        {
            if (!AmongUsClient.Instance.AmHost || !GameStates.IsInGame || !DoRPC) return false;

            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncGeneralOptions, SendOption.Reliable);
            writer.Write(player.PlayerId);
            writer.WritePacked((int)player.GetCustomRole());
            writer.Write(Main.PlayerStates[player.PlayerId].IsDead);
            writer.WritePacked((int)Main.PlayerStates[player.PlayerId].deathReason);
            writer.Write(Main.AllPlayerKillCooldown[player.PlayerId]);
            writer.Write(Main.AllPlayerSpeed[player.PlayerId]);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        
            return true;
        }

        public bool HasDesyncRole()
        {
            return player.Is(CustomRoles.Bloodlust) || player.GetCustomRole().IsDesyncRole();
        }

        public bool IsInsideMap()
        {
            if (!player) return false;

            var results = new Collider2D[10];
            int overlapPointNonAlloc = Physics2D.OverlapPointNonAlloc(player.Pos(), results, Constants.ShipOnlyMask);
            PlainShipRoom room = player.GetPlainShipRoom();
            Vector2 pos = player.Pos();

            return Main.CurrentMap switch
            {
                MapNames.Fungle when overlapPointNonAlloc >= 2 => true,
                MapNames.MiraHQ when overlapPointNonAlloc >= 1 => true,
                MapNames.MiraHQ when room && room.RoomId is SystemTypes.MedBay or SystemTypes.Comms => true,
                MapNames.Airship when overlapPointNonAlloc >= 1 => true,
                MapNames.Skeld or MapNames.Dleks when room => true,
                MapNames.Polus when overlapPointNonAlloc >= 1 => true,
                MapNames.Polus when pos.y is >= -26.11f and <= -6.41f && pos.x is >= 3.56f and <= 32.68f => true,
                (MapNames)6 => true,
                _ => false
            };
        }

        public void KillFlash()
        {
            if (GameStates.IsLobby || !player) return;

            // Kill flash (blackout + reactor flash) processing

            SystemTypes systemtypes = Main.CurrentMap switch
            {
                MapNames.Polus => SystemTypes.Laboratory,
                MapNames.Airship => SystemTypes.HeliSabotage,
                _ => SystemTypes.Reactor
            };

            bool reactorCheck = IsActive(systemtypes); // Checking whether the reactor sabotage is active

            float duration = Options.KillFlashDuration.GetFloat();
            if (reactorCheck) duration += 0.2f; // Extend blackout during reactor

            // Execution
            if (reactorCheck && !player.IsModdedClient())
            {
                Main.PlayerStates[player.PlayerId].IsBlackOut = true; // Blackout

                LateTask.New(() =>
                {
                    Main.PlayerStates[player.PlayerId].IsBlackOut = false; // Cancel blackout
                    player.MarkDirtySettings();
                }, duration, "RemoveKillFlash");
            }

            if (player.AmOwner)
            {
                FlashColor(new(1f, 0f, 0f, 0.3f));
                if (Constants.ShouldPlaySfx()) RPC.PlaySound(player.PlayerId, Sounds.KillSound);
            }
            else if (player.IsModdedClient())
            {
                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.KillFlash, SendOption.Reliable, player.OwnerId);
                AmongUsClient.Instance.FinishRpcImmediately(writer);
            }
            else if (!reactorCheck) player.ReactorFlash(canBlind: false); // Reactor flash

            player.MarkDirtySettings();
        }

        public void RpcGuardAndKill(PlayerControl target = null, bool forObserver = false, bool fromSetKCD = false)
        {
            if (!AmongUsClient.Instance.AmHost)
            {
                StackFrame caller = new(1, false);
                MethodBase callerMethod = caller.GetMethod();
                string callerMethodName = callerMethod?.Name;
                string callerClassName = callerMethod?.DeclaringType?.FullName;
                Logger.Warn($"Modded non-host client activated RpcGuardAndKill from {callerClassName}.{callerMethodName}", "RpcGuardAndKill");
                return;
            }

            if (!target) target = player;

            // Check Observer
            if (!forObserver && !MeetingStates.FirstMeeting) Main.EnumeratePlayerControls().Where(x => x.Is(CustomRoles.Observer) && player.PlayerId != x.PlayerId).Do(x => x.RpcGuardAndKill(target, true));

            // Host
            if (player.AmOwner) player.MurderPlayer(target, MurderResultFlags.FailedProtected);

            // Other Clients
            else
            {
                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(player.NetId, (byte)RpcCalls.MurderPlayer, SendOption.Reliable, player.OwnerId);
                writer.WriteNetObject(target);
                writer.Write((int)MurderResultFlags.FailedProtected);
                AmongUsClient.Instance.FinishRpcImmediately(writer);

                if (Options.CurrentGameMode == CustomGameMode.Standard && !MeetingStates.FirstMeeting && !AntiBlackout.SkipTasks && !ExileController.Instance && GameStates.IsInTask && player.IsBeginner() && Main.GotShieldAnimationInfoThisGame.Add(player.PlayerId))
                    player.Notify(GetString("PleaseStopBeingDumb"), 10f);
            }

            if (!fromSetKCD) player.AddKillTimerToDict(true);
        }

        public bool HasAbilityCD()
        {
            return Main.AbilityCD.ContainsKey(player.PlayerId);
        }

        public void AddKCDAsAbilityCD()
        {
            player.AddAbilityCD((int)Math.Round(Main.AllPlayerKillCooldown.TryGetValue(player.PlayerId, out float kcd) ? kcd : Options.AdjustedDefaultKillCooldown));
        }

        public void AddAbilityCD(bool includeDuration = true)
        {
            Utils.AddAbilityCD(player.GetCustomRole(), player.PlayerId, includeDuration);
        }

        public void AddAbilityCD(int cd)
        {
            Main.AbilityCD[player.PlayerId] = (TimeStamp, cd);
            SendRPC(CustomRPC.SyncAbilityCD, 1, player.PlayerId, cd);
        }

        public void RemoveAbilityCD()
        {
            if (Main.AbilityCD.Remove(player.PlayerId)) SendRPC(CustomRPC.SyncAbilityCD, 3, player.PlayerId);
        }

        public float GetAbilityUseLimit()
        {
            return Main.AbilityUseLimit.GetValueOrDefault(player.PlayerId, float.NaN);
        }

        public void RpcRemoveAbilityUse(bool log = true)
        {
            float current = player.GetAbilityUseLimit();
            if (float.IsNaN(current) || current <= 0f) return;

            player.SetAbilityUseLimit(current - 1, log: log);
        }

        public void RpcIncreaseAbilityUseLimitBy(float get, bool log = true)
        {
            float current = player.GetAbilityUseLimit();
            if (float.IsNaN(current)) return;

            player.SetAbilityUseLimit(current + get, log: log);
        }

        public void SetAbilityUseLimit(float limit, bool rpc = true, bool log = true)
        {
            player.PlayerId.SetAbilityUseLimit(limit, rpc, log);
        }

        public void Suicide(PlayerState.DeathReason deathReason = PlayerState.DeathReason.Suicide, PlayerControl realKiller = null)
        {
            if (!player.IsAlive() || player.Data.IsDead || !GameStates.IsInTask || ExileController.Instance) return;

            PlayerState state = Main.PlayerStates[player.PlayerId];

            switch (state.Role)
            {
                case SchrodingersCat cat when realKiller:
                    cat.OnCheckMurderAsTarget(realKiller, player);
                    return;
                case Veteran when Veteran.VeteranInProtect.Contains(player.PlayerId):
                case Pestilence:
                    return;
            }

            state.deathReason = deathReason;
            state.SetDead();

            Medic.IsDead(player);

            if (realKiller)
            {
                player.SetRealKiller(realKiller);

                if (realKiller.Is(CustomRoles.Damocles))
                    Damocles.OnMurder(realKiller.PlayerId);

                IncreaseAbilityUseLimitOnKill(realKiller);
            }

            player.Kill(player);

            if (Options.CurrentGameMode == CustomGameMode.NaturalDisasters)
                NaturalDisasters.RecordDeath(player, deathReason);
        }

        public void SetKillCooldown(float time = -1f, PlayerControl target = null, bool forceAnime = false)
        {
            if (!player) return;

            if (!Mathf.Approximately(time, -1f) && Commited.ReduceKCD.TryGetValue(player.PlayerId, out float reduction))
                time = Math.Max(time - reduction, 0.01f);

            Logger.Info($"{player.GetNameWithRole()}'s KCD set to {(time < 0f ? Main.AllPlayerKillCooldown[player.PlayerId] : time)}s", "SetKCD");

            if (player.GetCustomRole().UsesPetInsteadOfKill())
            {
                if (time < 0f)
                    player.AddKCDAsAbilityCD();
                else
                    player.AddAbilityCD((int)Math.Round(time));

                if (player.GetCustomRole() is not CustomRoles.Necromancer and not CustomRoles.Deathknight and not CustomRoles.Renegade and not CustomRoles.Sidekick) return;
            }

            if (!player.CanUseKillButton() && !AntiBlackout.SkipTasks && !IntroCutsceneDestroyPatch.PreventKill) return;

            player.AddKillTimerToDict(cd: time);
            if (!target) target = player;

            if (time >= 0f)
                Main.AllPlayerKillCooldown[player.PlayerId] = time * 2;
            else
                Main.AllPlayerKillCooldown[player.PlayerId] *= 2;

            if (player.Is(CustomRoles.Glitch) && Main.PlayerStates[player.PlayerId].Role is Glitch gc)
            {
                gc.LastKill = TimeStamp + ((int)(time / 2) - Glitch.KillCooldown.GetInt());
                gc.KCDTimer = (int)(time / 2);
            }
            else if (forceAnime || !player.IsModdedClient() || !Options.DisableShieldAnimations.GetBool())
            {
                player.SyncSettings();
                LateTask.New(() => player.RpcGuardAndKill(target, fromSetKCD: true), 0.1f, log: false);
            }
            else
            {
                time = Main.AllPlayerKillCooldown[player.PlayerId] / 2;

                if (player.AmOwner)
                    PlayerControl.LocalPlayer.SetKillTimer(time);
                else
                {
                    MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetKillTimer, SendOption.Reliable, player.OwnerId);
                    writer.Write(time);
                    AmongUsClient.Instance.FinishRpcImmediately(writer);
                }

                Main.EnumeratePlayerControls().Where(x => x.Is(CustomRoles.Observer) && target.PlayerId != x.PlayerId).Do(x => x.RpcGuardAndKill(target, true, true));
            }

            if (player.GetCustomRole() is not CustomRoles.Inhibitor and not CustomRoles.Saboteur)
                LateTask.New(() => player.ResetKillCooldown(), 0.3f, log: false);
        }

        public void RpcResetAbilityCooldown()
        {
            if (!AmongUsClient.Instance.AmHost) return;

            Logger.Info($"Reset Ability Cooldown for {player.name} (ID: {player.PlayerId})", "RpcResetAbilityCooldown");

            if (player.Is(CustomRoles.Glitch) && Main.PlayerStates[player.PlayerId].Role is Glitch gc)
            {
                gc.LastHack = TimeStamp;
                gc.HackCDTimer = 10;
            }
            else if (player.AmOwner)
            {
                // If target is host
                player.Data.Role.SetCooldown();
            }
            else if (player.IsModdedClient())
            {
                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.ResetAbilityCooldown, SendOption.Reliable, player.OwnerId);
                AmongUsClient.Instance.FinishRpcImmediately(writer);
            }
            else
            {
                // If target is not host
                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(player.NetId, (byte)RpcCalls.ProtectPlayer, SendOption.Reliable, player.OwnerId);
                writer.WriteNetObject(player);
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

        public void RpcDesyncRepairSystem(SystemTypes systemType, int amount)
        {
            MessageWriter messageWriter = AmongUsClient.Instance.StartRpcImmediately(ShipStatus.Instance.NetId, (byte)RpcCalls.UpdateSystem, SendOption.Reliable, player.OwnerId);
            messageWriter.Write((byte)systemType);
            messageWriter.WriteNetObject(player);
            messageWriter.Write((byte)amount);
            AmongUsClient.Instance.FinishRpcImmediately(messageWriter);
        }

        public void MarkDirtySettings()
        {
            PlayerGameOptionsSender.SetDirty(player.PlayerId);
        }

        public void SyncSettings()
        {
            PlayerGameOptionsSender.SendImmediately(player.PlayerId);
        }

        public TaskState GetTaskState()
        {
            return Main.PlayerStates.TryGetValue(player.PlayerId, out PlayerState state) ? state.TaskState : new();
        }

        public bool IsNonHostModdedClient()
        {
            return player.IsModdedClient() && !player.IsHost();
        }

        public string GetDisplayRoleName(PlayerControl target = null, bool pure = false, bool seeTargetBetrayalAddons = false)
        {
            if (!target) target = player;
            return Utils.GetDisplayRoleName(player.PlayerId, targetId: target.PlayerId, pure: pure, seeTargetBetrayalAddons: seeTargetBetrayalAddons);
        }

        public string GetSubRoleNames(bool forUser = false)
        {
            if (!Main.PlayerStates.TryGetValue(player.PlayerId, out PlayerState state)) return string.Empty;

            List<CustomRoles> subRoles = state.SubRoles;
            if (subRoles.Count == 0) return string.Empty;

            StringBuilder sb = new();

            foreach (CustomRoles role in subRoles)
            {
                if (role == CustomRoles.NotAssigned) continue;

                sb.Append($"{ColorString(Color.white, "\n<size=1>")}{GetRoleName(role, forUser)}");
            }

            return sb.ToString();
        }

        public string GetAllRoleName(bool forUser = true)
        {
            if (!player) return null;

            string text = GetRoleName(player.GetCustomRole(), forUser);
            text += player.GetSubRoleNames(forUser);
            return text;
        }

        public string GetNameWithRole(bool forUser = false)
        {
            try
            {
                bool addRoleName = GameStates.IsInGame && Options.CurrentGameMode is not CustomGameMode.FFA and not CustomGameMode.StopAndGo and not CustomGameMode.HotPotato and not CustomGameMode.Speedrun and not CustomGameMode.CaptureTheFlag and not CustomGameMode.NaturalDisasters and not CustomGameMode.RoomRush and not CustomGameMode.Quiz and not CustomGameMode.TheMindGame and not CustomGameMode.BedWars and not CustomGameMode.Deathrace and not CustomGameMode.Mingle and not CustomGameMode.Snowdown;
                return $"{player?.Data?.PlayerName}" + (addRoleName ? $" ({player?.GetAllRoleName(forUser).RemoveHtmlTags().Replace('\n', ' ')})" : string.Empty);
            }
            catch (Exception e)
            {
                ThrowException(e);
                return !player || !player.Data ? "Unknown Player" : player.Data.PlayerName;
            }
        }

        public string GetRoleColorCode()
        {
            return Utils.GetRoleColorCode(player.GetCustomRole());
        }

        public Color GetRoleColor()
        {
            return Utils.GetRoleColor(player.GetCustomRole());
        }

        public void FixBlackScreen()
        {
            if (!player || !AmongUsClient.Instance.AmHost) return;

            if (MeetingStates.FirstMeeting)
            {
                player.RpcSetRoleDesync(player.GetRoleTypes(), player.OwnerId);
                return;
            }
        
            if (player.IsModdedClient()) return;

            if (GameStates.IsMeeting || ExileController.Instance || AntiBlackout.SkipTasks || player.inVent || player.inMovingPlat || player.onLadder || !Main.EnumeratePlayerControls().FindFirst(x => !x.IsAlive(), out var dummyGhost))
            {
                if (BlackScreenWaitingPlayers.Add(player.PlayerId))
                    Main.Instance.StartCoroutine(Wait());

                return;

                IEnumerator Wait()
                {
                    Logger.Warn($"FixBlackScreen was called for {player.GetNameWithRole()}, but the conditions are not met to execute this code right now, waiting until it becomes possible to do so", "FixBlackScreen");

                    while (GameStates.InGame && !GameStates.IsEnded && !CancelBlackScreenFix.Contains(player.PlayerId) && (GameStates.IsMeeting || ExileController.Instance || AntiBlackout.SkipTasks || Main.EnumeratePlayerControls().All(x => x.IsAlive())))
                        yield return null;

                    if (CancelBlackScreenFix.Remove(player.PlayerId))
                    {
                        Logger.Msg($"The black screen fix was canceled for {player.GetNameWithRole()}", "FixBlackScreen");
                        BlackScreenWaitingPlayers.Remove(player.PlayerId);
                        yield break;
                    }

                    if (!GameStates.InGame || GameStates.IsEnded)
                    {
                        Logger.Msg($"During the waiting, the game ended, so the black screen fix will not be executed for {player.GetNameWithRole()}", "FixBlackScreen");
                        BlackScreenWaitingPlayers.Remove(player.PlayerId);
                        yield break;
                    }

                    yield return new WaitForSecondsRealtime(player.IsAlive() ? 1f : 3f);

                    if (!GameStates.InGame || GameStates.IsEnded)
                    {
                        Logger.Msg($"During the waiting, the game ended, so the black screen fix will not be executed for {player.GetNameWithRole()}", "FixBlackScreen");
                        BlackScreenWaitingPlayers.Remove(player.PlayerId);
                        yield break;
                    }

                    Logger.Msg($"Now that the conditions are met, fixing black screen for {player.GetNameWithRole()}", "FixBlackScreen");
                    BlackScreenWaitingPlayers.Remove(player.PlayerId);
                    player.FixBlackScreen();
                }
            }

            SystemTypes systemtype = Main.CurrentMap switch
            {
                MapNames.Polus => SystemTypes.Laboratory,
                MapNames.Airship => SystemTypes.HeliSabotage,
                _ => SystemTypes.Reactor
            };

            var sender = CustomRpcSender.Create($"Fix Black Screen For {player.GetNameWithRole()}", SendOption.Reliable);

            sender.RpcDesyncRepairSystem(player, systemtype, 128);

            int targetClientId = player.OwnerId;
            var ghostPos = dummyGhost.Pos();
            var pcPos = player.Pos();
            var timer = Math.Max(Main.KillTimers[player.PlayerId], 0.1f);

            if (player.IsAlive())
            {
                CheckInvalidMovementPatch.ExemptedPlayers.UnionWith([player.PlayerId, dummyGhost.PlayerId]);
                AFKDetector.TempIgnoredPlayers.UnionWith([player.PlayerId, dummyGhost.PlayerId]);
                LateTask.New(() => AFKDetector.TempIgnoredPlayers.ExceptWith([player.PlayerId, dummyGhost.PlayerId]), 3f, log: false);

                var murderPos = Pelican.GetBlackRoomPS();

                sender.TP(player, murderPos, noCheckState: true);

                sender.AutoStartRpc(player.NetId, 12, targetClientId);
                sender.WriteNetObject(dummyGhost);
                sender.Write((int)MurderResultFlags.Succeeded);
                sender.EndRpc();

                dummyGhost.NetTransform.SnapTo(murderPos, (ushort)(dummyGhost.NetTransform.lastSequenceId + 328));
                dummyGhost.NetTransform.SetDirtyBit(uint.MaxValue);

                sender.AutoStartRpc(dummyGhost.NetTransform.NetId, 21);
                sender.WriteVector2(murderPos);
                sender.Write((ushort)(dummyGhost.NetTransform.lastSequenceId + 8));
            }
            else
            {
                sender.AutoStartRpc(player.NetId, 12, targetClientId);
                sender.WriteNetObject(player);
                sender.Write((int)MurderResultFlags.Succeeded);
            }

            sender.EndRpc();

            sender.SendMessage();

            LateTask.New(() =>
            {
                sender = CustomRpcSender.Create($"Fix Black Screen For {player.GetNameWithRole()} (2)", SendOption.Reliable);

                sender.RpcDesyncRepairSystem(player, systemtype, 16);
                if (systemtype == SystemTypes.HeliSabotage) sender.RpcDesyncRepairSystem(player, systemtype, 17);

                if (player.IsAlive())
                {
                    sender.TP(player, pcPos, noCheckState: true);
                    sender.SetKillCooldown(player, timer);
                    sender.Notify(player, GetString("BlackScreenFixCompleteNotify"));

                    dummyGhost.NetTransform.SnapTo(ghostPos, (ushort)(dummyGhost.NetTransform.lastSequenceId + 328));
                    dummyGhost.NetTransform.SetDirtyBit(uint.MaxValue);

                    sender.AutoStartRpc(dummyGhost.NetTransform.NetId, 21);
                    sender.WriteVector2(ghostPos);
                    sender.Write((ushort)(dummyGhost.NetTransform.lastSequenceId + 8));
                    sender.EndRpc();
                }

                sender.SendMessage();
            }, 1f + (AmongUsClient.Instance.Ping / 1000f), log: false);
        }

        public void ReactorFlash(float delay = 0f, float flashDuration = float.NaN, bool canBlind = true)
        {
            if (!player) return;

            Logger.Info($"Reactor Flash for {player.GetNameWithRole()}", "ReactorFlash");

            SystemTypes systemtypes = Main.CurrentMap switch
            {
                MapNames.Polus => SystemTypes.Laboratory,
                MapNames.Airship => SystemTypes.HeliSabotage,
                _ => SystemTypes.Reactor
            };

            if (canBlind && IsActive(systemtypes))
            {
                Main.PlayerStates[player.PlayerId].IsBlackOut = true;
                player.MarkDirtySettings();

                LateTask.New(() =>
                {
                    Main.PlayerStates[player.PlayerId].IsBlackOut = false;
                    player.MarkDirtySettings();
                }, (float.IsNaN(flashDuration) ? Options.KillFlashDuration.GetFloat() : flashDuration) + delay, "Fix BlackOut Reactor Flash");

                return;
            }

            player.RpcDesyncRepairSystem(systemtypes, 128);

            LateTask.New(() =>
            {
                player.RpcDesyncRepairSystem(systemtypes, 16);

                if (Main.NormalOptions.MapId == 4) // on Airship
                    player.RpcDesyncRepairSystem(systemtypes, 17);
            }, (float.IsNaN(flashDuration) ? Options.KillFlashDuration.GetFloat() : flashDuration) + delay, "Fix Desync Reactor");
        }

        public string GetRealName(bool isMeeting = false)
        {
            try
            {
                string name = isMeeting ? player.Data.PlayerName : player.name;
                return name.RemoveHtmlTags();
            }
            catch (NullReferenceException nullReferenceException)
            {
                Logger.Error($"{nullReferenceException.Message} - player is null? {!player}", "GetRealName");
                return string.Empty;
            }
            catch (Exception exception)
            {
                ThrowException(exception);
                return string.Empty;
            }
        }

        public bool IsRoleBlocked()
        {
            return RoleBlockManager.RoleBlockedPlayers.ContainsKey(player.PlayerId);
        }

        public void BlockRole(float duration)
        {
            RoleBlockManager.AddRoleBlock(player, duration);
        }

        public bool HasKillButton()
        {
            CustomRoles role = player.GetCustomRole();
            if (player.Data.Role.Role == RoleTypes.GuardianAngel) return false;
            if (role.GetVNRole(true) is CustomRoles.Impostor or CustomRoles.ImpostorEHR or CustomRoles.Shapeshifter or CustomRoles.ShapeshifterEHR or CustomRoles.Phantom or CustomRoles.PhantomEHR or CustomRoles.Viper or CustomRoles.ViperEHR) return true;
            if (player.GetRoleTypes() is RoleTypes.Impostor or RoleTypes.Shapeshifter or RoleTypes.Phantom) return true;
            if (player.Is(CustomRoles.Bloodlust)) return true;
            return !HasTasks(player.Data, false);
        }

        public bool CanUseKillButton()
        {
            if (AntiBlackout.SkipTasks || TimeMaster.Rewinding || !Main.IntroDestroyed || IntroCutsceneDestroyPatch.PreventKill || !player.IsAlive()) return false;

            switch (Options.CurrentGameMode)
            {
                case CustomGameMode.StopAndGo or CustomGameMode.NaturalDisasters or CustomGameMode.RoomRush or CustomGameMode.TheMindGame or CustomGameMode.Mingle:
                case CustomGameMode.Speedrun when !Speedrun.CanKill.Contains(player.PlayerId):
                    return false;
                case CustomGameMode.HotPotato:
                    return HotPotato.CanPassViaKillButton && HotPotato.GetState().HolderID == player.PlayerId;
                case CustomGameMode.Quiz:
                    return Quiz.AllowKills;
                case CustomGameMode.KingOfTheZones:
                case CustomGameMode.CaptureTheFlag:
                case CustomGameMode.BedWars:
                case CustomGameMode.Deathrace:
                case CustomGameMode.Snowdown:
                    return true;
            }

            if (Mastermind.ManipulatedPlayers.ContainsKey(player.PlayerId)) return true;
            if (Penguin.IsVictim(player)) return false;
            if (Pelican.IsEaten(player.PlayerId)) return false;
            if (player.Data.Role.Role == RoleTypes.GuardianAngel) return false;
            if (player.Is(CustomRoles.Bloodlust)) return true;

            return player.GetCustomRole() switch
            {
                // Solo PVP
                CustomRoles.Challenger => player.SoloAlive(),
                // FFA
                CustomRoles.Killer => true,
                // Stop And Go
                CustomRoles.Tasker => false,
                // Hot Potato
                CustomRoles.Potato => HotPotato.CanPassViaKillButton && HotPotato.GetState().HolderID == player.PlayerId,
                // Speedrun
                CustomRoles.Runner => Speedrun.CanKill.Contains(player.PlayerId),
                // Quiz
                CustomRoles.QuizPlayer => Quiz.AllowKills,
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

                _ => Main.PlayerStates.TryGetValue(player.PlayerId, out PlayerState state) && state.Role.CanUseKillButton(player)
            };
        }

        public bool CanUseImpostorVentButton()
        {
            if (!Main.IntroDestroyed || !player.IsAlive() || player.Data.Role.Role == RoleTypes.GuardianAngel || Penguin.IsVictim(player)) return false;

            if (player.GetRoleTypes() == RoleTypes.Engineer) return false;

            return Options.CurrentGameMode switch
            {
                CustomGameMode.SoloPVP => SoloPVP.CanVent,
                CustomGameMode.FFA => !(FreeForAll.FFADisableVentingWhenKcdIsUp.GetBool() && Main.KillTimers.GetValueOrDefault(player.PlayerId) <= 0) && !(FreeForAll.FFADisableVentingWhenTwoPlayersAlive.GetBool() && Main.AllAlivePlayerControls.Count <= 2),
                CustomGameMode.StopAndGo => false,
                CustomGameMode.HotPotato => false,
                CustomGameMode.Speedrun => false,
                CustomGameMode.CaptureTheFlag => false,
                CustomGameMode.NaturalDisasters => false,
                CustomGameMode.RoomRush => false,
                CustomGameMode.Quiz => false,
                CustomGameMode.TheMindGame => false,
                CustomGameMode.Mingle => false,
                CustomGameMode.Snowdown => true,
                CustomGameMode.Deathrace => Deathrace.CanUseVent(player, player.GetClosestVent().Id),

                CustomGameMode.Standard when CopyCat.PlayerIdList.Contains(player.PlayerId) => true,
                CustomGameMode.Standard when player.Is(CustomRoles.Nimble) || Options.EveryoneCanVent.GetBool() => true,
                CustomGameMode.Standard when player.Is(CustomRoles.Bloodlust) || player.Is(CustomRoles.Renegade) => true,

                _ => Main.PlayerStates.TryGetValue(player.PlayerId, out PlayerState state) && state.Role.CanUseImpostorVentButton(player)
            };
        }

/*
        public bool CanUseSabotage()
        {
            if (!player.IsAlive() || player.Data.Role.Role == RoleTypes.GuardianAngel) return false;
            return Main.PlayerStates.TryGetValue(player.PlayerId, out PlayerState state) && state.Role.CanUseSabotage(player);
        }
*/

        public RoleTypes GetGhostRoleBasis()
        {
            RoleTypes roleType;
        
            if (GhostRolesManager.AssignedGhostRoles.TryGetValue(player.PlayerId, out var ghostRole))
                roleType = ghostRole.Instance.RoleTypes;
            else if (GhostRolesManager.ShouldHaveGhostRole(player))
                roleType = RoleTypes.GuardianAngel;
            else if (!(player.Is(CustomRoleTypes.Impostor) && Options.DeadImpCantSabotage.GetBool()) && Main.PlayerStates.TryGetValue(player.PlayerId, out var state) && state.Role.CanUseSabotage(player))
                roleType = RoleTypes.ImpostorGhost;
            else
                roleType = RoleTypes.CrewmateGhost;

            return roleType;
        }

        public Vector2 Pos()
        {
            if (player.AmOwner) return player.transform.position;

            try
            {
                var queue = player.NetTransform.incomingPosQueue;
        
                if (queue.Count > 0 && player.NetTransform.isActiveAndEnabled && !player.NetTransform.isPaused)
                {        
                    var array = queue._array;
                    int tail = queue._tail;
                    int index = (tail - 1 + array.Length) % array.Length; // handle wrap-around
                    return array[index];
                }
            }
            catch (Exception e) { ThrowException(e); }
        
            return player.transform.position;
        }

        // Next 5: https://github.com/Rabek009/MoreGamemodes/blob/master/Modules/ExtendedPlayerControl.cs

        public void MakeInvisible()
        {
            player.invisibilityAlpha = player.AmOwner ? 0.5f : PlayerControl.LocalPlayer.Data.Role.IsDead ? 0.5f : 0f;
            player.cosmetics.SetPhantomRoleAlpha(player.invisibilityAlpha);

            if (!player.AmOwner && !PlayerControl.LocalPlayer.Data.Role.IsDead)
            {
                player.shouldAppearInvisible = true;
                player.Visible = false;
            }
        }

        public void MakeVisible()
        {
            if (!player.AmOwner)
            {
                player.shouldAppearInvisible = false;
                player.Visible = true;
            }

            player.invisibilityAlpha = 1f;
            player.cosmetics.SetPhantomRoleAlpha(player.invisibilityAlpha);

            if (!player.AmOwner)
            {
                player.shouldAppearInvisible = false;
                player.Visible = !player.inVent;
            }
        }

        public void RpcMakeInvisible(bool phantom = false)
        {
            if (!AmongUsClient.Instance.AmHost) return;
            if (phantom && Options.CurrentGameMode != CustomGameMode.Standard) return;
            if (!Main.Invisible.Add(player.PlayerId)) return;
        
            player.RpcSetPet("");
        
            if (!(phantom && PlayerControl.LocalPlayer.IsImpostor()))
                player.MakeInvisible();

            if (!phantom)
            {
                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(player.NetId, (byte)CustomRPC.Invisibility, SendOption.Reliable);
                writer.WritePacked(1);
                AmongUsClient.Instance.FinishRpcImmediately(writer);
                NotifyRoles(SpecifySeer: player, SpecifyTarget: player, SendOption: SendOption.None);
            }
            else
            {
                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(player.NetId, (byte)CustomRPC.Invisibility, SendOption.Reliable);
                writer.WritePacked(11);
                AmongUsClient.Instance.FinishRpcImmediately(writer);
                NotifyRoles(SpecifyTarget: player);
            }

            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
            {
                if (pc.AmOwner || pc == player || (!phantom && pc.IsModdedClient()) || (phantom && pc.IsImpostor())) continue;
            
                var sender = CustomRpcSender.Create("RpcMakeInvisible", SendOption.Reliable);
                sender.StartMessage(pc.OwnerId);
                sender.StartRpc(player.NetTransform.NetId, RpcCalls.SnapTo)
                    .WriteVector2(new Vector2(50f, 50f))
                    .Write(player.NetTransform.lastSequenceId)
                    .EndRpc();
                sender.StartRpc(player.NetTransform.NetId, RpcCalls.SnapTo)
                    .WriteVector2(new Vector2(50f, 50f))
                    .Write((ushort)(player.NetTransform.lastSequenceId + 16383))
                    .EndRpc();
                sender.EndMessage();
                sender.SendMessage();

                NumSnapToCallsThisRound += 2;
            }

            Logger.Info($"Made {player.GetNameWithRole()} invisible", "RpcMakeInvisible");
        }

        public void RpcMakeVisible(bool phantom = false)
        {
            if (!AmongUsClient.Instance.AmHost) return;
            if (phantom && Options.CurrentGameMode != CustomGameMode.Standard) return;
            if (!Main.Invisible.Remove(player.PlayerId)) return;
        
            if (Options.UsePets.GetBool()) PetsHelper.SetPet(player, PetsHelper.GetPetId());
        
            if (!(phantom && PlayerControl.LocalPlayer.IsImpostor()))
                player.MakeVisible();

            if (!phantom)
            {
                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(player.NetId, (byte)CustomRPC.Invisibility, SendOption.Reliable);
                writer.WritePacked(0);
                AmongUsClient.Instance.FinishRpcImmediately(writer);
                NotifyRoles(SpecifySeer: player, SpecifyTarget: player);
            }
            else
            {
                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(player.NetId, (byte)CustomRPC.Invisibility, SendOption.Reliable);
                writer.WritePacked(10);
                AmongUsClient.Instance.FinishRpcImmediately(writer);
                NotifyRoles(SpecifyTarget: player);
            }

            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
            {
                if (pc.AmOwner || pc == player || (!phantom && pc.IsModdedClient()) || (phantom && pc.IsImpostor())) continue;
            
                var sender = CustomRpcSender.Create("RpcMakeVisible", SendOption.Reliable);
                sender.StartMessage(pc.OwnerId);
                sender.StartRpc(player.NetTransform.NetId, RpcCalls.SnapTo)
                    .WriteVector2(new Vector2(50f, 50f))
                    .Write((ushort)(player.NetTransform.lastSequenceId + 32767))
                    .EndRpc();
                sender.StartRpc(player.NetTransform.NetId, RpcCalls.SnapTo)
                    .WriteVector2(new Vector2(50f, 50f))
                    .Write((ushort)(player.NetTransform.lastSequenceId + 32767 + 16383))
                    .EndRpc();
                sender.StartRpc(player.NetTransform.NetId, RpcCalls.SnapTo)
                    .WriteVector2(player.transform.position)
                    .Write(player.NetTransform.lastSequenceId)
                    .EndRpc();
                sender.EndMessage();
                sender.SendMessage();

                NumSnapToCallsThisRound += 3;
            }

            Logger.Info($"Made {player.GetNameWithRole()} visible", "RpcMakeVisible");
        }

        public void RpcResetInvisibility(bool phantom = false)
        {
            if (!AmongUsClient.Instance.AmHost) return;
            if (!Main.Invisible.Contains(player.PlayerId)) return;
            if (phantom && Options.CurrentGameMode != CustomGameMode.Standard) return;

            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
            {
                if (pc.AmOwner || pc == player || (!phantom && pc.IsModdedClient()) || (phantom && pc.IsImpostor())) continue;
            
                var sender = CustomRpcSender.Create("RpcResetInvisibility", SendOption.Reliable);
                sender.StartMessage(pc.OwnerId);
                sender.RpcExiled(player, autoStartRpc: false, exileForHost: false);
                RoleTypes role = Utils.GetRoleMap(pc.PlayerId, player.PlayerId).RoleType;
                sender.StartRpc(player.NetId, RpcCalls.SetRole)
                    .Write((ushort)role)
                    .Write(true)
                    .EndRpc();
                sender.StartRpc(player.NetId, RpcCalls.CancelPet)
                    .EndRpc();
                sender.StartRpc(player.NetTransform.NetId, RpcCalls.SnapTo)
                    .WriteVector2(new Vector2(50f, 50f))
                    .Write((ushort)(player.NetTransform.lastSequenceId + 32767))
                    .EndRpc();
                sender.StartRpc(player.NetTransform.NetId, RpcCalls.SnapTo)
                    .WriteVector2(new Vector2(50f, 50f))
                    .Write((ushort)(player.NetTransform.lastSequenceId + 32767 + 16383))
                    .EndRpc();
                sender.StartRpc(player.NetTransform.NetId, RpcCalls.SnapTo)
                    .WriteVector2(new Vector2(50f, 50f))
                    .Write(player.NetTransform.lastSequenceId)
                    .EndRpc();
                sender.StartRpc(player.NetTransform.NetId, RpcCalls.SnapTo)
                    .WriteVector2(new Vector2(50f, 50f))
                    .Write((ushort)(player.NetTransform.lastSequenceId + 16383))
                    .EndRpc();
                sender.EndMessage();
                sender.SendMessage();

                NumSnapToCallsThisRound += 4;
            }

            Logger.Info($"Reset invisibility for {player.GetNameWithRole()}", "RpcResetInvisibility");
        }

        public void AddKillTimerToDict(bool half = false, float cd = -1f)
        {
            float resultKCD;

            if (Math.Abs(cd - -1f) < 0.5f)
            {
                resultKCD = Main.AllPlayerKillCooldown.GetValueOrDefault(player.PlayerId, 0f);

                if (half) resultKCD /= 2f;
            }
            else
                resultKCD = cd;

            if (player.GetCustomRole().UsesPetInsteadOfKill() && resultKCD > 0f)
                player.AddAbilityCD((int)Math.Round(resultKCD));

            if (Main.KillTimers.TryGetValue(player.PlayerId, out float timer) && timer > resultKCD) return;

            Main.KillTimers[player.PlayerId] = resultKCD;
        }

        public bool IsDousedPlayer(PlayerControl target)
        {
            if (!player || !target) return false;

            Arsonist.IsDoused.TryGetValue((player.PlayerId, target.PlayerId), out bool isDoused);
            return isDoused;
        }

        public bool IsDrawPlayer(PlayerControl target)
        {
            if (!player || !target) return false;

            Revolutionist.IsDraw.TryGetValue((player.PlayerId, target.PlayerId), out bool isDraw);
            return isDraw;
        }

        public bool IsRevealedPlayer(PlayerControl target)
        {
            if (!player || !target) return false;

            Investigator.IsRevealed.TryGetValue((player.PlayerId, target.PlayerId), out bool isDoused);
            return isDoused;
        }

        public void RpcSetDousedPlayer(PlayerControl target, bool isDoused)
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetDousedPlayer, SendOption.Reliable);
            writer.Write(player.PlayerId);
            writer.Write(target.PlayerId);
            writer.Write(isDoused);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public void RpcSetDrawPlayer(PlayerControl target, bool isDoused)
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetDrawPlayer, SendOption.Reliable);
            writer.Write(player.PlayerId);
            writer.Write(target.PlayerId);
            writer.Write(isDoused);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public void RpcSetRevealtPlayer(PlayerControl target, bool isDoused)
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetRevealedPlayer, SendOption.Reliable);
            writer.Write(player.PlayerId);
            writer.Write(target.PlayerId);
            writer.Write(isDoused);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public bool IsShifted()
        {
            return Main.CheckShapeshift.TryGetValue(player.PlayerId, out bool shifted) && shifted;
        }

        public bool HasSubRole()
        {
            return Main.PlayerStates[player.PlayerId].SubRoles.Count > 0;
        }

        public bool HasEvilAddon()
        {
            return Main.PlayerStates[player.PlayerId].SubRoles.Any(x => x.IsEvilAddon());
        }

        /// <summary>
        /// Sets a players kill cooldown without syncing settings.
        /// When using this method, ResetKillCooldown should set the player's kill cooldown to twice the value.
        /// </summary>
        public void SetKillCooldownNonSync(float kcd)
        {
            if (player.AmOwner)
            {
                player.SetKillTimer(kcd);
            }
            else if (player.IsModdedClient())
            {
                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetKillTimer, SendOption.Reliable, player.OwnerId);
                writer.Write(kcd);
                AmongUsClient.Instance.FinishRpcImmediately(writer);
            }
            else
            {
                // When using this method, ResetKillCooldown should set the player's kill cooldown to twice the value.
                // MurderPlayer RPC with FailedProtected flag sets a player's kill cooldown to half the set value.
                // *2 /2 = the value we began with. This avoids syncing settings on every CheckMurder.
                // This works if the kill cooldown is consistent throughout the entire game.
                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(player.NetId, (byte)RpcCalls.MurderPlayer, SendOption.Reliable, player.OwnerId);
                writer.WriteNetObject(player);
                writer.Write((int)MurderResultFlags.FailedProtected);
                AmongUsClient.Instance.FinishRpcImmediately(writer);
            }
        }

        public void ResetKillCooldown(bool sync = true)
        {
            Main.PlayerStates[player.PlayerId].Role.SetKillCooldown(player.PlayerId);

            Main.AllPlayerKillCooldown[player.PlayerId] = player.GetCustomRole() switch
            {
                CustomRoles.Challenger => SoloPVP.SoloPVP_ATKCooldown.GetFloat(),
                CustomRoles.Killer => FreeForAll.FFAKcd.GetFloat(),
                CustomRoles.Runner => Speedrun.KCD,
                CustomRoles.Potato => 2f,
                CustomRoles.CTFPlayer => CaptureTheFlag.KCD * 2f,
                CustomRoles.KOTZPlayer => KingOfTheZones.GetKillCooldown(player),
                CustomRoles.QuizPlayer => 3f,
                CustomRoles.BedWarsPlayer => 1f,
                CustomRoles.Racer => 3f,
                CustomRoles.SnowdownPlayer => 10f,
                _ when player.Is(CustomRoles.Underdog) => Main.AllAlivePlayerControls.Count <= Underdog.UnderdogMaximumPlayersNeededToKill.GetInt() ? Underdog.UnderdogKillCooldownWithLessPlayersAlive.GetInt() : Underdog.UnderdogKillCooldownWithMorePlayersAlive.GetInt(),
                _ => Main.AllPlayerKillCooldown[player.PlayerId]
            };

            if (player.PlayerId == LastImpostor.CurrentId) LastImpostor.SetKillCooldown();

            if (player.Is(CustomRoles.Mare))
            {
                if (IsActive(SystemTypes.Electrical))
                    Main.AllPlayerKillCooldown[player.PlayerId] = Options.MareKillCD.GetFloat();
                else
                    Main.AllPlayerKillCooldown[player.PlayerId] = Options.MareKillCDNormally.GetFloat();
            }

            if (player.Is(CustomRoles.Bloodlust)) Main.AllPlayerKillCooldown[player.PlayerId] = Bloodlust.KCD.GetFloat();

            if (Main.KilledDiseased.TryGetValue(player.PlayerId, out int value))
            {
                Main.AllPlayerKillCooldown[player.PlayerId] += value * Options.DiseasedCDOpt.GetFloat();
                Logger.Info($"KCD of player set to {Main.AllPlayerKillCooldown[player.PlayerId]}", "Diseased");
            }

            if (Main.KilledAntidote.TryGetValue(player.PlayerId, out int value1))
            {
                float kcd = Main.AllPlayerKillCooldown[player.PlayerId] - (value1 * Options.AntidoteCDOpt.GetFloat());
                if (kcd < 0) kcd = 0;

                Main.AllPlayerKillCooldown[player.PlayerId] = kcd;
                Logger.Info($"KCD of player set to {Main.AllPlayerKillCooldown[player.PlayerId]}", "Antidote");
            }

            if (Commited.ReduceKCD.TryGetValue(player.PlayerId, out float reduction))
                Main.AllPlayerKillCooldown[player.PlayerId] -= reduction;

            if (sync) player.SyncSettings();
        }

        public void BeartrapKilled(PlayerControl target)
        {
            Logger.Info($"{target?.Data?.PlayerName} was Beartrap", "Beartrap");
            float tmpSpeed = Main.AllPlayerSpeed[player.PlayerId];
            Main.AllPlayerSpeed[player.PlayerId] = Main.MinSpeed;
            ReportDeadBodyPatch.CanReport[player.PlayerId] = false;
            player.MarkDirtySettings();

            LateTask.New(() =>
            {
                Main.AllPlayerSpeed[player.PlayerId] = tmpSpeed;
                ReportDeadBodyPatch.CanReport[player.PlayerId] = true;
                player.MarkDirtySettings();
                RPC.PlaySoundRPC(player.PlayerId, Sounds.TaskComplete);
            }, Options.BeartrapBlockMoveTime.GetFloat(), "Beartrap BlockMove");

            if (player.AmOwner)
                Achievements.Type.TooCold.CompleteAfterGameEnd();
        }

        public bool IsDouseDone()
        {
            if (!player.Is(CustomRoles.Arsonist)) return false;

            (int, int) count = GetDousedPlayerCount(player.PlayerId);
            return count.Item1 >= count.Item2;
        }

        public bool IsDrawDone() // Determine whether the conditions to win are met
        {
            if (!player.Is(CustomRoles.Revolutionist)) return false;

            (int, int) count = GetDrawPlayerCount(player.PlayerId, out _);
            return count.Item1 >= count.Item2;
        }

        public void RpcExiled()
        {
            player.Exiled();
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(player.NetId, (byte)RpcCalls.Exiled, SendOption.Reliable);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public void RpcExileV2()
        {
            player.RpcExiled();
            FixedUpdatePatch.LoversSuicide(player.PlayerId);
        }

        public (Vector2 Location, string RoomName) GetPositionInfo()
        {
            PlainShipRoom room = player.GetPlainShipRoom();
            string roomName = GetString(!room ? "Outside" : $"{room.RoomId}");
            Vector2 pos = player.Pos();
            return (pos, roomName);
        }

        public bool TP(PlayerControl target, bool noCheckState = false, bool log = true)
        {
            return Utils.TP(player.NetTransform, target.Pos(), noCheckState, log);
        }

        public bool TP(Vector2 location, bool noCheckState = false, bool log = true)
        {
            return Utils.TP(player.NetTransform, location, noCheckState, log);
        }

        public bool TPToRandomVent(bool log = true)
        {
            return Utils.TPToRandomVent(player.NetTransform, log);
        }

        public void Kill(PlayerControl target)
        {
            if (!AmongUsClient.Instance.AmHost)
            {
                StackFrame caller = new(1, false);
                MethodBase callerMethod = caller.GetMethod();
                string callerMethodName = callerMethod?.Name;
                string callerClassName = callerMethod?.DeclaringType?.FullName;
                Logger.Warn($"Modded non-host client activated Kill from {callerClassName}.{callerMethodName}", "Kill");
                return;
            }
        
            if (Options.CurrentGameMode == CustomGameMode.SoloPVP || GameStates.IsLobby || !GameStates.InGame || !Main.IntroDestroyed) return;

            if (!target) target = player;

            CheckAndSpawnAdditionalRenegade(target.Data);

            if (target.GetTeam() is Team.Impostor or Team.Neutral) Stressed.OnNonCrewmateDead();

            if (player.Is(CustomRoles.Damocles))
                Damocles.OnMurder(player.PlayerId);
            else if (player.Is(Team.Impostor))
                Damocles.OnOtherImpostorMurder();
            else if (target.Is(Team.Impostor)) Damocles.OnImpostorDeath();

            if (player.Is(CustomRoles.Bloodlust))
                FixedUpdatePatch.AddExtraAbilityUsesOnFinishedTasks(player);
            else
                IncreaseAbilityUseLimitOnKill(player);

            target.SetRealKiller(player, true);
        
            PlayerControl realKiller = Main.PlayerStates.TryGetValue(target.PlayerId, out PlayerState state) ? state.RealKiller.ID.GetPlayer() ?? player : player;
            if (!realKiller) realKiller = player;

            if (target.PlayerId == Godfather.GodfatherTarget)
                realKiller.RpcSetCustomRole(CustomRoles.Renegade);

            if (target.Is(CustomRoles.Jackal)) Jackal.Instances.Do(x => x.PromoteSidekick());

            Main.DiedThisRound.Add(target.PlayerId);

            if (player.AmOwner && !player.HasKillButton() && player.PlayerId != target.PlayerId && Options.CurrentGameMode == CustomGameMode.Standard)
                Achievements.Type.InnocentKiller.Complete();

            if (Options.AnonymousBodies.GetBool() || realKiller.Is(CustomRoles.Concealer) || target.Is(CustomRoles.Hidden))
            {
                Main.AllPlayerSpeed[target.PlayerId] = Main.MinSpeed;
                target.SyncSettings();
                NetworkedPlayerInfo.PlayerOutfit newSkin = new NetworkedPlayerInfo.PlayerOutfit().Set(GetString("Dead"), 15, "", "", "", "", "");
                RpcChangeSkin(target, newSkin);

                LateTask.New(() =>
                {
                    target.RpcExileV2();
                    LateTask.New(DoKill, 0.5f, "Anonymous Body Delay 2");

                    LateTask.New(() =>
                    {
                        if (GameStates.IsEnded) return;
                        Main.AllPlayerSpeed[target.PlayerId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
                        target.MarkDirtySettings();
                    }, 0.7f, "Anonymous Body Delay 3");
                }, 0.1f, "Anonymous Body Delay 1");

                return;
            }

            switch (player.PlayerId == target.PlayerId)
            {
                case true when player.shapeshifting:
                    LateTask.New(DoKill, 1.5f, "Shapeshifting Suicide Delay");
                    return;
                case false when !player.Is(CustomRoles.Pestilence) && Main.PlayerStates[target.PlayerId].Role is SchrodingersCat cat:
                    cat.OnCheckMurderAsTarget(player, target);
                    return;
                default:
                    DoKill();
                    break;
            }

            return;

            void DoKill()
            {
                if (!Vacuum.BeforeMurderCheck(target))
                    player = target;
            
                if (AmongUsClient.Instance.AmClient)
                    player.MurderPlayer(target, MurderResultFlags.Succeeded);

                var sender = CustomRpcSender.Create("RpcMurderPlayer", SendOption.Reliable);
                sender.StartMessage();

                if (Main.Invisible.Contains(target.PlayerId))
                {
                    sender.StartRpc(target.NetTransform.NetId, RpcCalls.SnapTo)
                        .WriteVector2(new Vector2(50f, 50f))
                        .Write((ushort)(target.NetTransform.lastSequenceId + 16383))
                        .EndRpc();
                    sender.StartRpc(target.NetTransform.NetId, RpcCalls.SnapTo)
                        .WriteVector2(new Vector2(50f, 50f))
                        .Write((ushort)(target.NetTransform.lastSequenceId + 32767))
                        .EndRpc();
                    sender.StartRpc(target.NetTransform.NetId, RpcCalls.SnapTo)
                        .WriteVector2(new Vector2(50f, 50f))
                        .Write((ushort)(target.NetTransform.lastSequenceId + 32767 + 16383))
                        .EndRpc();
                    sender.StartRpc(target.NetTransform.NetId, RpcCalls.SnapTo)
                        .WriteVector2(target.transform.position)
                        .Write(target.NetTransform.lastSequenceId)
                        .EndRpc();

                    NumSnapToCallsThisRound += 4;
                }

                sender.StartRpc(player.NetId, RpcCalls.MurderPlayer)
                    .WriteNetObject(target)
                    .Write((int)MurderResultFlags.Succeeded)
                    .EndRpc();

                sender.SendMessage();

                if (Main.PlayerStates.TryGetValue(target.PlayerId, out var playerState) && !playerState.IsDead)
                    playerState.SetDead();

                LateTask.New(() =>
                {
                    if (ReportDeadBodyPatch.MeetingStarted || GameStates.IsMeeting) return;
                    Vector2 pos = Object.FindObjectsOfType<DeadBody>().First(x => x.ParentId == target.PlayerId).TruePosition;

                    if (!FastVector2.DistanceWithinRange(pos, Pelican.GetBlackRoomPS(), 2f))
                    {
                        foreach (PlayerState ps in Main.PlayerStates.Values)
                        {
                            if (!ps.IsDead && ps.Role.SeesArrowsToDeadBodies && !ps.SubRoles.Contains(CustomRoles.Blind) && ps.Player)
                            {
                                LocateArrow.Add(ps.Player.PlayerId, pos);
                                NotifyRoles(SpecifySeer: ps.Player, SpecifyTarget: ps.Player);
                            }
                        }
                    }
                }, 0.2f);
            }
        }

        public bool RpcCheckAndMurder(PlayerControl target, bool check = false)
        {
            return CheckMurderPatch.RpcCheckAndMurder(player, target, check);
        }

        public void NoCheckStartMeeting(NetworkedPlayerInfo target, bool force = false)
        {
            if (!HudManager.InstanceExists) return;
            if (Options.DisableMeeting.GetBool() && !force) return;

            ReportDeadBodyPatch.AfterReportTasks(player, target);
            MeetingRoomManager.Instance.AssignSelf(player, target);
            HudManager.Instance.OpenMeetingRoom(player);
            player.RpcStartMeeting(target);
        }

        public bool UsesPetInsteadOfKill()
        {
            return player && !player.Is(CustomRoles.Bloodlust) && player.GetCustomRole().UsesPetInsteadOfKill();
        }

        public bool IsModdedClient()
        {
            return player.AmOwner || player.IsHost() || Main.PlayerVersion.ContainsKey(player.PlayerId);
        }

        public bool IsValidTargetForKillButton()
        {
            // Code from AU code for kill button check target, without distance check but check colliders
            if (PlayerControl.LocalPlayer.Data.Role.IsValidTarget(player.Data) && player.Collider.enabled)
            {
                Vector2 lpPos = PlayerControl.LocalPlayer.GetTruePosition();
                Vector2 vector = player.GetTruePosition() - lpPos;
                float magnitude = vector.magnitude;
                if (!PhysicsHelpers.AnyNonTriggersBetween(lpPos, vector.normalized, magnitude, Constants.ShipAndObjectsMask))
                {
                    return true;
                }
            }
            return false;
        }

        public List<PlayerControl> GetPlayersInAbilityRangeSorted(bool ignoreColliders = false)
        {
            return player.GetPlayersInAbilityRangeSorted(_ => true, ignoreColliders);
        }

        public List<PlayerControl> GetPlayersInAbilityRangeSorted(Predicate<PlayerControl> predicate, bool ignoreColliders = false)
        {
            Il2CppSystem.Collections.Generic.List<PlayerControl> rangePlayersIL = RoleBehaviour.GetTempPlayerList();
            List<PlayerControl> rangePlayers = [];
            player.Data.Role.GetPlayersInAbilityRangeSorted(rangePlayersIL, ignoreColliders);

            foreach (PlayerControl pc in rangePlayersIL)
            {
                if (predicate(pc))
                    rangePlayers.Add(pc);
            }

            return rangePlayers;
        }

        public bool IsNeutralKiller()
        {
            return player.Is(CustomRoles.Bloodlust) || (player.GetCustomRole().IsNK() && !player.IsMadmate());
        }

        public bool IsNeutralBenign()
        {
            return player.GetCustomRole().GetNeutralRoleCategory() == RoleOptionType.Neutral_Benign;
        }

        public bool IsNeutralEvil()
        {
            return player.GetCustomRole().GetNeutralRoleCategory() == RoleOptionType.Neutral_Evil;
        }

        public bool IsNeutralPariah()
        {
            return player.GetCustomRole().GetNeutralRoleCategory() == RoleOptionType.Neutral_Pariah;
        }

        public bool IsSnitchTarget()
        {
            return player.Is(CustomRoles.Bloodlust) || Framer.FramedPlayers.Contains(player.PlayerId) || Enchanter.EnchantedPlayers.Contains(player.PlayerId) || Snitch.IsSnitchTarget(player);
        }

        public bool IsMadmate()
        {
            return player.Is(CustomRoles.Madmate) || player.GetCustomRole().IsMadmate();
        }

        public bool HasGhostRole()
        {
            return GhostRolesManager.AssignedGhostRoles.ContainsKey(player.PlayerId) || (Main.PlayerStates.TryGetValue(player.PlayerId, out PlayerState state) && state.SubRoles.Any(x => x.IsGhostRole()));
        }

        public bool KnowDeathReason(PlayerControl target)
        {
            return (player.Is(CustomRoles.Doctor)
                    || player.Is(CustomRoles.Autopsy)
                    || Options.EveryoneSeesDeathReasons.GetBool()
                    || target.Is(CustomRoles.Gravestone)
                    || (!player.IsAlive() && Options.GhostCanSeeDeathReason.GetBool()))
                   && !target.IsAlive();
        }

        public string GetRoleInfo(bool infoLong = false)
        {
            CustomRoles role = player.AmOwner && Main.GM.Value ? CustomRoles.GM : player.GetCustomRole();
            if (role is CustomRoles.Crewmate or CustomRoles.Impostor) infoLong = false;

            string info = (role.IsVanilla() ? "Blurb" : "Info") + (infoLong ? "Long" : string.Empty);
            string roleInfo = GetString($"{role.ToString()}{info}");
            return infoLong ? roleInfo.FixRoleName(role) : roleInfo;
        }

        public void SetRealKiller(PlayerControl killer, bool notOverRide = false)
        {
            if (!player)
            {
                Logger.Info("target is null", "SetRealKiller");
                return;
            }

            PlayerState state = Main.PlayerStates[player.PlayerId];
            if (state.RealKiller.TimeStamp != DateTime.MinValue && notOverRide) return; // Do not overwrite if value already exists

            byte killerId = !killer ? byte.MaxValue : killer.PlayerId;
            RPC.SetRealKiller(player.PlayerId, killerId);
        }

        public PlayerControl GetRealKiller()
        {
            byte killerId = Main.PlayerStates[player.PlayerId].GetRealKiller();
            return killerId == byte.MaxValue ? null : GetPlayerById(killerId);
        }

        public PlainShipRoom GetPlainShipRoom()
        {
            if (!player.IsAlive()) return null;

            byte id = player.PlayerId;
            Vector2 pos = player.Pos();
            Dictionary<SystemTypes, SystemTypes> overlappingRooms = OverlappingRooms.GetValueOrDefault(Main.CurrentMap, EmptyOverlap);

            if (PlayerRoomCache.TryGetValue(id, out var cached))
            {
                var area = cached.roomArea;
                if (area && area.bounds.Contains2D(pos))
                {
                    if (Check(cached, area, player, pos, overlappingRooms, out (bool found, PlainShipRoom room) correctRoom))
                        return cached;
                
                    if (correctRoom.found)
                    {
                        PlayerRoomCache[id] = correctRoom.room;
                        return correctRoom.room;
                    }
                }
            }

            foreach (var room in ShipStatus.Instance.AllRooms)
            {
                if (room.RoomId is SystemTypes.Hallway or SystemTypes.Outside) continue;
                var area = room.roomArea;

                if (area && area.bounds.Contains2D(pos))
                {
                    if (Check(room, area, player, pos, overlappingRooms, out (bool found, PlainShipRoom room) correctRoom))
                    {
                        PlayerRoomCache[id] = room;
                        return room;
                    }
                
                    if (correctRoom.found)
                    {
                        PlayerRoomCache[id] = correctRoom.room;
                        return correctRoom.room;
                    }
                }
            }

            PlayerRoomCache.Remove(id);
            return null;
        }

        public bool IsInRoom(PlainShipRoom room)
        {
            if (!room) return false;
            var roomArea = room.roomArea;
            if (!roomArea) return false;
            if (!player.IsAlive()) return false;
            Vector2 pos = player.Pos();
            return roomArea.bounds.Contains2D(pos) && Check(room, roomArea, player, pos, OverlappingRooms.GetValueOrDefault(Main.CurrentMap, EmptyOverlap), out _);
        }

        public bool IsInRoom(SystemTypes roomId)
        {
            if (!player.IsAlive()) return false;
            PlainShipRoom room = roomId.GetRoomClass();
            if (!room) return false;
            var roomArea = room.roomArea;
            if (!roomArea) return false;
            Vector2 pos = player.Pos();
            return roomArea.bounds.Contains2D(pos) && Check(room, roomArea, player, pos, OverlappingRooms.GetValueOrDefault(Main.CurrentMap, EmptyOverlap), out _);
        }

        public bool IsImpostor()
        {
            return !player.Is(CustomRoles.Bloodlust) && player.GetCustomRole().IsImpostor();
        }

        public bool IsCrewmate()
        {
            return !player.Is(CustomRoles.Bloodlust) && player.GetCustomRole().IsCrewmate() && !player.Is(CustomRoleTypes.Coven);
        }

        public CustomRoleTypes GetCustomRoleTypes()
        {
            return player.Is(CustomRoles.Bloodlust) ? CustomRoleTypes.Neutral : player.GetCustomRole().GetCustomRoleTypes();
        }

        public RoleTypes GetRoleTypes()
        {
            try
            {
                if (Main.HasJustStarted) return player.GetEstimatedRoleTypes();
                return player.GetRoleMap().RoleType;
            }
            catch
            {
                return player.GetEstimatedRoleTypes();
            }
        }

        public RoleTypes GetEstimatedRoleTypes()
        {
            return player.GetCustomSubRoles() switch
            {
                { } x when x.Contains(CustomRoles.Bloodlust) => RoleTypes.Impostor,
                { } x when x.Contains(CustomRoles.Nimble) && !player.HasDesyncRole() => RoleTypes.Engineer,
                { } x when x.Contains(CustomRoles.Physicist) => RoleTypes.Scientist,
                { } x when x.Contains(CustomRoles.Finder) => RoleTypes.Tracker,
                { } x when x.Contains(CustomRoles.Noisy) => RoleTypes.Noisemaker,
                { } x when x.Contains(CustomRoles.Examiner) => RoleTypes.Detective,
                { } x when x.Contains(CustomRoles.Venom) => RoleTypes.Viper,
                _ => player.GetCustomRole().GetRoleTypes()
            };
        }

        public bool Is(CustomRoles role)
        {
            return role > CustomRoles.NotAssigned ? player.GetCustomSubRoles().Contains(role) : player.GetCustomRole() == role;
        }

        public bool Is(CustomRoleTypes type)
        {
            return player.GetCustomRoleTypes() == type;
        }

        public bool Is(RoleTypes type)
        {
            return (player.Is(CustomRoles.Bloodlust) && type == RoleTypes.Impostor) || player.GetCustomRole().GetRoleTypes() == type;
        }

        public bool Is(CountTypes type)
        {
            return player.GetCountTypes() == type;
        }

        public bool Is(Team team)
        {
            return team switch
            {
                Team.Coven => player.GetCustomRole().IsCoven() || player.Is(CustomRoles.Entranced),
                Team.Impostor => (player.IsMadmate() || player.GetCustomRole().IsImpostorTeamV2() || Framer.FramedPlayers.Contains(player.PlayerId)) && !player.Is(CustomRoles.Bloodlust),
                Team.Neutral => player.GetCustomRole().IsNeutralTeamV2() || player.Is(CustomRoles.Bloodlust) || player.IsConverted(),
                Team.Crewmate => player.GetCustomRole().IsCrewmateTeamV2(),
                Team.None => player.Is(CustomRoles.GM) || player.Is(CountTypes.None) || player.Is(CountTypes.OutOfGame),
                _ => false
            };
        }

        public Team GetTeam()
        {
            if (Framer.FramedPlayers.Contains(player.PlayerId)) return Team.Impostor;

            List<CustomRoles> subRoles = player.GetCustomSubRoles();
            if (subRoles.Contains(CustomRoles.Bloodlust) || player.IsConverted()) return Team.Neutral;
            if (subRoles.Contains(CustomRoles.Madmate)) return Team.Impostor;

            CustomRoles role = player.GetCustomRole();
            if (role.IsCoven()) return Team.Coven;
            if (role.IsImpostorTeamV2()) return Team.Impostor;
            if (role.IsNeutralTeamV2()) return Team.Neutral;
            return role.IsCrewmateTeamV2() ? Team.Crewmate : Team.None;
        }

        public bool IsConverted()
        {
            foreach (CustomRoles subRole in player.GetCustomSubRoles())
                if (subRole.IsConverted()) return true;

            return false;
        }

        public bool IsAlive()
        {
            if (!player || player.Is(CustomRoles.GM)) return false;

            return GameStates.IsLobby || !Main.PlayerStates.TryGetValue(player.PlayerId, out PlayerState ps) || !ps.IsDead;
        }

        public bool IsProtected()
        {
            return player.protectedByGuardianId > -1;
        }

        public bool IsTrusted()
        {
            if (player.FriendCode.GetDevUser().up) return true;

            if (ChatCommands.IsPlayerVIP(player.FriendCode)) return true;
            if (PrivateTagManager.Tags.ContainsKey(player.FriendCode)) return true;
            if (Main.UserData.TryGetValue(player.FriendCode, out Options.UserData userData) && !string.IsNullOrWhiteSpace(userData.Tag) && userData.Tag.Length > 0) return true;
        
            ClientData client = player.GetClient();
            return client != null && FriendsListManager.InstanceExists && FriendsListManager.Instance.IsPlayerFriend(client.ProductUserId);
        }

        public bool IsBeginner()
        {
            if (player.IsModdedClient() || player.IsTrusted() || player.FriendCode.GetDevUser().HasTag()) return false;
            return !Main.GamesPlayed.TryGetValue(player.FriendCode, out int gamesPlayed) || gamesPlayed < 4;
        }
    }

    // If you use vanilla RpcSetRole, it will block further SetRole calls until the next game starts.

    /*
    public static void RpcShowScanAnimationDesync(this PlayerControl target, PlayerControl seer, bool on)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            StackFrame caller = new(1, false);
            MethodBase callerMethod = caller.GetMethod();
            string callerMethodName = callerMethod?.Name;
            string callerClassName = callerMethod?.DeclaringType?.FullName;
            Logger.Warn($"Modded non-host client activated RpcShowScanAnimation from {callerClassName}.{callerMethodName}", "RpcShowScanAnimation");
            return;
        }

        if (target == null || seer == null) return;

        int seerClientId = seer.OwnerId;
        if (seerClientId == -1) return;

        byte cnt = ++target.scannerCount;

        if (AmongUsClient.Instance.ClientId == seerClientId)
        {
            target.SetScanner(on, cnt);
            return;
        }

        MessageWriter messageWriter = AmongUsClient.Instance.StartRpcImmediately(target.NetId, (byte)RpcCalls.SetScanner, SendOption.Reliable, seerClientId);
        messageWriter.Write(on);
        messageWriter.Write(cnt);
        AmongUsClient.Instance.FinishRpcImmediately(messageWriter);
        target.scannerCount = cnt;
    }
    */

    extension(byte playerId)
    {
        public float GetAbilityUseLimit()
        {
            return Main.AbilityUseLimit.GetValueOrDefault(playerId, float.NaN);
        }

        public void SetAbilityUseLimit(float limit, bool rpc = true, bool log = true)
        {
            limit = (float)Math.Round(limit, 2);

            if (float.IsNaN(limit) || limit is < 0f or > 100f || (Main.AbilityUseLimit.TryGetValue(playerId, out float beforeLimit) && Math.Abs(beforeLimit - limit) < 0.01f)) return;

            Main.AbilityUseLimit[playerId] = limit;

            if (AmongUsClient.Instance.AmHost && playerId.IsPlayerModdedClient() && !playerId.IsHost() && rpc)
            {
                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncAbilityUseLimit, SendOption.Reliable);
                writer.Write(playerId);
                writer.Write(limit);
                AmongUsClient.Instance.FinishRpcImmediately(writer);
            }

            PlayerControl pc = GetPlayerById(playerId);
            if (Main.IntroDestroyed) NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
            if (log) Logger.Info($" {pc.GetNameWithRole()} => {Math.Round(limit, 2)}", "SetAbilityUseLimit");
        }

        public bool IsPlayerRoleBlocked()
        {
            return RoleBlockManager.RoleBlockedPlayers.ContainsKey(playerId);
        }

        public bool IsHost()
        {
            return GetPlayerById(playerId)?.OwnerId == AmongUsClient.Instance.HostId;
        }

        public bool IsPlayerShifted()
        {
            return Main.CheckShapeshift.TryGetValue(playerId, out bool shifted) && shifted;
        }
    }

    extension(NetworkedPlayerInfo player)
    {
        public CustomRoles GetCustomRole()
        {
            return (!player || !player.Object) ? CustomRoles.Crewmate : player.Object.GetCustomRole();
        }

        public DataFlagRateLimiter.QueuedAction SendGameData()
        {
            return DataFlagRateLimiter.Enqueue(() =>
            {
                MessageWriter writer = MessageWriter.Get(SendOption.Reliable);
                writer.StartMessage(5);
                writer.Write(AmongUsClient.Instance.GameId);
                writer.StartMessage(1);
                writer.WritePacked(player.NetId);
                player.Serialize(writer, false);
                writer.EndMessage();
                writer.EndMessage();
                AmongUsClient.Instance.SendOrDisconnect(writer);
                writer.Recycle();
            });
        }
    }

    public static void MassTP(this IEnumerable<PlayerControl> players, Vector2 location, bool noCheckState = false, bool log = true)
    {
        var sender = CustomRpcSender.Create("Mass TP", SendOption.Reliable);
        bool hasValue = players.Aggregate(false, (current, pc) => current | sender.TP(pc, location, noCheckState, log));
        sender.SendMessage(!hasValue);
    }

    public static bool IsHost(this InnerNetObject ino)
    {
        return ino.OwnerId == AmongUsClient.Instance.HostId;
    }

    public static PlainShipRoom GetRoomClass(this SystemTypes systemTypes)
    {
        return ShipStatus.Instance.FastRooms.TryGetValue(systemTypes, out var room) ? room : ShipStatus.Instance.AllRooms.FirstOrDefault(x => x.RoomId == systemTypes);
    }
    
    public static void RpcExitVentDesync(this PlayerPhysics physics, int ventId, PlayerControl seer)
    {
        if (!physics) return;

        int clientId = seer.OwnerId;

        if (AmongUsClient.Instance.ClientId == clientId)
        {
            physics.StopAllCoroutines();
            physics.StartCoroutine(physics.CoExitVent(ventId));
            return;
        }

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(physics.NetId, (byte)RpcCalls.ExitVent, SendOption.Reliable, clientId);
        writer.WritePacked(ventId);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    // WARNING: INACCURATE WITH NON-RECTANGLE ROOMS
    public static PlainShipRoom GetPlainShipRoom(this Vector2 pos)
    {
        // ReSharper disable once LoopCanBeConvertedToQuery
        foreach (var room in ShipStatus.Instance.AllRooms)
        {
            if (room.RoomId is SystemTypes.Hallway or SystemTypes.Outside) continue;
            var area = room.roomArea;

            if (area && area.bounds.Contains2D(pos))
                return room;
        }

        return null;
    }

    private static bool Check(PlainShipRoom toCheck, Collider2D roomArea, PlayerControl pc, Vector2 pos, Dictionary<SystemTypes, SystemTypes> overlappingRooms, out (bool found, PlainShipRoom room) correctRoom)
    {
        correctRoom = (false, null);
        if (IsRoomProblematic(toCheck)) return pc.Collider.IsTouching(roomArea);
        if (!overlappingRooms.TryGetValue(toCheck.RoomId, out SystemTypes otherRoom)) return true;
        PlainShipRoom otherRoomClass = otherRoom.GetRoomClass();
        if (!otherRoomClass) return true;
        Collider2D area = otherRoomClass.roomArea;
        if (!area) return true;
        correctRoom = (true, otherRoomClass);
        return !area.bounds.Contains2D(pos);
    }

    private static readonly Dictionary<byte, PlainShipRoom> PlayerRoomCache = [];

    // The Key is the larger room, its bounds overlap the Value room's bounds
    // Additional check when something is inside the Key room's bounds: also check the Value room's bounds
    // The Value room's bounds never overlap the Key room's bounds, so the check is only one way
    private static readonly Dictionary<MapNames, Dictionary<SystemTypes, SystemTypes>> OverlappingRooms = new()
    {
        [MapNames.MiraHQ] = new()
        {
            [SystemTypes.LockerRoom] = SystemTypes.Decontamination,
            [SystemTypes.Cafeteria] = SystemTypes.Storage
        },
        [MapNames.Polus] = new()
        {
            [SystemTypes.Electrical] = SystemTypes.Security
        }
    };
    private static readonly Dictionary<SystemTypes, SystemTypes> EmptyOverlap = [];

    // Rooms that aren't rectangular-shaped and overlap walkable areas outside the room
    private static readonly Dictionary<MapNames, List<SystemTypes>> ProblematicRooms = new()
    {
        [MapNames.Skeld] = [SystemTypes.MedBay, SystemTypes.Cafeteria, SystemTypes.LifeSupp, SystemTypes.Electrical],
        [MapNames.Dleks] = [SystemTypes.MedBay, SystemTypes.Cafeteria, SystemTypes.LifeSupp, SystemTypes.Electrical],
        [MapNames.Polus] = [SystemTypes.LifeSupp, SystemTypes.Storage, SystemTypes.Laboratory, SystemTypes.Comms, SystemTypes.Weapons, SystemTypes.Admin, SystemTypes.Decontamination2, SystemTypes.Decontamination3],
        [MapNames.Airship] = [SystemTypes.Electrical, SystemTypes.Security, SystemTypes.Engine, SystemTypes.Showers, SystemTypes.MainHall],
        [MapNames.Fungle] = [SystemTypes.Dropship]
    };

    private static bool IsRoomProblematic(PlainShipRoom room)
    {
        if (SubmergedCompatibility.IsSubmerged() || Main.LIMap) return true;
        return ProblematicRooms.TryGetValue(Main.CurrentMap, out var list) && list.Contains(room.RoomId);
    }

    public static void RpcSetCustomRole(byte playerId, CustomRoles role)
    {
        if (AmongUsClient.Instance.AmHost)
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetCustomRole, SendOption.Reliable);
            writer.Write(playerId);
            writer.WritePacked((int)role);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
    }
}
