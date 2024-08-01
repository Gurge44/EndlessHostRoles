namespace EHR.Crewmate
{
    internal class Observer : RoleBase
    {
        public override bool IsEnable => false;
        public override void SetupCustomOption() => Options.SetupRoleOptions(7500, TabGroup.CrewmateRoles, CustomRoles.Observer);
        public override void Init() => throw new System.NotImplementedException();
        public override void Add(byte playerId) => throw new System.NotImplementedException();
    }
}