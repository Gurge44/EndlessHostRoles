using static EHR.Options;

namespace EHR.Roles.Neutral;

public class Medusa : ISettingHolder
{
    private const int Id = 12400;

    public static OptionItem KillCooldown;
    public static OptionItem KillCooldownAfterStoneGazing;
    public static OptionItem CanVent;
    public static OptionItem HasImpostorVision;

    public void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Medusa);
        KillCooldown = FloatOptionItem.Create(Id + 10, "KillCooldown", new(0f, 180f, 0.5f), 30f, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Medusa])
            .SetValueFormat(OptionFormat.Seconds);
        KillCooldownAfterStoneGazing = FloatOptionItem.Create(Id + 14, "KillCooldownAfterStoneGazing", new(0f, 180f, 0.5f), 60f, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Medusa])
            .SetValueFormat(OptionFormat.Seconds);
        CanVent = BooleanOptionItem.Create(Id + 11, "CanVent", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Medusa]);
        HasImpostorVision = BooleanOptionItem.Create(Id + 13, "ImpostorVision", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Medusa]);
    }
}