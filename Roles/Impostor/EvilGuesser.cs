using UnityEngine;
using static TOHE.Options;

namespace TOHE.Roles.Impostor
{
    internal static class EvilGuesser
    {
        public static void SetupCustomOption()
        {
            SetupRoleOptions(1200, TabGroup.ImpostorRoles, CustomRoles.EvilGuesser);
            EGCanGuessTime = IntegerOptionItem.Create(1205, "GuesserCanGuessTimes", new(1, 15, 1), 15, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.EvilGuesser])
                .SetValueFormat(OptionFormat.Times);
            EGCanGuessImp = BooleanOptionItem.Create(1206, "EGCanGuessImp", true, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.EvilGuesser]);
            EGCanGuessAdt = BooleanOptionItem.Create(1207, "EGCanGuessAdt", false, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.EvilGuesser]);
            EGCanGuessTaskDoneSnitch = BooleanOptionItem.Create(1208, "EGCanGuessTaskDoneSnitch", true, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.EvilGuesser]);
            EGTryHideMsg = BooleanOptionItem.Create(1209, "GuesserTryHideMsg", true, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.EvilGuesser])
                .SetColor(Color.green);
        }
    }
}
