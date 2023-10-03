namespace TOHE.Roles.Crewmate
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using TOHE.Modules;
    using UnityEngine;
    using static TOHE.Options;
    using static TOHE.Translator;
    using static TOHE.Utils;

    public static class NiceHacker
    {
        private static readonly int Id = 641000;
        private static Dictionary<byte, bool> playerIdList = new();
        public static Dictionary<byte, float> UseLimit = new();
        private static Dictionary<byte, long> abilityInUse = new();

        public static OptionItem AbilityCD;
        public static OptionItem UseLimitOpt;
        public static OptionItem NiceHackerAbilityUseGainWithEachTaskCompleted;
        public static OptionItem AbilityChargesWhenFinishedTasks;
        public static OptionItem ModdedClientAbilityUseSecondsMultiplier;
        public static OptionItem ModdedClientCanMoveWhileViewingMap;
        public static OptionItem VanillaClientSeesInfoFor;

        public static void SetupCustomOption()
        {
            SetupSingleRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Hacker, 1);
            AbilityCD = FloatOptionItem.Create(Id + 10, "AbilityCD", new(0f, 70f, 1f), 15f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Hacker])
                .SetValueFormat(OptionFormat.Seconds);
            UseLimitOpt = IntegerOptionItem.Create(Id + 11, "AbilityUseLimit", new(1, 20, 1), 2, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Hacker])
                .SetValueFormat(OptionFormat.Times);
            NiceHackerAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(Id + 12, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.1f), 1f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Hacker])
                .SetValueFormat(OptionFormat.Times);
            AbilityChargesWhenFinishedTasks = FloatOptionItem.Create(Id + 13, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.1f), 0.2f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Hacker])
                .SetValueFormat(OptionFormat.Times);
            ModdedClientAbilityUseSecondsMultiplier = FloatOptionItem.Create(Id + 14, "HackerModdedClientAbilityUseSecondsMultiplier", new(0f, 70f, 1f), 3f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Hacker])
                .SetValueFormat(OptionFormat.Seconds);
            ModdedClientCanMoveWhileViewingMap = BooleanOptionItem.Create(Id + 15, "HackerModdedClientCanMoveWhileViewingMap", false, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.NiceHacker]);
            VanillaClientSeesInfoFor = FloatOptionItem.Create(Id + 16, "HackerVanillaClientSeesInfoFor", new(0f, 70f, 1f), 4f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Hacker])
                .SetValueFormat(OptionFormat.Seconds);
        }
        public static void Init()
        {
            playerIdList = new();
            UseLimit = new();
            abilityInUse = new();
        }
        public static void Add(byte playerId)
        {
            playerIdList.TryAdd(playerId, GetPlayerById(playerId).IsModClient());
            UseLimit.Add(playerId, UseLimitOpt.GetInt());
        }
        public static bool IsEnable => playerIdList.Any();
        public static void OnEnterVent(PlayerControl pc)
        {
            if (pc == null) return;
            if (!pc.Is(CustomRoles.Hacker)) return;
            if (pc.IsModClient()) return;

            if (UseLimit[pc.PlayerId] >= 1)
            {
                UseLimit[pc.PlayerId] -= 1;
                Main.HackerCD.TryAdd(pc.PlayerId, GetTimeStamp());
                var list = ExtendedPlayerControl.GetAllPlayerLocationsCount();
                var sb = new StringBuilder();
                foreach (var location in list)
                {
                    sb.Append($"\n<color=#00ffa5>{location.Key}:</color> {location.Value}");
                }
                pc.Notify(sb.ToString(), VanillaClientSeesInfoFor.GetFloat());
            }
            else
            {
                pc.Notify(GetString("OutOfAbilityUsesDoMoreTasks"));
            }
        }
        public static void OnFixedUpdate(PlayerControl pc)
        {
            if (pc == null) return;
            if (!pc.Is(CustomRoles.NiceHacker)) return;

            if (pc.GetPlayerTaskState().IsTaskFinished)
            {

            }
        }
        public static bool MapHandle(PlayerControl pc, MapBehaviour map, MapOptions opts)
        {
            if (pc == null) return true;
            if (map == null) return true;
            if (opts == null) return true;

            if (!pc.Is(CustomRoles.Hacker)) return true;

            if (UseLimit[pc.PlayerId] >= 1)
            {
                UseLimit[pc.PlayerId] -= 1;
                opts.Mode = MapOptions.Modes.CountOverlay;
                abilityInUse.TryAdd(pc.PlayerId, GetTimeStamp());
            }
            else
            {
                opts.Mode = MapOptions.Modes.Normal;
                pc.Notify(GetString("OutOfAbilityUsesDoMoreTasks"));
            }
        }
        public static string GetHudText(PlayerControl pc)
        {
            if (pc == null) return string.Empty;
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