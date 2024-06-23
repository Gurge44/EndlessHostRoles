using static EHR.Options;

namespace EHR.Crewmate
{
    internal class CrewmateVanillaRoles : IVanillaSettingHolder
    {
        public TabGroup Tab => TabGroup.CrewmateRoles;

        public void SetupCustomOption()
        {
            SetupRoleOptions(5050, Tab, CustomRoles.CrewmateEHR);
            VanillaCrewmateCannotBeGuessed = new BooleanOptionItem(5060, "VanillaCrewmateCannotBeGuessed", false, Tab)
                .SetParent(CustomRoleSpawnChances[CustomRoles.CrewmateEHR]);
            SetupRoleOptions(5000, Tab, CustomRoles.EngineerEHR);
            SetupRoleOptions(5100, Tab, CustomRoles.ScientistEHR);
            ScientistCD = new FloatOptionItem(5110, "VitalsCooldown", new(1f, 250f, 1f), 3f, Tab)
                .SetParent(CustomRoleSpawnChances[CustomRoles.ScientistEHR])
                .SetValueFormat(OptionFormat.Seconds);
            ScientistDur = new FloatOptionItem(5111, "VitalsDuration", new(1f, 250f, 1f), 15f, Tab)
                .SetParent(CustomRoleSpawnChances[CustomRoles.ScientistEHR])
                .SetValueFormat(OptionFormat.Seconds);
        }
    }
}