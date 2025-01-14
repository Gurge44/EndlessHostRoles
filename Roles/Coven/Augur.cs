namespace EHR.Coven;

public class Augur : Coven
{
    public static bool On;

    public static OptionItem MaxGuessesPerMeeting;
    public static OptionItem MaxGuessesPerGame;

    protected override NecronomiconReceivePriorities NecronomiconReceivePriority => NecronomiconReceivePriorities.Never;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(650070)
            .AutoSetupOption(ref MaxGuessesPerMeeting, 14, new IntegerValueRule(1, 14, 1), OptionFormat.Players)
            .AutoSetupOption(ref MaxGuessesPerGame, 14, new IntegerValueRule(1, 14, 1), OptionFormat.Players);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
    }
}