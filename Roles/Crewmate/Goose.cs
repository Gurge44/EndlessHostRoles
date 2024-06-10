namespace EHR.Crewmate
{
    public class Goose : ISettingHolder
    {
        private const int Id = 641820;

        public static OptionItem OptionAbductTimerLimit;
        public static OptionItem OptionMeetingKill;
        public static OptionItem OptionSpeedDuringDrag;
        public static OptionItem OptionVictimCanUseAbilities;
        public static OptionItem CanBeGuessed;

        public void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Goose);
            OptionAbductTimerLimit = new FloatOptionItem(Id + 11, "PenguinAbductTimerLimit", new(1f, 20f, 1f), 10f, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Goose])
                .SetValueFormat(OptionFormat.Seconds);
            OptionMeetingKill = new BooleanOptionItem(Id + 12, "PenguinMeetingKill", false, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Goose]);
            OptionSpeedDuringDrag = new FloatOptionItem(Id + 13, "PenguinSpeedDuringDrag", new(0.1f, 3f, 0.1f), 1f, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Goose])
                .SetValueFormat(OptionFormat.Multiplier);
            OptionVictimCanUseAbilities = new BooleanOptionItem(Id + 14, "PenguinVictimCanUseAbilities", false, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Goose]);
            CanBeGuessed = new BooleanOptionItem(Id + 15, "CanBeGuessed", true, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Goose]);
        }
    }
}