using static TOHE.Options;

namespace TOHE.Roles.Neutral
{
    internal class God : ISettingHolder
    {
        public void SetupCustomOption()
        {
            SetupRoleOptions(18200, TabGroup.NeutralRoles, CustomRoles.God);
            NotifyGodAlive = BooleanOptionItem.Create(18210, "NotifyGodAlive", true, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.God]);
            GodCanGuess = BooleanOptionItem.Create(18211, "CanGuess", false, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.God]);
        }
    }
}
