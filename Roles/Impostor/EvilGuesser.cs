using UnityEngine;
using static EHR.Options;

namespace EHR.Roles.Impostor
{
    internal class EvilGuesser : ISettingHolder
    {
        public void SetupCustomOption()
        {
            SetupRoleOptions(1200, TabGroup.ImpostorRoles, CustomRoles.EvilGuesser);
            EGCanGuessTime = new IntegerOptionItem(1205, "GuesserCanGuessTimes", new(1, 15, 1), 15, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.EvilGuesser])
                .SetValueFormat(OptionFormat.Times);
            EGCanGuessImp = new BooleanOptionItem(1206, "EGCanGuessImp", true, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.EvilGuesser]);
            EGCanGuessAdt = new BooleanOptionItem(1207, "EGCanGuessAdt", false, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.EvilGuesser]);
            EGTryHideMsg = new BooleanOptionItem(1209, "GuesserTryHideMsg", true, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.EvilGuesser])
                .SetColor(Color.green);
        }
    }
}