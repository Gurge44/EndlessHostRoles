using static TOHE.Options;

namespace TOHE.Roles.Impostor;

internal static class Undertaker
{
    private const int Id = 750;

    public static OptionItem MarkCooldown;
    public static OptionItem AssassinateCooldown;
    public static OptionItem CanKillAfterAssassinate;

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Undertaker);
        MarkCooldown = FloatOptionItem.Create(Id + 10, "UndertakerMarkCooldown", new(0f, 180f, 0.5f), 1f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Undertaker])
            .SetValueFormat(OptionFormat.Seconds);
        AssassinateCooldown = FloatOptionItem.Create(Id + 11, "UndertakerAssassinateCooldown", new(0f, 180f, 0.5f), 18.5f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Undertaker])
            .SetValueFormat(OptionFormat.Seconds);
        CanKillAfterAssassinate = BooleanOptionItem.Create(Id + 12, "UndertakerCanKillAfterAssassinate", true, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Undertaker]);
    }
}