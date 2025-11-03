namespace EHR.Neutral;

public class Thief : RoleBase
{
    public static bool On;

    private static OptionItem CanStealCovenRoles;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(656400)
            .AutoSetupOption(ref CanStealCovenRoles, false);
    }

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
        return pc.IsAlive();
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        bool success = target.IsImpostor() || target.Is(CustomRoles.Trickster) || target.IsMadmate() || target.Is(CustomRoles.Maverick) || target.IsNeutralKiller() || (target.Is(CustomRoleTypes.Coven) && CanStealCovenRoles.GetBool());
        if (!success) killer.Suicide();
        return success;
    }

    public override void OnMurder(PlayerControl killer, PlayerControl target)
    {
        CustomRoles? role = null;

        if (target.Is(CustomRoles.Trickster)) role = CustomRoles.DoubleAgent;
        else if (target.Is(CustomRoles.Maverick)) role = CustomRoles.SerialKiller;
        else if (target.IsMadmate()) role = CustomRoles.Refugee;
        else if (target.IsImpostor() || target.IsNeutralKiller()) role = target.GetCustomRole();
        else if (CanStealCovenRoles.GetBool() && target.Is(CustomRoleTypes.Coven)) role = CustomRoles.CovenMember;

        if (!role.HasValue)
        {
            if (killer.IsAlive()) killer.Suicide();
            return;
        }
        
        killer.RpcChangeRoleBasis(role.Value);
        killer.RpcSetCustomRole(role.Value);
        killer.ResetKillCooldown();
        killer.SetKillCooldown();
    }
}