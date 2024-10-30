using AmongUs.GameOptions;

namespace EHR.Neutral
{
    internal class Opportunist : RoleBase
    {
        public static bool On;

        public static OptionItem OppoImmuneToAttacksWhenTasksDone;
        public static OptionItem CanVent;
        private static OptionItem VentCooldown;
        private static OptionItem MaxInVentTime;
        public override bool IsEnable => On;

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
            if (!CanVent.GetBool())
            {
                return;
            }

            AURoleOptions.EngineerCooldown = VentCooldown.GetFloat();
            AURoleOptions.EngineerInVentMaxTime = MaxInVentTime.GetFloat();
        }

        public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
        {
            return !OppoImmuneToAttacksWhenTasksDone.GetBool() || !target.Is(CustomRoles.Opportunist) || !target.AllTasksCompleted();
        }

        public override void SetupCustomOption()
        {
            Options.SetupRoleOptions(10100, TabGroup.NeutralRoles, CustomRoles.Opportunist);
            OppoImmuneToAttacksWhenTasksDone = new BooleanOptionItem(10110, "ImmuneToAttacksWhenTasksDone", false, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Opportunist]);
            CanVent = new BooleanOptionItem(10111, "CanVent", false, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Opportunist]);
            VentCooldown = new FloatOptionItem(10112, "VentCooldown", new(0f, 120f, 0.5f), 30f, TabGroup.NeutralRoles)
                .SetParent(CanVent)
                .SetValueFormat(OptionFormat.Seconds);
            MaxInVentTime = new FloatOptionItem(10113, "MaxInVentTime", new(0f, 120f, 0.5f), 15f, TabGroup.NeutralRoles)
                .SetParent(CanVent)
                .SetValueFormat(OptionFormat.Seconds);
            Options.OverrideTasksData.Create(10114, TabGroup.NeutralRoles, CustomRoles.Opportunist);
        }
    }
}