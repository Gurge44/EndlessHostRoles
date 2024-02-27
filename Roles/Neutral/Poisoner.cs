namespace TOHE.Roles.Neutral;

public static class Poisoner
{
    private const int Id = 12700;
    public static OptionItem OptionKillDelay;
    public static OptionItem CanVent;
    public static OptionItem KillCooldown;

    public static void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Poisoner);
        KillCooldown = FloatOptionItem.Create(Id + 10, "PoisonCooldown", new(0f, 180f, 2.5f), 25f, TabGroup.NeutralRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Poisoner])
            .SetValueFormat(OptionFormat.Seconds);
        OptionKillDelay = FloatOptionItem.Create(Id + 11, "PoisonerKillDelay", new(1f, 30f, 1f), 3f, TabGroup.NeutralRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Poisoner])
            .SetValueFormat(OptionFormat.Seconds);
        CanVent = BooleanOptionItem.Create(Id + 12, "CanVent", true, TabGroup.NeutralRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Poisoner]);
    }
}
