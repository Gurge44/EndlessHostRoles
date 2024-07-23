using EHR.Crewmate;
using EHR.Modules;
using EHR.Neutral;
using HarmonyLib;
using Hazel;

namespace EHR;

//Based on:
//https://github.com/Koke1024/Town-Of-Moss/blob/main/TownOfMoss/Patches/MeltDownBoost.cs

[HarmonyPatch(typeof(ReactorSystemType), nameof(ReactorSystemType.Deteriorate))]
public static class ReactorSystemTypePatch
{
    private static bool SetDurationForReactorSabotage = true;

    public static void Prefix(ReactorSystemType __instance)
    {
        if (!Options.SabotageTimeControl.GetBool()) return;
        if ((MapNames)Main.NormalOptions.MapId is MapNames.Airship) return;

        // When the sabotage ends
        if (!__instance.IsActive || !SetDurationForReactorSabotage)
        {
            if (!SetDurationForReactorSabotage && !__instance.IsActive)
            {
                SetDurationForReactorSabotage = true;
            }

            return;
        }

        Logger.Info($" {ShipStatus.Instance.Type}", "ReactorSystemTypePatch - ShipStatus.Instance.Type");
        Logger.Info($" {SetDurationForReactorSabotage}", "ReactorSystemTypePatch - SetDurationCriticalSabotage");
        SetDurationForReactorSabotage = false;

        switch (ShipStatus.Instance.Type)
        {
            case ShipStatus.MapType.Ship: //The Skeld
                __instance.Countdown = Options.SkeldReactorTimeLimit.GetFloat();
                return;
            case ShipStatus.MapType.Hq: //Mira HQ
                __instance.Countdown = Options.MiraReactorTimeLimit.GetFloat();
                return;
            case ShipStatus.MapType.Pb: //Polus
                __instance.Countdown = Options.PolusReactorTimeLimit.GetFloat();
                return;
            case ShipStatus.MapType.Fungle: //The Fungle
                __instance.Countdown = Options.FungleReactorTimeLimit.GetFloat();
                return;
            default:
                return;
        }
    }
}

[HarmonyPatch(typeof(HeliSabotageSystem), nameof(HeliSabotageSystem.Deteriorate))]
public static class HeliSabotageSystemPatch
{
    private static bool SetDurationForReactorSabotage = true;

    public static void Prefix(HeliSabotageSystem __instance)
    {
        if (!Options.SabotageTimeControl.GetBool()) return;
        if ((MapNames)Main.NormalOptions.MapId is not MapNames.Airship) return;

        // When the sabotage ends
        if (!__instance.IsActive || ShipStatus.Instance == null || !SetDurationForReactorSabotage)
        {
            if (!SetDurationForReactorSabotage && !__instance.IsActive)
            {
                SetDurationForReactorSabotage = true;
            }

            return;
        }

        Logger.Info($" {ShipStatus.Instance.Type}", "HeliSabotageSystemPatch - ShipStatus.Instance.Type");
        Logger.Info($" {SetDurationForReactorSabotage}", "HeliSabotageSystemPatch - SetDurationCriticalSabotage");
        SetDurationForReactorSabotage = false;

        __instance.Countdown = Options.AirshipReactorTimeLimit.GetFloat();
    }
}

[HarmonyPatch(typeof(LifeSuppSystemType), nameof(LifeSuppSystemType.Deteriorate))]
public static class LifeSuppSystemTypePatch
{
    private static bool SetDurationForO2Sabotage = true;

    public static void Prefix(LifeSuppSystemType __instance)
    {
        if (!Options.SabotageTimeControl.GetBool()) return;
        if ((MapNames)Main.NormalOptions.MapId is MapNames.Polus or MapNames.Airship or MapNames.Fungle) return;

        // When the sabotage ends
        if (!__instance.IsActive || !SetDurationForO2Sabotage)
        {
            if (!SetDurationForO2Sabotage && !__instance.IsActive)
            {
                SetDurationForO2Sabotage = true;
            }

            return;
        }

        Logger.Info($" {ShipStatus.Instance.Type}", "LifeSuppSystemType - ShipStatus.Instance.Type");
        Logger.Info($" {SetDurationForO2Sabotage}", "LifeSuppSystemType - SetDurationCriticalSabotage");
        SetDurationForO2Sabotage = false;

        switch (ShipStatus.Instance.Type)
        {
            case ShipStatus.MapType.Ship: // The Skeld
                __instance.Countdown = Options.SkeldO2TimeLimit.GetFloat();
                return;
            case ShipStatus.MapType.Hq: // Mira HQ
                __instance.Countdown = Options.MiraO2TimeLimit.GetFloat();
                return;
            default:
                return;
        }
    }
}

[HarmonyPatch(typeof(MushroomMixupSabotageSystem), nameof(MushroomMixupSabotageSystem.Deteriorate))]
public static class MushroomMixupSabotageSystemPatch
{
    private static bool SetDurationMushroomMixupSabotage = true;

    public static void Prefix(MushroomMixupSabotageSystem __instance, ref bool __state)
    {
        __state = __instance.IsActive;

        if (Options.UsePets.GetBool())
        {
            __instance.petEmptyChance = 0;
        }

        if (!Options.SabotageTimeControl.GetBool()) return;
        if ((MapNames)Main.NormalOptions.MapId is not MapNames.Fungle) return;

        // When the sabotage ends
        if (!__instance.IsActive || !SetDurationMushroomMixupSabotage)
        {
            if (!SetDurationMushroomMixupSabotage && !__instance.IsActive)
            {
                SetDurationMushroomMixupSabotage = true;
            }

            return;
        }

        Logger.Info($" {ShipStatus.Instance.Type}", "MushroomMixupSabotageSystem - ShipStatus.Instance.Type");
        Logger.Info($" {SetDurationMushroomMixupSabotage}", "MushroomMixupSabotageSystem - SetDurationCriticalSabotage");
        SetDurationMushroomMixupSabotage = false;

        __instance.currentSecondsUntilHeal = Options.FungleMushroomMixupDuration.GetFloat();
    }

    public static void Postfix(MushroomMixupSabotageSystem __instance, bool __state)
    {
        // When Mushroom Mixup Sabotage ends
        if (AmongUsClient.Instance.AmHost && __instance.IsActive != __state && GameStates.IsInTask)
        {
            LateTask.New(() =>
            {
                // After MushroomMixup sabotage, shapeshift cooldown sets to 0
                foreach (var pc in Main.AllAlivePlayerControls)
                {
                    // Reset Ability Cooldown To Default For Alive Players
                    pc.RpcResetAbilityCooldown();

                    // Redo Unshift Trigger due to mushroom mixup breaking it
                    if (pc.GetCustomRole().SimpleAbilityTrigger() && Options.UseUnshiftTrigger.GetBool() && (!pc.IsNeutralKiller() || Options.UseUnshiftTriggerForNKs.GetBool()))
                    {
                        var target = Main.AllAlivePlayerControls.Without(pc).RandomElement();
                        var outfit = pc.Data.DefaultOutfit;
                        pc.RpcShapeshift(target, false);
                        Main.CheckShapeshift[pc.PlayerId] = false;
                        Utils.RpcChangeSkin(pc, outfit);
                    }
                }
            }, 1.2f, "Reset Ability Cooldown Arter Mushroom Mixup");

            foreach (var pc in Main.AllAlivePlayerControls)
            {
                if (!pc.Is(CustomRoleTypes.Impostor) && Main.ResetCamPlayerList.Contains(pc.PlayerId))
                {
                    Utils.NotifyRoles(SpecifySeer: pc, ForceLoop: true, MushroomMixup: true);
                }
            }
        }
    }
}

[HarmonyPatch(typeof(ElectricTask), nameof(ElectricTask.Initialize))]
public static class ElectricTaskInitializePatch
{
    private static long LastUpdate;

    public static void Postfix()
    {
        long now = Utils.TimeStamp;
        if (LastUpdate >= now) return;
        LastUpdate = now;

        Utils.MarkEveryoneDirtySettingsV2();

        if (GameStates.IsInTask)
        {
            foreach (PlayerControl pc in Main.AllAlivePlayerControls)
            {
                if (pc.GetCustomRole().NeedUpdateOnLights() || pc.Is(CustomRoles.Mare) || pc.Is(CustomRoles.Torch) || pc.Is(CustomRoles.Sleep) || Beacon.IsAffectedPlayer(pc.PlayerId))
                {
                    Utils.NotifyRoles(SpecifyTarget: pc, ForceLoop: true);
                }
            }
        }

        Logger.Info("Lights sabotage called", "ElectricTask");
    }
}

[HarmonyPatch(typeof(ElectricTask), nameof(ElectricTask.Complete))]
public static class ElectricTaskCompletePatch
{
    private static long LastUpdate;

    public static void Postfix()
    {
        long now = Utils.TimeStamp;
        if (LastUpdate >= now) return;
        LastUpdate = now;

        Utils.MarkEveryoneDirtySettingsV2();

        if (GameStates.IsInTask)
        {
            foreach (PlayerControl pc in Main.AllAlivePlayerControls)
            {
                if (pc.GetCustomRole().NeedUpdateOnLights() || pc.Is(CustomRoles.Mare) || pc.Is(CustomRoles.Torch) || pc.Is(CustomRoles.Sleep) || Beacon.IsAffectedPlayer(pc.PlayerId))
                {
                    Utils.NotifyRoles(SpecifyTarget: pc, ForceLoop: true);
                }
            }
        }

        Logger.Info("Lights sabotage fixed", "ElectricTask");
    }
}

// https://github.com/tukasa0001/TownOfHost/blob/357f7b5523e4bdd0bb58cda1e0ff6cceaa84813d/Patches/SabotageSystemPatch.cs
// Method called when sabotage occurs
[HarmonyPatch(typeof(SabotageSystemType), nameof(SabotageSystemType.UpdateSystem))]
public static class SabotageSystemTypeRepairDamagePatch
{
    public static bool IsCooldownModificationEnabled;
    public static float ModifiedCooldownSec;

    public static void Initialize()
    {
        IsCooldownModificationEnabled = Options.SabotageCooldownControl.GetBool();
        ModifiedCooldownSec = Options.SabotageCooldown.GetFloat();
    }

    public static bool Prefix([HarmonyArgument(0)] PlayerControl player)
    {
        if (Options.DisableSabotage.GetBool() || Options.CurrentGameMode != CustomGameMode.Standard) return false;
        if (SecurityGuard.BlockSabo.Count > 0) return false;
        if (player.IsRoleBlocked())
        {
            player.Notify(BlockedAction.Sabotage.GetBlockNotify());
            return false;
        }

        if (player.Is(Team.Impostor) && !player.IsAlive() && Options.DeadImpCantSabotage.GetBool()) return false;
        bool allow = player.GetCustomRole() switch
        {
            CustomRoles.Jackal when Jackal.CanSabotage.GetBool() => true,
            CustomRoles.Sidekick when Jackal.CanSabotageSK.GetBool() => true,
            CustomRoles.Traitor when Traitor.CanSabotage.GetBool() => true,
            CustomRoles.Parasite when player.IsAlive() => true,
            CustomRoles.Refugee when player.IsAlive() => true,
            _ => Main.PlayerStates[player.PlayerId].Role.CanUseSabotage(player) && Main.PlayerStates[player.PlayerId].Role.OnSabotage(player)
        };
        if (allow && QuizMaster.On) QuizMaster.Data.NumSabotages++;

        return allow;
    }


    public static void Postfix(SabotageSystemType __instance)
    {
        if (!IsCooldownModificationEnabled || !AmongUsClient.Instance.AmHost)
        {
            return;
        }

        __instance.Timer = ModifiedCooldownSec;
        __instance.IsDirty = true;
    }
}

[HarmonyPatch(typeof(SecurityCameraSystemType), nameof(SecurityCameraSystemType.UpdateSystem))]
public static class SecurityCameraPatch
{
    public static bool Prefix([HarmonyArgument(1)] MessageReader msgReader)
    {
        byte amount;
        {
            var newReader = MessageReader.Get(msgReader);
            amount = newReader.ReadByte();
            newReader.Recycle();
        }

        if (amount == SecurityCameraSystemType.IncrementOp)
        {
            return !((MapNames)Main.NormalOptions.MapId switch
            {
                MapNames.Skeld or MapNames.Dleks => Options.DisableSkeldCamera.GetBool(),
                MapNames.Polus => Options.DisablePolusCamera.GetBool(),
                MapNames.Airship => Options.DisableAirshipCamera.GetBool(),
                _ => false
            });
        }

        return true;
    }
}