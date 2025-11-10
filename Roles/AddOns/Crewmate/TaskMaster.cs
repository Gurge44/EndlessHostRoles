using EHR.Modules;
using Il2CppSystem;
using Exception = System.Exception;

namespace EHR.AddOns.Crewmate;

public class TaskMaster : IAddon
{
    public AddonTypes Type => AddonTypes.Helpful;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(655500, CustomRoles.TaskMaster, canSetNum: true);
    }

    public static void AfterMeetingTasks(PlayerControl pc)
    {
        try
        {
            TaskState ts = pc.GetTaskState();
            if (!ts.HasTasks || ts.IsTaskFinished || !Utils.HasTasks(pc.Data, forRecompute: false)) return;
            var incompleteTasks = pc.myTasks.FindAll((Predicate<PlayerTask>)(x => !x.IsComplete));
            LateTask.New(() =>
            {
                if (GameStates.IsEnded) return;
                RPC.PlaySoundRPC(pc.PlayerId, Sounds.TaskUpdateSound);
                pc.RpcCompleteTask(incompleteTasks[IRandom.Instance.Next(0, incompleteTasks.Count)].Id);
            }, 0.3f, log: false);
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }
}