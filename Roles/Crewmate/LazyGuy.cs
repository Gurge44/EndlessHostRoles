namespace EHR.Crewmate;

internal class LazyGuy : RoleBase
{
    public override bool IsEnable => false;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(5700, TabGroup.CrewmateRoles, CustomRoles.LazyGuy);
    }

    public override void Init() { }

    public override void Add(byte playerId) { }
}