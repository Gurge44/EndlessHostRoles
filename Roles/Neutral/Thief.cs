using AmongUs.GameOptions;

namespace EHR.Neutral;

public class Thief : RoleBase
{
    public static bool On;

    private static OptionItem CanStealCovenRoles;
    private static OptionItem ImpostorVision;
    private static OptionItem CanKillRound1;
    private static OptionItem KillCooldown;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(656400)
            .AutoSetupOption(ref CanStealCovenRoles, false)
            .AutoSetupOption(ref ImpostorVision, false)
            .AutoSetupOption(ref CanKillRound1, false)
            .AutoSetupOption(ref KillCooldown, 30f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds);
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
        return !MeetingStates.FirstMeeting || CanKillRound1.GetBool();
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        opt.SetVision(ImpostorVision.GetBool());
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        bool success = target.IsImpostor() || target.Is(CustomRoles.Trickster) || target.IsMadmate() || target.Is(CustomRoles.Maverick) || target.IsNeutralKiller() || (target.Is(CustomRoleTypes.Coven) && CanStealCovenRoles.GetBool());
        
        if (!success) killer.Suicide();
        else
        {
            GameEndChecker.ShouldNotCheck = true;
            LateTask.New(() => GameEndChecker.ShouldNotCheck = false, 1f, log: false);
        }
        
        return success;
    }

    public override void OnMurder(PlayerControl killer, PlayerControl target)
    {
        LateTask.New(() => 
        {
            CustomRoles? role = null;
            
            if (target.Is(CustomRoles.Trickster)) role = CustomRoles.DoubleAgent;
            else if (target.Is(CustomRoles.Maverick)) role = CustomRoles.SerialKiller;
            else if (target.IsMadmate()) role = CustomRoles.Renegade;
            else if (target.IsImpostor() || target.IsNeutralKiller()) role = target.GetCustomRole();
            else if (CanStealCovenRoles.GetBool() && target.Is(CustomRoleTypes.Coven)) role = CustomRoles.CovenMember;
    
            if (!role.HasValue)
            {
                if (killer.IsAlive()) killer.Suicide();
                return;
            }
            
            killer.RpcSetCustomRole(role.Value);
            killer.RpcChangeRoleBasis(role.Value);
            killer.ResetKillCooldown();
            killer.SetKillCooldown();
        }, 0.2f, log: false);
    }
}