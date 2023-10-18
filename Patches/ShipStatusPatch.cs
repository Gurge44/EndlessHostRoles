using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using TOHE.Roles.Crewmate;
using TOHE.Roles.Neutral;
using UnityEngine;

namespace TOHE;

[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.FixedUpdate))]
class ShipFixedUpdatePatch
{
    public static void Postfix(/*ShipStatus __instance*/)
    {
        //ここより上、全員が実行する
        if (!AmongUsClient.Instance.AmHost) return;
        //ここより下、ホストのみが実行する
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
[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.RepairSystem))]
class RepairSystemPatch
{
    public static bool IsComms;
    public static bool Prefix(ShipStatus __instance,
        [HarmonyArgument(0)] SystemTypes systemType,
        [HarmonyArgument(1)] PlayerControl player,
        [HarmonyArgument(2)] byte amount)
    {
        Logger.Msg("SystemType: " + systemType.ToString() + ", PlayerName: " + player.GetNameWithRole().RemoveHtmlTags() + ", amount: " + amount, "RepairSystem");
        if (RepairSender.enabled && AmongUsClient.Instance.NetworkMode != NetworkModes.OnlineGame)
            Logger.SendInGame("SystemType: " + systemType.ToString() + ", PlayerName: " + player.GetNameWithRole().RemoveHtmlTags() + ", amount: " + amount);

        if (!AmongUsClient.Instance.AmHost) return true; //以下、ホストのみ実行

        IsComms = PlayerControl.LocalPlayer.myTasks.ToArray().Any(x => x.TaskType == TaskTypes.FixComms);

        if ((Options.CurrentGameMode == CustomGameMode.SoloKombat || Options.CurrentGameMode == CustomGameMode.FFA) && systemType == SystemTypes.Sabotage) return false;

        if (Options.DisableSabotage.GetBool() && systemType == SystemTypes.Sabotage) return false;

        //Note: "SystemTypes.Laboratory" сauses bugs in the Host, it is better not to use
        if (player.Is(CustomRoles.Fool) &&
            (systemType is
            SystemTypes.Comms or
            SystemTypes.Electrical))
        { return false; }

        if (player.Is(CustomRoles.Unlucky) && player.IsAlive() &&
            (systemType is
            SystemTypes.Doors))
        {
            var Ue = IRandom.Instance;
            if (Ue.Next(0, 100) < Options.UnluckySabotageSuicideChance.GetInt())
            {
                player.RpcMurderPlayerV3(player);
                Main.PlayerStates[player.PlayerId].deathReason = PlayerState.DeathReason.Suicide;
                return false;
            }
        }

        /*if (player.Is(CustomRoles.Madmate) && !Options.MadmateCanFixSabotage.GetBool() && 
              (systemType is
              SystemTypes.Reactor or
              SystemTypes.LifeSupp or
              SystemTypes.Comms or
              SystemTypes.Electrical))
          { return false; }*/

        //SabotageMaster
        if (player.Is(CustomRoles.SabotageMaster))
            SabotageMaster.RepairSystem(__instance, systemType, amount);
        if (player.Is(CustomRoles.Alchemist) && Alchemist.FixNextSabo) Alchemist.RepairSystem(systemType, amount);

        if (systemType == SystemTypes.Electrical && 0 <= amount && amount <= 4)
        {
            switch (Main.NormalOptions.MapId)
            {
                case 4:
                    if (Options.DisableAirshipViewingDeckLightsPanel.GetBool() && Vector2.Distance(player.transform.position, new(-12.93f, -11.28f)) <= 2f) return false;
                    if (Options.DisableAirshipGapRoomLightsPanel.GetBool() && Vector2.Distance(player.transform.position, new(13.92f, 6.43f)) <= 2f) return false;
                    if (Options.DisableAirshipCargoLightsPanel.GetBool() && Vector2.Distance(player.transform.position, new(30.56f, 2.12f)) <= 2f) return false;
                    break;
            }
        }

        if (systemType == SystemTypes.Sabotage && AmongUsClient.Instance.NetworkMode != NetworkModes.FreePlay)
        {
            if (Main.BlockSabo.Any()) return false;

            if (player.Is(CustomRoleTypes.Impostor) && (player.IsAlive() || !Options.DeadImpCantSabotage.GetBool())) return true;

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
        }

        if (systemType == SystemTypes.Security && amount == 1)
        {
            var camerasDisabled = (MapNames)Main.NormalOptions.MapId switch
            {
                MapNames.Skeld => Options.DisableSkeldCamera.GetBool(),
                MapNames.Polus => Options.DisablePolusCamera.GetBool(),
                MapNames.Airship => Options.DisableAirshipCamera.GetBool(),
                _ => false,
            };
            return !camerasDisabled;
        }

        return true;
    }
    public static void Postfix(/*ShipStatus __instance*/)
    {
        Camouflage.CheckCamouflage();
    }
    public static void CheckAndOpenDoorsRange(ShipStatus __instance, int amount, int min, int max)
    {
        var Ids = new List<int>();
        for (var i = min; i <= max; i++)
        {
            Ids.Add(i);
        }
        CheckAndOpenDoors(__instance, amount, Ids.ToArray());
    }
    private static void CheckAndOpenDoors(ShipStatus __instance, int amount, params int[] DoorIds)
    {
        if (DoorIds.Contains(amount))
        {
            for (int i = 0; i < DoorIds.Length; i++)
            {
                int id = DoorIds[i];
                __instance.RpcRepairSystem(SystemTypes.Doors, id);
            }
        }
    }
}
[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.CloseDoorsOfType))]
class CloseDoorsPatch
{
    public static bool Prefix(/*ShipStatus __instance, SystemTypes room*/)
    {
        if (Main.BlockSabo.Any()) return false;
        if (Options.DisableCloseDoor.GetBool()) return false;

        return !Options.DisableSabotage.GetBool() && Options.CurrentGameMode != CustomGameMode.SoloKombat && Options.CurrentGameMode != CustomGameMode.FFA;
    }
}
[HarmonyPatch(typeof(SwitchSystem), nameof(SwitchSystem.RepairDamage))]
class SwitchSystemRepairPatch
{
    public static bool Prefix(/*SwitchSystem __instance,*/ [HarmonyArgument(0)] PlayerControl player/*, [HarmonyArgument(1)] byte amount*/)
    {
        if (player.Is(CustomRoles.Fool)) return false;
        else return true;
    }
    public static void Postfix(SwitchSystem __instance, [HarmonyArgument(0)] PlayerControl player, [HarmonyArgument(1)] byte amount)
    {
        if (player.Is(CustomRoles.SabotageMaster))
            SabotageMaster.SwitchSystemRepair(__instance, amount);
        if (player.Is(CustomRoles.Alchemist) && Alchemist.FixNextSabo == true)
        {
            if (amount is >= 0 and <= 4)
            {
                __instance.ActualSwitches = 0;
                __instance.ExpectedSwitches = 0;
            }
            Alchemist.FixNextSabo = false;
        }
    }
}
[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Start))]
class StartPatch
{
    public static void Postfix()
    {
        Logger.CurrentMethod();
        Logger.Info("-----------游戏开始-----------", "Phase");

        Utils.CountAlivePlayers(true);

        if (Options.AllowConsole.GetBool())
        {
            if (!BepInEx.ConsoleManager.ConsoleActive && BepInEx.ConsoleManager.ConsoleEnabled)
                BepInEx.ConsoleManager.CreateConsole();
        }
        else
        {
            if (BepInEx.ConsoleManager.ConsoleActive && !DebugModeManager.AmDebugger)
            {
                BepInEx.ConsoleManager.DetachConsole();
                Logger.SendInGame("很抱歉，本房间禁止使用控制台，因此已将您的控制台关闭");
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
        MeetingStates.DeadBodies = UnityEngine.Object.FindObjectsOfType<DeadBody>();
    }
}
[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Begin))]
class BeginPatch
{
    public static void Postfix()
    {
        Logger.CurrentMethod();

        //ホストの役職初期設定はここで行うべき？
    }
}
[HarmonyPatch(typeof(GameManager), nameof(GameManager.CheckTaskCompletion))]
class CheckTaskCompletionPatch
{
    public static bool Prefix(ref bool __result)
    {
        if (Options.DisableTaskWin.GetBool() || Options.NoGameEnd.GetBool() || TaskState.InitialTotalTasks == 0 || Options.CurrentGameMode == CustomGameMode.SoloKombat || Options.CurrentGameMode == CustomGameMode.FFA)
        {
            __result = false;
            return false;
        }
        return true;
    }
}