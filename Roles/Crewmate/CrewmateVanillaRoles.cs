using static TOHE.Options;

namespace TOHE.Roles.Crewmate
{
    internal class CrewmateVanillaRoles : IVanillaSettingHolder
    {
        public TabGroup Tab => TabGroup.CrewmateRoles;

        public void SetupCustomOption()
        {
            SetupRoleOptions(5050, Tab, CustomRoles.CrewmateTOHE);
            SetupRoleOptions(5000, Tab, CustomRoles.EngineerTOHE);
            SetupRoleOptions(5100, Tab, CustomRoles.ScientistTOHE);
            ScientistCD = FloatOptionItem.Create(5110, "VitalsCooldown", new(1f, 250f, 1f), 3f, Tab, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.ScientistTOHE])
                .SetValueFormat(OptionFormat.Seconds);
            ScientistDur = FloatOptionItem.Create(5111, "VitalsDuration", new(1f, 250f, 1f), 15f, Tab, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.ScientistTOHE])
                .SetValueFormat(OptionFormat.Seconds);
        }
    }
}
