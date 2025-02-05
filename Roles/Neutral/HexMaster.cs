using EHR.Impostor;
using static EHR.Options;

namespace EHR.Neutral;

public class HexMaster : RoleBase
{
    private const int Id = 11900;
    
    public static OptionItem ModeSwitchAction;
    private static OptionItem CanVent;
    private static OptionItem HasImpostorVision;

    public override bool IsEnable => false;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.HexMaster);

        ModeSwitchAction = new StringOptionItem(Id + 10, "WitchModeSwitchAction", Witch.SwitchTriggerText, 2, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.HexMaster]);

        CanVent = new BooleanOptionItem(Id + 11, "CanVent", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.HexMaster]);

        HasImpostorVision = new BooleanOptionItem(Id + 12, "ImpostorVision", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.HexMaster]);
    }

    public override void Init() { }

    public override void Add(byte playerId) { }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        opt.SetVision(HasImpostorVision.GetBool());
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return CanVent.GetBool();
    }
}
