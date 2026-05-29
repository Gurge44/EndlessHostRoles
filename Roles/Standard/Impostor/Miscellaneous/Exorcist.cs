namespace EHR.Roles;

public class Exorcist : RoleBase
{
    public static bool On;

    public static OptionItem AbilityDuration;
    private static OptionItem AbilityUseLimit;
    private static OptionItem AbilityUseGainWithEachKill;

    public static long AbilityEndTS;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(658500)
            .AutoSetupOption(ref AbilityDuration, 15, new IntegerValueRule(1, 120, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref AbilityUseLimit, 1f, new FloatValueRule(0, 20, 0.05f), OptionFormat.Times)
            .AutoSetupOption(ref AbilityUseGainWithEachKill, 1f, new FloatValueRule(0f, 5f, 0.25f), OptionFormat.Times);
    }

    public override void Init()
    {
        On = false;
        AbilityEndTS = 0;
    }

    public override void Add(byte playerId)
    {
        On = true;
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetFloat());
    }

    public override void OnMeetingShapeshift(PlayerControl shapeshifter, PlayerControl target)
    {
        const string command = "/exo";
        ChatCommands.ExoCommand(shapeshifter, command, command.Split(' '));
    }
}