using System.Collections.Generic;
using static EHR.Options;

namespace EHR.AddOns.Common;

internal class Lazy : IAddon
{
    public static Dictionary<byte, Vector2> BeforeMeetingPositions = [];
    public AddonTypes Type => AddonTypes.Helpful;

    public void SetupCustomOption()
    {
        SetupAdtRoleOptions(14100, CustomRoles.Lazy, canSetNum: true);
    }
}