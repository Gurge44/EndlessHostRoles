namespace EHR.Roles.Crewmate;

internal static class Monitor
{
    private const int Id = 2350;

    public static OptionItem CanCheckCamera;
    public static OptionItem CanVent;

    public static void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Monitor);
        CanCheckCamera = BooleanOptionItem.Create(Id + 10, "CanCheckCamera", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Monitor]);
        CanVent = BooleanOptionItem.Create(Id + 14, "CanVent", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Monitor]);
    }
}