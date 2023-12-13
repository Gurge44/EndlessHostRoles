namespace TOHE.Roles.Crewmate
{
    using Hazel;
    using System;
    using System.Collections.Generic;
    using System.Text;
    using UnityEngine;
    using static TOHE.Options;
    using static TOHE.Utils;

    public static class Tether
    {
        private static readonly int Id = 640300;
        public static List<byte> playerIdList = [];
        public static Dictionary<byte, float> UseLimit = [];
        private static byte Target = byte.MaxValue;

        public static OptionItem VentCooldown;
        public static OptionItem UseLimitOpt;
        public static OptionItem TetherAbilityUseGainWithEachTaskCompleted;

        public static void SetupCustomOption()
        {
            SetupSingleRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Tether, 1);
            VentCooldown = FloatOptionItem.Create(Id + 10, "VentCooldown", new(0f, 70f, 1f), 15f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Tether])
                .SetValueFormat(OptionFormat.Seconds);
            UseLimitOpt = IntegerOptionItem.Create(Id + 11, "AbilityUseLimit", new(0, 20, 1), 1, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Tether])
            .SetValueFormat(OptionFormat.Times);
            TetherAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(Id + 12, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.1f), 0.4f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Tether])
            .SetValueFormat(OptionFormat.Times);
        }
        public static void Init()
        {
            playerIdList = [];
            UseLimit = [];
            Target = byte.MaxValue;
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
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetTetherLimit, SendOption.Reliable, -1);
            writer.Write(playerId);
            writer.Write(UseLimit[playerId]);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void SendRPCSyncTarget(byte targetId)
        {
            if (!IsEnable || !DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetTetherTarget, SendOption.Reliable, -1);
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

            Target = reader.ReadByte();
        }
        public static void OnEnterVent(PlayerControl pc, int ventId, bool isPet = false)
        {
            if (pc == null) return;
            if (!pc.Is(CustomRoles.Tether)) return;

            if (Target != byte.MaxValue)
            {
                _ = new LateTask(() =>
                {
                    if (GameStates.IsInTask)
                    {
                        TP(pc.NetTransform, GetPlayerById(Target).Pos());
                    }
                }, isPet ? 0.1f : 2f, "Tether TP");
            }
            else if (!isPet)
            {
                _ = new LateTask(() =>
                {
                    pc.MyPhysics?.RpcBootFromVent(ventId);
                }, 0.5f, "Tether No Target Boot From Vent");
            }
        }
        public static void OnVote(PlayerControl pc, PlayerControl target)
        {
            if (pc == null) return;
            if (target == null) return;
            if (!pc.Is(CustomRoles.Tether)) return;
            if (pc.PlayerId == target.PlayerId) return;

            if (UseLimit[pc.PlayerId] >= 1)
            {
                UseLimit[pc.PlayerId] -= 1;
                Target = target.PlayerId;
                SendRPC(pc.PlayerId);
                SendRPCSyncTarget(Target);
            }
        }
        public static void OnReportDeadBody()
        {
            Target = byte.MaxValue;
            SendRPCSyncTarget(Target);
        }
        public static string GetProgressText(byte playerId, bool comms)
        {
            var sb = new StringBuilder();

            var taskState = Main.PlayerStates?[playerId].GetTaskState();
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
        public static string GetHudText(PlayerControl pc)
        {
            return !UsePets.GetBool() || !Main.PetCD.TryGetValue(pc.PlayerId, out var CD)
                ? string.Empty
                : string.Format(Translator.GetString("CDPT"), VentCooldown.GetInt() - (GetTimeStamp() - CD.START_TIMESTAMP) + 1);
        }
        public static string TargetText => Target != byte.MaxValue ? $"<color=#00ffa5>Target:</color> <color=#ffffff>{GetPlayerById(Target).GetRealName()}</color>" : string.Empty;
    }
}