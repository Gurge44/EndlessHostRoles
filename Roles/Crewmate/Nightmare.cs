namespace EHR.Crewmate
{
    public class Nightmare : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        public override void Add(byte playerId)
        {
            On = true;
        }

        public override void Init()
        {
            On = false;
        }

        public override void SetupCustomOption() => Options.SetupRoleOptions(642630, TabGroup.CrewmateRoles, CustomRoles.Nightmare);

        public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
        {
            return !Utils.IsActive(SystemTypes.Electrical);
        }
    }
}