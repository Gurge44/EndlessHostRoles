namespace EHR.Crewmate
{
    internal class Detour : RoleBase
    {
        public static int TotalRedirections;
        public override bool IsEnable => false;

        public override void SetupCustomOption()
        {
            Options.SetupRoleOptions(5590, TabGroup.CrewmateRoles, CustomRoles.Detour);
        }

        public override void Init()
        {
            TotalRedirections = 0;
        }

        public override void Add(byte playerId) { }
    }
}