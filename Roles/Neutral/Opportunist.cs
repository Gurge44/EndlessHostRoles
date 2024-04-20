namespace EHR.Roles.Neutral
{
    internal class Opportunist : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        public override void Add(byte playerId)
        {
            On = true;
        }

        public override void Init()
        {
            On = false;
        }

        public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
        {
            return !Options.OppoImmuneToAttacksWhenTasksDone.GetBool() || !target.Is(CustomRoles.Opportunist) || !target.AllTasksCompleted();
        }

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(10100, TabGroup.NeutralRoles, CustomRoles.Opportunist);
            Options.OppoImmuneToAttacksWhenTasksDone = BooleanOptionItem.Create(10110, "ImmuneToAttacksWhenTasksDone", false, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Opportunist]);
            Options.OverrideTasksData.Create(10111, TabGroup.NeutralRoles, CustomRoles.Opportunist);
        }
    }
}