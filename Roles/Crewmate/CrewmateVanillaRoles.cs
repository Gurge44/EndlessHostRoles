using static TOHE.Options;

namespace TOHE.Roles.Crewmate
{
    internal static class CrewmateVanillaRoles
    {
        public static void SetupCustomOption()
        {
            SetupRoleOptions(5050, TabGroup.CrewmateRoles, CustomRoles.CrewmateTOHE);
            SetupRoleOptions(5000, TabGroup.CrewmateRoles, CustomRoles.EngineerTOHE);
            SetupRoleOptions(5100, TabGroup.CrewmateRoles, CustomRoles.ScientistTOHE);
            ScientistCD = FloatOptionItem.Create(5110, "VitalsCooldown", new(1f, 250f, 1f), 3f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.ScientistTOHE])
                .SetValueFormat(OptionFormat.Seconds);
            ScientistDur = FloatOptionItem.Create(5111, "VitalsDuration", new(1f, 250f, 1f), 15f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.ScientistTOHE])
                .SetValueFormat(OptionFormat.Seconds);
        }
    }
}
