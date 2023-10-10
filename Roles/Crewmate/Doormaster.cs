namespace TOHE.Roles.Crewmate
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using TOHE.Modules;
    using UnityEngine;
    using static TOHE.Options;
    using static TOHE.Utils;

    public static class Doormaster
    {
        private static readonly int Id = 640000;
        private static List<byte> playerIdList = new();
        public static Dictionary<byte, float> UseLimit = new();

        public static OptionItem VentCooldown;
        public static OptionItem UseLimitOpt;
        public static OptionItem DoormasterAbilityUseGainWithEachTaskCompleted;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Doormaster);
            VentCooldown = FloatOptionItem.Create(Id + 10, "VentCooldown", new(0f, 70f, 1f), 15f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Doormaster])
                .SetValueFormat(OptionFormat.Seconds);
            UseLimitOpt = IntegerOptionItem.Create(Id + 11, "AbilityUseLimit", new(1, 20, 1), 1, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Doormaster])
                .SetValueFormat(OptionFormat.Times);
            DoormasterAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(Id + 12, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.1f), 1f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Doormaster])
                .SetValueFormat(OptionFormat.Times);
        }
        public static void Init()
        {
            playerIdList = new();
            UseLimit = new();
        }
        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            UseLimit.Add(playerId, UseLimitOpt.GetInt());
        }
        public static bool IsEnable => playerIdList.Any();
        public static void OnEnterVent(PlayerControl pc)
        {
            if (pc == null) return;
            if (!pc.Is(CustomRoles.Doormaster)) return;

            if (UseLimit[pc.PlayerId] >= 1)
            {
                UseLimit[pc.PlayerId] -= 1;
                DoorsReset.OpenAllDoors();
                Main.DoormasterCD.TryAdd(pc.PlayerId, GetTimeStamp());
            }
            else
            {
                pc.Notify(Translator.GetString("OutOfAbilityUsesDoMoreTasks"));
            }
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
            return !UsePets.GetBool() || !Main.DoormasterCD.TryGetValue(pc.PlayerId, out var cd)
                ? string.Empty
                : string.Format(Translator.GetString("CDPT"), VentCooldown.GetInt() - (GetTimeStamp() - cd));
        }
    }
}