namespace EHR.Crewmate
{
    public class Goose : RoleBase
    {
        private const int Id = 641820;

        public static OptionItem OptionAbductTimerLimit;
        public static OptionItem OptionSpeedDuringDrag;
        public static OptionItem OptionVictimCanUseAbilities;
        public static OptionItem CanBeGuessed;
        public static OptionItem Cooldown;

        public override bool IsEnable => false;

        public override void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Goose);
            OptionAbductTimerLimit = new FloatOptionItem(Id + 11, "PenguinAbductTimerLimit", new(1f, 20f, 1f), 10f, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Goose])
                .SetValueFormat(OptionFormat.Seconds);
            OptionSpeedDuringDrag = new FloatOptionItem(Id + 13, "PenguinSpeedDuringDrag", new(0.1f, 3f, 0.1f), 1f, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Goose])
                .SetValueFormat(OptionFormat.Multiplier);
            OptionVictimCanUseAbilities = new BooleanOptionItem(Id + 14, "PenguinVictimCanUseAbilities", false, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Goose]);
            CanBeGuessed = new BooleanOptionItem(Id + 15, "CanBeGuessed", true, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Goose]);
            Cooldown = new FloatOptionItem(Id + 16, "AbilityCooldown", new(0f, 180f, 0.5f), 15f, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Goose])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public override void Init()
        {
        }

        public override void Add(byte playerId)
        {
        }
    }
}