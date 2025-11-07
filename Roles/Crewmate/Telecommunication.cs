namespace EHR.Crewmate;

internal class Telecommunication : RoleBase
{
    private const int Id = 2350;

    public static OptionItem CanCheckCamera;
    public static OptionItem CanVent;
    public static OptionItem VentCooldown;
    public static OptionItem UseLimitOpt;
    public static OptionItem AbilityUseGainWithEachTaskCompleted;
    public static OptionItem AbilityChargesWhenFinishedTasks;

    public static bool On;

    public override bool IsEnable => On;

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        playerId.SetAbilityUseLimit(UseLimitOpt.GetFloat());
    }

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Telecommunication);

        CanCheckCamera = new BooleanOptionItem(Id + 10, "CanCheckCamera", true, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Telecommunication]);

        CanVent = new BooleanOptionItem(Id + 14, "CanVent", false, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Telecommunication]);

        VentCooldown = new FloatOptionItem(Id + 15, "AbilityCooldown", new(0f, 70f, 1f), 15f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Telecommunication])
            .SetValueFormat(OptionFormat.Seconds);

        UseLimitOpt = new IntegerOptionItem(Id + 11, "AbilityUseLimit", new(0, 20, 1), 1, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Telecommunication])
            .SetValueFormat(OptionFormat.Times);

        AbilityUseGainWithEachTaskCompleted = new FloatOptionItem(Id + 12, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 1f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Telecommunication])
            .SetValueFormat(OptionFormat.Times);

        AbilityChargesWhenFinishedTasks = new FloatOptionItem(Id + 13, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.5f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Telecommunication])
            .SetValueFormat(OptionFormat.Times);
    }
}