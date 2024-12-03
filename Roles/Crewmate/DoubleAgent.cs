using System;
using System.Collections.Generic;
using System.Linq;

namespace EHR.Crewmate;

public class DoubleAgent : RoleBase
{
    public static bool On;

    public static Dictionary<byte, CustomRoles> ShownRoles = [];

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(645175);
    }

    public override void Init()
    {
        On = false;
        ShownRoles = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        ShownRoles[playerId] = Enum.GetValues<CustomRoles>().Where(x => x is not CustomRoles.DoubleAgent and not CustomRoles.LovingImpostor && x.IsImpostor() && !x.IsVanilla() && !x.IsAdditionRole() && x.GetMode() != 0).RandomElement();
    }
}