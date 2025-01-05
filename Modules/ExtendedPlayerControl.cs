using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using AmongUs.GameOptions;
using EHR.AddOns.Crewmate;
using EHR.AddOns.Impostor;
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

    public static bool CanUseVent(this PlayerControl player)
    {
        return CanUseVent(player, GetClosestVent(player)?.Id ?? int.MaxValue);
    }

    public static bool CanUseVent(this PlayerControl player, int ventId)
    {
        if (CustomGameMode.RoomRush.IsActiveOrIntegrated()) return true;
        if (player.Is(CustomRoles.Trainee) && MeetingStates.FirstMeeting) return false;
        return GameStates.IsInTask && ((player.inVent && player.GetClosestVent()?.Id == ventId) || ((player.CanUseImpostorVentButton() || player.GetRoleTypes() == RoleTypes.Engineer) && Main.PlayerStates.Values.All(x => x.Role.CanUseVent(player, ventId))));
    }

    // Next 3: https://github.com/Rabek009/MoreGamemodes/blob/master/Modules/ExtendedPlayerControl.cs
    public static Vent GetClosestVent(this PlayerControl player)
    {
        Vector2 pos = player.Pos();
        return ShipStatus.Instance?.AllVents?.Where(x => x != null).MinBy(x => Vector2.Distance(pos, x.transform.position));
    }

    public static List<Vent> GetVentsFromClosest(this PlayerControl player)
    {
        Vector2 playerpos = player.transform.position;
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

    public static void RpcSetVentInteraction(this PlayerControl player)
    {
        VentilationSystemDeterioratePatch.SerializeV2(ShipStatus.Instance.Systems[SystemTypes.Ventilation].Cast<VentilationSystem>(), player);
    }

    public static void SetChatVisible(this PlayerControl player) // Credit: NikoCat233
    {
        if (!GameStates.IsInGame || !AmongUsClient.Instance.AmHost || GameStates.IsMeeting) return;

        if (player.IsHost())
        {
            HudManager.Instance.Chat.SetVisible(true);
            return;
        }

        if (player.IsModClient())
        {
            MessageWriter modsend = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.ShowChat, SendOption.Reliable, player.OwnerId);
            modsend.WritePacked(player.OwnerId);
            modsend.Write(true);
            AmongUsClient.Instance.FinishRpcImmediately(modsend);
            return;
        }

        uint customNetId = AmongUsClient.Instance.NetIdCnt++;
        MessageWriter vanillasend = MessageWriter.Get(SendOption.Reliable);
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

        AmongUsClient.Instance.SendOrDisconnect(vanillasend);
        vanillasend.Recycle();
    }

    public static void RpcExile(this PlayerControl player)
    {
        RPC.ExileAsync(player);
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

        return Main.PlayerStates.TryGetValue(player.PlayerId, out PlayerState State) ? State.MainRole : CustomRoles.Crewmate;
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

    public static void RpcSetNameEx(this PlayerControl player, string name)
    {
        try
        {
            if (player == null)
            {
                Logger.Warn("The player is null", "RpcSetNameEx");
                return;
            }

            foreach (PlayerControl seer in Main.AllPlayerControls)
            {
                try { Main.LastNotifyNames[(player.PlayerId, seer.PlayerId)] = name; }
                catch { }
            }

            if (player.Data != null) Logger.Info($"Set: {player.Data.PlayerName} => {name} for Everyone", "RpcSetNameEx");

            player.RpcSetName(name);
        }
        catch (Exception e) { ThrowException(e); }
    }

    public static void RpcSetNamePrivate(this PlayerControl player, string name, PlayerControl seer = null, bool force = false)
    {
        if (player == null || name == null || !AmongUsClient.Instance.AmHost) return;

        if (seer == null) seer = player;

        if (!force && Main.LastNotifyNames[(player.PlayerId, seer.PlayerId)] == name) return;

        Main.LastNotifyNames[(player.PlayerId, seer.PlayerId)] = name;
        Logger.Info($"Set: {player.Data?.PlayerName} => {name} for {seer.GetNameWithRole().RemoveHtmlTags()}", "RpcSetNamePrivate");

        if (seer == null || player == null) return;

        int clientId = seer.GetClientId();
        if (clientId == -1) return;

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(player.NetId, (byte)RpcCalls.SetName, SendOption.Reliable, clientId);
        writer.Write(seer.Data.NetId);
        writer.Write(name);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    // By TommyXL
    public static void RpcSetPetDesync(this PlayerControl player, string petId, PlayerControl seer)
    {
        int clientId = seer.GetClientId();
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

    public static void RpcSetRoleDesync(this PlayerControl player, RoleTypes role, int clientId)
    {
        if (player == null) return;

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

        GhostRolesManager.RemoveGhostRole(player.PlayerId);
        Main.PlayerStates[player.PlayerId].IsDead = false;
        Main.PlayerStates[player.PlayerId].deathReason = PlayerState.DeathReason.etc;
        player.RpcChangeRoleBasis(player.GetRoleMap().CustomRole, true);
        player.ResetKillCooldown();
        player.SyncSettings();
        player.SetKillCooldown();
        player.RpcResetAbilityCooldown();
        player.SyncGeneralOptions();

        if (Options.AnonymousBodies.GetBool() && Main.AllPlayerNames.TryGetValue(player.PlayerId, out var name))
            RpcChangeSkin(player, new NetworkedPlayerInfo.PlayerOutfit().Set(name, 15, "", "", "", "", ""));

        Camouflage.RpcSetSkin(player);

        NotifyRoles(SpecifySeer: player, NoCache: true);
        NotifyRoles(SpecifyTarget: player, NoCache: true);
    }

    // https://github.com/Ultradragon005/TownofHost-Enhanced/blob/ea5f1e8ea87e6c19466231c305d6d36d511d5b2d/Modules/ExtendedPlayerControl.cs
    public static void RpcChangeRoleBasis(this PlayerControl player, CustomRoles newCustomRole, bool loggerRoleMap = false)
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

        byte playerId = player.PlayerId;
        int playerClientId = player.GetClientId();
        CustomRoles playerRole = Utils.GetRoleMap(playerId).CustomRole;
        RoleTypes newRoleType = newCustomRole.GetRoleTypes();
        RoleTypes remeberRoleType;

        newRoleType = Options.CurrentGameMode switch
        {
            CustomGameMode.Speedrun when newCustomRole == CustomRoles.Runner => SpeedrunManager.CanKill.Contains(playerId) ? RoleTypes.Impostor : RoleTypes.Crewmate,
            CustomGameMode.Standard when StartGameHostPatch.BasisChangingAddons.FindFirst(x => x.Value.Contains(playerId), out KeyValuePair<CustomRoles, List<byte>> kvp) => kvp.Key switch
            {
                CustomRoles.Bloodlust when newRoleType is RoleTypes.Crewmate or RoleTypes.Engineer or RoleTypes.Scientist => RoleTypes.Impostor,
                CustomRoles.Nimble when newRoleType is RoleTypes.Crewmate or RoleTypes.Scientist => RoleTypes.Engineer,
                CustomRoles.Physicist when newRoleType == RoleTypes.Crewmate => RoleTypes.Scientist,
                CustomRoles.Finder when newRoleType is RoleTypes.Crewmate or RoleTypes.Scientist => RoleTypes.Tracker,
                CustomRoles.Noisy when newRoleType == RoleTypes.Crewmate => RoleTypes.Noisemaker,
                _ => newRoleType
            },
            _ => newRoleType
        };

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
                    int seerClientId = seer.GetClientId();
                    if (seerClientId == -1) continue;

                    bool seerIsHost = seer.IsHost();
                    bool self = playerId == seer.PlayerId;

                    if (!self && seer.HasDesyncRole() && !seerIsHost)
                        remeberRoleType = newRoleVN is CustomRoles.Noisemaker ? RoleTypes.Noisemaker : RoleTypes.Scientist;
                    else
                        remeberRoleType = newRoleType;

                    // Set role type for seer
                    StartGameHostPatch.RpcSetRoleReplacer.RoleMap[(seer.PlayerId, playerId)] = (remeberRoleType, newCustomRole);
                    player.RpcSetRoleDesync(remeberRoleType, seerClientId);

                    if (self) continue;

                    (RoleTypes seerRoleType, CustomRoles seerCustomRole) = seer.GetRoleMap();

                    if (seer.IsAlive())
                    {
                        if (seerCustomRole.IsDesyncRole())
                            remeberRoleType = seerIsHost ? RoleTypes.Crewmate : RoleTypes.Scientist;
                        else
                            remeberRoleType = seerRoleType;
                    }
                    else
                    {
                        bool playerIsKiller = playerRole.IsImpostor();

                        remeberRoleType = RoleTypes.CrewmateGhost;
                        if (!playerIsKiller && seer.Is(Team.Impostor)) remeberRoleType = RoleTypes.ImpostorGhost;

                        StartGameHostPatch.RpcSetRoleReplacer.RoleMap[(playerId, seer.PlayerId)] = (seerCustomRole.IsDesyncRole() ? seerIsHost ? RoleTypes.Crewmate : RoleTypes.Scientist : seerRoleType, seerCustomRole);
                        seer.RpcSetRoleDesync(remeberRoleType, playerClientId);
                        continue;
                    }

                    // Set role type for player
                    StartGameHostPatch.RpcSetRoleReplacer.RoleMap[(playerId, seer.PlayerId)] = (remeberRoleType, seerCustomRole);
                    seer.RpcSetRoleDesync(remeberRoleType, playerClientId);
                }

                break;
            }
            // Normal role to desync role
            case (false, true):
            {
                foreach (PlayerControl seer in Main.AllPlayerControls)
                {
                    int seerClientId = seer.GetClientId();
                    if (seerClientId == -1) continue;

                    bool self = playerId == seer.PlayerId;

                    if (self)
                    {
                        remeberRoleType = player.IsHost() ? RoleTypes.Crewmate : RoleTypes.Impostor;

                        // For Desync Shapeshifter
                        if (newRoleDY is RoleTypes.Shapeshifter) remeberRoleType = RoleTypes.Shapeshifter;
                    }
                    else
                        remeberRoleType = newRoleVN is CustomRoles.Noisemaker ? RoleTypes.Noisemaker : RoleTypes.Scientist;

                    StartGameHostPatch.RpcSetRoleReplacer.RoleMap[(seer.PlayerId, playerId)] = (remeberRoleType, newCustomRole);
                    player.RpcSetRoleDesync(remeberRoleType, seerClientId);

                    if (self) continue;

                    CustomRoles seerCustomRole = seer.GetRoleMap().CustomRole;

                    if (seer.IsAlive())
                        remeberRoleType = newRoleVN is CustomRoles.Noisemaker ? RoleTypes.Noisemaker : RoleTypes.Scientist;
                    else
                    {
                        remeberRoleType = RoleTypes.CrewmateGhost;

                        StartGameHostPatch.RpcSetRoleReplacer.RoleMap[(playerId, seer.PlayerId)] = (seerCustomRole.GetVNRole() is CustomRoles.Noisemaker ? RoleTypes.Noisemaker : RoleTypes.Scientist, seerCustomRole);
                        seer.RpcSetRoleDesync(remeberRoleType, playerClientId);
                        continue;
                    }

                    // Set role type for player
                    StartGameHostPatch.RpcSetRoleReplacer.RoleMap[(playerId, seer.PlayerId)] = (remeberRoleType, seerCustomRole);
                    seer.RpcSetRoleDesync(remeberRoleType, playerClientId);
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
                    int seerClientId = seer.GetClientId();
                    if (seerClientId == -1) continue;

                    if ((playerIsDesync || seer.HasDesyncRole()) && seer.PlayerId != playerId)
                        remeberRoleType = Utils.GetRoleMap(seer.PlayerId, playerId).RoleType;
                    else
                        remeberRoleType = newRoleType;

                    StartGameHostPatch.RpcSetRoleReplacer.RoleMap[(seer.PlayerId, playerId)] = (remeberRoleType, newCustomRole);
                    player.RpcSetRoleDesync(remeberRoleType, seerClientId);
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

        Logger.Info($"{player.GetNameWithRole()}'s role basis was changed to {newRoleType} ({newCustomRole}) (from role: {playerRole}) - oldRoleIsDesync: {oldRoleIsDesync}, newRoleIsDesync: {newRoleIsDesync}", "RpcChangeRoleBasis");

        Main.ChangedRole = true;
    }

    // https://github.com/Ultradragon005/TownofHost-Enhanced/blob/ea5f1e8ea87e6c19466231c305d6d36d511d5b2d/Modules/Utils.cs
    public static void SyncGeneralOptions(this PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost || !GameStates.IsInGame) return;

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncGeneralOptions, SendOption.Reliable);
        writer.Write(player.PlayerId);
        writer.WritePacked((int)player.GetCustomRole());
        writer.Write(Main.PlayerStates[player.PlayerId].IsDead);
        writer.WritePacked((int)Main.PlayerStates[player.PlayerId].deathReason);
        writer.Write(Main.AllPlayerKillCooldown[player.PlayerId]);
        writer.Write(Main.AllPlayerSpeed[player.PlayerId]);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    // https://github.com/0xDrMoe/TownofHost-Enhanced/pull/1189/files
    public static void RpcExileDesync(this PlayerControl player, PlayerControl seer)
    {
        int clientId = seer.GetClientId();

        if (AmongUsClient.Instance.ClientId == clientId)
        {
            player.Exiled();
            return;
        }

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(player.NetId, (byte)RpcCalls.Exiled, SendOption.None, clientId);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    // Next 8: https://github.com/0xDrMoe/TownofHost-Enhanced/blob/12487ce1aa7e4f5087f2300be452b5af7c04d1ff/Modules/ExtendedPlayerControl.cs#L239
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

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(physics.NetId, (byte)RpcCalls.EnterVent, SendOption.Reliable, seer.GetClientId());
        writer.WritePacked(ventId);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void RpcExitVentDesync(this PlayerPhysics physics, int ventId, PlayerControl seer)
    {
        if (physics == null) return;

        int clientId = seer.GetClientId();

        if (AmongUsClient.Instance.ClientId == clientId)
        {
            physics.StopAllCoroutines();
            physics.StartCoroutine(physics.CoExitVent(ventId));
            return;
        }

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(physics.NetId, (byte)RpcCalls.ExitVent, SendOption.Reliable, seer.GetClientId());
        writer.WritePacked(ventId);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void RpcBootFromVentDesync(this PlayerPhysics physics, int ventId, PlayerControl seer)
    {
        if (physics == null) return;

        int clientId = seer.GetClientId();

        if (AmongUsClient.Instance.ClientId == clientId)
        {
            physics.BootFromVent(ventId);
            return;
        }

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(physics.NetId, (byte)RpcCalls.BootFromVent, SendOption.Reliable, seer.GetClientId());
        writer.WritePacked(ventId);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void RpcCheckVanishDesync(this PlayerControl player, PlayerControl seer)
    {
        if (AmongUsClient.Instance.ClientId == seer.GetClientId())
        {
            player.CheckVanish();
            return;
        }

        MessageWriter messageWriter = AmongUsClient.Instance.StartRpcImmediately(player.NetId, (byte)RpcCalls.CheckVanish, SendOption.None, seer.GetClientId());
        messageWriter.Write(0); // not used, lol
        AmongUsClient.Instance.FinishRpcImmediately(messageWriter);
    }

    public static void RpcStartVanishDesync(this PlayerControl player, PlayerControl seer)
    {
        if (AmongUsClient.Instance.ClientId == seer.GetClientId())
        {
            player.SetRoleInvisibility(true, false, true);
            return;
        }

        MessageWriter msg = AmongUsClient.Instance.StartRpcImmediately(player.NetId, (byte)RpcCalls.StartVanish, SendOption.None, seer.GetClientId());
        AmongUsClient.Instance.FinishRpcImmediately(msg);
    }

    public static void RpcCheckAppearDesync(this PlayerControl player, bool shouldAnimate, PlayerControl seer)
    {
        if (AmongUsClient.Instance.ClientId == seer.GetClientId())
        {
            player.CheckAppear(shouldAnimate);
            return;
        }

        MessageWriter messageWriter = AmongUsClient.Instance.StartRpcImmediately(player.NetId, (byte)RpcCalls.CheckAppear, SendOption.None, seer.GetClientId());
        messageWriter.Write(shouldAnimate);
        AmongUsClient.Instance.FinishRpcImmediately(messageWriter);
    }

    public static void RpcStartAppearDesync(this PlayerControl player, bool shouldAnimate, PlayerControl seer)
    {
        if (AmongUsClient.Instance.ClientId == seer.GetClientId())
        {
            player.SetRoleInvisibility(false, shouldAnimate, true);
            return;
        }

        MessageWriter messageWriter = AmongUsClient.Instance.StartRpcImmediately(player.NetId, (byte)RpcCalls.StartAppear, SendOption.None, seer.GetClientId());
        messageWriter.Write(shouldAnimate);
        AmongUsClient.Instance.FinishRpcImmediately(messageWriter);
    }

    public static void RpcCheckAppear(this PlayerControl player, bool shouldAnimate)
    {
        player.CheckAppear(shouldAnimate);
        MessageWriter messageWriter = AmongUsClient.Instance.StartRpcImmediately(player.NetId, (byte)RpcCalls.CheckAppear, SendOption.None);
        messageWriter.Write(shouldAnimate);
        AmongUsClient.Instance.FinishRpcImmediately(messageWriter);
    }

    public static bool HasDesyncRole(this PlayerControl player)
    {
        return player.Is(CustomRoles.Bloodlust) || player.GetCustomRole().IsDesyncRole();
    }

    public static void KillFlash(this PlayerControl player)
    {
        if (GameStates.IsLobby) return;

        // Kill flash (blackout + reactor flash) processing

        SystemTypes systemtypes = (MapNames)Main.NormalOptions.MapId switch
        {
            MapNames.Polus => SystemTypes.Laboratory,
            MapNames.Airship => SystemTypes.HeliSabotage,
            _ => SystemTypes.Reactor
        };

        bool ReactorCheck = IsActive(systemtypes); // Checking whether the reactor sabotage is active

        float Duration = Options.KillFlashDuration.GetFloat();
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
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(killer.NetId, (byte)RpcCalls.MurderPlayer, SendOption.Reliable, killer.GetClientId());
            writer.WriteNetObject(target);
            writer.Write((int)MurderResultFlags.FailedProtected);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        if (!fromSetKCD) killer.AddKillTimerToDict(true);
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

    public static bool HasAbilityCD(this PlayerControl pc)
    {
        return Main.AbilityCD.ContainsKey(pc.PlayerId);
    }

    public static void AddKCDAsAbilityCD(this PlayerControl pc)
    {
        AddAbilityCD(pc, (int)Math.Round(Main.AllPlayerKillCooldown.TryGetValue(pc.PlayerId, out float KCD) ? KCD : Options.DefaultKillCooldown));
    }

    public static void AddAbilityCD(this PlayerControl pc, bool includeDuration = true)
    {
        Utils.AddAbilityCD(pc.GetCustomRole(), pc.PlayerId, includeDuration);
    }

    public static void AddAbilityCD(this PlayerControl pc, int CD)
    {
        Main.AbilityCD[pc.PlayerId] = (TimeStamp, CD);
        SendRPC(CustomRPC.SyncAbilityCD, 1, pc.PlayerId, CD);
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

        if (AmongUsClient.Instance.AmHost && playerId.IsPlayerModClient() && !playerId.IsHost() && rpc)
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncAbilityUseLimit, SendOption.Reliable);
            writer.Write(playerId);
            writer.Write(limit);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        PlayerControl pc = GetPlayerById(playerId);
        NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
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

        if (!player.CanUseKillButton()) return;

        player.AddKillTimerToDict(CD: time);
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
        else if (forceAnime || !player.IsModClient() || !Options.DisableShieldAnimations.GetBool())
        {
            player.SyncSettings();
            player.RpcGuardAndKill(target, fromSetKCD: true);
        }
        else
        {
            time = Main.AllPlayerKillCooldown[player.PlayerId] / 2;

            if (player.AmOwner)
                PlayerControl.LocalPlayer.SetKillTimer(time);
            else
            {
                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetKillTimer, SendOption.Reliable, player.GetClientId());
                writer.Write(time);
                AmongUsClient.Instance.FinishRpcImmediately(writer);
            }

            Main.AllPlayerControls.Where(x => x.Is(CustomRoles.Observer) && target.PlayerId != x.PlayerId).Do(x => x.RpcGuardAndKill(target, true, true));
        }

        if (player.GetCustomRole() is not CustomRoles.Inhibitor and not CustomRoles.Saboteur) player.ResetKillCooldown();
    }

    public static void RpcSpecificMurderPlayer(this PlayerControl killer, PlayerControl target = null)
    {
        if (target == null) target = killer;

        if (killer.AmOwner)
            killer.MurderPlayer(target, ResultFlags);
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
        MessageWriter messageWriter = AmongUsClient.Instance.StartRpcImmediately(ShipStatus.Instance.NetId, (byte)RpcCalls.UpdateSystem, SendOption.Reliable, target.GetClientId());
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

    public static bool IsNonHostModClient(this PlayerControl pc)
    {
        return pc.IsModClient() && !pc.IsHost();
    }

    public static string GetDisplayRoleName(this PlayerControl player, bool pure = false, bool seeTargetBetrayalAddons = false)
    {
        return Utils.GetDisplayRoleName(player.PlayerId, pure, seeTargetBetrayalAddons);
    }

    public static string GetSubRoleNames(this PlayerControl player, bool forUser = false)
    {
        List<CustomRoles> SubRoles = Main.PlayerStates[player.PlayerId].SubRoles;
        if (SubRoles.Count == 0) return string.Empty;

        StringBuilder sb = new();

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

        string text = GetRoleName(player.GetCustomRole(), forUser);
        text += player.GetSubRoleNames(forUser);
        return text;
    }

    public static string GetNameWithRole(this PlayerControl player, bool forUser = false)
    {
        return $"{player?.Data?.PlayerName}" + (GameStates.IsInGame && Options.CurrentGameMode is not CustomGameMode.FFA and not CustomGameMode.MoveAndStop and not CustomGameMode.HotPotato and not CustomGameMode.Speedrun and not CustomGameMode.CaptureTheFlag and not CustomGameMode.NaturalDisasters and not CustomGameMode.RoomRush and not CustomGameMode.AllInOne ? $" ({player?.GetAllRoleName(forUser).RemoveHtmlTags().Replace('\n', ' ')})" : string.Empty);
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

        SystemTypes systemtypes = (MapNames)Main.NormalOptions.MapId switch
        {
            MapNames.Polus => SystemTypes.Laboratory,
            MapNames.Airship => SystemTypes.HeliSabotage,
            _ => SystemTypes.Reactor
        };

        LateTask.New(() => pc.RpcDesyncRepairSystem(systemtypes, 128), 0f + delay, "Reactor Desync");

        LateTask.New(() => pc.RpcSpecificMurderPlayer(), 0.2f + delay, "Murder To Reset Cam");

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

        SystemTypes systemtypes = (MapNames)Main.NormalOptions.MapId switch
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

        int seerClientId = seer.GetClientId();
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

    public static bool HasKillButton(this PlayerControl pc)
    {
        CustomRoles role = pc.GetCustomRole();
        if (pc.Data.Role.Role == RoleTypes.GuardianAngel) return false;

        if (role.GetVNRole(true) is CustomRoles.Impostor or CustomRoles.Shapeshifter or CustomRoles.Phantom) return true;

        if (pc.GetRoleTypes() is RoleTypes.Impostor or RoleTypes.Shapeshifter or RoleTypes.Phantom) return true;

        if (pc.Is(CustomRoles.Bloodlust)) return true;

        return pc.Is(CustomRoleTypes.Impostor) || pc.IsNeutralKiller() || role.IsTasklessCrewmate();
    }

    public static bool CanUseKillButton(this PlayerControl pc)
    {
        if (AntiBlackout.SkipTasks || !pc.IsAlive()) return false;

        switch (Options.CurrentGameMode)
        {
            case CustomGameMode.HotPotato or CustomGameMode.MoveAndStop or CustomGameMode.NaturalDisasters or CustomGameMode.RoomRush:
            case CustomGameMode.Speedrun when !SpeedrunManager.CanKill.Contains(pc.PlayerId):
                return false;
            case CustomGameMode.CaptureTheFlag:
                return true;
            case CustomGameMode.AllInOne:
                return !AllInOneGameMode.Taskers.Contains(pc.PlayerId);
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

            _ => Main.PlayerStates.TryGetValue(pc.PlayerId, out PlayerState state) && state.Role.CanUseKillButton(pc)
        };
    }

    public static bool CanUseImpostorVentButton(this PlayerControl pc)
    {
        if (!pc.IsAlive() || pc.Data.Role.Role == RoleTypes.GuardianAngel || Penguin.IsVictim(pc)) return false;

        if (pc.GetRoleTypes() == RoleTypes.Engineer) return false;

        return Options.CurrentGameMode switch
        {
            CustomGameMode.SoloKombat => true,
            CustomGameMode.FFA => true,
            CustomGameMode.MoveAndStop => false,
            CustomGameMode.HotPotato => false,
            CustomGameMode.Speedrun => false,
            CustomGameMode.CaptureTheFlag => false,
            CustomGameMode.NaturalDisasters => false,
            CustomGameMode.RoomRush => false,
            CustomGameMode.AllInOne => false,

            CustomGameMode.Standard when CopyCat.Instances.Any(x => x.CopyCatPC.PlayerId == pc.PlayerId) => true,
            CustomGameMode.Standard when pc.Is(CustomRoles.Nimble) || Options.EveryoneCanVent.GetBool() => true,
            CustomGameMode.Standard when pc.Is(CustomRoles.Bloodlust) || pc.Is(CustomRoles.Refugee) => true,

            _ => Main.PlayerStates.TryGetValue(pc.PlayerId, out PlayerState state) && state.Role.CanUseImpostorVentButton(pc)
        };
    }

    public static bool CanUseSabotage(this PlayerControl pc)
    {
        if (!pc.IsAlive() || pc.inVent || pc.Data.Role.Role == RoleTypes.GuardianAngel) return false;
        return Main.PlayerStates.TryGetValue(pc.PlayerId, out PlayerState state) && state.Role.CanUseSabotage(pc);
    }

    public static Vector2 Pos(this PlayerControl pc)
    {
        return new(pc.transform.position.x, pc.transform.position.y);
    }

    public static void AddKillTimerToDict(this PlayerControl pc, bool half = false, float CD = -1f)
    {
        float resultKCD;

        if (Math.Abs(CD - -1f) < 0.5f)
        {
            resultKCD = Main.AllPlayerKillCooldown.GetValueOrDefault(pc.PlayerId, 0f);

            if (half) resultKCD /= 2f;
        }
        else
            resultKCD = CD;

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

    public static void ResetKillCooldown(this PlayerControl player)
    {
        Main.PlayerStates[player.PlayerId].Role.SetKillCooldown(player.PlayerId);

        Main.AllPlayerKillCooldown[player.PlayerId] = player.GetCustomRole() switch
        {
            CustomRoles.KB_Normal => SoloPVP.KB_ATKCooldown.GetFloat(),
            CustomRoles.Killer => FFAManager.FFAKcd.GetFloat(),
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

    public static void Kill(this PlayerControl killer, PlayerControl target)
    {
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
            var realKiller = Main.PlayerStates.TryGetValue(target.PlayerId, out var state) ? state.RealKiller.ID.GetPlayer() ?? killer : killer;
            realKiller.RpcSetCustomRole(CustomRoles.Refugee);
        }

        if (target.Is(CustomRoles.Jackal)) Jackal.Instances.Do(x => x.PromoteSidekick());

        Main.DiedThisRound.Add(target.PlayerId);

        if (killer.IsLocalPlayer() && !killer.HasKillButton() && killer.PlayerId != target.PlayerId && CustomGameMode.Standard.IsActiveOrIntegrated())
            Achievements.Type.InnocentKiller.Complete();

        if (Options.AnonymousBodies.GetBool())
        {
            Main.AllPlayerSpeed[target.PlayerId] = Main.MinSpeed;
            target.SyncSettings();
            var newSkin = new NetworkedPlayerInfo.PlayerOutfit().Set(GetString("Dead"), 15, "", "", "", "", "");
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

        void DoKill() => killer.RpcMurderPlayer(target, true);
    }

    public static bool RpcCheckAndMurder(this PlayerControl killer, PlayerControl target, bool check = false)
    {
        return CheckMurderPatch.RpcCheckAndMurder(killer, target, check);
    }

    public static void NoCheckStartMeeting(this PlayerControl reporter, NetworkedPlayerInfo target, bool force = false)
    {
        if (Options.DisableMeeting.GetBool() && !force) return;

        ReportDeadBodyPatch.AfterReportTasks(reporter, target, true);
        MeetingRoomManager.Instance.AssignSelf(reporter, target);
        DestroyableSingleton<HudManager>.Instance.OpenMeetingRoom(reporter);
        reporter.RpcStartMeeting(target);
    }

    public static void CheckAndSetUnshiftState(this PlayerControl pc, bool notify = true, bool force = false)
    {
        CustomRoles role = pc.GetCustomRole();

        if (force || role.AlwaysUsesUnshift() || (role.SimpleAbilityTrigger() && Options.UseUnshiftTrigger.GetBool() && (!pc.IsNeutralKiller() || Options.UseUnshiftTriggerForNKs.GetBool())))
        {
            Logger.Info($"Set Unshift State For {pc.GetNameWithRole()}", "CheckAndSetUnshiftState");
            PlayerControl target = Main.AllAlivePlayerControls.Without(pc).RandomElement();
            NetworkedPlayerInfo.PlayerOutfit outfit = pc.Data.DefaultOutfit;
            bool process = Main.ProcessShapeshifts;
            Main.ProcessShapeshifts = false;
            pc.RpcShapeshift(target, false);
            Main.ProcessShapeshifts = process;
            Main.CheckShapeshift[pc.PlayerId] = false;

            LateTask.New(() =>
            {
                RpcChangeSkin(pc, outfit);
                if (notify) NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc, NoCache: true);
            }, 0.1f, log: false);
        }
    }

    public static bool UsesPetInsteadOfKill(this PlayerControl pc) => pc != null && !pc.Is(CustomRoles.Bloodlust) && pc.GetCustomRole().UsesPetInsteadOfKill();

    public static bool IsLocalPlayer(this PlayerControl pc) => pc.PlayerId == PlayerControl.LocalPlayer.PlayerId;
    public static bool IsLocalPlayer(this NetworkedPlayerInfo npi) => npi.PlayerId == PlayerControl.LocalPlayer.PlayerId;

    public static bool IsModClient(this PlayerControl player)
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
            if (predicate(pc))
                rangePlayers.Add(pc);

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

    public static bool IsSnitchTarget(this PlayerControl player)
    {
        return player.Is(CustomRoles.Bloodlust) || Framer.FramedPlayers.Contains(player.PlayerId) || player.GetCustomRole().IsSnitchTarget();
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
                || (seer.Data.IsDead && Options.GhostCanSeeDeathReason.GetBool()))
               && target.Data.IsDead;
    }

    public static string GetRoleInfo(this PlayerControl player, bool InfoLong = false)
    {
        CustomRoles role = player.GetCustomRole();
        if (role is CustomRoles.Crewmate or CustomRoles.Impostor) InfoLong = false;

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

        string Info = (role.IsVanilla() ? "Blurb" : "Info") + (InfoLong ? "Long" : string.Empty);
        return GetString($"{Prefix}{text}{Info}");
    }

    public static void SetRealKiller(this PlayerControl target, PlayerControl killer, bool NotOverRide = false)
    {
        if (target == null)
        {
            Logger.Info("target is null", "SetRealKiller");
            return;
        }

        PlayerState State = Main.PlayerStates[target.PlayerId];
        if (State.RealKiller.TimeStamp != DateTime.MinValue && NotOverRide) return; // Do not overwrite if value already exists

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

        Il2CppReferenceArray<PlainShipRoom> Rooms = ShipStatus.Instance.AllRooms;
        return Rooms.Where(room => room.roomArea).FirstOrDefault(room => pc.Collider.IsTouching(room.roomArea));
    }

    public static bool IsImpostor(this PlayerControl pc)
    {
        return !pc.Is(CustomRoles.Bloodlust) && pc.GetCustomRole().IsImpostor();
    }

    public static bool IsCrewmate(this PlayerControl pc)
    {
        return !pc.Is(CustomRoles.Bloodlust) && pc.GetCustomRole().IsCrewmate();
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
}