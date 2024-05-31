using static EHR.Options;

namespace EHR.Roles.Neutral;

public class BloodKnight : ISettingHolder
{
    private const int Id = 11800;

    public static OptionItem KillCooldown;
    public static OptionItem CanVent;
    public static OptionItem HasImpostorVision;
    public static OptionItem ProtectDuration;

    public void SetupCustomOption()
    {
        SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.BloodKnight);
        KillCooldown = new FloatOptionItem(Id + 10, "KillCooldown", new(0f, 180f, 0.5f), 22.5f, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.BloodKnight])
            .SetValueFormat(OptionFormat.Seconds);
        CanVent = new BooleanOptionItem(Id + 11, "CanVent", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.BloodKnight]);
        HasImpostorVision = new BooleanOptionItem(Id + 13, "ImpostorVision", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.BloodKnight]);
        ProtectDuration = new FloatOptionItem(Id + 14, "BKProtectDuration", new(1f, 30f, 1f), 15f, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.BloodKnight])
            .SetValueFormat(OptionFormat.Seconds);
    }
}