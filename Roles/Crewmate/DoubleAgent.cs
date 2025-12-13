using System;
using System.Collections.Generic;
using System.Linq;
using static EHR.Options;

namespace EHR.Crewmate;

public class DoubleAgent : RoleBase
{
    private const int Id = 645175;
    public static bool On;

    public static Dictionary<byte, CustomRoles> ShownRoles = [];

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.DoubleAgent);
    }

    public override void Init()
    {
        On = false;
        ShownRoles = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        ShownRoles[playerId] = Enum.GetValues<CustomRoles>().Where(x => x is not CustomRoles.DoubleAgent and not CustomRoles.LovingImpostor && x.IsImpostor() && !x.IsVanilla() && !x.IsForOtherGameMode() && x.GetMode() != 0).RandomElement();
    }

    public override string GetProgressText(byte playerId, bool comms)
    {
        return string.Empty;
    }

    public override bool KnowRole(PlayerControl seer, PlayerControl target)
    {
        if (base.KnowRole(seer, target)) return true;
        return seer.IsImpostor() && Options.ImpKnowAlliesRole.GetBool() && ShownRoles.ContainsKey(target.PlayerId);
    }
}