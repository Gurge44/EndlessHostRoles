namespace EHR.Crewmate;

internal static class Monitor
{
    private const int Id = 2350;

    public static OptionItem CanCheckCamera;
    public static OptionItem CanVent;

    public static void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Monitor);
        CanCheckCamera = new BooleanOptionItem(Id + 10, "CanCheckCamera", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Monitor]);
        CanVent = new BooleanOptionItem(Id + 14, "CanVent", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Monitor]);
    }
}