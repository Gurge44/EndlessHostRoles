using UnityEngine;
using static EHR.Options;

namespace EHR.Roles;

internal class Guesser : IAddon
{
    public static OptionItem GCanGuessAdt;
    public AddonTypes Type => AddonTypes.Helpful;

    public void SetupCustomOption()
    {
        SetupAdtRoleOptions(19100, CustomRoles.Guesser, canSetNum: true, tab: TabGroup.Addons, teamSpawnOptions: true);

        GCanGuessAdt = new BooleanOptionItem(19110, "GCanGuessAdt", false, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Guesser]);
    }
}