namespace EHR.Crewmate;

public class Inquirer : RoleBase
{
    public static OptionItem FailChance;
    private static OptionItem AbilityUseLimit;
    private static OptionItem AbilityUseGainWithEachTaskCompleted;
    private static OptionItem AbilityChargesWhenFinishedTasks;

    public override bool IsEnable => false;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(649710, TabGroup.CrewmateRoles, CustomRoles.Inquirer);

        FailChance = new IntegerOptionItem(649712, "Inquirer.FailChance", new(0, 100, 5), 20, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Inquirer])
            .SetValueFormat(OptionFormat.Percent);

        AbilityUseLimit = new IntegerOptionItem(649713, "AbilityUseLimit", new(0, 10, 1), 1, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Inquirer])
            .SetValueFormat(OptionFormat.Times);

        AbilityUseGainWithEachTaskCompleted = new FloatOptionItem(649714, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 0.8f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Inquirer])
            .SetValueFormat(OptionFormat.Times);

        AbilityChargesWhenFinishedTasks = new FloatOptionItem(649715, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Inquirer])
            .SetValueFormat(OptionFormat.Times);
    }

    public override void Init() { }

    public override void Add(byte playerId)
    {
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetInt());
    }
}
