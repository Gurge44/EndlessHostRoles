namespace EHR.Neutral;

public class Wraith : RoleBase
{
    public static OptionItem WraithCooldown;
    public static OptionItem WraithDuration;
    public static OptionItem WraithVentNormallyOnCooldown;

    public override bool IsEnable => false;

    public override void SetupCustomOption()
    {
        StartSetup(13300)
            .AutoSetupOption(ref WraithCooldown, 20f, new FloatValueRule(0f, 60f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref WraithDuration, 10f, new FloatValueRule(0f, 30f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref WraithVentNormallyOnCooldown, true);
    }

    public override void Init() { }

    public override void Add(byte playerId) { }
}