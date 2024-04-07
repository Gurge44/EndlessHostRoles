using System;
using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using Hazel;
using static EHR.Translator;

namespace EHR.Roles.Crewmate
{
    internal class Benefactor : RoleBase
    {
        private const int Id = 8670;
        private static List<byte> playerIdList = [];

        public static Dictionary<byte, List<int>> taskIndex = [];
        public static Dictionary<byte, int> TaskMarkPerRound = [];
        public static Dictionary<byte, long> shieldedPlayers = [];
        private static int maxTasksMarkedPerRound;

        public static OptionItem TaskMarkPerRoundOpt;
        public static OptionItem ShieldDuration;

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Benefactor);
            TaskMarkPerRoundOpt = IntegerOptionItem.Create(Id + 10, "TaskMarkPerRound", new(1, 14, 1), 3, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Benefactor])
                .SetValueFormat(OptionFormat.Votes);
            ShieldDuration = IntegerOptionItem.Create(Id + 11, "AidDur", new(1, 30, 1), 10, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Benefactor])
                .SetValueFormat(OptionFormat.Seconds);
            Options.OverrideTasksData.Create(Id + 12, TabGroup.CrewmateRoles, CustomRoles.Benefactor);
        }

        public override void Init()
        {
            playerIdList = [];
            taskIndex = [];
            TaskMarkPerRound = [];
            shieldedPlayers = [];
            maxTasksMarkedPerRound = TaskMarkPerRoundOpt.GetInt();
        }

        public override void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            TaskMarkPerRound[playerId] = 0;
        }

        public override bool IsEnable => playerIdList.Count > 0;

        private static void SendRPC(byte benefactorId, int task_Index = -1, bool isShield = false, bool clearAll = false, bool shieldExpire = false, byte shieldedId = byte.MaxValue)
        {
            if (!Utils.DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncBenefactorMarkedTask, SendOption.Reliable);
            writer.Write(benefactorId);
            writer.Write(task_Index);
            writer.Write(isShield);
            writer.Write(clearAll);
            writer.Write(shieldExpire);
            writer.Write(shieldedId);
            if (!isShield) writer.Write(TaskMarkPerRound[benefactorId]);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public static void ReceiveRPC(MessageReader reader)
        {
            byte benefactorID = reader.ReadByte();
            if (Main.PlayerStates[benefactorID].Role is not Benefactor { IsEnable: true }) return;

            int taskInd = reader.ReadInt32();
            bool IsShield = reader.ReadBoolean();
            bool clearAll = reader.ReadBoolean();
            bool shieldExpire = reader.ReadBoolean();
            byte shieldedId = reader.ReadByte();
            if (!IsShield)
            {
                int uses = reader.ReadInt32();
                TaskMarkPerRound[benefactorID] = uses;
                if (!clearAll && !shieldExpire)
                {
                    if (!taskIndex.ContainsKey(benefactorID)) taskIndex[benefactorID] = [];
                    taskIndex[benefactorID].Add(taskInd);
                }
            }
            else
            {
                if (taskIndex.ContainsKey(benefactorID)) taskIndex[benefactorID].Remove(taskInd);
            }

            if (clearAll && taskIndex.ContainsKey(benefactorID)) taskIndex[benefactorID].Clear();
            if (IsShield)
            {
                shieldedPlayers.TryAdd(shieldedId, Utils.TimeStamp);
            }

            if (shieldExpire)
            {
                shieldedPlayers.Remove(shieldedId);
            }

            if (clearAll && shieldedPlayers.Count > 0) shieldedPlayers.Clear();
        }

        public override string GetProgressText(byte playerId, bool comms)
        {
            if (!IsEnable) return string.Empty;
            TaskMarkPerRound.TryAdd(playerId, 0);
            int markedTasks = TaskMarkPerRound[playerId];
            int x = Math.Max(maxTasksMarkedPerRound - markedTasks, 0);
            return Utils.ColorString(Utils.GetRoleColor(CustomRoles.Benefactor).ShadeColor(0.25f), $"({x})");
        }

        public override void AfterMeetingTasks()
        {
            if (!IsEnable) return;
            shieldedPlayers.Clear();
            foreach (var playerId in TaskMarkPerRound.Keys)
            {
                TaskMarkPerRound[playerId] = 0;
                if (taskIndex.ContainsKey(playerId)) taskIndex[playerId].Clear();
                SendRPC(playerId, clearAll: true);
            }
        }

        public override void OnFixedUpdate(PlayerControl pc)
        {
            if (!IsEnable) return;

            foreach (var x in shieldedPlayers.Where(x => x.Value + ShieldDuration.GetInt() < Utils.TimeStamp))
            {
                shieldedPlayers.Remove(x.Key);
                SendRPC(pc.PlayerId, shieldExpire: true, shieldedId: Utils.GetPlayerById(x.Key).PlayerId);
            }
        }

        public static void OnTaskComplete(PlayerControl player, PlayerTask task) // Special case for Benefactor
        {
            if (player == null) return;
            byte playerId = player.PlayerId;
            if (player.Is(CustomRoles.Benefactor))
            {
                TaskMarkPerRound.TryAdd(playerId, 0);
                if (TaskMarkPerRound[playerId] >= maxTasksMarkedPerRound)
                {
                    TaskMarkPerRound[playerId] = maxTasksMarkedPerRound;
                    Logger.Info($"Max task per round ({TaskMarkPerRound[playerId]}) reached for {player.GetNameWithRole()}", "Benefactor");
                    return;
                }

                TaskMarkPerRound[playerId]++;
                if (!taskIndex.ContainsKey(playerId)) taskIndex[playerId] = [];
                taskIndex[playerId].Add(task.Index);
                SendRPC(benefactorId: playerId, task_Index: task.Index);
                player.Notify(GetString("BenefactorTaskMarked"));
            }
            else
            {
                foreach (var benefactorId in taskIndex.Keys)
                {
                    if (taskIndex[benefactorId].Contains(task.Index))
                    {
                        var benefactorPC = Utils.GetPlayerById(benefactorId);
                        if (benefactorPC == null) continue;

                        player.Notify(GetString("BenefactorTargetGotShieldNotify"));
                        taskIndex[benefactorId].Remove(task.Index);
                        SendRPC(benefactorId: benefactorId, task_Index: task.Index, isShield: true, shieldedId: player.PlayerId);
                        Logger.Info($"{player.GetAllRoleName()} got a shield because the task was marked by {benefactorPC.GetNameWithRole()}", "Benefactor");
                    }
                }
            }
        }
    }
}