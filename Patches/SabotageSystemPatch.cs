using HarmonyLib;
using System.Linq;

namespace TOHE;

//参考
//https://github.com/Koke1024/Town-Of-Moss/blob/main/TownOfMoss/Patches/MeltDownBoost.cs

[HarmonyPatch(typeof(ReactorSystemType), nameof(ReactorSystemType.Detoriorate))]
public static class ReactorSystemTypePatch
{
    public static void Prefix(ReactorSystemType __instance)
    {
        if (!__instance.IsActive || !Options.SabotageTimeControl.GetBool())
            return;
        if (ShipStatus.Instance.Type == ShipStatus.MapType.Pb)
        {
            if (__instance.Countdown >= Options.PolusReactorTimeLimit.GetFloat())
                __instance.Countdown = Options.PolusReactorTimeLimit.GetFloat();
            return;
        }
        return;
    }
}
[HarmonyPatch(typeof(HeliSabotageSystem), nameof(HeliSabotageSystem.Detoriorate))]
public static class HeliSabotageSystemPatch
{
    public static void Prefix(HeliSabotageSystem __instance)
    {
        if (!__instance.IsActive || !Options.SabotageTimeControl.GetBool())
            return;
        if (ShipStatus.Instance != null)
            if (__instance.Countdown >= Options.AirshipReactorTimeLimit.GetFloat())
                __instance.Countdown = Options.AirshipReactorTimeLimit.GetFloat();
    }
}
[HarmonyPatch(typeof(ElectricTask), nameof(ElectricTask.Initialize))]
public static class ElectricTaskInitializePatch
{
    public static void Postfix()
    {
        _ = new LateTask(() => { if (Utils.IsActive(SystemTypes.Electrical)) Utils.MarkEveryoneDirtySettingsV2(); }, 0.1f);
        if (!GameStates.IsMeeting)
            foreach (var pc in Main.AllAlivePlayerControls.Where(pc => CustomRolesHelper.NeedUpdateOnLights(pc.GetCustomRole())))
            {
                Utils.NotifyRoles(SpecifySeer: pc);
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
            foreach (var pc in Main.AllAlivePlayerControls.Where(pc => CustomRolesHelper.NeedUpdateOnLights(pc.GetCustomRole())))
            {
                Utils.NotifyRoles(SpecifySeer: pc);
            }
    }
}
// https://github.com/tukasa0001/TownOfHost/blob/357f7b5523e4bdd0bb58cda1e0ff6cceaa84813d/Patches/SabotageSystemPatch.cs
// Method called when sabotage occurs
[HarmonyPatch(typeof(SabotageSystemType), nameof(SabotageSystemType.RepairDamage))]
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