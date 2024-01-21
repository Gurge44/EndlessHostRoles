using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TOHE.Roles.Crewmate
{
    internal class Rabbit
    {
        class RabbitState(PlayerControl player)
        {
            public PlayerControl Player { get => player; set => player = value; }
            private TaskState MyTaskState => Player.GetPlayerTaskState();

            public string ShowArrow = string.Empty;

            public void OnTaskComplete()
            {
                if (!Player.IsAlive() || (MyTaskState.CompletedTasksCount < TaskTrigger && !MyTaskState.IsTaskFinished)) return;

                var Impostors = Main.AllAlivePlayerControls.Where(pc => pc.Is(CustomRoleTypes.Impostor)).ToArray();
                var target = Impostors[IRandom.Instance.Next(Impostors.Length)];

                var dir = target.transform.position - Player.transform.position;
                int index;
                if (dir.magnitude < 2)
                {
                    index = 8;
                }
                else
                {
                    var angle = Vector3.SignedAngle(Vector3.down, dir, Vector3.back) + 180 + 22.5;
                    index = ((int)(angle / 45)) % 8;
                }
                ShowArrow = TargetArrow.Arrows[index];
                Logger.Info($"{Player.GetNameWithRole()}'s target: {target.GetNameWithRole()}[{TargetArrow.Arrows[index]}]", "Rabbit");

                _ = new LateTask(() =>
                {
                    ShowArrow = string.Empty;
                    Utils.NotifyRoles(SpecifySeer: Player, SpecifyTarget: Player);
                }, 5f, "Rabbit ShowArrow Empty");
            }

            public string Suffix => !GameStates.IsInTask || ShowArrow.Length == 0
                    ? string.Empty
                    : Utils.ColorString(Utils.GetRoleColor(CustomRoles.Rabbit), ShowArrow);
        }
        private static int Id => 643330;
        private static readonly Dictionary<byte, RabbitState> RabbitStates = [];
        private static OptionItem OptionTaskTrigger;
        private static int TaskTrigger;
        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Rabbit);
            OptionTaskTrigger = IntegerOptionItem.Create(Id + 2, "RabbitMinTasks", new(0, 90, 1), 3, TabGroup.CrewmateRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Rabbit]);
            Options.OverrideTasksData.Create(Id + 3, TabGroup.CrewmateRoles, CustomRoles.Rabbit);
        }
        public static void Init()
        {
            RabbitStates.Clear();
            TaskTrigger = OptionTaskTrigger.GetInt();
        }
        public static void Add(byte playerId) => RabbitStates[playerId] = new(Utils.GetPlayerById(playerId));
        public static void OnTaskComplete(PlayerControl pc)
        {
            if (pc == null) return;
            RabbitStates[pc.PlayerId].OnTaskComplete();
        }
        public static string GetSuffix(PlayerControl pc)
        {
            if (pc == null) return string.Empty;
            return RabbitStates[pc.PlayerId].Suffix;
        }
    }
}
