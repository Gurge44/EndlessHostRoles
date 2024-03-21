namespace EHR.Roles.Impostor
{
    internal class Godfather : RoleBase
    {
        public static byte GodfatherTarget = byte.MaxValue;
        public static bool On;
        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(555420, TabGroup.ImpostorRoles, CustomRoles.Godfather);
            Options.GodfatherCancelVote = Options.CreateVoteCancellingUseSetting(555422, CustomRoles.Godfather, TabGroup.ImpostorRoles);
        }

        public override void Add(byte playerId)
        {
            On = true;
        }

        public override void Init()
        {
            On = false;
        }
    }
}