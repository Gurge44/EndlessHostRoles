using AmongUs.GameOptions;
using static EHR.Options;

namespace EHR.Roles.Neutral
{
    internal class Jester : RoleBase
    {
        public static bool On;

        public static OptionItem JesterCanUseButton;
        public static OptionItem JesterCanVent;
        public static OptionItem JesterHasImpostorVision;
        public static OptionItem SunnyboyChance;
        public static OptionItem VentCooldown;
        public static OptionItem MaxInVentTime;
        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(10900, TabGroup.NeutralRoles, CustomRoles.Jester);
            JesterCanUseButton = BooleanOptionItem.Create(10910, "JesterCanUseButton", false, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Jester]);
            JesterCanVent = BooleanOptionItem.Create(10911, "CanVent", false, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Jester]);
            JesterHasImpostorVision = BooleanOptionItem.Create(10912, "ImpostorVision", false, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Jester]);
            VentCooldown = FloatOptionItem.Create(10913, "VentCooldown", new(0f, 60f, 0.5f), 0f, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Jester])
                .SetValueFormat(OptionFormat.Seconds);
            MaxInVentTime = FloatOptionItem.Create(10914, "MaxInVentTime", new(0f, 900f, 0.5f), 900f, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Jester])
                .SetValueFormat(OptionFormat.Seconds);
            SunnyboyChance = IntegerOptionItem.Create(10915, "SunnyboyChance", new(0, 100, 5), 0, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Jester])
                .SetValueFormat(OptionFormat.Percent);
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
            AURoleOptions.EngineerCooldown = VentCooldown.GetFloat();
            AURoleOptions.EngineerInVentMaxTime = MaxInVentTime.GetFloat();
            opt.SetVision(JesterHasImpostorVision.GetBool());
        }
    }
}