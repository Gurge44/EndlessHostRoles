namespace EHR.Crewmate
{
    internal class Dictator : RoleBase
    {
        public override bool IsEnable => false;
        public override void SetupCustomOption() => Options.SetupRoleOptions(9100, TabGroup.CrewmateRoles, CustomRoles.Dictator);
        public override void Init() => throw new System.NotImplementedException();
        public override void Add(byte playerId) => throw new System.NotImplementedException();
    }
}