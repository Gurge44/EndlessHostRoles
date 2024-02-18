using System.Collections.Generic;
using System.Linq;
using BepInEx;
using HarmonyLib;
using Hazel;
using TOHE.Roles.AddOns.Crewmate;
using TOHE.Roles.AddOns.Impostor;
using TOHE.Roles.Crewmate;
using TOHE.Roles.Impostor;
using TOHE.Roles.Neutral;
using UnityEngine;

namespace TOHE;

[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.FixedUpdate))]
class ShipFixedUpdatePatch
{
    public static void Postfix(/*ShipStatus __instance*/)
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
    public static void Prefix(ShipStatus __instance, [HarmonyArgument(0)] SystemTypes systemType, [HarmonyArgument(1)] PlayerControl player, [HarmonyArgument(2)] MessageReader reader)
    {
        try { RepairSystemPatch.Prefix(__instance, systemType, player, MessageReader.Get(reader).ReadByte()); } catch { }
    }
    public static void Postfix(/*ShipStatus __instance,*/ [HarmonyArgument(0)] SystemTypes systemType, [HarmonyArgument(1)] PlayerControl player, [HarmonyArgument(2)] MessageReader reader)
    {
        try { RepairSystemPatch.Postfix(/*__instance,*/ systemType, player, MessageReader.Get(reader).ReadByte()); } catch { }
    }
}
[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.UpdateSystem), typeof(SystemTypes), typeof(PlayerControl), typeof(byte))]
class RepairSystemPatch
{
    public static bool IsComms;
    public static bool Prefix(ShipStatus __instance,
        [HarmonyArgument(0)] SystemTypes systemType,
        [HarmonyArgument(1)] PlayerControl player,
        [HarmonyArgument(2)] byte amount)
    {
        Logger.Msg("SystemType: " + systemType + ", PlayerName: " + player.GetNameWithRole().RemoveHtmlTags() + ", amount: " + amount, "RepairSystem");
        if (RepairSender.enabled && AmongUsClient.Instance.NetworkMode != NetworkModes.OnlineGame)
            Logger.SendInGame("SystemType: " + systemType + ", PlayerName: " + player.GetNameWithRole().RemoveHtmlTags() + ", amount: " + amount);

        if (!AmongUsClient.Instance.AmHost) return true; //Execute the following only on the host

        IsComms = PlayerControl.LocalPlayer.myTasks.ToArray().Any(x => x.TaskType == TaskTypes.FixComms);

        if ((Options.CurrentGameMode is CustomGameMode.SoloKombat or CustomGameMode.FFA or CustomGameMode.MoveAndStop or CustomGameMode.HotPotato) && systemType == SystemTypes.Sabotage) return false;

        if (Options.DisableSabotage.GetBool() && systemType == SystemTypes.Sabotage) return false;

        //Note: "SystemTypes.Laboratory" сauses bugs in the Host, it is better not to use
        if (player.Is(CustomRoles.Fool) &&
            (systemType is
            SystemTypes.Comms or
            SystemTypes.Electrical))
        { return false; }

        switch (player.GetCustomRole())
        {
            case CustomRoles.SabotageMaster:
                SabotageMaster.RepairSystem(__instance, systemType, amount);
                Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player);
                break;
            case CustomRoles.Alchemist when Alchemist.FixNextSabo:
                Alchemist.RepairSystem(systemType, amount);
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
            case SystemTypes.Electrical when 0 <= amount && amount <= 4 && Main.NormalOptions.MapId == 4:
                if (Options.DisableAirshipViewingDeckLightsPanel.GetBool() && Vector2.Distance(player.transform.position, new(-12.93f, -11.28f)) <= 2f) return false;
                if (Options.DisableAirshipGapRoomLightsPanel.GetBool() && Vector2.Distance(player.transform.position, new(13.92f, 6.43f)) <= 2f) return false;
                if (Options.DisableAirshipCargoLightsPanel.GetBool() && Vector2.Distance(player.transform.position, new(30.56f, 2.12f)) <= 2f) return false;
                break;
            case SystemTypes.Sabotage when AmongUsClient.Instance.NetworkMode != NetworkModes.FreePlay:
                if (Options.CurrentGameMode is CustomGameMode.SoloKombat or CustomGameMode.FFA or CustomGameMode.MoveAndStop or CustomGameMode.HotPotato) return false;
                if (Main.BlockSabo.Count > 0) return false;
                if (Glitch.hackedIdList.ContainsKey(player.PlayerId))
                {
                    player.Notify(string.Format(Translator.GetString("HackedByGlitch"), "Sabotage"));
                    return false;
                }
                if (player.Is(CustomRoles.Mafioso)) Mafioso.OnSabotage();
                if (player.Is(CustomRoleTypes.Impostor) && (player.IsAlive() || !Options.DeadImpCantSabotage.GetBool()) && !player.Is(CustomRoles.Minimalism)) return true;
                switch (player.GetCustomRole())
                {
                    case CustomRoles.Glitch:
                        Glitch.Mimic(player);
                        return false;
                    case CustomRoles.Magician:
                        Magician.UseCard(player);
                        return false;
                    case CustomRoles.WeaponMaster:
                        WeaponMaster.SwitchMode();
                        return false;
                    case CustomRoles.Enderman:
                        Enderman.MarkPosition();
                        return false;
                    case CustomRoles.Hookshot:
                        Hookshot.ExecuteAction();
                        return false;
                    case CustomRoles.Mycologist when Mycologist.SpreadAction.GetValue() == 1 || (Mycologist.SpreadAction.GetValue() == 2 && !Options.UsePets.GetBool()):
                        Mycologist.SpreadSpores();
                        return false;
                    case CustomRoles.Sprayer:
                        Sprayer.PlaceTrap();
                        return false;
                    case CustomRoles.Jackal when Jackal.CanUseSabotage.GetBool():
                        return true;
                    case CustomRoles.Sidekick when Jackal.CanUseSabotageSK.GetBool():
                        return true;
                    case CustomRoles.Traitor when Traitor.CanUseSabotage.GetBool():
                        return true;
                    case CustomRoles.Parasite when player.IsAlive():
                        return true;
                    case CustomRoles.Refugee when player.IsAlive():
                        return true;
                    default:
                        return false;
                }
            case SystemTypes.Security when amount == 1:
                var camerasDisabled = (MapNames)Main.NormalOptions.MapId switch
                {
                    MapNames.Skeld => Options.DisableSkeldCamera.GetBool(),
                    MapNames.Polus => Options.DisablePolusCamera.GetBool(),
                    MapNames.Airship => Options.DisableAirshipCamera.GetBool(),
                    _ => false,
                };
                if (camerasDisabled)
                {
                    player.Notify(Translator.GetString("CamerasDisabledNotify"), 15f);
                }
                return !camerasDisabled;
        }
        return true;
    }
    public static void Postfix(/*ShipStatus __instance,*/
        [HarmonyArgument(0)] SystemTypes systemType,
        [HarmonyArgument(1)] PlayerControl player,
        [HarmonyArgument(2)] byte amount)
    {
        Camouflage.CheckCamouflage();

        switch (systemType)
        {
            case SystemTypes.Electrical when 0 <= amount && amount <= 4:
                var SwitchSystem = ShipStatus.Instance?.Systems?[SystemTypes.Electrical]?.Cast<SwitchSystem>();
                if (SwitchSystem != null && SwitchSystem.IsActive)
                {
                    switch (player.GetCustomRole())
                    {
                        case CustomRoles.SabotageMaster:
                            Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags()} instant-fix-lights", "SwitchSystem");
                            SabotageMaster.SwitchSystemRepair(SwitchSystem, amount);
                            Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player);
                            break;
                        case CustomRoles.Alchemist when Alchemist.FixNextSabo:
                            Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags()} instant-fix-lights", "SwitchSystem");
                            SwitchSystem.ActualSwitches = 0;
                            SwitchSystem.ExpectedSwitches = 0;
                            Alchemist.FixNextSabo = false;
                            Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player);
                            break;
                    }
                    if (player.Is(CustomRoles.Damocles) && Damocles.countRepairSabotage) Damocles.OnRepairSabotage(player.PlayerId);
                    if (player.Is(CustomRoles.Stressed) && Stressed.countRepairSabotage) Stressed.OnRepairSabotage(player);
                }
                break;
            case SystemTypes.Reactor:
            case SystemTypes.LifeSupp:
            case SystemTypes.Comms:
            case SystemTypes.Laboratory:
            case SystemTypes.HeliSabotage:
            case SystemTypes.Electrical:
                if (player.Is(CustomRoles.Damocles) && Damocles.countRepairSabotage) Damocles.OnRepairSabotage(player.PlayerId);
                if (player.Is(CustomRoles.Stressed) && Stressed.countRepairSabotage) Stressed.OnRepairSabotage(player);
                break;
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
class CloseDoorsPatch
{
    public static bool Prefix(/*ShipStatus __instance, */[HarmonyArgument(0)] SystemTypes room)
    {
        bool allow = !Options.DisableSabotage.GetBool() && Options.CurrentGameMode is not CustomGameMode.SoloKombat and not CustomGameMode.FFA and not CustomGameMode.MoveAndStop;

        if (Main.BlockSabo.Count > 0) allow = false;
        if (Options.DisableCloseDoor.GetBool()) allow = false;

        Logger.Info($"({room}) => {(allow ? "Allowed" : "Blocked")}", "DoorClose");
        return allow;
    }
}
[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Start))]
class StartPatch
{
    public static void Postfix()
    {
        Logger.CurrentMethod();
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
class StartMeetingPatch
{
    public static void Prefix(/*ShipStatus __instance, PlayerControl reporter,*/ GameData.PlayerInfo target)
    {
        MeetingStates.ReportTarget = target;
        MeetingStates.DeadBodies = Object.FindObjectsOfType<DeadBody>();
    }
}
[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Begin))]
class BeginPatch
{
    public static void Postfix()
    {
        Logger.CurrentMethod();

        //Should I initialize the host role here?
    }
}
[HarmonyPatch(typeof(GameManager), nameof(GameManager.CheckTaskCompletion))]
class CheckTaskCompletionPatch
{
    public static bool Prefix(ref bool __result)
    {
        if (Options.DisableTaskWin.GetBool() || Options.NoGameEnd.GetBool() || TaskState.InitialTotalTasks == 0 || Options.CurrentGameMode is CustomGameMode.SoloKombat or CustomGameMode.FFA or CustomGameMode.MoveAndStop or CustomGameMode.HotPotato)
        {
            __result = false;
            return false;
        }
        return true;
    }
}