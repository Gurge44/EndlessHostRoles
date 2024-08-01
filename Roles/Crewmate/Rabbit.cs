using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using Hazel;

namespace EHR.Crewmate
{
    internal class Rabbit : RoleBase
    {
        private static readonly Dictionary<byte, RabbitState> RabbitStates = [];
        private static OptionItem OptionTaskTrigger;
        private static int TaskTrigger;

        private static int Id => 643330;
        public override bool IsEnable => RabbitStates.Count > 0;

        public override void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Rabbit);
            OptionTaskTrigger = new IntegerOptionItem(Id + 2, "RabbitMinTasks", new(0, 90, 1), 3, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Rabbit]);
            Options.OverrideTasksData.Create(Id + 3, TabGroup.CrewmateRoles, CustomRoles.Rabbit);
        }

        public override void Init()
        {
            RabbitStates.Clear();
            TaskTrigger = OptionTaskTrigger.GetInt();
        }

        public override void Add(byte playerId) => RabbitStates[playerId] = new(Utils.GetPlayerById(playerId));

        public static void ReceiveRPC(MessageReader reader)
        {
            byte id = reader.ReadByte();
            bool hasArrow = reader.ReadBoolean();
            RabbitStates[id].ReceiveRPC(hasArrow);
        }

        public override void OnTaskComplete(PlayerControl pc, int completedTaskCount, int totalTaskCount)
        {
            if (pc == null || !RabbitStates.TryGetValue(pc.PlayerId, out RabbitState state)) return;
            state.OnTaskComplete();
        }

        public override string GetSuffix(PlayerControl pc, PlayerControl tar, bool HUD = false, bool m = false)
        {
            if (pc == null || pc.PlayerId != tar.PlayerId || !RabbitStates.TryGetValue(pc.PlayerId, out RabbitState state)) return string.Empty;
            string suffix = state.Suffix;
            return HUD ? $"<size=200%>{suffix}</size>" : suffix;
        }

        class RabbitState(PlayerControl player)
        {
            private bool HasArrow;
            private PlayerControl Player => player;
            private TaskState MyTaskState => Player.GetTaskState();

            public string Suffix => !GameStates.IsInTask || !HasArrow
                ? string.Empty
                : Utils.ColorString(Utils.GetRoleColor(CustomRoles.Rabbit), LocateArrow.GetArrows(Player));

            public void OnTaskComplete()
            {
                if (!Player.IsAlive() || (MyTaskState.CompletedTasksCount < TaskTrigger && !MyTaskState.IsTaskFinished)) return;

                var impostors = Main.AllAlivePlayerControls.Where(pc => pc.Is(CustomRoleTypes.Impostor)).ToArray();
                var target = impostors.RandomElement();
                if (target == null) return;

                var pos = target.Pos();
                LocateArrow.Add(Player.PlayerId, pos);
                HasArrow = true;
                SendRPC();
                Utils.NotifyRoles(SpecifySeer: Player, SpecifyTarget: Player);
                Logger.Info($"{Player.GetNameWithRole()}'s target: {target.GetNameWithRole()}", "Rabbit");

                LateTask.New(() =>
                {
                    LocateArrow.Remove(Player.PlayerId, pos);
                    HasArrow = false;
                    SendRPC();
                    Utils.NotifyRoles(SpecifySeer: Player, SpecifyTarget: Player);
                }, 5f, "Rabbit ShowArrow Empty");
            }

            void SendRPC()
            {
                var writer = Utils.CreateRPC(CustomRPC.SyncRabbit);
                writer.Write(Player.PlayerId);
                writer.Write(HasArrow);
                Utils.EndRPC(writer);
            }

            public void ReceiveRPC(bool hasArrow)
            {
                HasArrow = hasArrow;
            }
        }
    }
}