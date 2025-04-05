using AmongUs.GameOptions;

namespace EHR.Coven;

public class Augur : Coven
{
    public static bool On;

    public static OptionItem MaxGuessesPerMeeting;
    public static OptionItem MaxGuessesPerGame;
    public static OptionItem CanVent;
    public static OptionItem VentCooldown;
    public static OptionItem MaxInVentTime;

    protected override NecronomiconReceivePriorities NecronomiconReceivePriority => NecronomiconReceivePriorities.Never;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(650070)
            .AutoSetupOption(ref MaxGuessesPerMeeting, 14, new IntegerValueRule(1, 14, 1), OptionFormat.Players)
            .AutoSetupOption(ref MaxGuessesPerGame, 14, new IntegerValueRule(1, 14, 1), OptionFormat.Players)
            .AutoSetupOption(ref CanVent, false)
            .AutoSetupOption(ref VentCooldown, 5f, new FloatValueRule(0f, 120f, 1f), OptionFormat.Seconds)
            .AutoSetupOption(ref MaxInVentTime, 60f, new FloatValueRule(0f, 120f, 1f), OptionFormat.Seconds);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        if (CanVent.GetBool())
        {
            AURoleOptions.EngineerCooldown = VentCooldown.GetFloat();
            AURoleOptions.EngineerInVentMaxTime = MaxInVentTime.GetFloat();
        }
    }
}