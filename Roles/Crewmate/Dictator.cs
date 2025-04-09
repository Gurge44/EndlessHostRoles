namespace EHR.Crewmate;

internal class Dictator : RoleBase
{
    public override bool IsEnable => false;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(652400, TabGroup.CrewmateRoles, CustomRoles.Dictator);
    }

    public override void Init() { }

    public override void Add(byte playerId) { }
}