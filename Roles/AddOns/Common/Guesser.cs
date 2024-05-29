using UnityEngine;
using static EHR.Options;

namespace EHR.Roles.AddOns.Common
{
    internal class Guesser : IAddon
    {
        public static OptionItem GCanGuessAdt;
        public static OptionItem GTryHideMsg;
        public AddonTypes Type => AddonTypes.Helpful;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(19100, CustomRoles.Guesser, canSetNum: true, tab: TabGroup.Addons);
            GCanGuessAdt = BooleanOptionItem.Create(19110, "GCanGuessAdt", false, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Guesser]);
            GTryHideMsg = BooleanOptionItem.Create(19111, "GuesserTryHideMsg", true, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Guesser])
                .SetColor(Color.green);
        }
    }
}