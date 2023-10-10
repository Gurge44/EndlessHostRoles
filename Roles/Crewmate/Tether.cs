namespace TOHE.Roles.Crewmate
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using UnityEngine;
    using static TOHE.Options;
    using static TOHE.Utils;

    public static class Tether
    {
        private static readonly int Id = 640300;
        private static List<byte> playerIdList = new();
        public static Dictionary<byte, float> UseLimit = new();
        private static byte Target = byte.MaxValue;

        public static OptionItem VentCooldown;
        public static OptionItem UseLimitOpt;
        public static OptionItem TetherAbilityUseGainWithEachTaskCompleted;

        public static void SetupCustomOption()
        {
            SetupSingleRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Tether, 1);
            VentCooldown = FloatOptionItem.Create(Id + 10, "VentCooldown", new(0f, 70f, 1f), 15f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Tether])
                .SetValueFormat(OptionFormat.Seconds);
            UseLimitOpt = IntegerOptionItem.Create(Id + 11, "AbilityUseLimit", new(1, 20, 1), 1, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Tether])
            .SetValueFormat(OptionFormat.Times);
            TetherAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(Id + 12, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.1f), 0.4f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Tether])
            .SetValueFormat(OptionFormat.Times);
        }
        public static void Init()
        {
            playerIdList = new();
            UseLimit = new();
            Target = byte.MaxValue;
        }
        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            UseLimit.Add(playerId, UseLimitOpt.GetInt());
        }
        public static bool IsEnable => playerIdList.Any();
        public static void OnEnterVent(PlayerControl pc, int ventId, bool isPet = false)
        {
            if (pc == null) return;
            if (!pc.Is(CustomRoles.Tether)) return;

            if (UseLimit[pc.PlayerId] >= 1)
            {
                if (Target != byte.MaxValue)
                {
                    Main.TetherCD.TryAdd(pc.PlayerId, GetTimeStamp());
                    _ = new LateTask(() =>
                    {
                        if (GameStates.IsInTask)
                        {
                            UseLimit[pc.PlayerId] -= 1;
                            TP(pc.NetTransform, GetPlayerById(Target).GetTruePosition());
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
            else
            {
                _ = new LateTask(() =>
                {
                    if (!isPet) pc.MyPhysics?.RpcBootFromVent(ventId);
                    pc.Notify(Translator.GetString("OutOfAbilityUsesDoMoreTasks"));
                }, isPet ? 0.1f : 0.5f, "Tether No Uses Left Boot From Vent");
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
            }
        }
        public static void OnReportDeadBody()
        {
            Target = byte.MaxValue;
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

            if (Target != byte.MaxValue) sb.Append($" <color=#777777>-</color> Target: {GetPlayerById(Target).GetRealName()}");

            return sb.ToString();
        }
        public static string GetHudText(PlayerControl pc)
        {
            return !UsePets.GetBool() || !Main.TetherCD.TryGetValue(pc.PlayerId, out var cd)
                ? string.Empty
                : string.Format(Translator.GetString("CDPT"), VentCooldown.GetInt() - (GetTimeStamp() - cd) + 1);
        }
    }
}