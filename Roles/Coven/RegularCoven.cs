namespace EHR.Coven;

public class RegularCoven : Coven
{
    public static bool On;

    protected override NecronomiconReceivePriorities NecronomiconReceivePriority => NecronomiconReceivePriorities.Never;

    public override bool IsEnable => On;

    public override void SetupCustomOption() { }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return false;
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!CustomRoles.CovenLeader.RoleExist())
        {
            pc.RpcSetCustomRole(CustomRoles.CovenLeader);
            pc.RpcChangeRoleBasis(CustomRoles.CovenLeader);
            pc.SyncSettings();
            Utils.NotifyRoles(SpecifyTarget: pc);
        }
    }
}