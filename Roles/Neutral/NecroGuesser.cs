namespace EHR.Neutral;

public class NecroGuesser : RoleBase
{
    public static bool On;

    public static OptionItem NumGuessesToWin;

    public int GuessedPlayers;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(647125)
            .AutoSetupOption(ref NumGuessesToWin, 2, new IntegerValueRule(1, 14, 1), OptionFormat.Players);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        GuessedPlayers = 0;
    }
}