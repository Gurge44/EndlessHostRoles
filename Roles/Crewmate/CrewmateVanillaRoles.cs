using static EHR.Options;

namespace EHR.Roles.Crewmate
{
    internal class CrewmateVanillaRoles : IVanillaSettingHolder
    {
        public TabGroup Tab => TabGroup.CrewmateRoles;

        public void SetupCustomOption()
        {
            SetupRoleOptions(5050, Tab, CustomRoles.CrewmateEHR);
            SetupRoleOptions(5000, Tab, CustomRoles.EngineerEHR);
            SetupRoleOptions(5100, Tab, CustomRoles.ScientistEHR);
            ScientistCD = FloatOptionItem.Create(5110, "VitalsCooldown", new(1f, 250f, 1f), 3f, Tab, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.ScientistEHR])
                .SetValueFormat(OptionFormat.Seconds);
            ScientistDur = FloatOptionItem.Create(5111, "VitalsDuration", new(1f, 250f, 1f), 15f, Tab, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.ScientistEHR])
                .SetValueFormat(OptionFormat.Seconds);
        }
    }
}
