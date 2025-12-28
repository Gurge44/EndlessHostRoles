using AmongUs.GameOptions;
using EHR.Modules;
using static EHR.Options;

namespace EHR.Crewmate;

internal class Speedrunner : RoleBase
{
    public static bool On;

    public static OptionItem SpeedrunnerNotifyKillers;
    public static OptionItem SpeedrunnerNotifyAtXTasksLeft;
    public static OptionItem SpeedrunnerSpeed;

    private static PlayerControl SpeedrunnerPC;
    public override bool IsEnable => On;

    public override void Add(byte playerId)
    {
        On = true;
        SpeedrunnerPC = playerId.GetPlayer();
    }

    public override void Init()
    {
        On = false;
        SpeedrunnerPC = null;
    }

    public override void SetupCustomOption()
    {
        SetupSingleRoleOptions(652500, TabGroup.CrewmateRoles, CustomRoles.Speedrunner);

        SpeedrunnerNotifyKillers = new BooleanOptionItem(652510, "SpeedrunnerNotifyKillers", true, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Speedrunner]);

        SpeedrunnerNotifyAtXTasksLeft = new IntegerOptionItem(652511, "SpeedrunnerNotifyAtXTasksLeft", new(0, 90, 1), 3, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Speedrunner]);

        SpeedrunnerSpeed = new FloatOptionItem(652512, "SpeedrunnerSpeed", new(0.1f, 3f, 0.05f), 1.5f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Speedrunner])
            .SetValueFormat(OptionFormat.Multiplier);

        OverrideTasksData.Create(652513, TabGroup.CrewmateRoles, CustomRoles.Speedrunner);
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        Main.AllPlayerSpeed[playerId] = SpeedrunnerSpeed.GetFloat();
    }

    public override void OnTaskComplete(PlayerControl player, int CompletedTasksCount, int AllTasksCount)
    {
        if (!player.IsAlive()) return;

        int completedTasks = CompletedTasksCount + 1;

        if (completedTasks >= AllTasksCount)
        {
            Logger.Info("Speedrunner finished tasks", "Speedrunner");
            player.RPCPlayCustomSound("Congrats");
            GameData.Instance.CompletedTasks = GameData.Instance.TotalTasks;
            Logger.Info($"TotalTaskCounts = {GameData.Instance.CompletedTasks}/{GameData.Instance.TotalTasks}", "TaskState.Update");

            LateTask.New(() =>
            {
                if (!GameStates.IsEnded) CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Crewmate);
            }, 1f, log: false);
        }
        else if (completedTasks >= SpeedrunnerNotifyAtXTasksLeft.GetInt() && SpeedrunnerNotifyKillers.GetBool())
        {
            LateTask.New(() =>
            {
                foreach (PlayerControl pc in Main.AllAlivePlayerControls)
                {
                    if (!pc.Is(Team.Crewmate))
                        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
                }
            }, 0.1f, log: false);
        }
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != target.PlayerId || hud || seer.Is(Team.Crewmate) || meeting || SpeedrunnerPC == null || !SpeedrunnerPC.IsAlive() || !seer.IsAlive()) return string.Empty;

        TaskState ts = SpeedrunnerPC.GetTaskState();
        if (ts.CompletedTasksCount < SpeedrunnerNotifyAtXTasksLeft.GetInt() || !SpeedrunnerNotifyKillers.GetBool()) return string.Empty;

        string speedrunnerName = SpeedrunnerPC.PlayerId.ColoredPlayerName();
        string notifyString = "\n" + Translator.GetString("SpeedrunnerHasXTasksLeft");
        return string.Format(notifyString, speedrunnerName, ts.RemainingTasksCount);
    }

    public override void ManipulateGameEndCheckCrew(PlayerState playerState, out bool keepGameGoing, out int countsAs)
    {
        if (playerState.IsDead)
        {
            base.ManipulateGameEndCheckCrew(playerState, out keepGameGoing, out countsAs);
            return;
        }

        keepGameGoing = true;
        countsAs = 1;
    }
}
