using static EHR.Options;

namespace EHR.Neutral
{
    internal class God : RoleBase
    {
        public static OptionItem NotifyGodAlive;
        public static OptionItem GodCanGuess;
        public static OptionItem KnowInfo;

        private static readonly string[] KnowInfoMode =
        [
            "None",
            "Alignments",
            "Roles"
        ];

        public override bool IsEnable => false;

        public override void SetupCustomOption()
        {
            SetupRoleOptions(18200, TabGroup.NeutralRoles, CustomRoles.God);

            NotifyGodAlive = new BooleanOptionItem(18210, "NotifyGodAlive", true, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.God]);

            GodCanGuess = new BooleanOptionItem(18211, "CanGuess", false, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.God]);

            KnowInfo = new StringOptionItem(18212, "God.KnowInfo", KnowInfoMode, 2, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.God]);
        }

        public override void Init() { }

        public override void Add(byte playerId) { }
    }
}