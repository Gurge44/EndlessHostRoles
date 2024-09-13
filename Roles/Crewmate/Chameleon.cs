using static EHR.Options;

namespace EHR.Crewmate;

public class Chameleon : RoleBase
{
    private const int Id = 6300;

    public static OptionItem ChameleonCooldown;
    public static OptionItem ChameleonDuration;
    public static OptionItem UseLimitOpt;
    public static OptionItem ChameleonAbilityUseGainWithEachTaskCompleted;
    public static OptionItem AbilityChargesWhenFinishedTasks;

    public override bool IsEnable => false;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Chameleon);
        ChameleonCooldown = new FloatOptionItem(Id + 2, "ChameleonCooldown", new(1f, 60f, 1f), 20f, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Chameleon])
            .SetValueFormat(OptionFormat.Seconds);
        ChameleonDuration = new FloatOptionItem(Id + 3, "ChameleonDuration", new(1f, 30f, 1f), 10f, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Chameleon])
            .SetValueFormat(OptionFormat.Seconds);
        UseLimitOpt = new IntegerOptionItem(Id + 4, "AbilityUseLimit", new(0, 20, 1), 1, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Chameleon])
            .SetValueFormat(OptionFormat.Times);
        ChameleonAbilityUseGainWithEachTaskCompleted = new FloatOptionItem(Id + 5, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 0.5f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Chameleon])
            .SetValueFormat(OptionFormat.Times);
        AbilityChargesWhenFinishedTasks = new FloatOptionItem(Id + 6, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Chameleon])
            .SetValueFormat(OptionFormat.Times);
    }

    public override void Init()
    {
    }

    public override void Add(byte playerId)
    {
    }
}