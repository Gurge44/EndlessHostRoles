using static TOHE.Options;

namespace TOHE.Roles.Neutral
{
    internal class God : ISettingHolder
    {
        public void SetupCustomOption()
        {
            SetupRoleOptions(18200, TabGroup.OtherRoles, CustomRoles.God);
            NotifyGodAlive = BooleanOptionItem.Create(18210, "NotifyGodAlive", true, TabGroup.OtherRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.God]);
            GodCanGuess = BooleanOptionItem.Create(18211, "CanGuess", false, TabGroup.OtherRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.God]);
        }
    }
}
