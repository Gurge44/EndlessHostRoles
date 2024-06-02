namespace EHR.GameMode.HideAndSeekRoles
{
    internal class Fox : RoleBase, IHideAndSeekRole
    {
        public static bool On;

        public static OptionItem Vision;
        public static OptionItem Speed;

        public override bool IsEnable => On;
        public Team Team => Team.Neutral;
        public int Chance => CustomRoles.Fox.GetMode();
        public int Count => CustomRoles.Fox.GetCount();
        public float RoleSpeed => Speed.GetFloat();
        public float RoleVision => Vision.GetFloat();

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(69_211_301, TabGroup.NeutralRoles, CustomRoles.Fox, CustomGameMode.HideAndSeek);
            Vision = new FloatOptionItem(69_211_303, "FoxVision", new(0.05f, 5f, 0.05f), 1.25f, TabGroup.NeutralRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Multiplier)
                .SetColor(new(0, 255, 0, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Fox]);
            Speed = new FloatOptionItem(69_213_304, "FoxSpeed", new(0.05f, 5f, 0.05f), 1.25f, TabGroup.NeutralRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Multiplier)
                .SetColor(new(0, 255, 0, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Fox]);
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