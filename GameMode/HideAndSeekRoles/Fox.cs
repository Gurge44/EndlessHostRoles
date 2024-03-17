using AmongUs.GameOptions;

namespace TOHE.GameMode.HideAndSeekRoles
{
    internal class Fox : RoleBase, IHideAndSeekRole
    {
        public static bool On;
        public override bool IsEnable => On;

        public static OptionItem Vision;
        public static OptionItem Speed;

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(69_211_301, TabGroup.NeutralRoles, CustomRoles.Fox, CustomGameMode.HideAndSeek, true);
            Vision = FloatOptionItem.Create(69_211_303, "FoxVision", new(0.05f, 5f, 0.05f), 1.25f, TabGroup.NeutralRoles, false)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Multiplier)
                .SetColor(new(0, 255, 0, byte.MaxValue))
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Fox]);
            Speed = FloatOptionItem.Create(69_213_304, "FoxSpeed", new(0.05f, 5f, 0.05f), 1.25f, TabGroup.NeutralRoles, false)
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

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            Main.AllPlayerSpeed[playerId] = Speed.GetFloat();
            opt.SetFloat(FloatOptionNames.CrewLightMod, Vision.GetFloat());
            opt.SetFloat(FloatOptionNames.ImpostorLightMod, Vision.GetFloat());
            opt.SetFloat(FloatOptionNames.PlayerSpeedMod, Speed.GetFloat());
        }
    }
}
