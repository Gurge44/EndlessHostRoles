using AmongUs.GameOptions;
using static TOHE.Options;

namespace TOHE.Roles.Neutral
{
    internal class Jester : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(10900, TabGroup.NeutralRoles, CustomRoles.Jester);
            JesterCanUseButton = BooleanOptionItem.Create(10910, "JesterCanUseButton", false, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Jester]);
            JesterCanVent = BooleanOptionItem.Create(10911, "CanVent", false, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Jester]);
            JesterHasImpostorVision = BooleanOptionItem.Create(10913, "ImpostorVision", false, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Jester]);
            SunnyboyChance = IntegerOptionItem.Create(10912, "SunnyboyChance", new(0, 100, 5), 0, TabGroup.NeutralRoles, false)
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
            AURoleOptions.EngineerCooldown = 0f;
            AURoleOptions.EngineerInVentMaxTime = 0f;
            opt.SetVision(JesterHasImpostorVision.GetBool());
        }
    }
}