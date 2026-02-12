using EHR.Modules;

namespace EHR.Roles;

public class Pawn : RoleBase
{
    public static bool On;

    public static OptionItem KeepsGameGoing;
    
    public CustomRoles ChosenRole;

    private byte PawnId;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(651500)
            .AutoSetupOption(ref KeepsGameGoing, true)
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
        PawnId = playerId;
    }

    public override void OnTaskComplete(PlayerControl pc, int completedTaskCount, int totalTaskCount)
    {
        if (completedTaskCount + 1 < totalTaskCount || ChosenRole == CustomRoles.NotAssigned || ChosenRole.IsAdditionRole() || ChosenRole.IsForOtherGameMode()) return;

        pc.RpcSetCustomRole(ChosenRole);
        pc.RpcChangeRoleBasis(ChosenRole);
        
        if (pc.AmOwner && ChosenRole is CustomRoles.Crewmate or CustomRoles.CrewmateEHR)
            Achievements.Type.Why.Complete();
    }

    public override void AfterMeetingTasks()
    {
        var pc = PawnId.GetPlayer();
        if (pc == null || !pc.IsAlive()) return;
        
        if (!pc.AllTasksCompleted() || ChosenRole == CustomRoles.NotAssigned || ChosenRole.IsAdditionRole() || ChosenRole.IsForOtherGameMode()) return;

        pc.RpcSetCustomRole(ChosenRole);
        pc.RpcChangeRoleBasis(ChosenRole);
        
        if (pc.AmOwner && ChosenRole is CustomRoles.Crewmate or CustomRoles.CrewmateEHR)
            Achievements.Type.Why.Complete();
    }
}