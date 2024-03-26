using HarmonyLib;
using System.Collections.Generic;

namespace EHR;

public class AirshipElectricalDoors
{
    private static ElectricalDoors Instance
        => ShipStatus.Instance.Systems[SystemTypes.Decontamination].Cast<ElectricalDoors>();

    public static void Initialize()
    {
        if (Main.NormalOptions.MapId != 4) return;
        Instance.Initialize();
    }

    public static byte[] GetClosedDoors()
    {
        List<byte> DoorsArray = [];
        if (Instance.Doors == null || Instance.Doors.Length == 0) return [.. DoorsArray];
        for (byte i = 0; i < Instance.Doors.Count; i++)
        {
            var door = Instance.Doors[i];
            if (door != null && !door.IsOpen)
                DoorsArray.Add(i);
        }

        return DoorsArray?.ToArray();
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
class ElectricalDoorsInitializePatch
{
    public static void Postfix( /*ElectricalDoors __instance*/)
    {
        if (!GameStates.IsInGame) return;
        var closedoors = string.Empty;
        bool isFirst = true;
        byte[] array = AirshipElectricalDoors.GetClosedDoors();
        foreach (byte num in array)
        {
            if (isFirst)
            {
                isFirst = false;
                closedoors += num.ToString();
            }
            else
                closedoors += $", {num}";
        }

        Logger.Info($"Closed Doors: {closedoors}", "ElectricalDoors Initialize");
    }
}