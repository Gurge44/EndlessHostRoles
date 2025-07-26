using HarmonyLib;

namespace EHR.Patches;

[HarmonyPatch(typeof(DeconSystem), nameof(DeconSystem.UpdateSystem))]
public static class DeconSystemUpdateSystemPatch
{
    public static void Prefix(DeconSystem __instance)
    {
        if (!AmongUsClient.Instance.AmHost || SubmergedCompatibility.IsSubmerged()) return;

        bool bedwars = Options.CurrentGameMode == CustomGameMode.BedWars;
        bool roomrush = Options.CurrentGameMode == CustomGameMode.RoomRush;

        if (bedwars || roomrush || Options.ChangeDecontaminationTime.GetBool())
        {
            __instance.DoorOpenTime = bedwars || roomrush
                ? 2f
                : Main.CurrentMap switch
            {
                MapNames.MiraHQ => Options.DecontaminationDoorOpenTimeOnMiraHQ.GetFloat(),
                MapNames.Polus => Options.DecontaminationDoorOpenTimeOnPolus.GetFloat(),
                _ => 3f
            };

            __instance.DeconTime = bedwars || roomrush
                ? 0.1f
                : Main.CurrentMap switch
            {
                MapNames.MiraHQ => Options.DecontaminationTimeOnMiraHQ.GetFloat(),
                MapNames.Polus => Options.DecontaminationTimeOnPolus.GetFloat(),
                _ => 3f
            };
        }
        else
        {
            __instance.DoorOpenTime = 3f;
            __instance.DeconTime = 3f;
        }
    }
}