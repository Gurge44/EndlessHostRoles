using AmongUs.GameOptions;

namespace EHR.Roles;

public class Augur : CovenBase
{
    public static bool On;

    public static OptionItem MaxGuessesPerMeeting;
    public static OptionItem MaxGuessesPerGame;
    public static OptionItem CanVent;
    public static OptionItem VentCooldown;
    public static OptionItem MaxInVentTime;

    protected override NecronomiconReceivePriorities NecronomiconReceivePriority => NecronomiconReceivePriorities.Never;

    public override bool IsEnable => On;

    public bool CanGuess;

    public override void SetupCustomOption()
    {
        StartSetup(650070)
            .AutoSetupOption(ref MaxGuessesPerMeeting, 14, new IntegerValueRule(1, 14, 1), OptionFormat.Players)
            .AutoSetupOption(ref MaxGuessesPerGame, 14, new IntegerValueRule(1, 14, 1), OptionFormat.Players)
            .AutoSetupOption(ref CanVent, false)
            .AutoSetupOption(ref VentCooldown, 5f, new FloatValueRule(0f, 120f, 1f), OptionFormat.Seconds, overrideParent: CanVent)
            .AutoSetupOption(ref MaxInVentTime, 60f, new FloatValueRule(0f, 120f, 1f), OptionFormat.Seconds, overrideParent: CanVent);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        CanGuess = true;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        if (CanVent.GetBool())
        {
            AURoleOptions.EngineerCooldown = VentCooldown.GetFloat();
            AURoleOptions.EngineerInVentMaxTime = MaxInVentTime.GetFloat();
        }
    }

    public override void OnReportDeadBody()
    {
        CanGuess = true;
    }

    public override void AfterMeetingTasks()
    {
        CanGuess = true;
    }
}