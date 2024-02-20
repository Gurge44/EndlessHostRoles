namespace TOHE.Roles.Impostor
{
    internal class Kidnapper : RoleBase
    {
        private static int Id => 643300;

        public static bool On;
        public override bool IsEnable => On;

        public static OptionItem SSCD;
        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Kidnapper);
            SSCD = FloatOptionItem.Create(Id + 2, "ShapeshiftCooldown", new(0f, 180f, 2.5f), 30f, TabGroup.ImpostorRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Kidnapper])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public override bool OnShapeshift(PlayerControl kidnapper, PlayerControl target, bool shapeshifting)
        {
            if (kidnapper == null || target == null || !shapeshifting) return true;
            target.TP(kidnapper);
            return false;
        }

        public override void Init()
        {
            On = false;
        }

        public override void Add(byte playerId)
        {
            On = true;
        }
    }
}
