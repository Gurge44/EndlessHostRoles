using static EHR.Options;

namespace EHR.Roles.Neutral;

public class Wraith : ISettingHolder
{
    private const int Id = 13300;

    public static OptionItem WraithCooldown;
    public static OptionItem WraithDuration;
    public static OptionItem WraithVentNormallyOnCooldown;

    public void SetupCustomOption()
    {
        SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Wraith);
        WraithCooldown = FloatOptionItem.Create(Id + 2, "WraithCooldown", new(0f, 60f, 0.5f), 20f, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Wraith])
            .SetValueFormat(OptionFormat.Seconds);
        WraithDuration = FloatOptionItem.Create(Id + 3, "WraithDuration", new(0f, 30f, 0.5f), 10f, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Wraith])
            .SetValueFormat(OptionFormat.Seconds);
        WraithVentNormallyOnCooldown = BooleanOptionItem.Create(Id + 4, "WraithVentNormallyOnCooldown", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Wraith]);
    }
}