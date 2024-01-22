using Hazel;
using System.Collections.Generic;
using System.Linq;

namespace TOHE.Roles.Crewmate
{
    internal class Rabbit
    {
        class RabbitState(PlayerControl player)
        {
            public PlayerControl Player { get => player; set => player = value; }
            private TaskState MyTaskState => Player.GetTaskState();

            private (bool HasArrow, byte Target) Arrow = (false, byte.MaxValue);

            public void OnTaskComplete()
            {
                if (!Player.IsAlive() || (MyTaskState.CompletedTasksCount < TaskTrigger && !MyTaskState.IsTaskFinished)) return;

                var Impostors = Main.AllAlivePlayerControls.Where(pc => pc.Is(CustomRoleTypes.Impostor)).ToArray();
                var target = Impostors[IRandom.Instance.Next(Impostors.Length)];

                TargetArrow.Add(Player.PlayerId, target.PlayerId, update: false);
                Arrow = (true, target.PlayerId);
                SendRPC();
                Utils.NotifyRoles(SpecifySeer: Player, SpecifyTarget: Player);
                Logger.Info($"{Player.GetNameWithRole()}'s target: {target.GetNameWithRole()} ({TargetArrow.GetArrows(Player, Arrow.Target)})", "Rabbit");

                _ = new LateTask(() =>
                {
                    TargetArrow.Remove(Player.PlayerId, target.PlayerId);
                    Arrow = (false, byte.MaxValue);
                    SendRPC();
                    Utils.NotifyRoles(SpecifySeer: Player, SpecifyTarget: Player);
                }, 5f, "Rabbit ShowArrow Empty");
            }

            public void SendRPC()
            {
                var writer = Utils.CreateCustomRoleRPC(CustomRPC.SyncRabbit);
                writer.Write(Player.PlayerId);
                writer.Write(Arrow.HasArrow);
                writer.Write(Arrow.Target);
                Utils.EndRPC(writer);
            }

            public void ReceiveRPC(bool hasArrow, byte target)
            {
                Arrow.HasArrow = hasArrow;
                Arrow.Target = target;
            }

            public string Suffix => !GameStates.IsInTask || !Arrow.HasArrow
                    ? string.Empty
                    : Utils.ColorString(Utils.GetRoleColor(CustomRoles.Rabbit), TargetArrow.GetArrows(Player, Arrow.Target));
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
        public static void ReceiveRPC(MessageReader reader)
        {
            byte id = reader.ReadByte();
            bool hasArrow = reader.ReadBoolean();
            byte target = reader.ReadByte();
            RabbitStates[id].ReceiveRPC(hasArrow, target);
        }
        public static void OnTaskComplete(PlayerControl pc)
        {
            if (pc == null || !RabbitStates.TryGetValue(pc.PlayerId, out RabbitState state)) return;
            state.OnTaskComplete();
        }
        public static string GetSuffix(PlayerControl pc)
        {
            if (pc == null || !RabbitStates.TryGetValue(pc.PlayerId, out RabbitState state)) return string.Empty;
            return state.Suffix;
        }
    }
}
