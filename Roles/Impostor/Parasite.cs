using AmongUs.GameOptions;

namespace EHR.Impostor
{
    internal class Parasite : RoleBase
    {
        public static bool On;

        private static OptionItem ParasiteCD;
        private static OptionItem ShapeshiftCooldown;
        private static OptionItem ShapeshiftDuration;

        public static float SSCD;
        public static float SSDur;

        public override bool IsEnable => On;

        public override void SetupCustomOption()
        {
            Options.SetupSingleRoleOptions(4900, TabGroup.ImpostorRoles, CustomRoles.Parasite);
            ParasiteCD = new FloatOptionItem(4910, "KillCooldown", new(0f, 180f, 2.5f), 30f, TabGroup.ImpostorRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Parasite])
                .SetValueFormat(OptionFormat.Seconds);
            ShapeshiftCooldown = new FloatOptionItem(4911, "ShapeshiftCooldown", new(0f, 180f, 1f), 30f, TabGroup.ImpostorRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Parasite])
                .SetValueFormat(OptionFormat.Seconds);
            ShapeshiftDuration = new FloatOptionItem(4912, "ShapeshiftDuration", new(0f, 180f, 1f), 15f, TabGroup.ImpostorRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Parasite])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public override void Add(byte playerId)
        {
            On = true;
        }

        public override void Init()
        {
            On = false;
            SSCD = ShapeshiftCooldown.GetFloat();
            SSDur = ShapeshiftDuration.GetFloat();
        }

        public override void SetKillCooldown(byte id)
        {
            Main.AllPlayerKillCooldown[id] = ParasiteCD.GetFloat();
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            opt.SetVision(true);
            AURoleOptions.ShapeshifterCooldown = SSCD;
            AURoleOptions.ShapeshifterDuration = SSDur;
        }
    }
}