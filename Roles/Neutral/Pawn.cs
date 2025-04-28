namespace EHR.Neutral;

public class Pawn : RoleBase
{
    public static bool On;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(651500)
            .CreateOverrideTasksData();
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