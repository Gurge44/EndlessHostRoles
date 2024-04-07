using static EHR.Options;

namespace EHR.Roles.Neutral;

public class Jinx : ISettingHolder
{
    private const int Id = 12200;

    public static OptionItem KillCooldown;
    public static OptionItem CanVent;
    public static OptionItem HasImpostorVision;
    public static OptionItem JinxSpellTimes;
    public static OptionItem KillAttacker;

    public void SetupCustomOption()
    {
        SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Jinx);
        KillCooldown = FloatOptionItem.Create(Id + 10, "KillCooldown", new(0f, 180f, 2.5f), 22.5f, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Jinx])
            .SetValueFormat(OptionFormat.Seconds);
        CanVent = BooleanOptionItem.Create(Id + 11, "CanVent", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Jinx]);
        HasImpostorVision = BooleanOptionItem.Create(Id + 13, "ImpostorVision", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Jinx]);
        JinxSpellTimes = IntegerOptionItem.Create(Id + 14, "JinxSpellTimes", new(0, 15, 1), 1, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Jinx])
            .SetValueFormat(OptionFormat.Times);
        KillAttacker = BooleanOptionItem.Create(Id + 12, "killAttacker", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Jinx]);
    }
}
