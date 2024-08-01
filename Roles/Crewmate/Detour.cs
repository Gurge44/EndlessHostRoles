namespace EHR.Crewmate
{
    internal class Detour : RoleBase
    {
        public override bool IsEnable => false;
        public override void SetupCustomOption() => Options.SetupRoleOptions(5590, TabGroup.CrewmateRoles, CustomRoles.Detour);
        public override void Init() => throw new System.NotImplementedException();
        public override void Add(byte playerId) => throw new System.NotImplementedException();
    }
}