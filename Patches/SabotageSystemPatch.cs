using HarmonyLib;

namespace TOHE;

//参考
//https://github.com/Koke1024/Town-Of-Moss/blob/main/TownOfMoss/Patches/MeltDownBoost.cs

[HarmonyPatch(typeof(ReactorSystemType), nameof(ReactorSystemType.Deteriorate))]
public static class ReactorSystemTypePatch
{
    public static void Prefix(ReactorSystemType __instance)
    {
        if (!__instance.IsActive || !Options.SabotageTimeControl.GetBool() || __instance.Countdown < Options.PolusReactorTimeLimit.GetFloat()) return;

        switch (ShipStatus.Instance.Type)
        {
            case ShipStatus.MapType.Pb:
                __instance.Countdown = Options.PolusReactorTimeLimit.GetFloat();
                return;
            case ShipStatus.MapType.Hq:
                __instance.Countdown = Options.MiraReactorTimeLimit.GetFloat();
                return;
            default:
                return;
        }
    }
}
[HarmonyPatch(typeof(HeliSabotageSystem), nameof(HeliSabotageSystem.Deteriorate))]
public static class HeliSabotageSystemPatch
{
    public static void Prefix(HeliSabotageSystem __instance)
    {
        if (!__instance.IsActive || !Options.SabotageTimeControl.GetBool())
            return;
        if (ShipStatus.Instance != null && __instance.Countdown >= Options.AirshipReactorTimeLimit.GetFloat())
            __instance.Countdown = Options.AirshipReactorTimeLimit.GetFloat();
    }
}
[HarmonyPatch(typeof(LifeSuppSystemType), nameof(LifeSuppSystemType.Deteriorate))]
public static class LifeSuppSystemTypePatch
{
    public static void Prefix(LifeSuppSystemType __instance)
    {
        if (!__instance.IsActive || !Options.SabotageTimeControl.GetBool())
            return;
        if (ShipStatus.Instance.Type == ShipStatus.MapType.Hq && __instance.Countdown >= Options.MiraO2TimeLimit.GetFloat())
            __instance.Countdown = Options.MiraO2TimeLimit.GetFloat();
    }
}
[HarmonyPatch(typeof(MushroomMixupSabotageSystem), nameof(MushroomMixupSabotageSystem.Deteriorate))]
public static class MushroomMixupSabotageSystemPatch
{
    public static void Prefix(MushroomMixupSabotageSystem __instance)
    {
        if (Options.UsePets.GetBool()) __instance.petEmptyChance = 0;

        if (!__instance.IsActive || !Options.SabotageTimeControl.GetBool()) return;

        if (ShipStatus.Instance != null && __instance.secondsForAutoHeal >= Options.MushroomMixupTime.GetFloat())
            __instance.secondsForAutoHeal = Options.MushroomMixupTime.GetFloat();
    }
}
[HarmonyPatch(typeof(ElectricTask), nameof(ElectricTask.Initialize))]
public static class ElectricTaskInitializePatch
{
    public static void Postfix()
    {
        _ = new LateTask(() => { if (Utils.IsActive(SystemTypes.Electrical)) Utils.MarkEveryoneDirtySettingsV2(); }, 0.1f);
        if (!GameStates.IsMeeting)
            for (int i = 0; i < Main.AllAlivePlayerControls.Count; i++)
            {
                PlayerControl pc = Main.AllAlivePlayerControls[i];
                if (CustomRolesHelper.NeedUpdateOnLights(pc.GetCustomRole()))
                {
                    Utils.NotifyRoles(SpecifySeer: pc);
                }
            }
    }
}
[HarmonyPatch(typeof(ElectricTask), nameof(ElectricTask.Complete))]
public static class ElectricTaskCompletePatch
{
    public static void Postfix()
    {
        _ = new LateTask(() => { if (!Utils.IsActive(SystemTypes.Electrical)) Utils.MarkEveryoneDirtySettingsV2(); }, 0.1f);
        if (!GameStates.IsMeeting)
            for (int i = 0; i < Main.AllAlivePlayerControls.Count; i++)
            {
                PlayerControl pc = Main.AllAlivePlayerControls[i];
                if (CustomRolesHelper.NeedUpdateOnLights(pc.GetCustomRole()))
                {
                    Utils.NotifyRoles(SpecifySeer: pc);
                }
            }
    }
}
// https://github.com/tukasa0001/TownOfHost/blob/357f7b5523e4bdd0bb58cda1e0ff6cceaa84813d/Patches/SabotageSystemPatch.cs
// Method called when sabotage occurs
[HarmonyPatch(typeof(SabotageSystemType), nameof(SabotageSystemType.UpdateSystem))]
public static class SabotageSystemTypeRepairDamagePatch
{
    private static bool isCooldownModificationEnabled;
    private static float modifiedCooldownSec;

    public static void Initialize()
    {
        isCooldownModificationEnabled = Options.SabotageCooldownControl.GetBool();
        modifiedCooldownSec = Options.SabotageCooldown.GetFloat();
    }

    public static void Postfix(SabotageSystemType __instance)
    {
        if (!isCooldownModificationEnabled || !AmongUsClient.Instance.AmHost)
        {
            return;
        }
        __instance.Timer = modifiedCooldownSec;
        __instance.IsDirty = true;
    }
}