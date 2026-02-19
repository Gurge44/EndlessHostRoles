using System.Collections.Generic;

namespace EHR.Roles;

public class CovenMember : CovenBase
{
    public static bool On;
    private static List<CovenMember> Instances = [];

    protected override NecronomiconReceivePriorities NecronomiconReceivePriority => NecronomiconReceivePriorities.Never;

    public override bool IsEnable => On;

    private byte CovenMemberId;

    public override void SetupCustomOption() { }

    public override void Init()
    {
        On = false;
        Instances = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        Instances.Add(this);
        CovenMemberId = playerId;
    }

    public override void Remove(byte playerId)
    {
        Instances.Remove(this);
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return false;
    }

    public static void OnAnyoneDead()
    {
        if (Instances.Count > 0 && !CustomRoles.CovenLeader.RoleExist())
        {
            foreach (CovenMember instance in Instances)
            {
                PlayerControl pc = Utils.GetPlayerById(instance.CovenMemberId);
                if (!pc || !pc.IsAlive()) continue;
                pc.RpcSetCustomRole(CustomRoles.CovenLeader);
                pc.RpcChangeRoleBasis(CustomRoles.CovenLeader);
                pc.MarkDirtySettings();
                Utils.NotifyRoles(SpecifyTarget: pc);
                return;
            }
        }
    }
}