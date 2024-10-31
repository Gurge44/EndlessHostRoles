namespace EHR.Crewmate
{
    internal class Shiftguard : RoleBase
    {
        public override bool IsEnable => false;

        public override void SetupCustomOption()
        {
            Options.SetupRoleOptions(5594, TabGroup.CrewmateRoles, CustomRoles.Shiftguard);
        }

        public override void Init() { }

        public override void Add(byte playerId) { }
    }
}