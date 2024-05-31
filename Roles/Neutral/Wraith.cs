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
        WraithCooldown = new FloatOptionItem(Id + 2, "WraithCooldown", new(0f, 60f, 0.5f), 20f, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Wraith])
            .SetValueFormat(OptionFormat.Seconds);
        WraithDuration = new FloatOptionItem(Id + 3, "WraithDuration", new(0f, 30f, 0.5f), 10f, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Wraith])
            .SetValueFormat(OptionFormat.Seconds);
        WraithVentNormallyOnCooldown = new BooleanOptionItem(Id + 4, "WraithVentNormallyOnCooldown", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Wraith]);
    }
}