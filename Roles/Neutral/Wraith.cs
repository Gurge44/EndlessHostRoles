using static EHR.Options;

namespace EHR.Neutral;

public class Wraith : RoleBase
{
    private const int Id = 13300;

    public static OptionItem WraithCooldown;
    public static OptionItem WraithDuration;
    public static OptionItem WraithVentNormallyOnCooldown;

    public override bool IsEnable => false;

    public override void SetupCustomOption()
    {
        SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Wraith);
        WraithCooldown = new FloatOptionItem(Id + 2, "WraithCooldown", new(0f, 60f, 0.5f), 20f, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Wraith])
            .SetValueFormat(OptionFormat.Seconds);
        WraithDuration = new FloatOptionItem(Id + 3, "WraithDuration", new(0f, 30f, 0.5f), 10f, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Wraith])
            .SetValueFormat(OptionFormat.Seconds);
        WraithVentNormallyOnCooldown = new BooleanOptionItem(Id + 4, "WraithVentNormallyOnCooldown", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Wraith]);
    }

    public override void Init() => throw new System.NotImplementedException();
    public override void Add(byte playerId) => throw new System.NotImplementedException();
}