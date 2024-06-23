namespace EHR.Crewmate
{
    public class Inquirer : ISettingHolder
    {
        public static OptionItem FailChance;

        public void SetupCustomOption()
        {
            Options.SetupRoleOptions(649710, TabGroup.CrewmateRoles, CustomRoles.Inquirer);
            FailChance = new IntegerOptionItem(649712, "Inquirer.FailChance", new(0, 100, 5), 20, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Inquirer])
                .SetValueFormat(OptionFormat.Percent);
        }
    }
}