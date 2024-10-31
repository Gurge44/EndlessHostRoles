using static EHR.Options;

namespace EHR.Crewmate
{
    internal class CrewmateVanillaRoles : IVanillaSettingHolder
    {
        public static OptionItem VanillaCrewmateCannotBeGuessed;
        public static OptionItem EngineerCD;
        public static OptionItem EngineerDur;
        public static OptionItem NoiseMakerImpostorAlert;
        public static OptionItem NoisemakerAlertDuration;
        public static OptionItem ScientistDur;
        public static OptionItem ScientistCD;
        public static OptionItem TrackerCooldown;
        public static OptionItem TrackerDuration;
        public static OptionItem TrackerDelay;
        public TabGroup Tab => TabGroup.CrewmateRoles;

        public void SetupCustomOption()
        {
            SetupRoleOptions(5020, Tab, CustomRoles.CrewmateEHR);

            VanillaCrewmateCannotBeGuessed = new BooleanOptionItem(5022, "VanillaCrewmateCannotBeGuessed", false, Tab)
                .SetParent(CustomRoleSpawnChances[CustomRoles.CrewmateEHR]);

            SetupRoleOptions(5000, Tab, CustomRoles.EngineerEHR);

            EngineerCD = new FloatOptionItem(5002, "VentCooldown", new(1f, 250f, 1f), 30f, Tab)
                .SetParent(CustomRoleSpawnChances[CustomRoles.EngineerEHR])
                .SetValueFormat(OptionFormat.Seconds);

            EngineerDur = new FloatOptionItem(5003, "MaxInVentTime", new(1f, 250f, 1f), 15f, Tab)
                .SetParent(CustomRoleSpawnChances[CustomRoles.EngineerEHR])
                .SetValueFormat(OptionFormat.Seconds);

            SetupRoleOptions(5040, Tab, CustomRoles.NoisemakerEHR);

            NoiseMakerImpostorAlert = new BooleanOptionItem(5042, "NoisemakerImpostorAlert", false, Tab)
                .SetParent(CustomRoleSpawnChances[CustomRoles.NoisemakerEHR]);

            NoisemakerAlertDuration = new FloatOptionItem(5043, "NoisemakerAlertDuration", new(1f, 250f, 1f), 5f, Tab)
                .SetParent(CustomRoleSpawnChances[CustomRoles.NoisemakerEHR])
                .SetValueFormat(OptionFormat.Seconds);

            SetupRoleOptions(5100, Tab, CustomRoles.ScientistEHR);

            ScientistCD = new FloatOptionItem(5102, "VitalsCooldown", new(1f, 250f, 1f), 3f, Tab)
                .SetParent(CustomRoleSpawnChances[CustomRoles.ScientistEHR])
                .SetValueFormat(OptionFormat.Seconds);

            ScientistDur = new FloatOptionItem(5103, "VitalsDuration", new(1f, 250f, 1f), 15f, Tab)
                .SetParent(CustomRoleSpawnChances[CustomRoles.ScientistEHR])
                .SetValueFormat(OptionFormat.Seconds);

            SetupRoleOptions(5060, Tab, CustomRoles.TrackerEHR);

            TrackerCooldown = new FloatOptionItem(5062, "TrackerCooldown", new(1f, 250f, 1f), 25f, Tab)
                .SetParent(CustomRoleSpawnChances[CustomRoles.TrackerEHR])
                .SetValueFormat(OptionFormat.Seconds);

            TrackerDuration = new FloatOptionItem(5063, "TrackerDuration", new(1f, 250f, 1f), 20f, Tab)
                .SetParent(CustomRoleSpawnChances[CustomRoles.TrackerEHR])
                .SetValueFormat(OptionFormat.Seconds);

            TrackerDelay = new FloatOptionItem(5064, "TrackerDelay", new(1f, 250f, 1f), 5f, Tab)
                .SetParent(CustomRoleSpawnChances[CustomRoles.TrackerEHR])
                .SetValueFormat(OptionFormat.Seconds);
        }
    }
}