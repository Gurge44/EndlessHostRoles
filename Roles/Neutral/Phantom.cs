using static TOHE.Options;

namespace TOHE.Roles.Neutral
{
    internal class Phantom : ISettingHolder
    {
        public void SetupCustomOption()
        {
            SetupRoleOptions(11400, TabGroup.NeutralRoles, CustomRoles.Phantom);
            PhantomCanVent = BooleanOptionItem.Create(11410, "CanVent", false, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Phantom]);
            PhantomSnatchesWin = BooleanOptionItem.Create(11411, "SnatchesWin", false, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Phantom]);
            PhantomCanGuess = BooleanOptionItem.Create(11412, "CanGuess", false, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Phantom]);
            PhantomTasks = OverrideTasksData.Create(11413, TabGroup.NeutralRoles, CustomRoles.Phantom);
        }
    }
}
