using System.Linq;
using EHR.Modules;
using Il2CppSystem;

namespace EHR.Crewmate;

public class Helper : RoleBase
{
    public static bool On;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(651300)
            .CreateOverrideTasksData();
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
    }

    public override void OnTaskComplete(PlayerControl pc, int completedTaskCount, int totalTaskCount)
    {
        var randomPlayer = Main.AllPlayerControls.Without(pc).Where(x => x.Is(Team.Crewmate)).Select(x => (pc: x, ts: x.GetTaskState())).Where(x => !x.ts.IsTaskFinished && x.ts.HasTasks).Select(x => x.pc).RandomElement();
        var incompleteTasks = randomPlayer.myTasks.FindAll((Predicate<PlayerTask>)(x => !x.IsComplete));
        RPC.PlaySoundRPC(randomPlayer.PlayerId, Sounds.TaskUpdateSound);
        randomPlayer.RpcCompleteTask(incompleteTasks[IRandom.Instance.Next(0, incompleteTasks.Count)].Id);
        randomPlayer.Notify(string.Format(Translator.GetString("HelperCompletedTaskForYou"), CustomRoles.Helper.ToColoredString()));
    }
}
