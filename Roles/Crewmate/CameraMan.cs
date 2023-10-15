namespace TOHE.Roles.Crewmate
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using UnityEngine;
    using static TOHE.Options;
    using static TOHE.Utils;

    public static class CameraMan
    {
        private static readonly int Id = 641600;
        private static List<byte> playerIdList = new();
        public static Dictionary<byte, float> UseLimit = new();

        public static OptionItem VentCooldown;
        public static OptionItem UseLimitOpt;
        public static OptionItem CameraManAbilityUseGainWithEachTaskCompleted;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.CameraMan);
            VentCooldown = FloatOptionItem.Create(Id + 10, "VentCooldown", new(0f, 70f, 1f), 15f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.CameraMan])
                .SetValueFormat(OptionFormat.Seconds);
            UseLimitOpt = IntegerOptionItem.Create(Id + 11, "AbilityUseLimit", new(1, 20, 1), 1, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.CameraMan])
                .SetValueFormat(OptionFormat.Times);
            CameraManAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(Id + 12, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.1f), 1f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.CameraMan])
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
            if (!pc.Is(CustomRoles.CameraMan)) return;

            if (UseLimit[pc.PlayerId] >= 1)
            {
                UseLimit[pc.PlayerId] -= 1;
                Main.CameraManCD.TryAdd(pc.PlayerId, GetTimeStamp());

                Vector2 pos;

                switch (Main.NormalOptions.MapId)
                {
                    case 0:
                        pos = new(-13.5f, -5.5f);
                        break;
                    case 1:
                        pos = new(15.3f, 3.8f);
                        break;
                    case 2:
                        pos = new(3.0f, -12.0f);
                        break;
                    case 4:
                        pos = new(5.8f, -10.8f);
                        break;
                    default:
                        TOHE.Logger.Error("Invalid MapID", "CameraMan Teleport");
                        return;
                }

                _ = new LateTask(() => { TP(pc.NetTransform, pos); }, UsePets.GetBool() ? 0.1f : 2f, "CameraMan Teleport");
            }
            else
            {
                if (!NameNotifyManager.Notice.ContainsKey(pc.PlayerId)) pc.Notify(Translator.GetString("OutOfAbilityUsesDoMoreTasks"));
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
            return !UsePets.GetBool() || !Main.CameraManCD.TryGetValue(pc.PlayerId, out var cd)
                ? string.Empty
                : string.Format(Translator.GetString("CDPT"), VentCooldown.GetInt() - (GetTimeStamp() - cd) + 1);
        }
    }
}