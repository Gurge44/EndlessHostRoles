namespace EHR.Modules;

public static class DoorsReset
{
    public enum ResetMode
    {
        AllOpen,
        AllClosed,
        RandomByDoor
    }

    private static bool IsEnabled;
    private static ResetMode Mode;

    public static void Initialize()
    {
        // Not supported except for Airship, Polus and Fungle
        if (Main.CurrentMap is not (MapNames.Airship or MapNames.Polus or MapNames.Fungle) && !(SubmergedCompatibility.Loaded && Main.NormalOptions.MapId == 6))
        {
            IsEnabled = false;
            return;
        }

        IsEnabled = Options.ResetDoorsEveryTurns.GetBool();
        Mode = (ResetMode)Options.DoorsResetMode.GetValue();
        Logger.Info($"Initalization: [ {IsEnabled}, {Mode} ]", "DoorsReset");
    }

    /// <summary>Reset door status according to settings</summary>
    public static void ResetDoors()
    {
        if (!IsEnabled) return;

        Logger.Info("Reset Completed", "DoorsReset");

        SetDoors(Mode);
    }

    public static void SetDoors(ResetMode resetMode)
    {
        if (!ShipStatus.Instance || !ShipStatus.Instance.Systems.TryGetValue(SystemTypes.Doors, out ISystemType system)) return;

        bool autoOpenDoors = system.TryCast(out AutoDoorsSystemType autoDoorsSystemType);

        for (var index = 0; index < ShipStatus.Instance.AllDoors.Count; index++)
        {
            OpenableDoor door = ShipStatus.Instance.AllDoors[index];
            if (!door) continue;
            bool open = resetMode switch
            {
                ResetMode.AllOpen => true,
                ResetMode.AllClosed => false,
                _ => IRandom.Instance.Next(2) > 0
            };
            SetDoorOpenState(door, open);
            if (autoOpenDoors) autoDoorsSystemType.dirtyBits |= (uint)(1 << index);
        }

        if (autoOpenDoors || !system.TryCast(out DoorsSystemType doorsSystemType)) return;
        doorsSystemType.IsDirty = true;
    }

    /// <summary>Sets the open/close status of the door. Do nothing for doors that cannot be closed by sabotage</summary>
    /// <param name="door">Target door</param>
    /// <param name="isOpen">true for open, false for close</param>
    private static void SetDoorOpenState(OpenableDoor door, bool isOpen)
    {
        if (IsValidDoor(door)) door.SetDoorway(isOpen);
    }

    /// <summary>Determine if the door is subject to reset</summary>
    /// <returns>true if it is subject to reset</returns>
    private static bool IsValidDoor(OpenableDoor door)
    {
        // Airship lounge toilets and Polus decontamination room doors are not closed
        return door.Room is not (SystemTypes.Lounge or SystemTypes.Decontamination);
    }
}