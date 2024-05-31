namespace EHR.GameMode.HideAndSeekRoles
{
    internal class Troll : RoleBase, IHideAndSeekRole
    {
        public static bool On;

        public static OptionItem Vision;
        public static OptionItem Speed;

        public override bool IsEnable => On;
        public Team Team => Team.Neutral;
        public int Chance => CustomRoles.Troll.GetMode();
        public int Count => CustomRoles.Troll.GetCount();
        public float RoleSpeed => Speed.GetFloat();
        public float RoleVision => Vision.GetFloat();

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(69_211_401, TabGroup.NeutralRoles, CustomRoles.Troll, CustomGameMode.HideAndSeek);
            Vision = new FloatOptionItem(69_211_403, "TrollVision", new(0.05f, 5f, 0.05f), 1.25f, TabGroup.NeutralRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Multiplier)
                .SetColor(new(255, 0, 255, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Troll]);
            Speed = new FloatOptionItem(69_211_404, "TrollSpeed", new(0.05f, 5f, 0.05f), 1.25f, TabGroup.NeutralRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Multiplier)
                .SetColor(new(255, 0, 255, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Troll]);
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