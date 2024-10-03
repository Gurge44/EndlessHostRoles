using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using BepInEx;
using EHR.AddOns.Crewmate;
using EHR.AddOns.Impostor;
using EHR.Crewmate;
using EHR.Modules;
using EHR.Neutral;
using EHR.Patches;
using HarmonyLib;
using Hazel;
using UnityEngine;

namespace EHR;

[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.FixedUpdate))]
static class ShipFixedUpdatePatch
{
    public static void Postfix( /*ShipStatus __instance*/)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (Main.IsFixedCooldown && Main.RefixCooldownDelay >= 0)
        {
            Main.RefixCooldownDelay -= Time.fixedDeltaTime;
        }
        else if (!float.IsNaN(Main.RefixCooldownDelay))
        {
            Utils.MarkEveryoneDirtySettingsV4();
            Main.RefixCooldownDelay = float.NaN;
            Logger.Info("Refix Cooldown", "CoolDown");
        }
    }
}

[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.UpdateSystem), typeof(SystemTypes), typeof(PlayerControl), typeof(MessageReader))]
public static class MessageReaderUpdateSystemPatch
{
    public static bool Prefix([HarmonyArgument(0)] SystemTypes systemType, [HarmonyArgument(1)] PlayerControl player, [HarmonyArgument(2)] MessageReader reader)
    {
        try
        {
            if (systemType is SystemTypes.Ventilation or SystemTypes.Security or SystemTypes.Decontamination or SystemTypes.Decontamination2 or SystemTypes.Decontamination3 or SystemTypes.MedBay) return true;

            var amount = MessageReader.Get(reader).ReadByte();
            if (EAC.CheckInvalidSabotage(systemType, player, amount))
            {
                Logger.Info("EAC patched Sabotage RPC", "MessageReaderUpdateSystemPatch");
                return false;
            }

            return RepairSystemPatch.Prefix(systemType, player, amount);
        }
        catch
        {
        }

        return true;
    }

    public static void Postfix([HarmonyArgument(0)] SystemTypes systemType, [HarmonyArgument(1)] PlayerControl player)
    {
        try
        {
            if (systemType is SystemTypes.Ventilation or SystemTypes.Security or SystemTypes.Decontamination or SystemTypes.Decontamination2 or SystemTypes.Decontamination3 or SystemTypes.MedBay) return;
            RepairSystemPatch.Postfix(systemType, player);
        }
        catch
        {
        }
    }
}

[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.UpdateSystem), typeof(SystemTypes), typeof(PlayerControl), typeof(byte))]
static class RepairSystemPatch
{
    public static bool Prefix( /*ShipStatus __instance,*/
        [HarmonyArgument(0)] SystemTypes systemType,
        [HarmonyArgument(1)] PlayerControl player,
        [HarmonyArgument(2)] byte amount)
    {
        Logger.Msg($"SystemType: {systemType}, PlayerName: {player.GetNameWithRole().RemoveHtmlTags()}, amount: {amount}", "RepairSystem");
        if (RepairSender.Enabled && AmongUsClient.Instance.NetworkMode != NetworkModes.OnlineGame)
            Logger.SendInGame($"SystemType: {systemType}, PlayerName: {player.GetNameWithRole().RemoveHtmlTags()}, amount: {amount}");

        if (!AmongUsClient.Instance.AmHost) return true; // Execute the following only on the host

        if ((Options.CurrentGameMode != CustomGameMode.Standard || Options.DisableSabotage.GetBool()) && systemType == SystemTypes.Sabotage) return false;

        // Note: "SystemTypes.Laboratory" сauses bugs in the Host, it is better not to use
        if (player.Is(CustomRoles.Fool) && (systemType is SystemTypes.Comms or SystemTypes.Electrical))
        {
            return false;
        }

        switch (player.GetCustomRole())
        {
            case CustomRoles.SabotageMaster:
                SabotageMaster.RepairSystem(player.PlayerId, systemType, amount);
                Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player);
                break;
            case CustomRoles.Alchemist when Main.PlayerStates[player.PlayerId].Role is Alchemist { IsEnable: true, FixNextSabo: true }:
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
            case SystemTypes.Doors when player.Is(CustomRoles.Unlucky) && player.IsAlive():
                var Ue = IRandom.Instance;
                if (Ue.Next(0, 100) < Options.UnluckySabotageSuicideChance.GetInt())
                {
                    player.Suicide();
                    return false;
                }

                break;
            case SystemTypes.Electrical when amount <= 4 && Main.NormalOptions.MapId == 4:
                if (Options.DisableAirshipViewingDeckLightsPanel.GetBool() && Vector2.Distance(player.transform.position, new(-12.93f, -11.28f)) <= 2f) return false;
                if (Options.DisableAirshipGapRoomLightsPanel.GetBool() && Vector2.Distance(player.transform.position, new(13.92f, 6.43f)) <= 2f) return false;
                if (Options.DisableAirshipCargoLightsPanel.GetBool() && Vector2.Distance(player.transform.position, new(30.56f, 2.12f)) <= 2f) return false;
                goto Next;
            case SystemTypes.Electrical when amount <= 4:
                Next:
            {
                var SwitchSystem = ShipStatus.Instance?.Systems?[SystemTypes.Electrical]?.Cast<SwitchSystem>();
                if (SwitchSystem is { IsActive: true })
                {
                    switch (Main.PlayerStates[player.PlayerId].Role)
                    {
                        case SabotageMaster:
                        {
                            Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags()} instant-fix-lights", "SabotageMaster");
                            SabotageMaster.SwitchSystemRepair(player.PlayerId, SwitchSystem, amount);
                            Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player);
                            break;
                        }
                        case Alchemist { FixNextSabo: true } am:
                        {
                            Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags()} instant-fix-lights", "Alchemist");
                            if (amount.HasBit(SwitchSystem.DamageSystem)) break;
                            SwitchSystem.ActualSwitches = (byte)(SwitchSystem.ExpectedSwitches ^ 1 << amount);
                            am.FixNextSabo = false;
                            Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player);
                            break;
                        }
                        case Adventurer av:
                        {
                            Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags()} instant-fix-lights", "Adventurer");
                            if (amount.HasBit(SwitchSystem.DamageSystem)) break;
                            SwitchSystem.ActualSwitches = (byte)(SwitchSystem.ExpectedSwitches ^ 1 << amount);
                            av.OnLightsFix();
                            Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player);
                            break;
                        }
                        case Technician:
                        {
                            Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags()} instant-fix-lights", "Technician");
                            Technician.SwitchSystemRepair(player.PlayerId, SwitchSystem, amount);
                            Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player);
                            break;
                        }
                    }

                    if (player.Is(CustomRoles.Damocles) && Damocles.CountRepairSabotage) Damocles.OnRepairSabotage(player.PlayerId);
                    if (player.Is(CustomRoles.Stressed) && Stressed.CountRepairSabotage) Stressed.OnRepairSabotage(player);
                }

                break;
            }
            case SystemTypes.Sabotage when AmongUsClient.Instance.NetworkMode != NetworkModes.FreePlay:
                if (Options.CurrentGameMode != CustomGameMode.Standard) return false;
                if (SecurityGuard.BlockSabo.Count > 0) return false;
                if (player.IsRoleBlocked())
                {
                    player.Notify(BlockedAction.Sabotage.GetBlockNotify());
                    return false;
                }

                if (player.Is(Team.Impostor) && !player.IsAlive() && Options.DeadImpCantSabotage.GetBool()) return false;
                if (!player.Is(Team.Impostor) && !player.IsAlive()) return false;
                return player.GetCustomRole() switch
                {
                    CustomRoles.Jackal when Jackal.CanSabotage.GetBool() => true,
                    CustomRoles.Sidekick when Jackal.CanSabotageSK.GetBool() => true,
                    CustomRoles.Traitor when Traitor.CanSabotage.GetBool() => true,
                    CustomRoles.Parasite when player.IsAlive() => true,
                    CustomRoles.Refugee when player.IsAlive() => true,
                    _ => Main.PlayerStates[player.PlayerId].Role.CanUseSabotage(player)
                };
            case SystemTypes.Security when amount == 1:
                var camerasDisabled = (MapNames)Main.NormalOptions.MapId switch
                {
                    MapNames.Skeld => Options.DisableSkeldCamera.GetBool(),
                    MapNames.Polus => Options.DisablePolusCamera.GetBool(),
                    MapNames.Airship => Options.DisableAirshipCamera.GetBool(),
                    _ => false
                };
                if (camerasDisabled)
                {
                    player.Notify(Translator.GetString("CamerasDisabledNotify"), 15f);
                }

                return !camerasDisabled;
        }

        return true;
    }

    public static void Postfix([HarmonyArgument(0)] SystemTypes systemType, [HarmonyArgument(1)] PlayerControl player)
    {
        Camouflage.CheckCamouflage();

        switch (systemType)
        {
            case SystemTypes.Reactor:
            case SystemTypes.LifeSupp:
            case SystemTypes.Comms:
            case SystemTypes.Laboratory:
            case SystemTypes.HeliSabotage:
            case SystemTypes.Electrical:
            {
                if (player.Is(CustomRoles.Damocles) && Damocles.CountRepairSabotage) Damocles.OnRepairSabotage(player.PlayerId);
                if (player.Is(CustomRoles.Stressed) && Stressed.CountRepairSabotage) Stressed.OnRepairSabotage(player);
                if (Main.PlayerStates[player.PlayerId].Role is Rogue rg) rg.OnFixSabotage();
                break;
            }
        }
    }

    public static void CheckAndOpenDoorsRange(ShipStatus __instance, int amount, int min, int max)
    {
        var Ids = new List<int>();
        for (var i = min; i <= max; i++)
        {
            Ids.Add(i);
        }

        CheckAndOpenDoors(__instance, amount, [.. Ids]);
    }

    private static void CheckAndOpenDoors(ShipStatus __instance, int amount, params int[] DoorIds)
    {
        if (!DoorIds.Contains(amount)) return;
        foreach (int id in DoorIds)
        {
            __instance.RpcUpdateSystem(SystemTypes.Doors, (byte)id);
        }
    }
}

[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.CloseDoorsOfType))]
static class CloseDoorsPatch
{
    public static bool Prefix( /*ShipStatus __instance, */ [HarmonyArgument(0)] SystemTypes room)
    {
        bool allow = !Options.DisableSabotage.GetBool() && Options.CurrentGameMode is not CustomGameMode.SoloKombat and not CustomGameMode.FFA and not CustomGameMode.MoveAndStop and not CustomGameMode.HotPotato and not CustomGameMode.Speedrun and not CustomGameMode.CaptureTheFlag and not CustomGameMode.NaturalDisasters and not CustomGameMode.RoomRush;

        if (SecurityGuard.BlockSabo.Count > 0) allow = false;
        if (Options.DisableCloseDoor.GetBool()) allow = false;

        Logger.Info($"({room}) => {(allow ? "Allowed" : "Blocked")}", "DoorClose");
        return allow;
    }
}

[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Start))]
static class StartPatch
{
    public static void Postfix()
    {
        Logger.Info("-----------Game start-----------", "Phase");

        Utils.CountAlivePlayers(true);

        if (Options.AllowConsole.GetBool())
        {
            if (!ConsoleManager.ConsoleActive && ConsoleManager.ConsoleEnabled)
                ConsoleManager.CreateConsole();
        }
        else
        {
            if (ConsoleManager.ConsoleActive && !DebugModeManager.AmDebugger)
            {
                ConsoleManager.DetachConsole();
                Logger.SendInGame("Sorry, console use is prohibited in this room, so your console has been turned off");
            }
        }
    }
}

[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.StartMeeting))]
static class StartMeetingPatch
{
    public static void Prefix( /*ShipStatus __instance, PlayerControl reporter,*/ NetworkedPlayerInfo target)
    {
        MeetingStates.ReportTarget = target;
        MeetingStates.DeadBodies = Object.FindObjectsOfType<DeadBody>();
    }
}

[HarmonyPatch(typeof(GameManager), nameof(GameManager.CheckTaskCompletion))]
static class CheckTaskCompletionPatch
{
    public static bool Prefix(ref bool __result)
    {
        if (Options.DisableTaskWin.GetBool() || Options.NoGameEnd.GetBool() || TaskState.InitialTotalTasks == 0 || (Options.DisableTaskWinIfAllCrewsAreDead.GetBool() && !Main.AllAlivePlayerControls.Any(x => x.Is(CustomRoleTypes.Crewmate))) || (Options.DisableTaskWinIfAllCrewsAreConverted.GetBool() && Main.AllAlivePlayerControls.Where(x => x.Is(Team.Crewmate) && x.GetRoleTypes() is RoleTypes.Crewmate or RoleTypes.Engineer or RoleTypes.Scientist or RoleTypes.CrewmateGhost or RoleTypes.GuardianAngel).All(x => x.IsConverted())) || Options.CurrentGameMode != CustomGameMode.Standard)
        {
            __result = false;
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(HauntMenuMinigame), nameof(HauntMenuMinigame.SetFilterText))]
public static class HauntMenuMinigameSetFilterTextPatch
{
    public static bool Prefix(HauntMenuMinigame __instance)
    {
        if (__instance.HauntTarget != null && Options.GhostCanSeeOtherRoles.GetBool())
        {
            var id = __instance.HauntTarget.PlayerId;
            __instance.FilterText.text = Utils.GetDisplayRoleName(id) + Utils.GetProgressText(id);
            return false;
        }

        return true;
    }
}

// From https://github.com/0xDrMoe/TownofHost-Enhanced
[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Begin))]
static class ShipStatusBeginPatch
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
            foreach (var player in Main.AllPlayerControls)
            {
                Main.PlayerStates[player.PlayerId].InitTask(player);
            }

            GameData.Instance.RecomputeTaskCounts();
            TaskState.InitialTotalTasks = GameData.Instance.TotalTasks;

            Utils.DoNotifyRoles(ForceLoop: true, NoCache: true);
        }
    }
}

// From https://github.com/0xDrMoe/TownofHost-Enhanced
[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.SpawnPlayer))]
static class ShipStatusSpawnPlayerPatch
{
    // Since SnapTo is unstable on the server side,
    // after a meeting, sometimes not all players appear on the table,
    // it's better to manually teleport them
    public static bool Prefix(ShipStatus __instance, PlayerControl player, int numPlayers, bool initialSpawn)
    {
        if (!AmongUsClient.Instance.AmHost || initialSpawn || !player.IsAlive()) return true;

        // Lazy doesn't teleport to the meeting table
        if (player.Is(CustomRoles.Lazy)) return false;

        Vector2 direction = Vector2.up.Rotate((player.PlayerId - 1) * (360f / numPlayers));
        Vector2 position = __instance.MeetingSpawnCenter + direction * __instance.SpawnRadius + new Vector2(0.0f, 0.3636f);

        player.TP(position, log: false);
        return false;
    }
}

// From https://github.com/0xDrMoe/TownofHost-Enhanced
[HarmonyPatch(typeof(PolusShipStatus), nameof(PolusShipStatus.SpawnPlayer))]
static class PolusShipStatusSpawnPlayerPatch
{
    public static bool Prefix(PolusShipStatus __instance,
        [HarmonyArgument(0)] PlayerControl player,
        [HarmonyArgument(1)] int numPlayers,
        [HarmonyArgument(2)] bool initialSpawn)
    {
        if (!AmongUsClient.Instance.AmHost || initialSpawn || !player.IsAlive()) return true;

        // Lazy doesn't teleport to the meeting table
        if (player.Is(CustomRoles.Lazy)) return false;

        int num1 = Mathf.FloorToInt(numPlayers / 2f);
        int num2 = player.PlayerId % 15;

        Vector2 position = num2 >= num1
            ? __instance.MeetingSpawnCenter2 + Vector2.right * (num2 - num1) * 0.6f
            : __instance.MeetingSpawnCenter + Vector2.right * num2 * 0.6f;

        player.TP(position, log: false);
        return false;
    }
}

// All below are from: https://github.com/Rabek009/MoreGamemodes/blob/master/Patches/ShipStatusPatch.cs

[HarmonyPatch(typeof(VentilationSystem), nameof(VentilationSystem.PerformVentOp))]
static class PerformVentOpPatch
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

[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Serialize))]
static class ShipStatusSerializePatch
{
    public static void Prefix(ShipStatus __instance, [HarmonyArgument(1)] bool initialState)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (initialState) return;

        var cancel = Main.AllPlayerControls.Any(VentilationSystemDeterioratePatch.BlockVentInteraction);
        var ventilationSystem = __instance.Systems[SystemTypes.Ventilation].Cast<VentilationSystem>();

        if (cancel && ventilationSystem is { IsDirty: true })
        {
            Utils.SetAllVentInteractions();
            ventilationSystem.IsDirty = false;
        }
    }
}

[HarmonyPatch(typeof(VentilationSystem), nameof(VentilationSystem.Deteriorate))]
static class VentilationSystemDeterioratePatch
{
    public static Dictionary<byte, int> LastClosestVent = [];
    private static readonly Dictionary<byte, bool> LastCanUseVent = [];
    private static readonly Dictionary<byte, int> LastClosestVentForUpdate = [];
    private static readonly Dictionary<byte, long> LastVentInteractionCheck = [];

    public static void Postfix(VentilationSystem __instance)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (!GameStates.InGame) return;
        foreach (var pc in Main.AllPlayerControls)
        {
            if (BlockVentInteraction(pc))
            {
                int players = 0;
                foreach (var playerInfo in GameData.Instance.AllPlayers)
                {
                    if (playerInfo != null && !playerInfo.Disconnected)
                        ++players;
                }

                if (pc.GetClosestVent().Id == LastClosestVent[pc.PlayerId] && players >= 3) continue;
                LastClosestVent[pc.PlayerId] = pc.GetClosestVent().Id;
                MessageWriter writer = MessageWriter.Get();
                writer.StartMessage(6);
                writer.Write(AmongUsClient.Instance.GameId);
                writer.WritePacked(pc.GetClientId());
                writer.StartMessage(1);
                writer.WritePacked(ShipStatus.Instance.NetId);
                writer.StartMessage((byte)SystemTypes.Ventilation);
                int vents = ShipStatus.Instance.AllVents.Count(vent => !pc.CanUseVent(vent.Id));
                List<NetworkedPlayerInfo> AllPlayers = [];
                foreach (var playerInfo in GameData.Instance.AllPlayers)
                {
                    if (playerInfo != null && !playerInfo.Disconnected)
                        AllPlayers.Add(playerInfo);
                }

                int maxVents = Math.Min(vents, AllPlayers.Count);
                int blockedVents = 0;
                writer.WritePacked(maxVents);
                foreach (var vent in pc.GetVentsFromClosest())
                {
                    if (!pc.CanUseVent(vent.Id))
                    {
                        writer.Write(AllPlayers[blockedVents].PlayerId);
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
                AmongUsClient.Instance.SendOrDisconnect(writer);
                writer.Recycle();
            }
        }
    }

    public static bool BlockVentInteraction(PlayerControl pc)
    {
        return !pc.AmOwner && !pc.IsModClient() && !pc.Data.IsDead && (pc.IsImpostor() || pc.GetRoleTypes() is RoleTypes.Engineer or RoleTypes.Impostor or RoleTypes.Shapeshifter or RoleTypes.Phantom) && ShipStatus.Instance.AllVents.Any(vent => !pc.CanUseVent(vent.Id));
    }

    public static void SerializeV2(VentilationSystem __instance, PlayerControl player = null)
    {
        foreach (var pc in Main.AllPlayerControls)
        {
            if (pc.AmOwner) continue;
            if (player != null && pc != player) continue;
            if (BlockVentInteraction(pc))
            {
                MessageWriter writer = MessageWriter.Get();
                writer.StartMessage(6);
                writer.Write(AmongUsClient.Instance.GameId);
                writer.WritePacked(pc.GetClientId());
                writer.StartMessage(1);
                writer.WritePacked(ShipStatus.Instance.NetId);
                writer.StartMessage((byte)SystemTypes.Ventilation);
                int vents = ShipStatus.Instance.AllVents.Count(vent => !pc.CanUseVent(vent.Id));
                List<NetworkedPlayerInfo> AllPlayers = [];
                foreach (var playerInfo in GameData.Instance.AllPlayers)
                {
                    if (playerInfo != null && !playerInfo.Disconnected)
                        AllPlayers.Add(playerInfo);
                }

                int maxVents = Math.Min(vents, AllPlayers.Count);
                int blockedVents = 0;
                writer.WritePacked(maxVents);
                foreach (var vent in pc.GetVentsFromClosest())
                {
                    if (!pc.CanUseVent(vent.Id))
                    {
                        writer.Write(AllPlayers[blockedVents].PlayerId);
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
                AmongUsClient.Instance.SendOrDisconnect(writer);
                writer.Recycle();
            }
            else
            {
                MessageWriter writer = MessageWriter.Get();
                writer.StartMessage(6);
                writer.Write(AmongUsClient.Instance.GameId);
                writer.WritePacked(pc.GetClientId());
                writer.StartMessage(1);
                writer.WritePacked(ShipStatus.Instance.NetId);
                writer.StartMessage((byte)SystemTypes.Ventilation);
                __instance.Serialize(writer, false);
                writer.EndMessage();
                writer.EndMessage();
                writer.EndMessage();
                AmongUsClient.Instance.SendOrDisconnect(writer);
                writer.Recycle();
            }
        }
    }

    public static void CheckVentInteraction(PlayerControl pc)
    {
        if (!GameStates.IsInTask || ExileController.Instance) return;

        if (!LastVentInteractionCheck.TryGetValue(pc.PlayerId, out long lastCheck))
        {
            LastVentInteractionCheck[pc.PlayerId] = Utils.TimeStamp;
            return;
        }

        if (Utils.TimeStamp == lastCheck) return;
        LastVentInteractionCheck[pc.PlayerId] = Utils.TimeStamp;


        int closestVent = pc.GetClosestVent().Id;
        if (!LastClosestVentForUpdate.TryGetValue(pc.PlayerId, out int lastClosestVent))
        {
            LastClosestVentForUpdate[pc.PlayerId] = closestVent;
            return;
        }

        bool canUse = pc.CanUseVent(closestVent);

        if (!LastCanUseVent.TryGetValue(pc.PlayerId, out bool couldUse))
        {
            LastCanUseVent[pc.PlayerId] = canUse;
            return;
        }

        if (couldUse != canUse || lastClosestVent != closestVent)
        {
            LastCanUseVent[pc.PlayerId] = canUse;
            pc.RpcSetVentInteraction();
        }
    }
}

[HarmonyPatch(typeof(VentilationSystem), nameof(VentilationSystem.IsVentCurrentlyBeingCleaned))]
static class VentSystemIsVentCurrentlyBeingCleanedPatch
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