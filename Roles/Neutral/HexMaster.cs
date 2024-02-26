using System.Collections.Generic;
using TOHE.Roles.Impostor;
using static TOHE.Options;

namespace TOHE.Roles.Neutral;

public static class HexMaster
{
    private const int Id = 11900;
    public static List<byte> playerIdList = [];
    public static OptionItem ModeSwitchAction;

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.HexMaster);
        ModeSwitchAction = StringOptionItem.Create(Id + 10, "WitchModeSwitchAction", Witch.SwitchTriggerText, 2, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.HexMaster]);
    }
}