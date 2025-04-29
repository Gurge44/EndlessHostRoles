namespace EHR.Neutral;

public class Pawn : RoleBase
{
    public static bool On;

    public CustomRoles ChosenRole;

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
        ChosenRole = CustomRoles.NotAssigned;
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!pc.AllTasksCompleted() || ChosenRole == CustomRoles.NotAssigned || ChosenRole.IsAdditionRole() || ChosenRole.IsForOtherGameMode()) return;

        pc.RpcSetCustomRole(ChosenRole);
        pc.RpcChangeRoleBasis(ChosenRole);
    }
}