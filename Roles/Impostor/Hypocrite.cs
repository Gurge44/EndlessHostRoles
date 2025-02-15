using EHR.Modules;

namespace EHR.Impostor;

public class Hypocrite : RoleBase
{
    public static bool On;

    public static OptionItem KnowsAllies;
    public static OptionItem AlliesKnowHypocrite;
    private static OptionItem NonImpGetsNotifyWhenLowTasks;
    private static OptionItem NotifyAtXTasksLeft;

    public override bool IsEnable => On;

    private PlayerControl HypocritePC;

    public override void SetupCustomOption()
    {
        StartSetup(645200)
            .AutoSetupOption(ref KnowsAllies, true)
            .AutoSetupOption(ref AlliesKnowHypocrite, true)
            .AutoSetupOption(ref NonImpGetsNotifyWhenLowTasks, true)
            .AutoSetupOption(ref NotifyAtXTasksLeft, 3, new IntegerValueRule(1, 20, 1), overrideParent: NonImpGetsNotifyWhenLowTasks)
            .CreateOverrideTasksData();
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        HypocritePC = playerId.GetPlayer();
    }

    public override void OnTaskComplete(PlayerControl pc, int completedTaskCount, int totalTaskCount)
    {
        if (!pc.IsAlive()) return;

        if (completedTaskCount + 1 >= totalTaskCount)
        {
            Logger.Info($"Hypocrite ({pc.GetNameWithRole()}) finished all tasks", "Hypocrite");
            pc.RPCPlayCustomSound("Congrats");
            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Impostor);
        }
        else if (NonImpGetsNotifyWhenLowTasks.GetBool() && completedTaskCount + 1 >= totalTaskCount - NotifyAtXTasksLeft.GetInt())
        {
            Logger.Info($"Hypocrite ({pc.GetNameWithRole()}) has {NotifyAtXTasksLeft.GetInt()} tasks left", "Hypocrite");
            
            LateTask.New(() =>
            {
                foreach (PlayerControl player in Main.AllAlivePlayerControls)
                    if (!player.Is(Team.Impostor))
                        Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player);
            }, 0.1f, log: false);
        }
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != target.PlayerId || hud || seer.Is(Team.Impostor) || meeting || HypocritePC == null || !HypocritePC.IsAlive() || !seer.IsAlive()) return string.Empty;

        TaskState ts = HypocritePC.GetTaskState();
        if (!NonImpGetsNotifyWhenLowTasks.GetBool() || ts.RemainingTasksCount > NotifyAtXTasksLeft.GetInt()) return string.Empty;

        string hypocriteName = HypocritePC.PlayerId.ColoredPlayerName();
        string notifyString = "\n" + Translator.GetString("HypocriteHasXTasksLeft");
        return string.Format(notifyString, hypocriteName, ts.RemainingTasksCount);
    }
}