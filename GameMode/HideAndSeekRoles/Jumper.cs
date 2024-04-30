namespace EHR.GameMode.HideAndSeekRoles
{
    public class Jumper : RoleBase, IHideAndSeekRole
    {
        public static bool On;

        public static OptionItem VentCooldown;
        public static OptionItem MaxInVentTime;
        public static OptionItem Vision;
        public static OptionItem Speed;

        public override bool IsEnable => On;
        public Team Team => Team.Neutral;
        public int Chance => CustomRoles.Jumper.GetMode();
        public int Count => CustomRoles.Jumper.GetCount();
        public float RoleSpeed => Speed.GetFloat();
        public float RoleVision => Vision.GetFloat();

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(69_211_301, TabGroup.NeutralRoles, CustomRoles.Jumper, CustomGameMode.HideAndSeek);
            Vision = FloatOptionItem.Create(69_211_303, "JumperVision", new(0.05f, 5f, 0.05f), 1.25f, TabGroup.NeutralRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Multiplier)
                .SetColor(new(0, 255, 0, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Jumper]);
            Speed = FloatOptionItem.Create(69_213_304, "JumperSpeed", new(0.05f, 5f, 0.05f), 1.25f, TabGroup.NeutralRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Multiplier)
                .SetColor(new(0, 255, 0, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Jumper]);
            VentCooldown = FloatOptionItem.Create(69_213_305, "VentCooldown", new(0f, 60f, 0.5f), 0f, TabGroup.NeutralRoles)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Multiplier)
                .SetColor(new(0, 255, 0, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Jumper]);
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