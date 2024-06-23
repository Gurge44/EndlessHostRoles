using UnityEngine;
using static EHR.Options;

namespace EHR.Crewmate
{
    internal class NiceGuesser : ISettingHolder
    {
        public void SetupCustomOption()
        {
            SetupRoleOptions(8600, TabGroup.CrewmateRoles, CustomRoles.NiceGuesser);
            GGCanGuessTime = new IntegerOptionItem(8610, "GuesserCanGuessTimes", new(0, 15, 1), 15, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.NiceGuesser])
                .SetValueFormat(OptionFormat.Times);
            GGCanGuessCrew = new BooleanOptionItem(8611, "GGCanGuessCrew", true, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.NiceGuesser]);
            GGCanGuessAdt = new BooleanOptionItem(8612, "GGCanGuessAdt", false, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.NiceGuesser]);
            GGTryHideMsg = new BooleanOptionItem(8613, "GuesserTryHideMsg", true, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.NiceGuesser])
                .SetColor(Color.green);
        }
    }
}