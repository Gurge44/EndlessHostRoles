using static EHR.Options;

namespace EHR.Neutral;

public class Jinx : RoleBase
{
    private const int Id = 12200;

    public static OptionItem KillCooldown;
    public static OptionItem CanVent;
    public static OptionItem HasImpostorVision;
    public static OptionItem JinxSpellTimes;
    public static OptionItem KillAttacker;

    public override bool IsEnable => false;

    public override void SetupCustomOption()
    {
        SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Jinx);
        KillCooldown = new FloatOptionItem(Id + 10, "KillCooldown", new(0f, 180f, 0.5f), 22.5f, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Jinx])
            .SetValueFormat(OptionFormat.Seconds);
        CanVent = new BooleanOptionItem(Id + 11, "CanVent", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Jinx]);
        HasImpostorVision = new BooleanOptionItem(Id + 13, "ImpostorVision", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Jinx]);
        JinxSpellTimes = new IntegerOptionItem(Id + 14, "JinxSpellTimes", new(0, 15, 1), 1, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Jinx])
            .SetValueFormat(OptionFormat.Times);
        KillAttacker = new BooleanOptionItem(Id + 12, "killAttacker", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Jinx]);
    }

    public override void Init()
    {
    }

    public override void Add(byte playerId)
    {
    }
}