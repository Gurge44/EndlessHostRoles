﻿using EHR.Modules;
using Hazel;
using System.Collections.Generic;
using System.Linq;

namespace EHR.Roles.Crewmate
{
    internal class Rabbit : RoleBase
    {
        class RabbitState(PlayerControl player)
        {
            private PlayerControl Player => player;
            private TaskState MyTaskState => Player.GetTaskState();

            private bool HasArrow;

            public void OnTaskComplete()
            {
                if (!Player.IsAlive() || (MyTaskState.CompletedTasksCount < TaskTrigger && !MyTaskState.IsTaskFinished)) return;

                var Impostors = Main.AllAlivePlayerControls.Where(pc => pc.Is(CustomRoleTypes.Impostor)).ToArray();
                var target = Impostors[IRandom.Instance.Next(Impostors.Length)];

                var pos = target.Pos();
                LocateArrow.Add(Player.PlayerId, pos);
                HasArrow = true;
                SendRPC();
                Utils.NotifyRoles(SpecifySeer: Player, SpecifyTarget: Player);
                Logger.Info($"{Player.GetNameWithRole()}'s target: {target.GetNameWithRole()}", "Rabbit");

                _ = new LateTask(() =>
                {
                    LocateArrow.Remove(Player.PlayerId, pos);
                    HasArrow = false;
                    SendRPC();
                    Utils.NotifyRoles(SpecifySeer: Player, SpecifyTarget: Player);
                }, 5f, "Rabbit ShowArrow Empty");
            }

            void SendRPC()
            {
                var writer = Utils.CreateCustomRoleRPC(CustomRPC.SyncRabbit);
                writer.Write(Player.PlayerId);
                writer.Write(HasArrow);
                Utils.EndRPC(writer);
            }

            public void ReceiveRPC(bool hasArrow)
            {
                HasArrow = hasArrow;
            }

            public string Suffix => !GameStates.IsInTask || !HasArrow
                ? string.Empty
                : Utils.ColorString(Utils.GetRoleColor(CustomRoles.Rabbit), LocateArrow.GetArrows(Player));
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

        public override void Init()
        {
            RabbitStates.Clear();
            TaskTrigger = OptionTaskTrigger.GetInt();
        }

        public override void Add(byte playerId) => RabbitStates[playerId] = new(Utils.GetPlayerById(playerId));
        public override bool IsEnable => RabbitStates.Count > 0;

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

        public static string GetSuffix(PlayerControl pc, bool HUD = false)
        {
            if (pc == null || !RabbitStates.TryGetValue(pc.PlayerId, out RabbitState state)) return string.Empty;
            string suffix = state.Suffix;
            return HUD ? $"<size=200%>{suffix}</size>" : suffix;
        }
    }
}