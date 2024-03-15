namespace TOHE.Roles.Impostor
{
    internal class Visionary : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        public static void SetupCustomOption() => Options.SetupRoleOptions(16150, TabGroup.ImpostorRoles, CustomRoles.Visionary);

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