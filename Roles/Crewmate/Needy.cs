namespace EHR.Crewmate
{
    internal class Needy : RoleBase
    {
        public override bool IsEnable => false;

        public override void SetupCustomOption()
        {
            Options.SetupRoleOptions(5700, TabGroup.CrewmateRoles, CustomRoles.Needy);
        }

        public override void Init()
        {
        }

        public override void Add(byte playerId)
        {
        }
    }
}