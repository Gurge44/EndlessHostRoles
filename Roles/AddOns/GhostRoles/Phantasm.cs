using AmongUs.GameOptions;
using EHR.Modules;

namespace EHR.AddOns.GhostRoles;

// TOU Phantom
internal class Phantasm : IGhostRole
{
    private static OptionItem SnatchWin;

    public bool IsWon;
    public Team Team => Team.Neutral;
    public RoleTypes RoleTypes => RoleTypes.CrewmateGhost;
    public int Cooldown => 900;

    public void OnAssign(PlayerControl pc)
    {
        IsWon = false;

        LateTask.New(() =>
        {
            TaskState taskState = pc.GetTaskState();
            if (taskState == null) return;

            taskState.HasTasks = true;
            taskState.CompletedTasksCount = 0;
            taskState.AllTasksCount = Utils.TotalTaskCount - Main.RealOptionsData.GetInt(Int32OptionNames.NumCommonTasks);

            pc.RpcResetTasks();
            pc.SyncSettings();
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }, 1f, "Phantasm Assign");
    }

    public void OnProtect(PlayerControl pc, PlayerControl target) { }

    public void SetupCustomOption()
    {
        Options.SetupRoleOptions(649100, TabGroup.OtherRoles, CustomRoles.Phantasm);

        SnatchWin = new BooleanOptionItem(649102, "SnatchWin", false, TabGroup.OtherRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Phantasm]);

        Options.OverrideTasksData.Create(649103, TabGroup.OtherRoles, CustomRoles.Phantasm);
    }

    public void OnFinishedTasks(PlayerControl pc)
    {
        if (!SnatchWin.GetBool())
        {
            IsWon = true;
            return;
        }

        pc.RPCPlayCustomSound("Congrats");
        CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Phantasm);
        CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
    }
}
