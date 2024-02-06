namespace TOHE.Roles.Crewmate
{
    using Hazel;
    using System;
    using System.Collections.Generic;
    using System.Text;
    using UnityEngine;
    using static TOHE.Options;
    using static TOHE.Utils;

    public static class Ricochet
    {
        private static readonly int Id = 640100;
        public static List<byte> playerIdList = [];
        public static Dictionary<byte, float> UseLimit = [];
        public static byte ProtectAgainst = byte.MaxValue;

        public static OptionItem UseLimitOpt;
        public static OptionItem RicochetAbilityUseGainWithEachTaskCompleted;
        public static OptionItem AbilityChargesWhenFinishedTasks;
        public static OptionItem CancelVote;

        public static void SetupCustomOption()
        {
            SetupSingleRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Ricochet, 1);
            UseLimitOpt = IntegerOptionItem.Create(Id + 10, "AbilityUseLimit", new(0, 20, 1), 1, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Ricochet])
                .SetValueFormat(OptionFormat.Times);
            RicochetAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(Id + 11, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.1f), 0.5f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Ricochet])
                .SetValueFormat(OptionFormat.Times);
            AbilityChargesWhenFinishedTasks = FloatOptionItem.Create(Id + 13, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.1f), 0.2f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Ricochet])
                .SetValueFormat(OptionFormat.Times);
            CancelVote = CreateVoteCancellingUseSetting(Id + 12, CustomRoles.Ricochet, TabGroup.CrewmateRoles);
        }
        public static void Init()
        {
            playerIdList = [];
            UseLimit = [];
            ProtectAgainst = byte.MaxValue;
        }
        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            UseLimit.Add(playerId, UseLimitOpt.GetInt());
        }
        public static bool IsEnable => playerIdList.Count > 0;
        public static void SendRPC(byte playerId)
        {
            if (!IsEnable || !DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetRicochetLimit, SendOption.Reliable, -1);
            writer.Write(playerId);
            writer.Write(UseLimit[playerId]);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void SendRPCSyncTarget(byte targetId)
        {
            if (!IsEnable || !DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetRicochetTarget, SendOption.Reliable, -1);
            writer.Write(targetId);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void ReceiveRPC(MessageReader reader)
        {
            if (AmongUsClient.Instance.AmHost) return;

            byte playerId = reader.ReadByte();
            float uses = reader.ReadSingle();
            UseLimit[playerId] = uses;
        }
        public static void ReceiveRPCSyncTarget(MessageReader reader)
        {
            if (AmongUsClient.Instance.AmHost) return;

            ProtectAgainst = reader.ReadByte();
        }
        public static bool OnKillAttempt(PlayerControl killer, PlayerControl target)
        {
            if (killer == null) return false;
            if (target == null) return false;
            if (!target.Is(CustomRoles.Ricochet)) return true;

            if (ProtectAgainst == killer.PlayerId)
            {
                killer.SetKillCooldown(time: 5f);
                return false;
            }

            return true;
        }
        public static bool OnVote(PlayerControl pc, PlayerControl target)
        {
            if (target == null || pc == null || pc.PlayerId == target.PlayerId || !pc.Is(CustomRoles.Ricochet) || Main.DontCancelVoteList.Contains(pc.PlayerId)) return false;

            if (UseLimit[pc.PlayerId] >= 1)
            {
                UseLimit[pc.PlayerId] -= 1;
                ProtectAgainst = target.PlayerId;
                SendRPC(pc.PlayerId);
                SendRPCSyncTarget(ProtectAgainst);
                Main.DontCancelVoteList.Add(pc.PlayerId);
                return true;
            }
            return false;
        }
        public static void OnReportDeadBody()
        {
            ProtectAgainst = byte.MaxValue;
            SendRPCSyncTarget(ProtectAgainst);
        }
        public static string GetProgressText(byte playerId, bool comms)
        {
            if (GetPlayerById(playerId) == null) return string.Empty;

            var sb = new StringBuilder();

            var taskState = Main.PlayerStates?[playerId]?.TaskState;
            Color TextColor;
            var TaskCompleteColor = Color.green;
            var NonCompleteColor = Color.yellow;
            var NormalColor = taskState.IsTaskFinished ? TaskCompleteColor : NonCompleteColor;
            TextColor = comms ? Color.gray : NormalColor;
            string Completed = comms ? "?" : $"{taskState.CompletedTasksCount}";

            Color TextColor1;
            if (UseLimit[playerId] < 1) TextColor1 = Color.red;
            else TextColor1 = Color.white;

            sb.Append(ColorString(TextColor, $"<color=#777777>-</color> {Completed}/{taskState.AllTasksCount}"));
            sb.Append(ColorString(TextColor1, $" <color=#777777>-</color> {Math.Round(UseLimit[playerId], 1)}"));

            return sb.ToString();
        }
        public static string TargetText => ProtectAgainst != byte.MaxValue ? $"<color=#00ffa5>Target:</color> <color=#ffffff>{GetPlayerById(ProtectAgainst).GetRealName()}</color>" : string.Empty;
    }
}