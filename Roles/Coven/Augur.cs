namespace EHR.Coven;

public class Augur : Coven
{
    public static bool On;

    public override NecronomiconReceivePriorities NecronomiconReceivePriority => NecronomiconReceivePriorities.Never;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(650070);
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