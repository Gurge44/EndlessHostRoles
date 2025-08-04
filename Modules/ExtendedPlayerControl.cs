using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using AmongUs.GameOptions;
using EHR.AddOns.Crewmate;
using EHR.AddOns.Impostor;
using EHR.Coven;
using EHR.Crewmate;
using EHR.Impostor;
using EHR.Modules;
using EHR.Neutral;
using Hazel;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using InnerNet;
using UnityEngine;
using static EHR.Translator;
using static EHR.Utils;

namespace EHR;

internal static class ExtendedPlayerControl
{
    public const MurderResultFlags ResultFlags = MurderResultFlags.Succeeded;

    public static readonly HashSet<byte> BlackScreenWaitingPlayers = [];
    public static readonly HashSet<byte> CancelBlackScreenFix = [];

    public static void SetRole(this PlayerControl player, RoleTypes role, bool canOverride = true)
    {
        player.StartCoroutine(player.CoSetRole(role, canOverride));
    }

    public static void RpcSetCustomRole(this PlayerControl player, CustomRoles role, bool replaceAllAddons = false)
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

    public static bool CanUseVent(this PlayerControl player)
    {
        try { return CanUseVent(player, GetClosestVent(player)?.Id ?? int.MaxValue); }
        catch (Exception e)
        {
            ThrowException(e);
            return true;
        }
    }

    public static bool CanUseVent(this PlayerControl player, int ventId)
    {
        switch (Options.CurrentGameMode)
        {
            case CustomGameMode.RoomRush:
                return true;
            case CustomGameMode.Standard when Main.AllAlivePlayerControls.Length == 2:
                return false;
        }

        if (player.Is(CustomRoles.Trainee) && MeetingStates.FirstMeeting) return false;
        if (player.Is(CustomRoles.Blocked) && player.GetClosestVent()?.Id != ventId) return false;
        if (!GameStates.IsInTask || ExileController.Instance || AntiBlackout.SkipTasks || Main.Invisible.Contains(player.PlayerId)) return false;
        if (player.inVent && player.GetClosestVent()?.Id == ventId) return true;
        return (player.CanUseImpostorVentButton() || player.GetRoleTypes() == RoleTypes.Engineer) && Main.PlayerStates.Values.All(x => x.Role.CanUseVent(player, ventId));
    }

    // Next 2: https://github.com/Rabek009/MoreGamemodes/blob/master/Modules/ExtendedPlayerControl.cs
    public static Vent GetClosestVent(this PlayerControl player)
    {
        Vector2 pos = player.Pos();
        return ShipStatus.Instance?.AllVents?.Where(x => x != null).MinBy(x => Vector2.Distance(pos, x.transform.position));
    }

    public static List<Vent> GetVentsFromClosest(this PlayerControl player)
    {
        Vector2 playerpos = player.Pos();
        List<Vent> vents = ShipStatus.Instance.AllVents.ToList();
        vents.Sort((v1, v2) => Vector2.Distance(playerpos, v1.transform.position).CompareTo(Vector2.Distance(playerpos, v2.transform.position)));

        if ((player.walkingToVent || player.inVent) && vents[0] != null)
        {
            List<Vent> nextvents = vents[0].NearbyVents.ToList();
            nextvents.RemoveAll(v => v == null);
            nextvents.ForEach(v => vents.Remove(v));
            vents.InsertRange(0, nextvents);
        }

        return vents;
    }

    // From: https://github.com/Rabek009/MoreGamemodes/blob/master/Modules/ExtendedPlayerControl.cs - coded by Rabek009
    public static void SetChatVisible(this PlayerControl player, bool visible)
    {
        Logger.Info($"Setting the chat {(visible ? "visible" : "hidden")} for {player.GetNameWithRole()}", "SetChatVisible");

        if (player.AmOwner)
        {
            HudManager.Instance.Chat.SetVisible(visible);
            HudManager.Instance.Chat.HideBanButton();
            return;
        }

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
    }

    public static ClientData GetClient(this PlayerControl player)
    {
        try { return AmongUsClient.Instance.allClients.ToArray().FirstOrDefault(cd => cd.Character.PlayerId == player.PlayerId); }
        catch { return null; }
    }

    public static int GetClientId(this PlayerControl player)
    {
        if (player == null) return -1;

        ClientData client = player.GetClient();
        return client?.Id ?? -1;
    }

    public static CustomRoles GetCustomRole(this NetworkedPlayerInfo player)
    {
        return player == null || player.Object == null ? CustomRoles.Crewmate : player.Object.GetCustomRole();
    }

    /// <summary>
    ///     *Sub-roles cannot be obtained.
    /// </summary>
    public static CustomRoles GetCustomRole(this PlayerControl player)
    {
        if (player == null)
        {
            MethodBase callerMethod = new StackFrame(1, false).GetMethod();
            string callerMethodName = callerMethod?.Name;
            Logger.Warn($"{callerMethod?.DeclaringType?.FullName}.{callerMethodName} tried to get a CustomRole, but the target was null.", "GetCustomRole");
            return CustomRoles.Crewmate;
        }

        return Main.PlayerStates.TryGetValue(player.PlayerId, out PlayerState state) ? state.MainRole : CustomRoles.Crewmate;
    }

    public static List<CustomRoles> GetCustomSubRoles(this PlayerControl player)
    {
        if (GameStates.IsLobby) return [];

        if (player == null)
        {
            Logger.Warn("The player is null", "GetCustomSubRoles");
            return [];
        }

        return Main.PlayerStates.TryGetValue(player.PlayerId, out PlayerState state) ? state.SubRoles : [];
    }

    public static CountTypes GetCountTypes(this PlayerControl player)
    {
        if (player == null)
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

    // By TommyXL
    public static void RpcSetPetDesync(this PlayerControl player, string petId, PlayerControl seer)
    {
        int clientId = seer.OwnerId;
        if (clientId == -1) return;

        if (AmongUsClient.Instance.ClientId == clientId)
        {
            player.SetPet(petId);
            return;
        }

        player.Data.DefaultOutfit.PetSequenceId += 10;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(player.NetId, (byte)RpcCalls.SetPetStr, SendOption.Reliable, clientId);
        writer.Write(petId);
        writer.Write(player.GetNextRpcSequenceId(RpcCalls.SetPetStr));
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void RpcResetTasks(this PlayerControl player, bool init = true)
    {
        if (!AmongUsClient.Instance.AmHost || !GameStates.IsInGame || player == null) return;

        player.Data.RpcSetTasks(new Il2CppStructArray<byte>(0));
        if (init) Main.PlayerStates[player.PlayerId].InitTask(player);
    }

    public static readonly HashSet<byte> TempExiled = [];

    public static void ExileTemporarily(this PlayerControl pc) // Only used in game modes
    {
        if (!TempExiled.Add(pc.PlayerId)) return;
        
        pc.Exiled();
        Main.PlayerStates[pc.PlayerId].SetDead();

        CustomRpcSender.Create("Temporary Death", SendOption.Reliable)
            .AutoStartRpc(pc.NetId, RpcCalls.Exiled)
            .EndRpc()
            .SendMessage();

        pc.SyncSettings();

        if (!pc.AmOwner)
        {
            var sender = CustomRpcSender.Create("Temporary Death (2)", SendOption.Reliable);
            sender.StartMessage(pc.OwnerId);
            sender.StartRpc(pc.NetId, RpcCalls.SetRole)
                .Write((ushort)RoleTypes.GuardianAngel)
                .Write(true)
                .EndRpc();
            sender.StartRpc(pc.NetId, RpcCalls.ProtectPlayer)
                .WriteNetObject(pc)
                .Write(0)
                .EndRpc();
            sender.SendMessage();
        }
        else
        {
            pc.SetRole(RoleTypes.GuardianAngel);
            pc.Data.Role.SetCooldown();
        }
    }

    // Saves some RPC calls for vanilla servers to make innersloth's rate limit happy
    public static void ReviveFromTemporaryExile(this PlayerControl player) // Only used in game modes
    {
        if (GameStates.CurrentServerType != GameStates.ServerType.Vanilla)
        {
            player.RpcRevive();
            return;
        }

        TempExiled.Remove(player.PlayerId);

        PlayerState state = Main.PlayerStates[player.PlayerId];
        state.IsDead = false;
        state.deathReason = PlayerState.DeathReason.etc;

        var sender = CustomRpcSender.Create("ReviveFromTemporaryExile", SendOption.Reliable);
        var hasValue = false;

        player.RpcSetRoleGlobal(RoleTypes.Crewmate);

        RoleTypes newRoleType = state.MainRole.GetRoleTypes();

        if (Options.CurrentGameMode is CustomGameMode.SoloKombat or CustomGameMode.FFA or CustomGameMode.CaptureTheFlag or CustomGameMode.KingOfTheZones or CustomGameMode.BedWars)
            hasValue |= sender.RpcSetRole(player, newRoleType, player.OwnerId);

        player.ResetKillCooldown();
        LateTask.New(() => player.SetKillCooldown(), 0.2f, log: false);

        if (newRoleType is not (RoleTypes.Crewmate or RoleTypes.Impostor or RoleTypes.Noisemaker))
            hasValue |= sender.RpcResetAbilityCooldown(player);

        if (DoRPC)
        {
            sender.SyncGeneralOptions(player);
            hasValue = true;
        }

        sender.SendMessage(!hasValue);
    }

    // If you use vanilla RpcSetRole, it will block further SetRole calls until the next game starts.
    private static void RpcSetRoleGlobal(this PlayerControl player, RoleTypes roleTypes)
    {
        if (AmongUsClient.Instance.AmClient) player.StartCoroutine(player.CoSetRole(roleTypes, true));
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(player.NetId, (byte)RpcCalls.SetRole, SendOption.Reliable);
        writer.Write((ushort)roleTypes);
        writer.Write(true);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void RpcSetRoleDesync(this PlayerControl player, RoleTypes role, int clientId, bool setRoleMap = false)
    {
        if (player == null) return;

        if (setRoleMap)
        {
            try
            {
                (byte, byte) key = (player.PlayerId, GetClientById(clientId).Character.PlayerId);

                if (StartGameHostPatch.RpcSetRoleReplacer.RoleMap.TryGetValue(key, out (RoleTypes RoleType, CustomRoles CustomRole) pair))
                {
                    pair.RoleType = role;
                    StartGameHostPatch.RpcSetRoleReplacer.RoleMap[key] = pair;
                }
            }
            catch (Exception e) { ThrowException(e); }
        }

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

    public static (RoleTypes RoleType, CustomRoles CustomRole) GetRoleMap(this PlayerControl player, byte targetId = byte.MaxValue)
    {
        return Utils.GetRoleMap(player.PlayerId, targetId);
    }

    // https://github.com/Ultradragon005/TownofHost-Enhanced/blob/ea5f1e8ea87e6c19466231c305d6d36d511d5b2d/Modules/ExtendedPlayerControl.cs
    public static void RpcRevive(this PlayerControl player)
    {
        if (player == null) return;

        if (!player.Data.IsDead)
        {
            Logger.Warn($"Invalid Revive for {player.GetRealName()} / Player was already alive? {!player.Data.IsDead}", "RpcRevive");
            return;
        }

        TempExiled.Remove(player.PlayerId);
        GhostRolesManager.RemoveGhostRole(player.PlayerId);
        Main.PlayerStates[player.PlayerId].IsDead = false;
        Main.PlayerStates[player.PlayerId].deathReason = PlayerState.DeathReason.etc;
        var sender = CustomRpcSender.Create("RpcRevive", SendOption.Reliable);
        player.RpcChangeRoleBasis(player.GetRoleMap().CustomRole);
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

    // https://github.com/Ultradragon005/TownofHost-Enhanced/blob/ea5f1e8ea87e6c19466231c305d6d36d511d5b2d/Modules/ExtendedPlayerControl.cs
    public static void RpcChangeRoleBasis(this PlayerControl player, CustomRoles newCustomRole, bool loggerRoleMap = false, bool forced = false)
    {
        if (!forced)
        {
            if (!AmongUsClient.Instance.AmHost || !GameStates.IsInGame || player == null || !player.IsAlive()) return;

            if (AntiBlackout.SkipTasks || ExileController.Instance)
            {
                StackTrace stackTrace = new(1, true);
                MethodBase callerMethod = stackTrace.GetFrame(0)?.GetMethod();
                string callerMethodName = callerMethod?.Name;
                string callerClassName = callerMethod?.DeclaringType?.FullName;
                Logger.Warn($"{callerClassName}.{callerMethodName} tried to change the role basis of {player.GetNameWithRole()} during anti-blackout processing or ejection screen showing, delaying the code to run after these tasks are complete", "RpcChangeRoleBasis");
                Main.Instance.StartCoroutine(DelayBasisChange());
                return;

                IEnumerator DelayBasisChange()
                {
                    while (AntiBlackout.SkipTasks || ExileController.Instance) yield return null;
                    yield return new WaitForSeconds(1f);
                    Logger.Msg($"Now that the anti-blackout processing or ejection screen showing is complete, the role basis of {player.GetNameWithRole()} will be changed", "RpcChangeRoleBasis");
                    player.RpcChangeRoleBasis(newCustomRole, loggerRoleMap);
                }
            }
        }

        byte playerId = player.PlayerId;
        int playerClientId = player.OwnerId;
        CustomRoles playerRole = Utils.GetRoleMap(playerId).CustomRole;
        RoleTypes newRoleType = newCustomRole.GetRoleTypes();
        RoleTypes rememberRoleType;

        if (!forced)
        {
            newRoleType = Options.CurrentGameMode switch
            {
                CustomGameMode.Speedrun when newCustomRole == CustomRoles.Runner => Speedrun.CanKill.Contains(playerId) ? RoleTypes.Impostor : RoleTypes.Crewmate,
                CustomGameMode.Standard when StartGameHostPatch.BasisChangingAddons.FindFirst(x => x.Value.Contains(playerId), out KeyValuePair<CustomRoles, List<byte>> kvp) => kvp.Key switch
                {
                    CustomRoles.Bloodlust when newRoleType is RoleTypes.Crewmate or RoleTypes.Engineer or RoleTypes.Scientist or RoleTypes.Tracker or RoleTypes.Noisemaker => RoleTypes.Impostor,
                    CustomRoles.Nimble when newRoleType is RoleTypes.Crewmate or RoleTypes.Scientist or RoleTypes.Noisemaker or RoleTypes.Tracker => RoleTypes.Engineer,
                    CustomRoles.Physicist when newRoleType == RoleTypes.Crewmate => RoleTypes.Scientist,
                    CustomRoles.Finder when newRoleType is RoleTypes.Crewmate or RoleTypes.Scientist or RoleTypes.Noisemaker => RoleTypes.Tracker,
                    CustomRoles.Noisy when newRoleType == RoleTypes.Crewmate => RoleTypes.Noisemaker,
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
                foreach (PlayerControl seer in Main.AllPlayerControls)
                {
                    int seerClientId = seer.OwnerId;
                    if (seerClientId == -1) continue;

                    bool seerIsHost = seer.IsHost();
                    bool self = playerId == seer.PlayerId;

                    if (!self && seer.HasDesyncRole() && !seerIsHost)
                        rememberRoleType = newRoleVN is CustomRoles.Noisemaker ? RoleTypes.Noisemaker : RoleTypes.Scientist;
                    else
                        rememberRoleType = newRoleType;

                    // Set role type for seer
                    StartGameHostPatch.RpcSetRoleReplacer.RoleMap[(seer.PlayerId, playerId)] = (rememberRoleType, newCustomRole);
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

                        StartGameHostPatch.RpcSetRoleReplacer.RoleMap[(playerId, seer.PlayerId)] = (seerCustomRole.IsDesyncRole() ? seerIsHost ? RoleTypes.Crewmate : RoleTypes.Scientist : seerRoleType, seerCustomRole);
                        seer.RpcSetRoleDesync(rememberRoleType, playerClientId);
                        continue;
                    }

                    // Set role type for player
                    StartGameHostPatch.RpcSetRoleReplacer.RoleMap[(playerId, seer.PlayerId)] = (rememberRoleType, seerCustomRole);
                    seer.RpcSetRoleDesync(rememberRoleType, playerClientId);
                }

                break;
            }
            // Normal role to desync role
            case (false, true):
            {
                foreach (PlayerControl seer in Main.AllPlayerControls)
                {
                    int seerClientId = seer.OwnerId;
                    if (seerClientId == -1) continue;

                    bool self = playerId == seer.PlayerId;

                    if (self)
                    {
                        rememberRoleType = player.IsHost() ? RoleTypes.Crewmate : RoleTypes.Impostor;

                        // For Desync Shapeshifter
                        if (newRoleDY is RoleTypes.Shapeshifter or RoleTypes.Phantom)
                            rememberRoleType = newRoleDY;
                    }
                    else
                        rememberRoleType = newRoleVN is CustomRoles.Noisemaker ? RoleTypes.Noisemaker : RoleTypes.Scientist;

                    StartGameHostPatch.RpcSetRoleReplacer.RoleMap[(seer.PlayerId, playerId)] = (rememberRoleType, newCustomRole);
                    player.RpcSetRoleDesync(rememberRoleType, seerClientId);

                    if (self) continue;

                    CustomRoles seerCustomRole = seer.GetRoleMap().CustomRole;

                    if (seer.IsAlive())
                        rememberRoleType = newRoleVN is CustomRoles.Noisemaker ? RoleTypes.Noisemaker : RoleTypes.Scientist;
                    else
                    {
                        rememberRoleType = RoleTypes.CrewmateGhost;

                        StartGameHostPatch.RpcSetRoleReplacer.RoleMap[(playerId, seer.PlayerId)] = (seerCustomRole.GetVNRole() is CustomRoles.Noisemaker ? RoleTypes.Noisemaker : RoleTypes.Scientist, seerCustomRole);
                        seer.RpcSetRoleDesync(rememberRoleType, playerClientId);
                        continue;
                    }

                    // Set role type for player
                    StartGameHostPatch.RpcSetRoleReplacer.RoleMap[(playerId, seer.PlayerId)] = (rememberRoleType, seerCustomRole);
                    seer.RpcSetRoleDesync(rememberRoleType, playerClientId);
                }

                break;
            }
            // Desync role to desync role
            // Normal role to normal role
            default:
            {
                bool playerIsDesync = player.HasDesyncRole();

                foreach (PlayerControl seer in Main.AllPlayerControls)
                {
                    int seerClientId = seer.OwnerId;
                    if (seerClientId == -1) continue;

                    if ((playerIsDesync || seer.HasDesyncRole()) && seer.PlayerId != playerId)
                        rememberRoleType = Utils.GetRoleMap(seer.PlayerId, playerId).RoleType;
                    else
                        rememberRoleType = newRoleType;

                    StartGameHostPatch.RpcSetRoleReplacer.RoleMap[(seer.PlayerId, playerId)] = (rememberRoleType, newCustomRole);
                    player.RpcSetRoleDesync(rememberRoleType, seerClientId);
                }

                break;
            }
        }

        if (loggerRoleMap)
        {
            foreach (PlayerControl seer in Main.AllPlayerControls)
            {
                NetworkedPlayerInfo seerData = seer.Data;

                foreach (PlayerControl target in Main.AllPlayerControls)
                {
                    NetworkedPlayerInfo targetData = target.Data;
                    (RoleTypes roleType, CustomRoles customRole) = seer.GetRoleMap(targetData.PlayerId);
                    Logger.Info($"seer {seerData?.PlayerName}-{seerData?.PlayerId}, target {targetData.PlayerName}-{targetData.PlayerId} => {roleType}, {customRole}", "Role Map");
                }
            }
        }

        if (!forced) Logger.Info($"{player.GetNameWithRole()}'s role basis was changed to {newRoleType} ({newCustomRole}) (from role: {playerRole}) - oldRoleIsDesync: {oldRoleIsDesync}, newRoleIsDesync: {newRoleIsDesync}", "RpcChangeRoleBasis");
    }

    // https://github.com/Ultradragon005/TownofHost-Enhanced/blob/ea5f1e8ea87e6c19466231c305d6d36d511d5b2d/Modules/Utils.cs
    public static void SyncGeneralOptions(this CustomRpcSender sender, PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost || !GameStates.IsInGame || !DoRPC) return;

        sender.AutoStartRpc(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncGeneralOptions);
        sender.Write(player.PlayerId);
        sender.WritePacked((int)player.GetCustomRole());
        sender.Write(Main.PlayerStates[player.PlayerId].IsDead);
        sender.WritePacked((int)Main.PlayerStates[player.PlayerId].deathReason);
        sender.Write(Main.AllPlayerKillCooldown[player.PlayerId]);
        sender.Write(Main.AllPlayerSpeed[player.PlayerId]);
        sender.EndRpc();
    }

    // Next 3: https://github.com/0xDrMoe/TownofHost-Enhanced/blob/12487ce1aa7e4f5087f2300be452b5af7c04d1ff/Modules/ExtendedPlayerControl.cs#L239

    public static void RpcExitVentDesync(this PlayerPhysics physics, int ventId, PlayerControl seer)
    {
        if (physics == null) return;

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

    public static void RpcEnterVentDesync(this PlayerPhysics physics, int ventId, PlayerControl seer)
    {
        if (physics == null) return;

        int clientId = seer.GetClientId();

        if (AmongUsClient.Instance.ClientId == clientId)
        {
            physics.StopAllCoroutines();
            physics.StartCoroutine(physics.CoEnterVent(ventId));
            return;
        }

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(physics.NetId, (byte)RpcCalls.EnterVent, SendOption.Reliable, clientId);
        writer.WritePacked(ventId);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void RpcStartAppearDesync(this PlayerControl player, bool shouldAnimate, PlayerControl seer)
    {
        int clientId = seer.OwnerId;

        if (AmongUsClient.Instance.ClientId == clientId)
        {
            player.SetRoleInvisibility(false, shouldAnimate, true);
            return;
        }

        MessageWriter messageWriter = AmongUsClient.Instance.StartRpcImmediately(player.NetId, (byte)RpcCalls.StartAppear, SendOption.None, clientId);
        messageWriter.Write(shouldAnimate);
        AmongUsClient.Instance.FinishRpcImmediately(messageWriter);
    }

    public static bool HasDesyncRole(this PlayerControl player)
    {
        return player.Is(CustomRoles.Bloodlust) || player.GetCustomRole().IsDesyncRole();
    }

    public static bool IsInsideMap(this PlayerControl player)
    {
        if (player == null) return false;

        var results = new Collider2D[10];
        int overlapPointNonAlloc = Physics2D.OverlapPointNonAlloc(player.Pos(), results, Constants.ShipOnlyMask);
        PlainShipRoom room = player.GetPlainShipRoom();
        Vector2 pos = player.Pos();

        return Main.CurrentMap switch
        {
            MapNames.Fungle when overlapPointNonAlloc >= 2 => true,
            MapNames.MiraHQ when overlapPointNonAlloc >= 1 => true,
            MapNames.MiraHQ when room != null && room.RoomId is SystemTypes.MedBay or SystemTypes.Comms => true,
            MapNames.Airship when overlapPointNonAlloc >= 1 => true,
            MapNames.Skeld or MapNames.Dleks when room != null => true,
            MapNames.Polus when overlapPointNonAlloc >= 1 => true,
            MapNames.Polus when pos.y is >= -26.11f and <= -6.41f && pos.x is >= 3.56f and <= 32.68f => true,
            (MapNames)6 => true,
            _ => false
        };
    }

    public static void KillFlash(this PlayerControl player)
    {
        if (GameStates.IsLobby) return;

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
        Main.PlayerStates[player.PlayerId].IsBlackOut = true; // Blackout

        LateTask.New(() =>
        {
            Main.PlayerStates[player.PlayerId].IsBlackOut = false; // Cancel blackout
            player.MarkDirtySettings();
        }, duration, "RemoveKillFlash");

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
        else if (!reactorCheck) player.ReactorFlash(); // Reactor flash

        player.MarkDirtySettings();
    }

    public static void RpcGuardAndKill(this PlayerControl killer, PlayerControl target = null, bool forObserver = false, bool fromSetKCD = false)
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

        if (target == null) target = killer;

        // Check Observer
        if (!forObserver && !MeetingStates.FirstMeeting) Main.AllPlayerControls.Where(x => x.Is(CustomRoles.Observer) && killer.PlayerId != x.PlayerId).Do(x => x.RpcGuardAndKill(target, true));

        // Host
        if (killer.AmOwner) killer.MurderPlayer(target, MurderResultFlags.FailedProtected);

        // Other Clients
        if (!killer.IsHost())
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(killer.NetId, (byte)RpcCalls.MurderPlayer, SendOption.Reliable, killer.OwnerId);
            writer.WriteNetObject(target);
            writer.Write((int)MurderResultFlags.FailedProtected);
            AmongUsClient.Instance.FinishRpcImmediately(writer);

            if (!MeetingStates.FirstMeeting && !AntiBlackout.SkipTasks && !ExileController.Instance && GameStates.IsInTask && killer.IsBeginner() && Main.GotShieldAnimationInfoThisGame.Add(killer.PlayerId))
                killer.Notify(GetString("PleaseStopBeingDumb"), 10f);
        }

        if (!fromSetKCD) killer.AddKillTimerToDict(true);
    }

    public static bool HasAbilityCD(this PlayerControl pc)
    {
        return Main.AbilityCD.ContainsKey(pc.PlayerId);
    }

    public static void AddKCDAsAbilityCD(this PlayerControl pc)
    {
        AddAbilityCD(pc, (int)Math.Round(Main.AllPlayerKillCooldown.TryGetValue(pc.PlayerId, out float kcd) ? kcd : Options.AdjustedDefaultKillCooldown));
    }

    public static void AddAbilityCD(this PlayerControl pc, bool includeDuration = true)
    {
        Utils.AddAbilityCD(pc.GetCustomRole(), pc.PlayerId, includeDuration);
    }

    public static void AddAbilityCD(this PlayerControl pc, int cd)
    {
        Main.AbilityCD[pc.PlayerId] = (TimeStamp, cd);
        SendRPC(CustomRPC.SyncAbilityCD, 1, pc.PlayerId, cd);
    }

    public static void RemoveAbilityCD(this PlayerControl pc)
    {
        if (Main.AbilityCD.Remove(pc.PlayerId)) SendRPC(CustomRPC.SyncAbilityCD, 3, pc.PlayerId);
    }

    public static float GetAbilityUseLimit(this PlayerControl pc)
    {
        return Main.AbilityUseLimit.GetValueOrDefault(pc.PlayerId, float.NaN);
    }

    public static float GetAbilityUseLimit(this byte playerId)
    {
        return Main.AbilityUseLimit.GetValueOrDefault(playerId, float.NaN);
    }

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

    public static void SetAbilityUseLimit(this PlayerControl pc, float limit, bool rpc = true, bool log = true)
    {
        pc.PlayerId.SetAbilityUseLimit(limit, rpc, log);
    }

    public static void SetAbilityUseLimit(this byte playerId, float limit, bool rpc = true, bool log = true)
    {
        limit = (float)Math.Round(limit, 1);

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
        if (log) Logger.Info($" {pc.GetNameWithRole()} => {Math.Round(limit, 1)}", "SetAbilityUseLimit");
    }

    public static void Suicide(this PlayerControl pc, PlayerState.DeathReason deathReason = PlayerState.DeathReason.Suicide, PlayerControl realKiller = null)
    {
        if (!pc.IsAlive() || pc.Data.IsDead || !GameStates.IsInTask || ExileController.Instance) return;

        PlayerState state = Main.PlayerStates[pc.PlayerId];

        if (realKiller != null && state.Role is SchrodingersCat cat)
        {
            cat.OnCheckMurderAsTarget(realKiller, pc);
            return;
        }

        state.deathReason = deathReason;
        state.SetDead();

        Medic.IsDead(pc);

        if (realKiller != null)
        {
            pc.SetRealKiller(realKiller);

            if (realKiller.Is(CustomRoles.Damocles))
                Damocles.OnMurder(realKiller.PlayerId);

            IncreaseAbilityUseLimitOnKill(realKiller);
        }

        pc.Kill(pc);

        if (Options.CurrentGameMode == CustomGameMode.NaturalDisasters)
            NaturalDisasters.RecordDeath(pc, deathReason);
    }

    public static void SetKillCooldown(this PlayerControl player, float time = -1f, PlayerControl target = null, bool forceAnime = false)
    {
        if (player == null) return;

        Logger.Info($"{player.GetNameWithRole()}'s KCD set to {(Math.Abs(time - -1f) < 0.5f ? Main.AllPlayerKillCooldown[player.PlayerId] : time)}s", "SetKCD");

        if (player.GetCustomRole().UsesPetInsteadOfKill())
        {
            if (Math.Abs(time - -1f) < 0.5f)
                player.AddKCDAsAbilityCD();
            else
                player.AddAbilityCD((int)Math.Round(time));

            if (player.GetCustomRole() is not CustomRoles.Necromancer and not CustomRoles.Deathknight and not CustomRoles.Refugee and not CustomRoles.Sidekick) return;
        }

        if (!player.CanUseKillButton() && !AntiBlackout.SkipTasks) return;

        player.AddKillTimerToDict(cd: time);
        if (target == null) target = player;

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

            Main.AllPlayerControls.Where(x => x.Is(CustomRoles.Observer) && target.PlayerId != x.PlayerId).Do(x => x.RpcGuardAndKill(target, true, true));
        }

        if (player.GetCustomRole() is not CustomRoles.Inhibitor and not CustomRoles.Saboteur)
            LateTask.New(() => player.ResetKillCooldown(), 0.3f, log: false);
    }

    public static void RpcResetAbilityCooldown(this PlayerControl target)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        Logger.Info($"Reset Ability Cooldown for {target.name} (ID: {target.PlayerId})", "RpcResetAbilityCooldown");

        if (target.Is(CustomRoles.Glitch) && Main.PlayerStates[target.PlayerId].Role is Glitch gc)
        {
            gc.LastHack = TimeStamp;
            gc.HackCDTimer = 10;
        }
        else if (target.AmOwner)
        {
            // If target is host
            target.Data.Role.SetCooldown();
        }
        else if (target.IsModdedClient())
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.ResetAbilityCooldown, SendOption.Reliable, target.OwnerId);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        else
        {
            // If target is not host
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(target.NetId, (byte)RpcCalls.ProtectPlayer, SendOption.Reliable, target.OwnerId);
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
        MessageWriter messageWriter = AmongUsClient.Instance.StartRpcImmediately(ShipStatus.Instance.NetId, (byte)RpcCalls.UpdateSystem, SendOption.Reliable, target.OwnerId);
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

    public static TaskState GetTaskState(this PlayerControl player)
    {
        return Main.PlayerStates.TryGetValue(player.PlayerId, out PlayerState state) ? state.TaskState : new();
    }

    public static bool IsNonHostModdedClient(this PlayerControl pc)
    {
        return pc.IsModdedClient() && !pc.IsHost();
    }

    public static string GetDisplayRoleName(this PlayerControl player, bool pure = false, bool seeTargetBetrayalAddons = false)
    {
        return Utils.GetDisplayRoleName(player.PlayerId, pure, seeTargetBetrayalAddons);
    }

    public static string GetSubRoleNames(this PlayerControl player, bool forUser = false)
    {
        List<CustomRoles> subRoles = Main.PlayerStates[player.PlayerId].SubRoles;
        if (subRoles.Count == 0) return string.Empty;

        StringBuilder sb = new();

        foreach (CustomRoles role in subRoles)
        {
            if (role == CustomRoles.NotAssigned) continue;

            sb.Append($"{ColorString(Color.white, "\n<size=1>")}{GetRoleName(role, forUser)}");
        }

        return sb.ToString();
    }

    public static string GetAllRoleName(this PlayerControl player, bool forUser = true)
    {
        if (!player) return null;

        string text = GetRoleName(player.GetCustomRole(), forUser);
        text += player.GetSubRoleNames(forUser);
        return text;
    }

    public static string GetNameWithRole(this PlayerControl player, bool forUser = false)
    {
        try
        {
            bool addRoleName = GameStates.IsInGame && Options.CurrentGameMode is not CustomGameMode.FFA and not CustomGameMode.MoveAndStop and not CustomGameMode.HotPotato and not CustomGameMode.Speedrun and not CustomGameMode.CaptureTheFlag and not CustomGameMode.NaturalDisasters and not CustomGameMode.RoomRush and not CustomGameMode.Quiz and not CustomGameMode.TheMindGame and not CustomGameMode.BedWars;
            return $"{player?.Data?.PlayerName}" + (addRoleName ? $" ({player?.GetAllRoleName(forUser).RemoveHtmlTags().Replace('\n', ' ')})" : string.Empty);
        }
        catch (Exception e)
        {
            ThrowException(e);
            return player == null || player.Data == null ? "Unknown Player" : player.Data.PlayerName;
        }
    }

    public static string GetRoleColorCode(this PlayerControl player)
    {
        return Utils.GetRoleColorCode(player.GetCustomRole());
    }

    public static Color GetRoleColor(this PlayerControl player)
    {
        return Utils.GetRoleColor(player.GetCustomRole());
    }

    public static void FixBlackScreen(this PlayerControl pc)
    {
        if (pc == null || !AmongUsClient.Instance.AmHost || pc.IsModdedClient()) return;

        if (GameStates.IsMeeting || ExileController.Instance || AntiBlackout.SkipTasks || pc.inVent || pc.inMovingPlat || pc.onLadder || !Main.AllPlayerControls.FindFirst(x => !x.IsAlive(), out var dummyGhost))
        {
            if (BlackScreenWaitingPlayers.Add(pc.PlayerId))
                Main.Instance.StartCoroutine(Wait());

            return;

            IEnumerator Wait()
            {
                Logger.Warn($"FixBlackScreen was called for {pc.GetNameWithRole()}, but the conditions are not met to execute this code right now, waiting until it becomes possible to do so", "FixBlackScreen");

                while (GameStates.InGame && !GameStates.IsEnded && !CancelBlackScreenFix.Contains(pc.PlayerId) && (GameStates.IsMeeting || ExileController.Instance || AntiBlackout.SkipTasks || Main.AllPlayerControls.All(x => x.IsAlive())))
                    yield return null;

                if (CancelBlackScreenFix.Remove(pc.PlayerId))
                {
                    Logger.Msg($"The black screen fix was canceled for {pc.GetNameWithRole()}", "FixBlackScreen");
                    BlackScreenWaitingPlayers.Remove(pc.PlayerId);
                    yield break;
                }

                if (!GameStates.InGame || GameStates.IsEnded)
                {
                    Logger.Msg($"During the waiting, the game ended, so the black screen fix will not be executed for {pc.GetNameWithRole()}", "FixBlackScreen");
                    BlackScreenWaitingPlayers.Remove(pc.PlayerId);
                    yield break;
                }

                yield return new WaitForSeconds(pc.IsAlive() ? 1f : 3f);

                if (!GameStates.InGame || GameStates.IsEnded)
                {
                    Logger.Msg($"During the waiting, the game ended, so the black screen fix will not be executed for {pc.GetNameWithRole()}", "FixBlackScreen");
                    BlackScreenWaitingPlayers.Remove(pc.PlayerId);
                    yield break;
                }

                Logger.Msg($"Now that the conditions are met, fixing black screen for {pc.GetNameWithRole()}", "FixBlackScreen");
                BlackScreenWaitingPlayers.Remove(pc.PlayerId);
                pc.FixBlackScreen();
            }
        }

        SystemTypes systemtype = Main.CurrentMap switch
        {
            MapNames.Polus => SystemTypes.Laboratory,
            MapNames.Airship => SystemTypes.HeliSabotage,
            _ => SystemTypes.Reactor
        };

        var sender = CustomRpcSender.Create($"Fix Black Screen For {pc.GetNameWithRole()}", SendOption.Reliable);

        sender.RpcDesyncRepairSystem(pc, systemtype, 128);

        int targetClientId = pc.OwnerId;
        var ghostPos = dummyGhost.Pos();
        var pcPos = pc.Pos();
        var timer = Math.Max(Main.KillTimers[pc.PlayerId], 0.1f);

        if (pc.IsAlive())
        {
            CheckInvalidMovementPatch.ExemptedPlayers.UnionWith([pc.PlayerId, dummyGhost.PlayerId]);
            AFKDetector.TempIgnoredPlayers.UnionWith([pc.PlayerId, dummyGhost.PlayerId]);
            LateTask.New(() => AFKDetector.TempIgnoredPlayers.ExceptWith([pc.PlayerId, dummyGhost.PlayerId]), 3f, log: false);

            var murderPos = Pelican.GetBlackRoomPS();

            sender.TP(pc, murderPos);

            sender.AutoStartRpc(pc.NetId, 12, targetClientId);
            sender.WriteNetObject(dummyGhost);
            sender.Write((int)MurderResultFlags.Succeeded);
            sender.EndRpc();

            dummyGhost.NetTransform.SnapTo(murderPos, (ushort)(dummyGhost.NetTransform.lastSequenceId + 328));
            dummyGhost.NetTransform.SetDirtyBit(uint.MaxValue);

            sender.AutoStartRpc(dummyGhost.NetTransform.NetId, 21);
            sender.WriteVector2(murderPos);
            sender.Write((ushort)(dummyGhost.NetTransform.lastSequenceId + 8));
            sender.EndRpc();
        }
        else
        {
            sender.AutoStartRpc(pc.NetId, 12, targetClientId);
            sender.WriteNetObject(pc);
            sender.Write((int)MurderResultFlags.Succeeded);
            sender.EndRpc();
        }

        sender.SendMessage();

        LateTask.New(() =>
        {
            sender = CustomRpcSender.Create($"Fix Black Screen For {pc.GetNameWithRole()} (2)", SendOption.Reliable);

            sender.RpcDesyncRepairSystem(pc, systemtype, 16);
            if (systemtype == SystemTypes.HeliSabotage) sender.RpcDesyncRepairSystem(pc, systemtype, 17);

            if (pc.IsAlive())
            {
                sender.TP(pc, pcPos);
                sender.SetKillCooldown(pc, timer);
                sender.Notify(pc, GetString("BlackScreenFixCompleteNotify"));

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

    public static void ReactorFlash(this PlayerControl pc, float delay = 0f, float flashDuration = float.NaN)
    {
        if (pc == null) return;

        Logger.Info($"Reactor Flash for {pc.GetNameWithRole()}", "ReactorFlash");

        SystemTypes systemtypes = Main.CurrentMap switch
        {
            MapNames.Polus => SystemTypes.Laboratory,
            MapNames.Airship => SystemTypes.HeliSabotage,
            _ => SystemTypes.Reactor
        };

        if (IsActive(systemtypes))
        {
            Main.PlayerStates[pc.PlayerId].IsBlackOut = true;
            pc.MarkDirtySettings();

            LateTask.New(() =>
            {
                Main.PlayerStates[pc.PlayerId].IsBlackOut = false;
                pc.MarkDirtySettings();
            }, (float.IsNaN(flashDuration) ? Options.KillFlashDuration.GetFloat() : flashDuration) + delay, "Fix BlackOut Reactor Flash");

            return;
        }

        pc.RpcDesyncRepairSystem(systemtypes, 128);

        LateTask.New(() =>
        {
            pc.RpcDesyncRepairSystem(systemtypes, 16);

            if (Main.NormalOptions.MapId == 4) // on Airship
                pc.RpcDesyncRepairSystem(systemtypes, 17);
        }, (float.IsNaN(flashDuration) ? Options.KillFlashDuration.GetFloat() : flashDuration) + delay, "Fix Desync Reactor");
    }

    public static string GetRealName(this PlayerControl player, bool isMeeting = false)
    {
        try
        {
            string name = isMeeting ? player.Data.PlayerName : player.name;
            return name.RemoveHtmlTags();
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

    public static bool IsRoleBlocked(this PlayerControl pc)
    {
        return RoleBlockManager.RoleBlockedPlayers.ContainsKey(pc.PlayerId);
    }

    public static bool IsPlayerRoleBlocked(this byte id)
    {
        return RoleBlockManager.RoleBlockedPlayers.ContainsKey(id);
    }

    public static void BlockRole(this PlayerControl pc, float duration)
    {
        RoleBlockManager.AddRoleBlock(pc, duration);
    }

    public static bool IsHost(this InnerNetObject ino)
    {
        return ino.OwnerId == AmongUsClient.Instance.HostId;
    }

    public static bool IsHost(this byte id)
    {
        return GetPlayerById(id)?.OwnerId == AmongUsClient.Instance.HostId;
    }

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
    public static bool HasKillButton(this PlayerControl pc)
    {
        CustomRoles role = pc.GetCustomRole();
        if (pc.Data.Role.Role == RoleTypes.GuardianAngel) return false;
        if (role.GetVNRole(true) is CustomRoles.Impostor or CustomRoles.ImpostorEHR or CustomRoles.Shapeshifter or CustomRoles.ShapeshifterEHR or CustomRoles.Phantom or CustomRoles.PhantomEHR) return true;
        if (pc.GetRoleTypes() is RoleTypes.Impostor or RoleTypes.Shapeshifter or RoleTypes.Phantom) return true;
        if (pc.Is(CustomRoles.Bloodlust)) return true;
        return !HasTasks(pc.Data, false);
    }

    public static bool CanUseKillButton(this PlayerControl pc)
    {
        if (AntiBlackout.SkipTasks || TimeMaster.Rewinding || !Main.IntroDestroyed || !pc.IsAlive()) return false;

        switch (Options.CurrentGameMode)
        {
            case CustomGameMode.MoveAndStop or CustomGameMode.NaturalDisasters or CustomGameMode.RoomRush or CustomGameMode.TheMindGame:
            case CustomGameMode.Speedrun when !Speedrun.CanKill.Contains(pc.PlayerId):
                return false;
            case CustomGameMode.HotPotato:
                return HotPotato.CanPassViaKillButton && HotPotato.GetState().HolderID == pc.PlayerId;
            case CustomGameMode.Quiz:
                return Quiz.AllowKills;
            case CustomGameMode.KingOfTheZones:
            case CustomGameMode.CaptureTheFlag:
            case CustomGameMode.BedWars:
                return true;
        }

        if (Mastermind.ManipulatedPlayers.ContainsKey(pc.PlayerId)) return true;
        if (Penguin.IsVictim(pc)) return false;
        if (Pelican.IsEaten(pc.PlayerId)) return false;
        if (pc.Data.Role.Role == RoleTypes.GuardianAngel) return false;
        if (pc.Is(CustomRoles.Bloodlust)) return true;

        return pc.GetCustomRole() switch
        {
            // SoloKombat
            CustomRoles.KB_Normal => pc.SoloAlive(),
            // FFA
            CustomRoles.Killer => pc.IsAlive(),
            // Move And Stop
            CustomRoles.Tasker => false,
            // Hot Potato
            CustomRoles.Potato => HotPotato.CanPassViaKillButton && HotPotato.GetState().HolderID == pc.PlayerId,
            // Speedrun
            CustomRoles.Runner => Speedrun.CanKill.Contains(pc.PlayerId),
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

            _ => Main.PlayerStates.TryGetValue(pc.PlayerId, out PlayerState state) && state.Role.CanUseKillButton(pc)
        };
    }

    public static bool CanUseImpostorVentButton(this PlayerControl pc)
    {
        if (!pc.IsAlive() || pc.Data.Role.Role == RoleTypes.GuardianAngel || Penguin.IsVictim(pc)) return false;

        if (pc.GetRoleTypes() == RoleTypes.Engineer) return false;

        return Options.CurrentGameMode switch
        {
            CustomGameMode.SoloKombat => SoloPVP.CanVent,
            CustomGameMode.FFA => true,
            CustomGameMode.MoveAndStop => false,
            CustomGameMode.HotPotato => false,
            CustomGameMode.Speedrun => false,
            CustomGameMode.CaptureTheFlag => false,
            CustomGameMode.NaturalDisasters => false,
            CustomGameMode.RoomRush => false,
            CustomGameMode.Quiz => false,
            CustomGameMode.TheMindGame => false,

            CustomGameMode.Standard when CopyCat.Instances.Any(x => x.CopyCatPC.PlayerId == pc.PlayerId) => true,
            CustomGameMode.Standard when pc.Is(CustomRoles.Nimble) || Options.EveryoneCanVent.GetBool() => true,
            CustomGameMode.Standard when pc.Is(CustomRoles.Bloodlust) || pc.Is(CustomRoles.Refugee) => true,

            _ => Main.PlayerStates.TryGetValue(pc.PlayerId, out PlayerState state) && state.Role.CanUseImpostorVentButton(pc)
        };
    }

    public static bool CanUseSabotage(this PlayerControl pc)
    {
        if (!pc.IsAlive() || pc.Data.Role.Role == RoleTypes.GuardianAngel) return false;
        return Main.PlayerStates.TryGetValue(pc.PlayerId, out PlayerState state) && state.Role.CanUseSabotage(pc);
    }

    // Next 6: From MoreGamemodes by Rabek009 (https://github.com/Rabek009/MoreGamemodes)
    
    public static Vector2 Pos(this PlayerControl pc)
    {
        if (pc.AmOwner) return pc.transform.position;
        if (pc.NetTransform.incomingPosQueue.Count > 0 && pc.NetTransform.isActiveAndEnabled && !pc.NetTransform.isPaused)
            return pc.NetTransform.incomingPosQueue.ToArray()[^1];
        return pc.transform.position;
    }

    public static void MakeInvisible(this PlayerControl player)
    {
        player.invisibilityAlpha = player.AmOwner ? 0.5f : PlayerControl.LocalPlayer.Data.Role.IsDead ? 0.5f : 0f;
        player.cosmetics.SetPhantomRoleAlpha(player.invisibilityAlpha);

        if (!player.AmOwner && !PlayerControl.LocalPlayer.Data.Role.IsDead)
        {
            player.shouldAppearInvisible = true;
            player.Visible = false;
        }
    }

    public static void MakeVisible(this PlayerControl player)
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

    public static void RpcMakeInvisible(this PlayerControl player, bool phantom = false)
    {
        if (!Main.Invisible.Add(player.PlayerId)) return;
        if (phantom && Options.CurrentGameMode != CustomGameMode.Standard) return;
        player.RpcSetPet("");

        if (!phantom)
        {
            player.MakeInvisible();
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(player.NetId, (byte)CustomRPC.Invisibility, SendOption.Reliable);
            writer.Write(true);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
        {
            if (pc.AmOwner || pc == player || (!phantom && pc.IsModdedClient()) || (phantom && pc.IsImpostor())) continue;
            
            var sender = CustomRpcSender.Create("RpcMakeInvisible", SendOption.Reliable);
            sender.StartMessage(pc.GetClientId());
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

    public static void RpcMakeVisible(this PlayerControl player, bool phantom = false)
    {
        if (!Main.Invisible.Remove(player.PlayerId)) return;
        if (phantom && Options.CurrentGameMode != CustomGameMode.Standard) return;
        if (Options.UsePets.GetBool()) PetsHelper.SetPet(player, PetsHelper.GetPetId());

        if (!phantom)
        {
            player.MakeVisible();
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(player.NetId, (byte)CustomRPC.Invisibility, SendOption.Reliable);
            writer.Write(false);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
        {
            if (pc.AmOwner || pc == player || (!phantom && pc.IsModdedClient()) || (phantom && pc.IsImpostor())) continue;
            
            var sender = CustomRpcSender.Create("RpcMakeVisible", SendOption.Reliable);
            sender.StartMessage(pc.GetClientId());
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

    public static void RpcResetInvisibility(this PlayerControl player, bool phantom = false)
    {
        if (!Main.Invisible.Contains(player.PlayerId)) return;
        if (phantom && Options.CurrentGameMode != CustomGameMode.Standard) return;

        foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
        {
            if (pc.AmOwner || pc == player || (!phantom && pc.IsModdedClient()) || (phantom && pc.IsImpostor())) continue;
            
            var sender = CustomRpcSender.Create("RpcResetInvisibility", SendOption.Reliable);
            sender.StartMessage(pc.GetClientId());
            sender.StartRpc(player.NetId, RpcCalls.Exiled)
                .EndRpc();
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

    public static void AddKillTimerToDict(this PlayerControl pc, bool half = false, float cd = -1f)
    {
        float resultKCD;

        if (Math.Abs(cd - -1f) < 0.5f)
        {
            resultKCD = Main.AllPlayerKillCooldown.GetValueOrDefault(pc.PlayerId, 0f);

            if (half) resultKCD /= 2f;
        }
        else
            resultKCD = cd;

        if (pc.GetCustomRole().UsesPetInsteadOfKill() && resultKCD > 0f)
            pc.AddAbilityCD((int)Math.Round(resultKCD));

        if (Main.KillTimers.TryGetValue(pc.PlayerId, out float timer) && timer > resultKCD) return;

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
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetDousedPlayer, SendOption.Reliable);
        writer.Write(player.PlayerId);
        writer.Write(target.PlayerId);
        writer.Write(isDoused);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void RpcSetDrawPlayer(this PlayerControl player, PlayerControl target, bool isDoused)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetDrawPlayer, SendOption.Reliable);
        writer.Write(player.PlayerId);
        writer.Write(target.PlayerId);
        writer.Write(isDoused);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void RpcSetRevealtPlayer(this PlayerControl player, PlayerControl target, bool isDoused)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetRevealedPlayer, SendOption.Reliable);
        writer.Write(player.PlayerId);
        writer.Write(target.PlayerId);
        writer.Write(isDoused);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static bool IsShifted(this PlayerControl pc)
    {
        return Main.CheckShapeshift.TryGetValue(pc.PlayerId, out bool shifted) && shifted;
    }

    public static bool IsPlayerShifted(this byte id)
    {
        return Main.CheckShapeshift.TryGetValue(id, out bool shifted) && shifted;
    }

    public static bool HasSubRole(this PlayerControl pc)
    {
        return Main.PlayerStates[pc.PlayerId].SubRoles.Count > 0;
    }

    public static bool HasEvilAddon(this PlayerControl pc)
    {
        return Main.PlayerStates[pc.PlayerId].SubRoles.Any(x => x.IsEvilAddon());
    }

    public static void ResetKillCooldown(this PlayerControl player, bool sync = true)
    {
        Main.PlayerStates[player.PlayerId].Role.SetKillCooldown(player.PlayerId);

        Main.AllPlayerKillCooldown[player.PlayerId] = player.GetCustomRole() switch
        {
            CustomRoles.KB_Normal => SoloPVP.KB_ATKCooldown.GetFloat(),
            CustomRoles.Killer => FreeForAll.FFAKcd.GetFloat(),
            CustomRoles.Runner => Speedrun.KCD,
            CustomRoles.CTFPlayer => CaptureTheFlag.KCD,
            CustomRoles.KOTZPlayer => KingOfTheZones.KCD,
            CustomRoles.QuizPlayer => 3f,
            CustomRoles.BedWarsPlayer => 1f,
            _ when player.Is(CustomRoles.Underdog) => Main.AllAlivePlayerControls.Length <= Underdog.UnderdogMaximumPlayersNeededToKill.GetInt() ? Underdog.UnderdogKillCooldownWithLessPlayersAlive.GetInt() : Underdog.UnderdogKillCooldownWithMorePlayersAlive.GetInt(),
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

        if (sync) player.SyncSettings();
    }

    public static void TrapperKilled(this PlayerControl killer, PlayerControl target)
    {
        Logger.Info($"{target?.Data?.PlayerName} was Trapper", "Trapper");
        float tmpSpeed = Main.AllPlayerSpeed[killer.PlayerId];
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

        if (killer.IsLocalPlayer())
            Achievements.Type.TooCold.CompleteAfterGameEnd();
    }

    public static bool IsDouseDone(this PlayerControl player)
    {
        if (!player.Is(CustomRoles.Arsonist)) return false;

        (int, int) count = GetDousedPlayerCount(player.PlayerId);
        return count.Item1 >= count.Item2;
    }

    public static bool IsDrawDone(this PlayerControl player) // Determine whether the conditions to win are met
    {
        if (!player.Is(CustomRoles.Revolutionist)) return false;

        (int, int) count = GetDrawPlayerCount(player.PlayerId, out _);
        return count.Item1 >= count.Item2;
    }

    public static void RpcExileV2(this PlayerControl player)
    {
        player.Exiled();
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(player.NetId, (byte)RpcCalls.Exiled, SendOption.Reliable);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
        FixedUpdatePatch.LoversSuicide(player.PlayerId);
    }

    public static (Vector2 Location, string RoomName) GetPositionInfo(this PlayerControl pc)
    {
        PlainShipRoom room = pc.GetPlainShipRoom();
        string roomName = GetString(room == null ? "Outside" : $"{room.RoomId}");
        Vector2 pos = pc.Pos();

        return (pos, roomName);
    }

    public static void MassTP(this IEnumerable<PlayerControl> players, Vector2 location, bool noCheckState = false, bool log = true)
    {
        var sender = CustomRpcSender.Create("Mass TP", SendOption.Reliable);
        bool hasValue = players.Aggregate(false, (current, pc) => current | sender.TP(pc, location, noCheckState, log));
        sender.SendMessage(!hasValue);
    }

    public static bool TP(this PlayerControl pc, PlayerControl target, bool noCheckState = false, bool log = true)
    {
        return Utils.TP(pc.NetTransform, target.Pos(), noCheckState, log);
    }

    public static bool TP(this PlayerControl pc, Vector2 location, bool noCheckState = false, bool log = true)
    {
        return Utils.TP(pc.NetTransform, location, noCheckState, log);
    }

    public static bool TPToRandomVent(this PlayerControl pc, bool log = true)
    {
        return Utils.TPToRandomVent(pc.NetTransform, log);
    }

    public static void SendGameData(this NetworkedPlayerInfo playerInfo)
    {
        MessageWriter writer = MessageWriter.Get(SendOption.Reliable);
        writer.StartMessage(5);
        writer.Write(AmongUsClient.Instance.GameId);
        writer.StartMessage(1);
        writer.WritePacked(playerInfo.NetId);
        playerInfo.Serialize(writer, false);
        writer.EndMessage();
        writer.EndMessage();
        AmongUsClient.Instance.SendOrDisconnect(writer);
        writer.Recycle();
    }

    public static void Kill(this PlayerControl killer, PlayerControl target)
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
        
        if (Options.CurrentGameMode == CustomGameMode.SoloKombat) return;

        if (target == null) target = killer;

        CheckAndSpawnAdditionalRefugee(target.Data);

        if (target.GetTeam() is Team.Impostor or Team.Neutral) Stressed.OnNonCrewmateDead();

        if (killer.Is(CustomRoles.Damocles))
            Damocles.OnMurder(killer.PlayerId);
        else if (killer.Is(Team.Impostor))
            Damocles.OnOtherImpostorMurder();
        else if (target.Is(Team.Impostor)) Damocles.OnImpostorDeath();

        if (killer.Is(CustomRoles.Bloodlust))
            FixedUpdatePatch.AddExtraAbilityUsesOnFinishedTasks(killer);
        else
            IncreaseAbilityUseLimitOnKill(killer);

        target.SetRealKiller(killer, true);

        if (target.PlayerId == Godfather.GodfatherTarget)
        {
            PlayerControl realKiller = Main.PlayerStates.TryGetValue(target.PlayerId, out PlayerState state) ? state.RealKiller.ID.GetPlayer() ?? killer : killer;
            realKiller.RpcSetCustomRole(CustomRoles.Refugee);
        }

        if (target.Is(CustomRoles.Jackal)) Jackal.Instances.Do(x => x.PromoteSidekick());

        Main.DiedThisRound.Add(target.PlayerId);

        if (killer.IsLocalPlayer() && !killer.HasKillButton() && killer.PlayerId != target.PlayerId && Options.CurrentGameMode == CustomGameMode.Standard)
            Achievements.Type.InnocentKiller.Complete();

        if (Options.AnonymousBodies.GetBool())
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

        switch (killer.PlayerId == target.PlayerId)
        {
            case true when killer.shapeshifting:
                LateTask.New(DoKill, 1.5f, "Shapeshifting Suicide Delay");
                return;
            case false when !killer.Is(CustomRoles.Pestilence) && Main.PlayerStates[target.PlayerId].Role is SchrodingersCat cat:
                cat.OnCheckMurderAsTarget(killer, target);
                return;
            default:
                DoKill();
                break;
        }

        return;

        void DoKill()
        {
            killer.RpcMurderPlayer(target, true);

            if (Main.PlayerStates.TryGetValue(target.PlayerId, out var state) && !state.IsDead)
                state.SetDead();

            LateTask.New(() =>
            {
                Vector2 pos = Object.FindObjectsOfType<DeadBody>().First(x => x.ParentId == target.PlayerId).TruePosition;

                if (Vector2.Distance(pos, Pelican.GetBlackRoomPS()) > 2f)
                {
                    foreach (PlayerState ps in Main.PlayerStates.Values)
                    {
                        if (!ps.IsDead && ps.Role.SeesArrowsToDeadBodies && !ps.SubRoles.Contains(CustomRoles.Blind) && ps.Player != null)
                        {
                            LocateArrow.Add(ps.Player.PlayerId, pos);
                            NotifyRoles(SpecifySeer: ps.Player, SpecifyTarget: ps.Player);
                        }
                    }
                }
            }, 0.2f);
        }
    }

    public static bool RpcCheckAndMurder(this PlayerControl killer, PlayerControl target, bool check = false)
    {
        return CheckMurderPatch.RpcCheckAndMurder(killer, target, check);
    }

    public static void NoCheckStartMeeting(this PlayerControl reporter, NetworkedPlayerInfo target, bool force = false)
    {
        if (Options.DisableMeeting.GetBool() && !force) return;

        ReportDeadBodyPatch.AfterReportTasks(reporter, target);
        MeetingRoomManager.Instance.AssignSelf(reporter, target);

        LateTask.New(() =>
        {
            FastDestroyableSingleton<HudManager>.Instance.OpenMeetingRoom(reporter);
            reporter.RpcStartMeeting(target);
        }, 0.2f, "NoCheckStartMeeting Delay");
    }

    public static bool UsesPetInsteadOfKill(this PlayerControl pc)
    {
        return pc != null && !pc.Is(CustomRoles.Bloodlust) && pc.GetCustomRole().UsesPetInsteadOfKill();
    }

    public static bool IsLocalPlayer(this PlayerControl pc)
    {
        if (pc == null) return false;
        return pc.PlayerId == PlayerControl.LocalPlayer.PlayerId;
    }

    public static bool IsLocalPlayer(this NetworkedPlayerInfo npi)
    {
        if (npi == null) return false;
        return npi.PlayerId == PlayerControl.LocalPlayer.PlayerId;
    }

    public static bool IsModdedClient(this PlayerControl player)
    {
        return player.IsLocalPlayer() || player.IsHost() || Main.PlayerVersion.ContainsKey(player.PlayerId);
    }

    public static List<PlayerControl> GetPlayersInAbilityRangeSorted(this PlayerControl player, bool ignoreColliders = false)
    {
        return GetPlayersInAbilityRangeSorted(player, _ => true, ignoreColliders);
    }

    public static List<PlayerControl> GetPlayersInAbilityRangeSorted(this PlayerControl player, Predicate<PlayerControl> predicate, bool ignoreColliders = false)
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

    public static bool IsNeutralKiller(this PlayerControl player)
    {
        return player.Is(CustomRoles.Bloodlust) || (player.GetCustomRole().IsNK() && !player.IsMadmate());
    }

    public static bool IsNeutralBenign(this PlayerControl player)
    {
        return player.GetCustomRole().GetNeutralRoleCategory() == RoleOptionType.Neutral_Benign;
    }

    public static bool IsNeutralEvil(this PlayerControl player)
    {
        return player.GetCustomRole().GetNeutralRoleCategory() == RoleOptionType.Neutral_Evil;
    }

    public static bool IsNeutralPariah(this PlayerControl player)
    {
        return player.GetCustomRole().GetNeutralRoleCategory() == RoleOptionType.Neutral_Pariah;
    }

    public static bool IsSnitchTarget(this PlayerControl player)
    {
        return player.Is(CustomRoles.Bloodlust) || Framer.FramedPlayers.Contains(player.PlayerId) || Enchanter.EnchantedPlayers.Contains(player.PlayerId) || player.GetCustomRole().IsSnitchTarget();
    }

    public static bool IsMadmate(this PlayerControl player)
    {
        return player.Is(CustomRoles.Madmate) || player.GetCustomRole().IsMadmate();
    }

    public static bool HasGhostRole(this PlayerControl player)
    {
        return GhostRolesManager.AssignedGhostRoles.ContainsKey(player.PlayerId) || (Main.PlayerStates.TryGetValue(player.PlayerId, out PlayerState state) && state.SubRoles.Any(x => x.IsGhostRole()));
    }

    public static bool KnowDeathReason(this PlayerControl seer, PlayerControl target)
    {
        return (seer.Is(CustomRoles.Doctor)
                || seer.Is(CustomRoles.Autopsy)
                || Options.EveryoneSeesDeathReasons.GetBool()
                || target.Is(CustomRoles.Gravestone)
                || (!seer.IsAlive() && Options.GhostCanSeeDeathReason.GetBool()))
               && !target.IsAlive();
    }

    public static string GetRoleInfo(this PlayerControl player, bool infoLong = false)
    {
        CustomRoles role = player.GetCustomRole();
        if (role is CustomRoles.Crewmate or CustomRoles.Impostor) infoLong = false;

        string info = (role.IsVanilla() ? "Blurb" : "Info") + (infoLong ? "Long" : string.Empty);
        return GetString($"{role.ToString()}{info}");
    }

    public static void SetRealKiller(this PlayerControl target, PlayerControl killer, bool notOverRide = false)
    {
        if (target == null)
        {
            Logger.Info("target is null", "SetRealKiller");
            return;
        }

        PlayerState state = Main.PlayerStates[target.PlayerId];
        if (state.RealKiller.TimeStamp != DateTime.MinValue && notOverRide) return; // Do not overwrite if value already exists

        byte killerId = killer == null ? byte.MaxValue : killer.PlayerId;
        RPC.SetRealKiller(target.PlayerId, killerId);
    }

    public static PlayerControl GetRealKiller(this PlayerControl target)
    {
        byte killerId = Main.PlayerStates[target.PlayerId].GetRealKiller();
        return killerId == byte.MaxValue ? null : GetPlayerById(killerId);
    }

    public static PlainShipRoom GetPlainShipRoom(this PlayerControl pc)
    {
        if (!pc.IsAlive() || Pelican.IsEaten(pc.PlayerId)) return null;

        Il2CppReferenceArray<PlainShipRoom> rooms = ShipStatus.Instance.AllRooms;
        return rooms.Where(room => room.roomArea).FirstOrDefault(room => pc.Collider.IsTouching(room.roomArea));
    }

    public static bool IsImpostor(this PlayerControl pc)
    {
        return !pc.Is(CustomRoles.Bloodlust) && pc.GetCustomRole().IsImpostor();
    }

    public static bool IsCrewmate(this PlayerControl pc)
    {
        return !pc.Is(CustomRoles.Bloodlust) && pc.GetCustomRole().IsCrewmate() && !pc.Is(CustomRoleTypes.Coven);
    }

    public static CustomRoleTypes GetCustomRoleTypes(this PlayerControl pc)
    {
        return pc.Is(CustomRoles.Bloodlust) ? CustomRoleTypes.Neutral : pc.GetCustomRole().GetCustomRoleTypes();
    }

    public static RoleTypes GetRoleTypes(this PlayerControl pc)
    {
        try
        {
            if (Main.HasJustStarted) throw new("HasJustStarted");
            return pc.GetRoleMap().RoleType;
        }
        catch
        {
            return pc.GetCustomSubRoles() switch
            {
                { } x when x.Contains(CustomRoles.Bloodlust) => RoleTypes.Impostor,
                { } x when x.Contains(CustomRoles.Nimble) && !pc.HasDesyncRole() => RoleTypes.Engineer,
                { } x when x.Contains(CustomRoles.Physicist) => RoleTypes.Scientist,
                { } x when x.Contains(CustomRoles.Finder) => RoleTypes.Tracker,
                { } x when x.Contains(CustomRoles.Noisy) => RoleTypes.Noisemaker,
                _ => pc.GetCustomRole().GetRoleTypes()
            };
        }
    }

    public static bool Is(this PlayerControl target, CustomRoles role)
    {
        return role > CustomRoles.NotAssigned ? target.GetCustomSubRoles().Contains(role) : target.GetCustomRole() == role;
    }

    public static bool Is(this PlayerControl target, CustomRoleTypes type)
    {
        return target.GetCustomRoleTypes() == type;
    }

    public static bool Is(this PlayerControl target, RoleTypes type)
    {
        return (target.Is(CustomRoles.Bloodlust) && type == RoleTypes.Impostor) || target.GetCustomRole().GetRoleTypes() == type;
    }

    public static bool Is(this PlayerControl target, CountTypes type)
    {
        return target.GetCountTypes() == type;
    }

    public static bool Is(this PlayerControl target, Team team)
    {
        return team switch
        {
            Team.Coven => target.GetCustomRole().IsCoven() || target.Is(CustomRoles.Entranced),
            Team.Impostor => (target.IsMadmate() || target.GetCustomRole().IsImpostorTeamV2() || Framer.FramedPlayers.Contains(target.PlayerId)) && !target.Is(CustomRoles.Bloodlust),
            Team.Neutral => target.GetCustomRole().IsNeutralTeamV2() || target.Is(CustomRoles.Bloodlust) || target.IsConverted(),
            Team.Crewmate => target.GetCustomRole().IsCrewmateTeamV2(),
            Team.None => target.Is(CustomRoles.GM) || target.Is(CountTypes.None) || target.Is(CountTypes.OutOfGame),
            _ => false
        };
    }

    public static Team GetTeam(this PlayerControl target)
    {
        if (Framer.FramedPlayers.Contains(target.PlayerId)) return Team.Impostor;

        List<CustomRoles> subRoles = target.GetCustomSubRoles();
        if (subRoles.Contains(CustomRoles.Bloodlust) || target.IsConverted()) return Team.Neutral;
        if (subRoles.Contains(CustomRoles.Madmate)) return Team.Impostor;

        CustomRoles role = target.GetCustomRole();
        if (role.IsCoven()) return Team.Coven;
        if (role.IsImpostorTeamV2()) return Team.Impostor;
        if (role.IsNeutralTeamV2()) return Team.Neutral;
        return role.IsCrewmateTeamV2() ? Team.Crewmate : Team.None;
    }

    public static bool IsConverted(this PlayerControl target)
    {
        return target.GetCustomSubRoles().Any(x => x.IsConverted());
    }

    public static bool IsAlive(this PlayerControl target)
    {
        if (target == null || target.Is(CustomRoles.GM)) return false;

        return GameStates.IsLobby || !Main.PlayerStates.TryGetValue(target.PlayerId, out PlayerState ps) || !ps.IsDead;
    }

    public static bool IsProtected(this PlayerControl self)
    {
        return self.protectedByGuardianId > -1;
    }

    public static bool IsTrusted(this PlayerControl pc)
    {
        if (pc.FriendCode.GetDevUser().up) return true;

        if (ChatCommands.IsPlayerModerator(pc.FriendCode)) return true;
        if (ChatCommands.IsPlayerVIP(pc.FriendCode)) return true;
        if (PrivateTagManager.Tags.ContainsKey(pc.FriendCode)) return true;
        
        ClientData client = pc.GetClient();
        return client != null && FastDestroyableSingleton<FriendsListManager>.Instance.IsPlayerFriend(client.ProductUserId);
    }

    public static bool IsBeginner(this PlayerControl pc)
    {
        if (pc.IsModdedClient() || pc.IsTrusted() || pc.FriendCode.GetDevUser().HasTag()) return false;
        return !Main.GamesPlayed.TryGetValue(pc.FriendCode, out int gamesPlayed) || gamesPlayed < 4;
    }
}