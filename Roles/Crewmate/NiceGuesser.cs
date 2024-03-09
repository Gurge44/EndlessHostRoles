using UnityEngine;
using static TOHE.Options;

namespace TOHE.Roles.Crewmate
{
    internal static class NiceGuesser
    {
        public static void SetupCustomOption()
        {
            SetupRoleOptions(8600, TabGroup.CrewmateRoles, CustomRoles.NiceGuesser);
            GGCanGuessTime = IntegerOptionItem.Create(8610, "GuesserCanGuessTimes", new(0, 15, 1), 15, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.NiceGuesser])
                .SetValueFormat(OptionFormat.Times);
            GGCanGuessCrew = BooleanOptionItem.Create(8611, "GGCanGuessCrew", true, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.NiceGuesser]);
            GGCanGuessAdt = BooleanOptionItem.Create(8612, "GGCanGuessAdt", false, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.NiceGuesser]);
            GGTryHideMsg = BooleanOptionItem.Create(8613, "GuesserTryHideMsg", true, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.NiceGuesser])
                .SetColor(Color.green);
        }
    }
}
