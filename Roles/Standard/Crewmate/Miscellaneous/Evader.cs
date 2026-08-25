namespace EHR.Roles;

public class Evader : RoleBase
{
    public static bool On;

    public override bool IsEnable => On;

    private byte EvaderId;

    public override void SetupCustomOption()
    {
        StartSetup(659800);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        EvaderId = playerId;
    }

    public override void AfterMeetingTasks()
    {
        if (!Utils.IsAnySabotageActive()) return;
        PlayerControl pc = EvaderId.GetPlayer();
        if (!pc || !pc.IsAlive()) return;
        pc.RpcMakeInvisible();
    }
}