namespace EHR.Crewmate
{
    public class Inquirer : RoleBase
    {
        public static OptionItem FailChance;

        public override bool IsEnable => false;

        public override void SetupCustomOption()
        {
            Options.SetupRoleOptions(649710, TabGroup.CrewmateRoles, CustomRoles.Inquirer);
            FailChance = new IntegerOptionItem(649712, "Inquirer.FailChance", new(0, 100, 5), 20, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Inquirer])
                .SetValueFormat(OptionFormat.Percent);
        }

        public override void Init()
        {
        }

        public override void Add(byte playerId)
        {
        }
    }
}