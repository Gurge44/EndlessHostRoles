using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AmongUs.GameOptions;
using BepInEx;
using EHR.Gamemodes;
using EHR.Patches;
using EHR.Roles;
using HarmonyLib;
using Hazel;
using UnityEngine;

namespace EHR;

[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.UpdateSystem), typeof(SystemTypes), typeof(PlayerControl), typeof(MessageReader))]
public static class MessageReaderUpdateSystemPatch
{
    public static bool Prefix([HarmonyArgument(0)] SystemTypes systemType, [HarmonyArgument(1)] PlayerControl player, [HarmonyArgument(2)] MessageReader reader)
    {
        try
        {
            if (systemType is SystemTypes.Ventilation or SystemTypes.Security or SystemTypes.Decontamination or SystemTypes.Decontamination2 or SystemTypes.Decontamination3 or SystemTypes.MedBay) return true;

            byte amount;
            {
                MessageReader newReader = MessageReader.Get(reader);
                amount = newReader.ReadByte();
                newReader.Recycle();
            }

            if (EAC.CheckInvalidSabotage(systemType, player, amount))
            {
                Logger.Info("EAC patched Sabotage RPC", "MessageReaderUpdateSystemPatch");
                return false;
            }

            return RepairSystemPatch.Prefix(systemType, player, amount);
        }
        catch { }

        return true;
    }

    public static void Postfix([HarmonyArgument(0)] SystemTypes systemType, [HarmonyArgument(1)] PlayerControl player)
    {
        try
        {
            if (systemType is SystemTypes.Ventilation or SystemTypes.Security or SystemTypes.Decontamination or SystemTypes.Decontamination2 or SystemTypes.Decontamination3 or SystemTypes.MedBay) return;

            RepairSystemPatch.Postfix(systemType, player);
        }
        catch { }
    }
}

[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.UpdateSystem), typeof(SystemTypes), typeof(PlayerControl), typeof(byte))]
internal static class RepairSystemPatch
{
    public static bool Prefix( /*ShipStatus __instance,*/
        [HarmonyArgument(0)] SystemTypes systemType,
        [HarmonyArgument(1)] PlayerControl player,
        [HarmonyArgument(2)] byte amount)
    {
        Logger.Msg($"SystemType: {systemType}, PlayerName: {player.GetNameWithRole().RemoveHtmlTags()}, amount: {amount}", "RepairSystem");
#if DEBUG
        if (RepairSender.Enabled && AmongUsClient.Instance.NetworkMode != NetworkModes.OnlineGame)
            Logger.SendInGame($"SystemType: {systemType}, PlayerName: {player.GetNameWithRole().RemoveHtmlTags()}, amount: {amount}");
#endif

        if (!AmongUsClient.Instance.AmHost) return true; // Execute the following only on the host

        if ((Options.CurrentGameMode is not (CustomGameMode.Standard or CustomGameMode.Snowdown) || Options.DisableSabotage.GetBool()) && systemType == SystemTypes.Sabotage) return false;
        if (player.Is(CustomRoles.Fool) && systemType is SystemTypes.Comms or SystemTypes.Electrical) return false;

        if (SubmergedCompatibility.IsSubmerged() && systemType is not (SystemTypes.Electrical or SystemTypes.Comms)) return true;

        switch (player.GetCustomRole())
        {
            case CustomRoles.Mechanic:
                Mechanic.RepairSystem(player.PlayerId, systemType, amount);
                Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player);
                break;
            case CustomRoles.Alchemist when systemType != SystemTypes.Electrical && Main.PlayerStates[player.PlayerId].Role is Alchemist { IsEnable: true, FixNextSabo: true }:
                Alchemist.RepairSystem(player, systemType, amount);
                Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player);
                break;
            case CustomRoles.Technician:
                Technician.RepairSystem(player.PlayerId, systemType, amount);
                Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player);
                break;
        }

        switch (systemType)
        {
            case SystemTypes.Doors when player.Is(CustomRoles.Unlucky) && player.IsAlive() && IRandom.Instance.Next(0, 100) < Options.UnluckySabotageSuicideChance.GetInt():
            {
                player.Suicide();
                return false;
            }
            case SystemTypes.Electrical when amount <= 4:
            {
                if (Main.NormalOptions.MapId == 4)
                {
                    if (Options.DisableAirshipViewingDeckLightsPanel.GetBool() && FastVector2.DistanceWithinRange(player.Pos(), new(-12.93f, -11.28f), 2f)) return false;
                    if (Options.DisableAirshipGapRoomLightsPanel.GetBool() && FastVector2.DistanceWithinRange(player.Pos(), new(13.92f, 6.43f), 2f)) return false;
                    if (Options.DisableAirshipCargoLightsPanel.GetBool() && FastVector2.DistanceWithinRange(player.Pos(), new(30.56f, 2.12f), 2f)) return false;
                }

                var switchSystem = ShipStatus.Instance?.Systems?[SystemTypes.Electrical]?.CastFast<SwitchSystem>();

                if (switchSystem is { IsActive: true })
                {
                    switch (Main.PlayerStates[player.PlayerId].Role)
                    {
                        case Mechanic:
                        {
                            Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags()} instant-fix-lights", "Mechanic");
                            Mechanic.SwitchSystemRepair(player.PlayerId, switchSystem, amount);
                            Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player);
                            break;
                        }
                        case Alchemist { FixNextSabo: true } am:
                        {
                            Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags()} instant-fix-lights", "Alchemist");
                            if (amount.HasBit(SwitchSystem.DamageSystem)) break;

                            switchSystem.ActualSwitches = (byte)(switchSystem.ExpectedSwitches ^ (1 << amount));
                            am.FixNextSabo = false;
                            Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player);
                            break;
                        }
                        case Adventurer av:
                        {
                            Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags()} instant-fix-lights", "Adventurer");
                            if (amount.HasBit(SwitchSystem.DamageSystem)) break;

                            switchSystem.ActualSwitches = (byte)(switchSystem.ExpectedSwitches ^ (1 << amount));
                            av.OnLightsFix();
                            Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player);
                            break;
                        }
                        case Technician:
                        {
                            Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags()} instant-fix-lights", "Technician");
                            Technician.SwitchSystemRepair(player.PlayerId, switchSystem, amount);
                            Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player);
                            break;
                        }
                    }
                }

                break;
            }
            case SystemTypes.Sabotage when AmongUsClient.Instance.NetworkMode != NetworkModes.FreePlay:
                return SabotageSystemTypeRepairDamagePatch.CheckSabotage(null, player, systemType);
            case SystemTypes.Security when amount == 1:
            {
                bool camerasDisabled = Main.CurrentMap switch
                {
                    MapNames.Skeld => Options.DisableSkeldCamera.GetBool(),
                    MapNames.Polus => Options.DisablePolusCamera.GetBool(),
                    MapNames.Airship => Options.DisableAirshipCamera.GetBool(),
                    _ => false
                };

                if (camerasDisabled) player.Notify(Translator.GetString("CamerasDisabledNotify"), 15f);
                return !camerasDisabled;
            }
        }

        return true;
    }

    public static void Postfix([HarmonyArgument(0)] SystemTypes systemType, [HarmonyArgument(1)] PlayerControl player)
    {
        switch (systemType)
        {
            case SystemTypes.Comms:
            {
                if (!Camouflage.CheckCamouflage()) Utils.NotifyRoles();
                goto case SystemTypes.Reactor;
            }
            case SystemTypes.Reactor:
            case SystemTypes.LifeSupp:
            case SystemTypes.Laboratory:
            case SystemTypes.HeliSabotage:
            case SystemTypes.Electrical when !Utils.IsActive(SystemTypes.Electrical):
            {
                if (player.Is(CustomRoles.Damocles) && Damocles.CountRepairSabotage)
                    Damocles.OnRepairSabotage(player.PlayerId);

                if (player.Is(CustomRoles.Stressed) && Stressed.CountRepairSabotage)
                    Stressed.OnRepairSabotage(player);

                if (Main.PlayerStates[player.PlayerId].Role is Rogue rg)
                    rg.OnFixSabotage();

                break;
            }
        }

        if (new List<SystemTypes> { SystemTypes.Electrical, SystemTypes.Reactor, SystemTypes.Laboratory, SystemTypes.LifeSupp, SystemTypes.Comms, SystemTypes.HeliSabotage, SystemTypes.MushroomMixupSabotage }.Contains(systemType) && !Utils.IsActive(systemType))
        {
            bool petcd = !Options.UsePhantomBasis.GetBool();

            foreach (PlayerControl pc in Main.EnumerateAlivePlayerControls())
            {
                if (pc.Is(CustomRoles.Wiper))
                {
                    if (petcd) pc.AddAbilityCD();
                    else pc.RpcResetAbilityCooldown();
                }
            }
        }
    }

    public static void CheckAndOpenDoorsRange(ShipStatus __instance, int amount, int min, int max)
    {
        CheckAndOpenDoors(__instance, amount, Enumerable.Range(min, max - min + 1).ToList());
    }

    private static void CheckAndOpenDoors(ShipStatus __instance, int amount, params List<int> doorIds)
    {
        if (!doorIds.Contains(amount)) return;
        foreach (int id in doorIds) __instance.RpcUpdateSystem(SystemTypes.Doors, (byte)id);
    }
}

[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.CloseDoorsOfType))]
internal static class CloseDoorsPatch
{
    public static bool Prefix([HarmonyArgument(0)] SystemTypes room)
    {
        bool allow = !Options.DisableSabotage.GetBool() && !AntiBlackout.SkipTasks && Options.CurrentGameMode is not CustomGameMode.SoloPVP and not CustomGameMode.FFA and not CustomGameMode.StopAndGo and not CustomGameMode.HotPotato and not CustomGameMode.Speedrun and not CustomGameMode.CaptureTheFlag and not CustomGameMode.NaturalDisasters and not CustomGameMode.RoomRush and not CustomGameMode.KingOfTheZones and not CustomGameMode.Quiz and not CustomGameMode.TheMindGame and not CustomGameMode.BedWars and not CustomGameMode.Deathrace and not CustomGameMode.Mingle and not CustomGameMode.Snowdown;

        if (Doorjammer.JammedRooms.Contains(room)) allow = false;
        if (SecurityGuard.BlockSabo.Count > 0) allow = false;
        if (Options.DisableCloseDoor.GetBool()) allow = false;
        if (Main.CurrentMap != MapNames.Polus && SabotageSystemTypeRepairDamagePatch.Instance != null && SabotageSystemTypeRepairDamagePatch.Instance.AnyActive) allow = false;

        Logger.Info($"({room}) => {(allow ? "Allowed" : "Blocked")}", "DoorClose");
        return allow;
    }
}

[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Start))]
internal static class StartPatch
{
    public static void Postfix()
    {
        Logger.Info("-----------Game start-----------", "Phase");

        Utils.CountAlivePlayers(true);

        if (Options.AllowConsole.GetBool())
        {
            if (!ConsoleManager.ConsoleActive && ConsoleManager.ConsoleEnabled) ConsoleManager.CreateConsole();
        }
        else
        {
            if (ConsoleManager.ConsoleActive && !DebugModeManager.AmDebugger)
            {
                ConsoleManager.DetachConsole();
                Logger.SendInGame("Sorry, console use is prohibited in this room, so your console has been turned off", Color.red);
            }
        }
    }
}

[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.StartMeeting))]
internal static class StartMeetingPatch
{
    public static void Prefix( /*ShipStatus __instance, PlayerControl reporter,*/ NetworkedPlayerInfo target)
    {
        MeetingStates.ReportTarget = target;
        MeetingStates.DeadBodies = Object.FindObjectsOfType<DeadBody>();
    }
}

[HarmonyPatch(typeof(GameManager), nameof(GameManager.CheckTaskCompletion))]
internal static class CheckTaskCompletionPatch
{
    public static bool Prefix(ref bool __result)
    {
        if (Options.DisableTaskWin.GetBool() || Options.NoGameEnd.GetBool() || TaskState.InitialTotalTasks == 0 || (Options.DisableTaskWinIfAllCrewsAreDead.GetBool() && !Main.EnumerateAlivePlayerControls().Any(x => x.Is(CustomRoleTypes.Crewmate))) || (Options.DisableTaskWinIfAllCrewsAreConverted.GetBool() && Main.EnumerateAlivePlayerControls().Where(x => x.Is(Team.Crewmate) && x.GetRoleTypes() is RoleTypes.Crewmate or RoleTypes.Engineer or RoleTypes.Scientist or RoleTypes.CrewmateGhost or RoleTypes.GuardianAngel).All(x => x.IsConverted())) || Options.CurrentGameMode != CustomGameMode.Standard)
        {
            __result = false;
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(HauntMenuMinigame), nameof(HauntMenuMinigame.Start))]
internal static class HauntMenuMinigameStartPatch
{
    public static HauntMenuMinigame Instance;

    public static void Postfix(HauntMenuMinigame __instance)
    {
        Instance = __instance;
    }
}

[HarmonyPatch(typeof(HauntMenuMinigame), nameof(HauntMenuMinigame.SetHauntTarget))]
public static class HauntMenuMinigameSetHauntTargetPatch
{
    public static bool Prefix(HauntMenuMinigame __instance, [HarmonyArgument(0)] PlayerControl target)
    {
        if (Options.CurrentGameMode == CustomGameMode.Quiz && Quiz.AllowKills) return false;

        if (target == null)
        {
            __instance.HauntTarget = null;
            __instance.NameText.text = "";
            __instance.FilterText.text = "";
            __instance.HauntingText.enabled = false;
        }
        else
        {
            __instance.HauntTarget = target;
            __instance.HauntingText.enabled = true;
            __instance.NameText.text = Main.AllPlayerNames.GetValueOrDefault(target.PlayerId, target.Data?.GetPlayerName(PlayerOutfitType.Default));

            if (__instance.HauntTarget != null && Options.GhostCanSeeOtherRoles.GetBool() && (!Main.DiedThisRound.Contains(PlayerControl.LocalPlayer.PlayerId) || !Utils.IsRevivingRoleAlive()))
                __instance.FilterText.text = __instance.HauntTarget.GetCustomRole().ToColoredString();
            else
                __instance.FilterText.text = "";
        }

        return false;
    }
}

// From https://github.com/0xDrMoe/TownofHost-Enhanced
[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Begin))]
internal static class ShipStatusBeginPatch
{
    public static bool RolesIsAssigned = false;

    public static bool Prefix()
    {
        return RolesIsAssigned;
    }

    public static void Postfix()
    {
        if (RolesIsAssigned && !Main.IntroDestroyed)
        {
            foreach (PlayerControl player in Main.EnumeratePlayerControls()) Main.PlayerStates[player.PlayerId].InitTask(player);

            GameData.Instance.RecomputeTaskCounts();
            TaskState.InitialTotalTasks = GameData.Instance.TotalTasks;
        }
    }
}

// From https://github.com/0xDrMoe/TownofHost-Enhanced
[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.SpawnPlayer))]
internal static class ShipStatusSpawnPlayerPatch
{
    // Since SnapTo is unstable on the server side,
    // after a meeting, sometimes not all players appear on the table,
    // it's better to manually teleport them
    public static bool Prefix(ShipStatus __instance, PlayerControl player, int numPlayers, bool initialSpawn)
    {
        if (!AmongUsClient.Instance.AmHost || initialSpawn || !player.IsAlive()) return true;

        Vector2 direction = Vector2.up.Rotate((player.PlayerId - 1) * (360f / numPlayers));
        Vector2 position = __instance.MeetingSpawnCenter + (direction * __instance.SpawnRadius) + new Vector2(0.0f, 0.3636f);

        LateTask.New(() => player.TP(position, true, false), 1.5f, log: false);
        return false;
    }
}

// From https://github.com/0xDrMoe/TownofHost-Enhanced
[HarmonyPatch(typeof(PolusShipStatus), nameof(PolusShipStatus.SpawnPlayer))]
internal static class PolusShipStatusSpawnPlayerPatch
{
    public static bool Prefix(PolusShipStatus __instance,
        [HarmonyArgument(0)] PlayerControl player,
        [HarmonyArgument(1)] int numPlayers,
        [HarmonyArgument(2)] bool initialSpawn)
    {
        if (!AmongUsClient.Instance.AmHost || initialSpawn || !player.IsAlive()) return true;

        int num1 = Mathf.FloorToInt(numPlayers / 2f);
        int num2 = player.PlayerId % 15;

        Vector2 position = num2 >= num1
            ? __instance.MeetingSpawnCenter2 + (Vector2.right * (num2 - num1) * 0.6f)
            : __instance.MeetingSpawnCenter + (Vector2.right * num2 * 0.6f);

        LateTask.New(() => player.TP(position, true, false), 1.5f, log: false);
        return false;
    }
}

// All below are from: https://github.com/Rabek009/MoreGamemodes/blob/master/Patches/ShipStatusPatch.cs

[HarmonyPatch(typeof(VentilationSystem), nameof(VentilationSystem.PerformVentOp))]
internal static class PerformVentOpPatch
{
    public static bool Prefix(VentilationSystem __instance, [HarmonyArgument(0)] byte playerId, [HarmonyArgument(1)] VentilationSystem.Operation op, [HarmonyArgument(2)] byte ventId, [HarmonyArgument(3)] SequenceBuffer<VentilationSystem.VentMoveInfo> seqBuffer)
    {
        if (!AmongUsClient.Instance.AmHost) return true;

        if (Utils.GetPlayerById(playerId) == null) return true;

        switch (op)
        {
            case VentilationSystem.Operation.Move:
                if (!__instance.PlayersInsideVents.ContainsKey(playerId))
                {
                    seqBuffer.BumpSid();
                    return false;
                }

                break;
        }

        return true;
    }
}

[HarmonyPatch(typeof(VentilationSystem), nameof(VentilationSystem.IsVentCurrentlyBeingCleaned))]
internal static class VentSystemIsVentCurrentlyBeingCleanedPatch
{
    // Patch block use vent for host becouse host always skips RpcSerializeVent
    public static bool Prefix([HarmonyArgument(0)] int id, ref bool __result)
    {
        if (!AmongUsClient.Instance.AmHost) return true;

        if (!PlayerControl.LocalPlayer.CanUseVent(id))
        {
            __result = true;
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Serialize))]
internal static class ShipStatusSerializePatch
{
    public static void Prefix(ShipStatus __instance, [HarmonyArgument(0)] MessageWriter writer, [HarmonyArgument(1)] bool initialState)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (initialState) return;
        if (SubmergedCompatibility.IsSubmerged()) return;

        var cancel = false;

        foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
        {
            if (VentilationSystemDeterioratePatch.BlockVentInteraction(pc))
                cancel = true;
        }

        var hudOverrideSystem = __instance.Systems[SystemTypes.Comms].TryCast<HudOverrideSystemType>();

        if (Options.CurrentGameMode == CustomGameMode.Standard && hudOverrideSystem is { IsDirty: true })
        {
            SerializeHudOverrideSystemV2(hudOverrideSystem);
            hudOverrideSystem.IsDirty = false;
        }

        var hqHudSystem = __instance.Systems[SystemTypes.Comms].TryCast<HqHudSystemType>();

        if (Options.CurrentGameMode == CustomGameMode.Standard && hqHudSystem is { IsDirty: true })
        {
            SerializeHqHudSystemV2(hqHudSystem);
            hqHudSystem.IsDirty = false;
        }

        var ventilationSystem = __instance.Systems[SystemTypes.Ventilation].TryCast<VentilationSystem>();

        if (cancel && ventilationSystem is { IsDirty: true })
        {
            Utils.SetAllVentInteractions();
            ventilationSystem.IsDirty = false;
        }
    }

    private static void SerializeHudOverrideSystemV2(HudOverrideSystemType __instance)
    {
        foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
        {
            if (pc.IsRoleBlocked()) continue;
            MessageWriter writer = MessageWriter.Get(SendOption.Reliable);
            writer.StartMessage(6);
            writer.Write(AmongUsClient.Instance.GameId);
            writer.WritePacked(pc.OwnerId);
            writer.StartMessage(1);
            writer.WritePacked(ShipStatus.Instance.NetId);
            writer.StartMessage((byte)SystemTypes.Comms);
            __instance.Serialize(writer, false);
            writer.EndMessage();
            writer.EndMessage();
            writer.EndMessage();
            AmongUsClient.Instance.SendOrDisconnect(writer);
            writer.Recycle();
        }
    }

    private static void SerializeHqHudSystemV2(HqHudSystemType __instance)
    {
        foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
        {
            if (Main.AllPlayerSpeed.TryGetValue(pc.PlayerId, out float speed) && Mathf.Approximately(speed, Main.MinSpeed)) continue;
            MessageWriter writer = MessageWriter.Get(SendOption.Reliable);
            writer.StartMessage(6);
            writer.Write(AmongUsClient.Instance.GameId);
            writer.WritePacked(pc.OwnerId);
            writer.StartMessage(1);
            writer.WritePacked(ShipStatus.Instance.NetId);
            writer.StartMessage((byte)SystemTypes.Comms);
            __instance.Serialize(writer, false);
            writer.EndMessage();
            writer.EndMessage();
            writer.EndMessage();
            AmongUsClient.Instance.SendOrDisconnect(writer);
            writer.Recycle();
        }
    }
}

[HarmonyPatch(typeof(VentilationSystem), nameof(VentilationSystem.Deteriorate))]
internal static class VentilationSystemDeterioratePatch
{
    public static void Postfix(VentilationSystem __instance)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!GameStates.InGame || !Main.IntroDestroyed) return;
        List<NetworkedPlayerInfo> allPlayers = [];

        foreach (NetworkedPlayerInfo playerInfo in GameData.Instance.AllPlayers)
        {
            if (playerInfo != null && !playerInfo.Disconnected)
                allPlayers.Add(playerInfo);
        }


        foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
        {
            if (BlockVentInteraction(pc))
            {
                int vents = ShipStatus.Instance.AllVents.Count(vent => !pc.CanUseVent(vent.Id));
                if (allPlayers.Count >= vents) continue;
                MessageWriter writer = MessageWriter.Get(SendOption.Reliable);
                writer.StartMessage(6);
                writer.Write(AmongUsClient.Instance.GameId);
                writer.WritePacked(pc.OwnerId);
                writer.StartMessage(1);
                writer.WritePacked(ShipStatus.Instance.NetId);
                writer.StartMessage((byte)SystemTypes.Ventilation);
                var blockedVents = 0;
                writer.WritePacked(allPlayers.Count);

                foreach (Vent vent in pc.GetVentsFromClosest())
                {
                    if (!pc.CanUseVent(vent.Id))
                    {
                        writer.Write(allPlayers[blockedVents].PlayerId);
                        writer.Write((byte)vent.Id);
                        ++blockedVents;
                    }

                    if (blockedVents >= allPlayers.Count)
                        break;
                }

                writer.WritePacked(__instance.PlayersInsideVents.Count);

                foreach (Il2CppSystem.Collections.Generic.KeyValuePair<byte, byte> keyValuePair2 in __instance.PlayersInsideVents)
                {
                    writer.Write(keyValuePair2.Key);
                    writer.Write(keyValuePair2.Value);
                }

                writer.EndMessage();
                writer.EndMessage();
                writer.EndMessage();

                AmongUsClient.Instance.SendOrDisconnect(writer);
                writer.Recycle();
            }
        }
    }

    public static bool BlockVentInteraction(PlayerControl pc)
    {
        return !pc.AmOwner && !pc.IsModdedClient() && !pc.Data.IsDead && pc.GetRoleTypes() is RoleTypes.Engineer or RoleTypes.Impostor or RoleTypes.Shapeshifter or RoleTypes.Phantom && ShipStatus.Instance.AllVents.Any(vent => !pc.CanUseVent(vent.Id));
    }

    public static void SerializeV2(VentilationSystem __instance, PlayerControl player = null)
    {
        foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
        {
            if (pc.AmOwner) continue;
            if (player != null && pc != player) continue;

            MessageWriter writer = MessageWriter.Get(SendOption.Reliable);

            if (BlockVentInteraction(pc))
            {
                writer.StartMessage(6);
                writer.Write(AmongUsClient.Instance.GameId);
                writer.WritePacked(pc.OwnerId);
                writer.StartMessage(1);
                writer.WritePacked(ShipStatus.Instance.NetId);
                writer.StartMessage((byte)SystemTypes.Ventilation);
                int vents = ShipStatus.Instance.AllVents.Count(vent => !pc.CanUseVent(vent.Id));
                List<NetworkedPlayerInfo> allPlayers = [];

                foreach (NetworkedPlayerInfo playerInfo in GameData.Instance.AllPlayers)
                {
                    if (playerInfo != null && !playerInfo.Disconnected)
                        allPlayers.Add(playerInfo);
                }

                int maxVents = Math.Min(vents, allPlayers.Count);
                var blockedVents = 0;
                writer.WritePacked(maxVents);

                foreach (Vent vent in pc.GetVentsFromClosest())
                {
                    if (!pc.CanUseVent(vent.Id))
                    {
                        writer.Write(allPlayers[blockedVents].PlayerId);
                        writer.Write((byte)vent.Id);
                        ++blockedVents;
                    }

                    if (blockedVents >= maxVents)
                        break;
                }

                writer.WritePacked(__instance.PlayersInsideVents.Count);

                foreach (Il2CppSystem.Collections.Generic.KeyValuePair<byte, byte> keyValuePair2 in __instance.PlayersInsideVents)
                {
                    writer.Write(keyValuePair2.Key);
                    writer.Write(keyValuePair2.Value);
                }

                writer.EndMessage();
                writer.EndMessage();
                writer.EndMessage();
            }
            else
            {
                writer.StartMessage(6);
                writer.Write(AmongUsClient.Instance.GameId);
                writer.WritePacked(pc.OwnerId);
                writer.StartMessage(1);
                writer.WritePacked(ShipStatus.Instance.NetId);
                writer.StartMessage((byte)SystemTypes.Ventilation);
                __instance.Serialize(writer, false);
                writer.EndMessage();
                writer.EndMessage();
                writer.EndMessage();
            }

            AmongUsClient.Instance.SendOrDisconnect(writer);
            writer.Recycle();
        }
    }
}

//[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.FixedUpdate))]
internal static class ShipStatusFixedUpdatePatch
{
    public static Dictionary<byte, int> ClosestVent = [];
    public static Dictionary<byte, bool> CanUseClosestVent = [];

    private static Stopwatch Stopwatch;

    public static System.Collections.IEnumerator Postfix()
    {
        Stopwatch = Stopwatch.StartNew();
        
        while (ShipStatus.Instance)
        {
            if (ReportDeadBodyPatch.MeetingStarted || GameStates.IsMeeting || ExileController.Instance || AntiBlackout.SkipTasks)
            {
                Stopwatch.Reset();
                yield return new WaitForSecondsRealtime(AntiBlackout.SkipTasks ? 2f : 5f);
                Stopwatch.Start();
                continue;
            }

            var ventilationSystem = ShipStatus.Instance.Systems[SystemTypes.Ventilation].CastFast<VentilationSystem>();
            
            if (ventilationSystem == null)
            {
                Stopwatch.Reset();
                yield return new WaitForSecondsRealtime(0.1f);
                Stopwatch.Start();
                continue;
            }

            foreach (PlayerControl pc in Main.EnumerateAlivePlayerControls())
            {
                try
                {
                    Vent closestVent = pc.GetClosestVent();
                    int ventId = closestVent.Id;
                    bool canUseVent = pc.CanUseVent(ventId);

                    if (!ClosestVent.TryGetValue(pc.PlayerId, out int lastVentId) || !CanUseClosestVent.TryGetValue(pc.PlayerId, out bool lastCanUseVent))
                    {
                        ClosestVent[pc.PlayerId] = ventId;
                        CanUseClosestVent[pc.PlayerId] = canUseVent;
                        continue;
                    }

                    if (ventId != lastVentId || canUseVent != lastCanUseVent)
                        VentilationSystemDeterioratePatch.SerializeV2(ventilationSystem, pc);

                    ClosestVent[pc.PlayerId] = ventId;
                    CanUseClosestVent[pc.PlayerId] = canUseVent;
                }
                catch (Exception e) { Utils.ThrowException(e); }
                
                if (Stopwatch.ElapsedMilliseconds > 3)
                {
                    Stopwatch.Reset();
                    yield return null;
                    Stopwatch.Start();
                }
            }

            Stopwatch.Reset();
            yield return new WaitForSecondsRealtime(0.5f);
            Stopwatch.Start();
        }
        
        if (ShipStatus.Instance)
            Main.Instance.StartCoroutine(Postfix());
    }
}