using AmongUs.GameOptions;

namespace EHR.Roles.Crewmate
{
    internal class Doctor : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(5600, TabGroup.CrewmateRoles, CustomRoles.Doctor);
            Options.DoctorTaskCompletedBatteryCharge = FloatOptionItem.Create(5610, "DoctorTaskCompletedBatteryCharge", new(0f, 250f, 1f), 50f, TabGroup.CrewmateRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Doctor])
                .SetValueFormat(OptionFormat.Seconds);
            Options.DoctorVisibleToEveryone = BooleanOptionItem.Create(5611, "DoctorVisibleToEveryone", false, TabGroup.CrewmateRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Doctor]);
        }

        public override void Add(byte playerId)
        {
            On = true;
        }

        public override void Init()
        {
            On = false;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            AURoleOptions.ScientistCooldown = 0f;
            AURoleOptions.ScientistBatteryCharge = Options.DoctorTaskCompletedBatteryCharge.GetFloat();
        }
    }
}