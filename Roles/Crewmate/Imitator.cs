namespace EHR.Crewmate;

public class Imitator : RoleBase
{
    public static bool On;

    public override bool IsEnable => On;

    private PlayerControl ImitatorPC;
    public CustomRoles ImitatingRole;

    public override void SetupCustomOption()
    {
        StartSetup(653190);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        ImitatorPC = playerId.GetPlayer();
        ImitatingRole = CustomRoles.Imitator;
    }

    public override void OnReportDeadBody()
    {
        ImitatingRole = CustomRoles.Imitator;
    }

    public override void AfterMeetingTasks()
    {
        if (ImitatorPC == null || !ImitatorPC.IsAlive()) return;

        ImitatorPC.RpcChangeRoleBasis(ImitatingRole);
        ImitatorPC.RpcSetCustomRole(ImitatingRole);
    }
}