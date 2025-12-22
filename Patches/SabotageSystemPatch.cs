using System;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Crewmate;
using EHR.Impostor;
using EHR.Modules;
using EHR.Neutral;
using HarmonyLib;
using Hazel;

namespace EHR;
// Based on:
// https://github.com/Koke1024/Town-Of-Moss/blob/main/TownOfMoss/Patches/MeltDownBoost.cs

[HarmonyPatch(typeof(ReactorSystemType), nameof(ReactorSystemType.Deteriorate))]
public static class ReactorSystemTypePatch
{
    private static bool SetDurationForReactorSabotage = true;

    public static void Prefix(ReactorSystemType __instance)
    {
        if (!Options.SabotageTimeControl.GetBool()) return;

        if (Main.CurrentMap is MapNames.Airship) return;

        // When the sabotage ends
        if (!__instance.IsActive || !SetDurationForReactorSabotage)
        {
            if (!SetDurationForReactorSabotage && !__instance.IsActive)
                SetDurationForReactorSabotage = true;

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

        if (Main.CurrentMap is not MapNames.Airship) return;

        // When the sabotage ends
        if (!__instance.IsActive || ShipStatus.Instance == null || !SetDurationForReactorSabotage)
        {
            if (!SetDurationForReactorSabotage && !__instance.IsActive)
                SetDurationForReactorSabotage = true;

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

        if (Main.CurrentMap is MapNames.Polus or MapNames.Airship or MapNames.Fungle) return;

        // When the sabotage ends
        if (!__instance.IsActive || !SetDurationForO2Sabotage)
        {
            if (!SetDurationForO2Sabotage && !__instance.IsActive)
                SetDurationForO2Sabotage = true;

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

[HarmonyPatch(typeof(MushroomMixupSabotageSystem), nameof(MushroomMixupSabotageSystem.UpdateSystem))]
public static class MushroomMixupSabotageSystemUpdateSystemPatch
{
    public static void Postfix()
    {
        Logger.Info(" IsActive", "MushroomMixupSabotageSystem.UpdateSystem.Postfix");

        foreach (PlayerControl pc in Main.AllAlivePlayerControls)
        {
            if (!pc.Is(Team.Impostor) && pc.HasDesyncRole())
            {
                // Need for hiding player names if player is desync Impostor
                Utils.NotifyRoles(SpecifySeer: pc, ForceLoop: true, MushroomMixup: true);
            }
        }

        ReportDeadBodyPatch.CanReport.SetAllValues(false);
    }
}

[HarmonyPatch(typeof(MushroomMixupSabotageSystem), nameof(MushroomMixupSabotageSystem.Deteriorate))]
public static class MushroomMixupSabotageSystemPatch
{
    private static bool SetDurationMushroomMixupSabotage = true;

    public static void Prefix(MushroomMixupSabotageSystem __instance, ref bool __state)
    {
        __state = __instance.IsActive;

        if (Options.UsePets.GetBool()) __instance.petEmptyChance = 0;

        if (!Options.SabotageTimeControl.GetBool()) return;

        if (Main.CurrentMap is not MapNames.Fungle) return;

        // When the sabotage ends
        if (!__instance.IsActive || !SetDurationMushroomMixupSabotage)
        {
            if (!SetDurationMushroomMixupSabotage && !__instance.IsActive)
                SetDurationMushroomMixupSabotage = true;

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
                Main.AllAlivePlayerControls.DoIf(x => x.GetRoleTypes() == RoleTypes.Shapeshifter, x => x.RpcResetAbilityCooldown());

                ReportDeadBodyPatch.CanReport.SetAllValues(true);
            }, 1.2f, "Reset Ability Cooldown Arter Mushroom Mixup");

            foreach (PlayerControl pc in Main.AllAlivePlayerControls)
            {
                if (!pc.Is(CustomRoleTypes.Impostor) && pc.HasDesyncRole())
                    Utils.NotifyRoles(SpecifySeer: pc, ForceLoop: true, MushroomMixup: true);
            }
        }
    }
}

// Thanks: https://github.com/tukasa0001/TownOfHost/tree/main/Patches/ISystemType/SwitchSystemPatch.cs
[HarmonyPatch(typeof(SwitchSystem), nameof(SwitchSystem.UpdateSystem))]
internal static class SwitchSystemUpdatePatch
{
    private static bool Prefix(SwitchSystem __instance, [HarmonyArgument(0)] PlayerControl player, [HarmonyArgument(1)] MessageReader msgReader)
    {
        byte amount;
        {
            MessageReader newReader = MessageReader.Get(msgReader);
            amount = newReader.ReadByte();
            newReader.Recycle();
        }

        if (!AmongUsClient.Instance.AmHost) return true;

        // No matter if the blackout sabotage is sounded (beware of misdirection as it flies under the host's name)
        if (amount.HasBit(SwitchSystem.DamageSystem)) return true;

        // Cancel if player can't fix a specific outage on Airship
        if (Main.CurrentMap == MapNames.Airship)
        {
            Vector2 pos = player.Pos();
            if (Options.DisableAirshipViewingDeckLightsPanel.GetBool() && Vector2.Distance(pos, new(-12.93f, -11.28f)) <= 2f) return false;
            if (Options.DisableAirshipGapRoomLightsPanel.GetBool() && Vector2.Distance(pos, new(13.92f, 6.43f)) <= 2f) return false;
            if (Options.DisableAirshipCargoLightsPanel.GetBool() && Vector2.Distance(pos, new(30.56f, 2.12f)) <= 2f) return false;
        }

        if (player.Is(CustomRoles.Fool)) return false;

        if (Options.BlockDisturbancesToSwitches.GetBool())
        {
            // Shift 1 to the left by amount
            // Each digit corresponds to each switch
            // Far left switch - (amount: 0) 00001
            // Far right switch - (amount: 4) 10000
            // ref: SwitchSystem.RepairDamage, SwitchMinigame.FixedUpdate
            var switchedKnob = (byte)(0b_00001 << amount);

            // ExpectedSwitches: Up and down state of switches when all are on
            // ActualSwitches: Actual up/down state of switch
            // if Expected and Actual are the same for the operated knob, the knob is already fixed
            if ((__instance.ActualSwitches & switchedKnob) == (__instance.ExpectedSwitches & switchedKnob)) return false;
        }

        return true;
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
                if (pc.GetCustomRole().NeedUpdateOnLights() || pc.Is(CustomRoles.Torch) || pc.Is(CustomRoles.Sleep) || Beacon.IsAffectedPlayer(pc.PlayerId))
                    Utils.NotifyRoles(SpecifyTarget: pc, ForceLoop: true, SendOption: SendOption.None);
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

        if (GameStates.IsInTask)
        {
            foreach (PlayerControl pc in Main.AllAlivePlayerControls)
            {
                CustomRoles role = pc.GetCustomRole();

                if (role.NeedUpdateOnLights() || pc.Is(CustomRoles.Torch) || pc.Is(CustomRoles.Sleep) || Beacon.IsAffectedPlayer(pc.PlayerId))
                    Utils.NotifyRoles(SpecifyTarget: pc, ForceLoop: true, SendOption: SendOption.None);

                if (role == CustomRoles.Wiper)
                {
                    if (Options.UsePhantomBasis.GetBool()) pc.RpcResetAbilityCooldown();
                    else pc.AddAbilityCD();
                }

                if (pc.Is(CustomRoles.Sleep))
                    Main.AllPlayerSpeed[pc.PlayerId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
            }
        }

        Utils.MarkEveryoneDirtySettingsV2();

        Logger.Info("Lights sabotage fixed", "ElectricTask");
    }
}

[HarmonyPatch(typeof(SabotageSystemType), nameof(SabotageSystemType.AnyActive), MethodType.Getter)]
internal static class SabotageSystemTypeAnyActivePatch
{
    public static bool Prefix(SabotageSystemType __instance, ref bool __result)
    {
        __result = __instance.specials.ToArray().Any(s => s.IsActive) || CustomSabotage.Instances.Count > 0;
        return false;
    }
}

// https://github.com/tukasa0001/TownOfHost/blob/357f7b5523e4bdd0bb58cda1e0ff6cceaa84813d/Patches/SabotageSystemPatch.cs
// Method called when sabotage occurs
[HarmonyPatch(typeof(SabotageSystemType), nameof(SabotageSystemType.UpdateSystem))]
public static class SabotageSystemTypeRepairDamagePatch
{
    public static bool IsCooldownModificationEnabled;
    public static float ModifiedCooldownSec;

    public static SabotageSystemType Instance;

    public static void Initialize()
    {
        IsCooldownModificationEnabled = Options.SabotageCooldownControl.GetBool();
        ModifiedCooldownSec = Options.SabotageCooldown.GetFloat();
    }

    public static class SabotageDoubleTrigger
    {
        public static bool WaitingForDoubleTrigger;
        public static PlayerControl TriggeringPlayer;
        public static float TimeSinceFirstTrigger;
        public static Action SingleTriggerAction;

        public static void SetUp(PlayerControl player, Action singleTriggerAction)
        {
            WaitingForDoubleTrigger = true;
            TriggeringPlayer = player;
            TimeSinceFirstTrigger = 0f;
            SingleTriggerAction = singleTriggerAction;
        }

        public static void Update(float deltaTime)
        {
            if (!WaitingForDoubleTrigger || TriggeringPlayer == null) return;

            TimeSinceFirstTrigger += deltaTime;

            if (TimeSinceFirstTrigger > 1f)
            {
                SingleTriggerAction();
                Reset();
            }
        }

        public static void Reset()
        {
            WaitingForDoubleTrigger = false;
            TriggeringPlayer = null;
            TimeSinceFirstTrigger = 0f;
            SingleTriggerAction = null;
        }
    }

    public static bool Prefix(SabotageSystemType __instance, [HarmonyArgument(0)] PlayerControl player, [HarmonyArgument(1)] MessageReader msgReader)
    {
        Instance = __instance;
        if (Options.CurrentGameMode is not (CustomGameMode.Standard or CustomGameMode.Snowdown) || __instance.Timer > 0f) return false;

        SystemTypes systemTypes;
        {
            MessageReader newReader = MessageReader.Get(msgReader);
            systemTypes = (SystemTypes)newReader.ReadByte();
            newReader.Recycle();
        }

        if (SubmergedCompatibility.IsSubmerged()) return CheckSabotage(__instance, player, systemTypes);

        if (Options.EnableCustomSabotages.GetBool())
        {
            if (Options.EnableGrabOxygenMaskCustomSabotage.GetBool() && systemTypes == SystemTypes.Comms)
            {
                if (!SabotageDoubleTrigger.WaitingForDoubleTrigger)
                {
                    SabotageDoubleTrigger.SetUp(player, () =>
                    {
                        new GrabOxygenMaskSabotage().Deteriorate();
                        __instance.Timer = IsCooldownModificationEnabled ? ModifiedCooldownSec : 30f;
                        __instance.IsDirty = true;
                    });
                    return false;
                }

                SabotageDoubleTrigger.Reset();
            }
        }

        return CheckSabotage(__instance, player, systemTypes);
    }

    public static bool CheckSabotage(SabotageSystemType __instance, PlayerControl player, SystemTypes systemTypes)
    {
        if (Options.DisableSabotage.GetBool())
        {
            switch (systemTypes)
            {
                case SystemTypes.Hallway:
                case SystemTypes.Storage:
                case SystemTypes.Cafeteria:
                case SystemTypes.UpperEngine:
                case SystemTypes.Nav:
                case SystemTypes.Admin:
                    break;
                case SystemTypes.Reactor when Options.DisableReactorOnSkeldAndMira.GetBool():
                case SystemTypes.Electrical when Options.DisableLights.GetBool():
                case SystemTypes.LifeSupp when Options.DisableO2.GetBool():
                    return false;
                default:
                    if ((uint)systemTypes <= 21U)
                    {
                        switch (systemTypes)
                        {
                            case SystemTypes.Comms when Options.DisableComms.GetBool():
                            case SystemTypes.Laboratory when Options.DisableReactorOnPolus.GetBool():
                                return false;
                        }
                    }
                    else
                    {
                        switch (systemTypes)
                        {
                            case SystemTypes.MushroomMixupSabotage when Options.DisableMushroomMixup.GetBool():
                            case SystemTypes.HeliSabotage when Options.DisableReactorOnAirship.GetBool():
                                return false;
                        }
                    }

                    break;
            }
        }

        if (Stasis.IsTimeFrozen) return false;

        if (Pelican.IsEaten(player.PlayerId)) return false;

        if (__instance != null && systemTypes == SystemTypes.Electrical && Main.PlayerStates.Values.FindFirst(x => !x.IsDead && x.MainRole == CustomRoles.Battery && x.Player != null && x.Player.GetAbilityUseLimit() >= 1f, out var batteryState))
        {
            batteryState.Player.RpcRemoveAbilityUse();
            player.Notify(string.Format(Translator.GetString("BatteryNotify"), CustomRoles.Battery.ToColoredString()), 10f);
            __instance.Timer = IsCooldownModificationEnabled ? ModifiedCooldownSec : 30f;
            __instance.IsDirty = true;
            return false;
        }

        if (SecurityGuard.BlockSabo.Count > 0) return false;

        if (Doorjammer.BlockSabotagesFromJammedRooms.GetBool() && Doorjammer.JammedRooms.Count > 0)
        {
            var room = player.GetPlainShipRoom();
            
            if (room != null && Doorjammer.JammedRooms.Contains(room.RoomId))
            {
                player.Notify(Translator.GetString("DoorjammerBlockSabo"));
                return false;
            }
        }

        if (player.IsRoleBlocked())
        {
            player.Notify(BlockedAction.Sabotage.GetBlockNotify());
            return false;
        }
        
        if (Pelican.IsEaten(player.PlayerId)) return false;

        if (!Rhapsode.CheckAbilityUse(player) || Stasis.IsTimeFrozen || TimeMaster.Rewinding) return false;

        if (player.Is(CustomRoles.Mischievous)) return true;

        if (player.Is(Team.Impostor) && !player.IsAlive() && Options.DeadImpCantSabotage.GetBool()) return false;
        if (!player.Is(Team.Impostor) && !player.IsAlive()) return false;

        bool allow = player.GetCustomRole() switch
        {
            CustomRoles.Jackal when Jackal.CanSabotage.GetBool() => true,
            CustomRoles.Sidekick when Jackal.CanSabotageSK.GetBool() => true,
            CustomRoles.Traitor when Traitor.CanSabotage.GetBool() => true,
            CustomRoles.Parasite or CustomRoles.Renegade when player.IsAlive() => true,
            _ => Main.PlayerStates[player.PlayerId].Role.CanUseSabotage(player) && Main.PlayerStates[player.PlayerId].Role.OnSabotage(player)
        };

        if (player.Is(CustomRoles.Trainee) && MeetingStates.FirstMeeting)
        {
            player.Notify(Translator.GetString("TraineeNotify"));
            allow = false;
        }

        if (allow)
        {
            if (QuizMaster.On) QuizMaster.Data.NumSabotages++;

            if (Main.CurrentMap == MapNames.Skeld)
                LateTask.New(DoorsReset.OpenAllDoors, 1f, "Opening All Doors On Sabotage (Skeld)");

            foreach (PlayerControl pc in Main.AllAlivePlayerControls)
            {
                if (pc.Is(CustomRoles.Sensor) && pc.GetAbilityUseLimit() >= 1f)
                {
                    pc.RpcRemoveAbilityUse();
                    TargetArrow.Add(pc.PlayerId, player.PlayerId);
                    Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
                    LateTask.New(() =>
                    {
                        TargetArrow.Remove(pc.PlayerId, player.PlayerId);
                        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
                    }, Sensor.ArrowDuration.GetInt(), "Sensor Arrow");
                }
            }
        }

        return allow;
    }

    public static void Postfix(SabotageSystemType __instance)
    {
        if (!IsCooldownModificationEnabled || !AmongUsClient.Instance.AmHost) return;

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
            MessageReader newReader = MessageReader.Get(msgReader);
            amount = newReader.ReadByte();
            newReader.Recycle();
        }

        if (amount == SecurityCameraSystemType.IncrementOp)
        {
            return !(Main.CurrentMap switch
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

[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.RepairCriticalSabotages))]
internal static class ShipStatusRepairCriticalSabotagesPatch
{
    public static void Postfix()
    {
        CustomSabotage.Reset();
    }
}