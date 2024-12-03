using AmongUs.GameOptions;

namespace EHR.Crewmate;

internal class Doctor : RoleBase
{
    public static bool On;
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(5600, TabGroup.CrewmateRoles, CustomRoles.Doctor);

        Options.DoctorTaskCompletedBatteryCharge = new FloatOptionItem(5610, "DoctorTaskCompletedBatteryCharge", new(0f, 300f, 1f), 90f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Doctor])
            .SetValueFormat(OptionFormat.Seconds);

        Options.DoctorVisibleToEveryone = new BooleanOptionItem(5611, "DoctorVisibleToEveryone", false, TabGroup.CrewmateRoles)
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