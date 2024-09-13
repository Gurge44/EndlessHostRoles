namespace EHR.Crewmate;

internal class Monitor : RoleBase
{
    private const int Id = 2350;

    public static OptionItem CanCheckCamera;
    public static OptionItem CanVent;
    public static bool On;

    public override bool IsEnable => On;

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
    }

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Monitor);
        CanCheckCamera = new BooleanOptionItem(Id + 10, "CanCheckCamera", true, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Monitor]);
        CanVent = new BooleanOptionItem(Id + 14, "CanVent", true, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Monitor]);
    }
}