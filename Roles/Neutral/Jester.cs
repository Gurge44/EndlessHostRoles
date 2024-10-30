using AmongUs.GameOptions;
using static EHR.Options;

namespace EHR.Neutral
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
        public static OptionItem BlockVentMovement;
        public override bool IsEnable => On;

        public override void SetupCustomOption()
        {
            SetupRoleOptions(10900, TabGroup.NeutralRoles, CustomRoles.Jester);
            JesterCanUseButton = new BooleanOptionItem(10910, "JesterCanUseButton", false, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Jester]);
            JesterCanVent = new BooleanOptionItem(10911, "CanVent", false, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Jester]);
            VentCooldown = new FloatOptionItem(10913, "VentCooldown", new(0f, 60f, 0.5f), 0f, TabGroup.NeutralRoles)
                .SetParent(JesterCanVent)
                .SetValueFormat(OptionFormat.Seconds);
            MaxInVentTime = new FloatOptionItem(10914, "MaxInVentTime", new(0f, 60f, 0.5f), 30f, TabGroup.NeutralRoles)
                .SetParent(JesterCanVent)
                .SetValueFormat(OptionFormat.Seconds);
            BlockVentMovement = new BooleanOptionItem(10916, "BlockVentMovement", false, TabGroup.NeutralRoles)
                .SetParent(JesterCanVent);
            JesterHasImpostorVision = new BooleanOptionItem(10912, "ImpostorVision", false, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Jester]);
            SunnyboyChance = new IntegerOptionItem(10915, "SunnyboyChance", new(0, 100, 5), 0, TabGroup.NeutralRoles)
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

        public override bool CanUseVent(PlayerControl pc, int ventId)
        {
            return !BlockVentMovement.GetBool() || pc.GetClosestVent()?.Id == ventId;
        }
    }
}