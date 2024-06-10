using static EHR.Options;

namespace EHR.Roles.Neutral
{
    internal class God : ISettingHolder
    {
        public void SetupCustomOption()
        {
            SetupRoleOptions(18200, TabGroup.NeutralRoles, CustomRoles.God);
            NotifyGodAlive = new BooleanOptionItem(18210, "NotifyGodAlive", true, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.God]);
            GodCanGuess = new BooleanOptionItem(18211, "CanGuess", false, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.God]);
        }
    }
}