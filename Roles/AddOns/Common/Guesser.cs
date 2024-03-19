using UnityEngine;
using static TOHE.Options;

namespace TOHE.Roles.AddOns.Common
{
    internal class Guesser : IAddon
    {
        public AddonTypes Type => AddonTypes.Helpful;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(19100, CustomRoles.Guesser, canSetNum: true, tab: TabGroup.Addons);
            ImpCanBeGuesser = BooleanOptionItem.Create(19110, "ImpCanBeGuesser", true, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Guesser]);
            CrewCanBeGuesser = BooleanOptionItem.Create(19111, "CrewCanBeGuesser", true, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Guesser]);
            NeutralCanBeGuesser = BooleanOptionItem.Create(19112, "NeutralCanBeGuesser", true, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Guesser]);
            GCanGuessAdt = BooleanOptionItem.Create(19115, "GCanGuessAdt", false, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Guesser]);
            GCanGuessTaskDoneSnitch = BooleanOptionItem.Create(19116, "GCanGuessTaskDoneSnitch", true, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Guesser]);
            GTryHideMsg = BooleanOptionItem.Create(19117, "GuesserTryHideMsg", true, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Guesser])
                .SetColor(Color.green);
        }
    }
}
