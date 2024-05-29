using AmongUs.GameOptions;
using EHR.Modules;
using static EHR.Options;

namespace EHR.Roles.Crewmate
{
    internal class Speedrunner : RoleBase
    {
        public static bool On;

        public static OptionItem SpeedrunnerNotifyKillers;
        public static OptionItem SpeedrunnerNotifyAtXTasksLeft;
        public static OptionItem SpeedrunnerSpeed;
        public override bool IsEnable => On;

        public override void Add(byte playerId)
        {
            On = true;
        }

        public override void Init()
        {
            On = false;
        }

        public static void SetupCustomOption()
        {
            SetupRoleOptions(9170, TabGroup.CrewmateRoles, CustomRoles.Speedrunner);
            SpeedrunnerNotifyKillers = BooleanOptionItem.Create(9178, "SpeedrunnerNotifyKillers", true, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Speedrunner]);
            SpeedrunnerNotifyAtXTasksLeft = IntegerOptionItem.Create(9179, "SpeedrunnerNotifyAtXTasksLeft", new(0, 90, 1), 3, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Speedrunner]);
            SpeedrunnerSpeed = FloatOptionItem.Create(9177, "SpeedrunnerSpeed", new(0.1f, 3f, 0.1f), 1.5f, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Speedrunner])
                .SetValueFormat(OptionFormat.Multiplier);
            OverrideTasksData.Create(9180, TabGroup.CrewmateRoles, CustomRoles.Speedrunner);
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            Main.AllPlayerSpeed[playerId] = SpeedrunnerSpeed.GetFloat();
        }

        public override void OnTaskComplete(PlayerControl player, int CompletedTasksCount, int AllTasksCount)
        {
            if (!player.IsAlive()) return;

            var completedTasks = CompletedTasksCount + 1;
            int remainingTasks = AllTasksCount - completedTasks;
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
                string speedrunnerName = player.GetRealName().RemoveHtmlTags();
                string notifyString = Translator.GetString("SpeedrunnerHasXTasksLeft");
                foreach (var pc in Main.AllAlivePlayerControls)
                {
                    if (!pc.Is(Team.Crewmate))
                    {
                        pc.Notify(string.Format(notifyString, speedrunnerName, remainingTasks));
                    }
                }
            }
        }
    }
}