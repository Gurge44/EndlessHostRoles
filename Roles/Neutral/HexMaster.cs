using EHR.Impostor;
using static EHR.Options;

namespace EHR.Neutral;

public class HexMaster : RoleBase
{
    private const int Id = 11900;
    public static OptionItem ModeSwitchAction;

    public override bool IsEnable => false;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.HexMaster);

        ModeSwitchAction = new StringOptionItem(Id + 10, "WitchModeSwitchAction", Witch.SwitchTriggerText, 2, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.HexMaster]);
    }

    public override void Init() { }

    public override void Add(byte playerId) { }
}