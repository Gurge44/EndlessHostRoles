namespace EHR.Neutral;

public class Poisoner : RoleBase
{
    private const int Id = 12700;
    public static OptionItem OptionKillDelay;
    public static OptionItem CanVent;
    public static OptionItem KillCooldown;
    public static OptionItem CanKillNormally;

    public override bool IsEnable => false;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Poisoner);
        KillCooldown = new FloatOptionItem(Id + 10, "PoisonCooldown", new(0f, 180f, 0.5f), 25f, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Poisoner])
            .SetValueFormat(OptionFormat.Seconds);
        OptionKillDelay = new FloatOptionItem(Id + 11, "PoisonerKillDelay", new(1f, 30f, 1f), 3f, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Poisoner])
            .SetValueFormat(OptionFormat.Seconds);
        CanVent = new BooleanOptionItem(Id + 12, "CanVent", true, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Poisoner]);
        CanKillNormally = new BooleanOptionItem(Id + 13, "CanKillNormally", true, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Poisoner]);
    }

    public override void Init() => throw new System.NotImplementedException();
    public override void Add(byte playerId) => throw new System.NotImplementedException();
}