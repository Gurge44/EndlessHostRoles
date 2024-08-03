using static EHR.Options;

namespace EHR.Neutral;

public class Medusa : RoleBase
{
    private const int Id = 12400;

    public static OptionItem KillCooldown;
    public static OptionItem KillCooldownAfterStoneGazing;
    public static OptionItem CanVent;
    public static OptionItem HasImpostorVision;
    public static OptionItem CannotStoneGazeWhenKCDIsntUp;

    public override bool IsEnable => false;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Medusa);
        KillCooldown = new FloatOptionItem(Id + 10, "KillCooldown", new(0f, 180f, 0.5f), 30f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Medusa])
            .SetValueFormat(OptionFormat.Seconds);
        KillCooldownAfterStoneGazing = new FloatOptionItem(Id + 14, "KillCooldownAfterStoneGazing", new(0f, 180f, 0.5f), 60f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Medusa])
            .SetValueFormat(OptionFormat.Seconds);
        CanVent = new BooleanOptionItem(Id + 11, "CanVent", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Medusa]);
        HasImpostorVision = new BooleanOptionItem(Id + 13, "ImpostorVision", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Medusa]);
        CannotStoneGazeWhenKCDIsntUp = new BooleanOptionItem(Id + 12, "CannotStoneGazeWhenKCDIsntUp", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Medusa]);
    }

    public override void Init()
    {
    }

    public override void Add(byte playerId)
    {
    }
}