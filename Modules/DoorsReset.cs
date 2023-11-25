namespace TOHE.Modules;

public static class DoorsReset
{
    private static bool isEnabled;
    private static ResetMode mode;
    private static DoorsSystemType DoorsSystem => ShipStatus.Instance.Systems.TryGetValue(SystemTypes.Doors, out var system) ? system.TryCast<DoorsSystemType>() : null;

    public static void Initialize()
    {
        // Not supported except for Airship, Polus and Fungle
        if ((MapNames)Main.NormalOptions.MapId is not (MapNames.Airship or MapNames.Polus or MapNames.Fungle))
        {
            isEnabled = false;
            return;
        }
        isEnabled = Options.ResetDoorsEveryTurns.GetBool();
        mode = (ResetMode)Options.DoorsResetMode.GetValue();
        Logger.Info($"Initalization: [ {isEnabled}, {mode} ]", "DoorsReset");
    }

    /// <summary>Reset door status according to settings</summary>
    public static void ResetDoors()
    {
        if (!isEnabled || DoorsSystem == null)
        {
            return;
        }
        Logger.Info("Reset Completed", "DoorsReset");

        switch (mode)
        {
            case ResetMode.AllOpen: OpenAllDoors(); break;
            case ResetMode.AllClosed: CloseAllDoors(); break;
            case ResetMode.RandomByDoor: OpenOrCloseAllDoorsRandomly(); break;
            default: Logger.Warn($"Invalid Reset Doors Mode: {mode}", "DoorsReset"); break;
        }
    }
    /// <summary>Open all doors on the map</summary>
    public static void OpenAllDoors()
    {
        foreach (var door in ShipStatus.Instance?.AllDoors)
        {
            if (door == null) continue;
            SetDoorOpenState(door, true);
        }
        DoorsSystem.IsDirty = true;
    }
    /// <summary>Close all doors on the map</summary>
    private static void CloseAllDoors()
    {
        foreach (var door in ShipStatus.Instance?.AllDoors)
        {
            if (door == null) continue;
            SetDoorOpenState(door, false);
        }
        DoorsSystem.IsDirty = true;
    }
    /// <summary>Randomly opens and closes all doors on the map</summary>
    private static void OpenOrCloseAllDoorsRandomly()
    {
        foreach (var door in ShipStatus.Instance.AllDoors)
        {
            var isOpen = IRandom.Instance.Next(2) > 0;
            SetDoorOpenState(door, isOpen);
        }
        DoorsSystem.IsDirty = true;
    }

    /// <summary>Sets the open/close status of the door. Do nothing for doors that cannot be closed by sabotage</summary>
    /// <param name="door">Target door</param>
    /// <param name="isOpen">true for open, false for close</param>
    private static void SetDoorOpenState(OpenableDoor door, bool isOpen)
    {
        if (IsValidDoor(door))
        {
            door.SetDoorway(isOpen);
        }
    }
    /// <summary>Determine if the door is subject to reset</summary>
    /// <returns>true if it is subject to reset</returns>
    private static bool IsValidDoor(OpenableDoor door)
    {
        // Airship lounge toilets and Polus decontamination room doors are not closed
        return door.Room is not (SystemTypes.Lounge or SystemTypes.Decontamination);
    }

    public enum ResetMode { AllOpen, AllClosed, RandomByDoor, }
}
