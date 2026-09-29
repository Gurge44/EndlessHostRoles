using System.Collections.Generic;
using HarmonyLib;

namespace EHR;

public static class AirshipElectricalDoors
{
#if IL2CPP
    private static ElectricalDoors Instance => ShipStatus.Instance.Systems[SystemTypes.Decontamination].CastFast<ElectricalDoors>();
#else
    private static ElectricalDoors Instance => ShipStatus.Instance.Systems[SystemTypes.Decontamination] as ElectricalDoors;
#endif

    public static void Initialize()
    {
        if (Main.NormalOptions.MapId != 4) return;
        Instance.Initialize();
    }

    public static IEnumerable<byte> GetClosedDoors()
    {
        List<byte> doorsArray = [];
        ElectricalDoors electricalDoors = Instance;
        if (electricalDoors.Doors == null || electricalDoors.Doors.Length == 0) return doorsArray;

        for (byte i = 0; i < electricalDoors.Doors.Length; i++)
        {
            StaticDoor door = electricalDoors.Doors[i];
            if (door != null && !door.IsOpen) doorsArray.Add(i);
        }

        return doorsArray;
    }
    // 0: BottomRightHort
    // 1: BottomHort
    // 2: TopRightHort
    // 3: TopCenterHort
    // 4: TopLeftHort
    // 5: LeftVert
    // 6: RightVert
    // 7: TopRightVert
    // 8: TopLeftVert
    // 9: BottomRightVert
    // 10: LeftDoorTop
    // 11: LeftDoorBottom
}

[HarmonyPatch(typeof(ElectricalDoors), nameof(ElectricalDoors.Initialize))]
internal static class ElectricalDoorsInitializePatch
{
    public static void Postfix( /*ElectricalDoors __instance*/)
    {
        if (!GameStates.IsInGame) return;
        Logger.Info($"Closed Doors: {string.Join(", ", AirshipElectricalDoors.GetClosedDoors())}", "ElectricalDoors Initialize");
    }
}