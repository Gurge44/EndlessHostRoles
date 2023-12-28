using Hazel;
using System;
using System.Collections.Generic;
using System.Linq;
using static TOHE.Options;
using static TOHE.Translator;
using static TOHE.Utils;

namespace TOHE.Roles.Crewmate
{
    internal class Benefactor
    {
        private static readonly int Id = 8670;
        private static List<byte> playerIdList = [];
        public static bool IsEnable;

        public static Dictionary<byte, List<int>> taskIndex = [];
        public static Dictionary<byte, int> TaskMarkPerRound = [];
        public static Dictionary<byte, long> shieldedPlayers = [];
        private static int maxTasksMarkedPerRound;

        public static OptionItem TaskMarkPerRoundOpt;
        public static OptionItem ShieldDuration;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Benefactor);
            TaskMarkPerRoundOpt = IntegerOptionItem.Create(Id + 10, "TaskMarkPerRound", new(1, 14, 1), 3, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Benefactor])
                .SetValueFormat(OptionFormat.Votes);
            ShieldDuration = IntegerOptionItem.Create(Id + 11, "AidDur", new(1, 30, 1), 10, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Benefactor])
                .SetValueFormat(OptionFormat.Seconds);
            OverrideTasksData.Create(Id + 12, TabGroup.CrewmateRoles, CustomRoles.Benefactor);
        }

        public static void Init()
        {
            playerIdList = [];
            taskIndex = [];
            TaskMarkPerRound = [];
            shieldedPlayers = [];
            IsEnable = false;
            maxTasksMarkedPerRound = TaskMarkPerRoundOpt.GetInt();
        }
        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            TaskMarkPerRound[playerId] = 0;
            IsEnable = true;
        }
        private static void SendRPC(byte benefactorID, int taskIndex = -1, bool IsShield = false, bool clearAll = false, bool shieldExpire = false, byte shieldedId = byte.MaxValue)
        {
            if (!IsEnable || !DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncBenefactorMarkedTask, SendOption.Reliable, -1);
            writer.Write(benefactorID);
            writer.Write(taskIndex);
            writer.Write(IsShield);
            writer.Write(clearAll);
            writer.Write(shieldExpire);
            writer.Write(shieldedId);
            if (!IsShield) writer.Write(TaskMarkPerRound[benefactorID]);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void ReceiveRPC(MessageReader reader)
        {
            if (!IsEnable) return;
            byte benefactorID = reader.ReadByte();
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
                shieldedPlayers.TryAdd(shieldedId, GetTimeStamp());
            }
            if (shieldExpire)
            {
                shieldedPlayers.Remove(shieldedId);
            }
            if (clearAll && shieldedPlayers.Count > 0) shieldedPlayers.Clear();
        }
        public static string GetProgressText(byte playerId)
        {
            if (!IsEnable) return string.Empty;
            if (!TaskMarkPerRound.ContainsKey(playerId)) TaskMarkPerRound[playerId] = 0;
            int markedTasks = TaskMarkPerRound[playerId];
            int x = Math.Max(maxTasksMarkedPerRound - markedTasks, 0);
            return ColorString(GetRoleColor(CustomRoles.Benefactor).ShadeColor(0.25f), $"({x})");
        }
        public static void AfterMeetingTasks()
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
        public static void OnFixedUpdate(PlayerControl pc)
        {
            if (!IsEnable) return;

            foreach (var x in shieldedPlayers.Where(x => x.Value + ShieldDuration.GetInt() < GetTimeStamp()))
            {
                shieldedPlayers.Remove(x.Key);
                SendRPC(pc.PlayerId, shieldExpire: true, shieldedId: GetPlayerById(x.Key).PlayerId);
            }
        }
        public static void OnTaskComplete(PlayerControl player, PlayerTask task)
        {
            if (!IsEnable) return;
            if (player == null) return;
            byte playerId = player.PlayerId;
            if (player.Is(CustomRoles.Benefactor))
            {
                if (!TaskMarkPerRound.ContainsKey(playerId)) TaskMarkPerRound[playerId] = 0;
                if (TaskMarkPerRound[playerId] >= maxTasksMarkedPerRound)
                {
                    TaskMarkPerRound[playerId] = maxTasksMarkedPerRound;
                    Logger.Info($"Max task per round ({TaskMarkPerRound[playerId]}) reached for {player.GetNameWithRole()}", "Benefactor");
                    return;
                }
                TaskMarkPerRound[playerId]++;
                if (!taskIndex.ContainsKey(playerId)) taskIndex[playerId] = [];
                taskIndex[playerId].Add(task.Index);
                SendRPC(benefactorID: playerId, taskIndex: task.Index);
                player.Notify(GetString("BenefactorTaskMarked"));
            }
            else
            {
                foreach (var benefactorId in taskIndex.Keys)
                {
                    if (taskIndex[benefactorId].Contains(task.Index))
                    {
                        var benefactorPC = GetPlayerById(benefactorId);
                        if (benefactorPC == null) continue;

                        player.Notify(GetString("BenefactorTargetGotShieldNotify"));
                        taskIndex[benefactorId].Remove(task.Index);
                        SendRPC(benefactorID: benefactorId, taskIndex: task.Index, IsShield: true, shieldedId: player.PlayerId);
                        Logger.Info($"{player.GetAllRoleName()} got a shield because the task was marked by {benefactorPC.GetNameWithRole()}", "Benefactor");
                    }
                }
            }
        }
    }
}
