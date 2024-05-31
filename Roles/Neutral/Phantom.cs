using static EHR.Options;

namespace EHR.Roles.Neutral
{
    internal class Phantom : ISettingHolder
    {
        public void SetupCustomOption()
        {
            SetupRoleOptions(11400, TabGroup.NeutralRoles, CustomRoles.Phantom);
            PhantomCanVent = new BooleanOptionItem(11410, "CanVent", false, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Phantom]);
            PhantomSnatchesWin = new BooleanOptionItem(11411, "SnatchesWin", false, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Phantom]);
            PhantomCanGuess = new BooleanOptionItem(11412, "CanGuess", false, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Phantom]);
            OverrideTasksData.Create(11413, TabGroup.NeutralRoles, CustomRoles.Phantom);
        }
    }
}