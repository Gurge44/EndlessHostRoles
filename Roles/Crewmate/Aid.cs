namespace TOHE.Roles.Crewmate
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using UnityEngine;
    using static TOHE.Options;
    using static TOHE.Utils;

    public static class Aid
    {
        private static readonly int Id = 640200;
        private static List<byte> playerIdList = new();
        public static Dictionary<byte, float> UseLimit = new();
        public static Dictionary<byte, long> ShieldedPlayers = new();

        public static OptionItem AidDur;
        public static OptionItem AidCD;
        public static OptionItem UseLimitOpt;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Aid);
            AidCD = FloatOptionItem.Create(Id + 10, "AidCD", new(0f, 60f, 1f), 15f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Aid])
                .SetValueFormat(OptionFormat.Seconds);
            AidDur = FloatOptionItem.Create(Id + 11, "AidDur", new(0f, 60f, 1f), 10f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Aid])
                .SetValueFormat(OptionFormat.Seconds);
            UseLimitOpt = IntegerOptionItem.Create(Id + 12, "AbilityUseLimit", new(1, 20, 1), 5, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Aid])
            .SetValueFormat(OptionFormat.Times);
        }
        public static void Init()
        {
            playerIdList = new();
            UseLimit = new();
            ShieldedPlayers = new();
        }
        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            UseLimit.Add(playerId, UseLimitOpt.GetInt());

            if (!AmongUsClient.Instance.AmHost) return;
            if (!Main.ResetCamPlayerList.Contains(playerId))
                Main.ResetCamPlayerList.Add(playerId);
        }
        public static void SetKillCooldown(byte playerId) => Main.AllPlayerKillCooldown[playerId] = AidCD.GetInt();
        public static bool IsEnable => playerIdList.Any();
        public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (killer == null) return false;
            if (target == null) return false;
            if (!killer.Is(CustomRoles.Aid)) return true;

            if (UseLimit[killer.PlayerId] >= 1)
            {
                UseLimit[killer.PlayerId] -= 1;
                ShieldedPlayers.TryAdd(target.PlayerId, GetTimeStamp());
                NotifyRoles(SpecifySeer: killer);
                return false;
            }
            else
            {
                return false;
            }
        }
        public static void OnFixedUpdate(PlayerControl pc)
        {
            if (pc == null) return;
            if (!pc.Is(CustomRoles.Aid)) return;
            if (!ShieldedPlayers.Any()) return;

            bool change = false;

            foreach (var x in ShieldedPlayers)
            {
                if (x.Value + AidDur.GetInt() < GetTimeStamp() || !GameStates.IsInTask)
                {
                    ShieldedPlayers.Remove(x.Key);
                    change = true;
                }
            }

            if (change && GameStates.IsInTask) { NotifyRoles(SpecifySeer: pc); }
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
    }
}