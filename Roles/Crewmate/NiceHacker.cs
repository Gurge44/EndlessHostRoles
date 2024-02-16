namespace TOHE.Roles.Crewmate
{
    using Hazel;
    using System;
    using System.Collections.Generic;
    using System.Text;
    using TOHE;
    using static TOHE.Options;
    using static TOHE.Translator;
    using static TOHE.Utils;

    public static class NiceHacker
    {
        private static readonly int Id = 641000;
        public static Dictionary<byte, bool> playerIdList = [];
        public static Dictionary<byte, float> UseLimit = [];
        public static Dictionary<byte, float> UseLimitSeconds = [];

        private static Dictionary<byte, long> LastUpdate = [];

        public static OptionItem AbilityCD;
        public static OptionItem UseLimitOpt;
        public static OptionItem NiceHackerAbilityUseGainWithEachTaskCompleted;
        public static OptionItem AbilityChargesWhenFinishedTasks;
        public static OptionItem ModdedClientAbilityUseSecondsMultiplier;
        public static OptionItem ModdedClientCanMoveWhileViewingMap;
        public static OptionItem VanillaClientSeesInfoFor;

        public static void SetupCustomOption()
        {
            SetupSingleRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.NiceHacker, 1);
            AbilityCD = FloatOptionItem.Create(Id + 10, "AbilityCD", new(0f, 70f, 1f), 15f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.NiceHacker])
                .SetValueFormat(OptionFormat.Seconds);
            UseLimitOpt = IntegerOptionItem.Create(Id + 11, "AbilityUseLimit", new(1, 20, 1), 2, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.NiceHacker])
                .SetValueFormat(OptionFormat.Times);
            NiceHackerAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(Id + 12, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.1f), 1f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.NiceHacker])
                .SetValueFormat(OptionFormat.Times);
            AbilityChargesWhenFinishedTasks = FloatOptionItem.Create(Id + 13, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.1f), 0.2f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.NiceHacker])
                .SetValueFormat(OptionFormat.Times);
            ModdedClientAbilityUseSecondsMultiplier = FloatOptionItem.Create(Id + 14, "NiceHackerModdedClientAbilityUseSecondsMultiplier", new(0f, 70f, 1f), 3f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.NiceHacker])
                .SetValueFormat(OptionFormat.Seconds);
            ModdedClientCanMoveWhileViewingMap = BooleanOptionItem.Create(Id + 15, "NiceHackerModdedClientCanMoveWhileViewingMap", false, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.NiceHacker]);
            VanillaClientSeesInfoFor = FloatOptionItem.Create(Id + 16, "NiceHackerVanillaClientSeesInfoFor", new(0f, 70f, 1f), 4f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.NiceHacker])
                .SetValueFormat(OptionFormat.Seconds);
        }
        public static void Init()
        {
            playerIdList = [];
            UseLimit = [];
            UseLimitSeconds = [];
            LastUpdate = [];
        }
        public static void Add(byte playerId)
        {
            playerIdList.TryAdd(playerId, GetPlayerById(playerId).IsModClient());
            if (!GetPlayerById(playerId).IsModClient()) UseLimit.Add(playerId, UseLimitOpt.GetInt());
            else UseLimitSeconds.Add(playerId, UseLimitOpt.GetInt() * ModdedClientAbilityUseSecondsMultiplier.GetInt());
        }
        public static bool IsEnable => playerIdList.Count > 0;
        public static void SendRPC(byte playerId, float secondsLeft)
        {
            if (!IsEnable || !DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetNiceHackerLimit, SendOption.Reliable, -1);
            writer.Write(playerId);
            writer.Write(secondsLeft);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void ReceiveRPC(MessageReader reader)
        {
            if (AmongUsClient.Instance.AmHost) return;

            byte playerId = reader.ReadByte();
            float secondsLeft = reader.ReadSingle();

            UseLimitSeconds[playerId] = secondsLeft;
        }
        public static void OnEnterVent(PlayerControl pc)
        {
            if (pc == null) return;
            if (!pc.Is(CustomRoles.NiceHacker)) return;
            if (pc.IsModClient() || !UseLimit.ContainsKey(pc.PlayerId)) return;

            if (UseLimit[pc.PlayerId] >= 1)
            {
                UseLimit[pc.PlayerId] -= 1;
                var list = GetAllPlayerLocationsCount();
                var sb = new StringBuilder();
                foreach (var location in list)
                {
                    sb.Append($"\n<color=#00ffa5>{location.Key}:</color> {location.Value}");
                }
                pc.Notify(sb.ToString(), VanillaClientSeesInfoFor.GetFloat());

            }
            else
            {
                if (!NameNotifyManager.Notice.ContainsKey(pc.PlayerId))
                    pc.Notify(GetString("OutOfAbilityUsesDoMoreTasks"));
            }
        }
        public static void OnFixedUpdate(PlayerControl pc)
        {
            if (pc == null) return;
            if (!pc.Is(CustomRoles.NiceHacker)) return;
            if (GameStates.IsMeeting) return;

            if (Main.PlayerStates[pc.PlayerId].TaskState.IsTaskFinished)
            {
                LastUpdate.TryAdd(pc.PlayerId, TimeStamp);
                if (LastUpdate[pc.PlayerId] + 5 < TimeStamp)
                {
                    if (pc.IsModClient()) UseLimitSeconds[pc.PlayerId] += AbilityChargesWhenFinishedTasks.GetFloat() * ModdedClientAbilityUseSecondsMultiplier.GetInt();
                    else UseLimit[pc.PlayerId] += AbilityChargesWhenFinishedTasks.GetFloat();
                    LastUpdate[pc.PlayerId] = TimeStamp;
                }
            }
        }
        public static void MapHandle(PlayerControl pc, MapBehaviour map, MapOptions opts)
        {
            if (!pc.Is(CustomRoles.NiceHacker)) return;

            map.countOverlayAllowsMovement = ModdedClientCanMoveWhileViewingMap.GetBool();

            if (UseLimitSeconds[pc.PlayerId] >= 1)
            {
                UseLimitSeconds[pc.PlayerId] -= 1; // Remove 1s for opening the map, so they can't utilize spamming
                SendRPC(pc.PlayerId, UseLimitSeconds[pc.PlayerId]);
                opts.Mode = MapOptions.Modes.CountOverlay;
                _ = new LateTask(() => { MapCountdown(pc, map, opts, (int)UseLimitSeconds[pc.PlayerId]); }, 1f, "NiceHacker.StartCountdown");
            }
            else
            {
                opts.Mode = MapOptions.Modes.Normal;
                pc.Notify(GetString("OutOfAbilityUsesDoMoreTasks"));
            }

            return;
        }
        public static void MapCountdown(PlayerControl pc, MapBehaviour map, MapOptions opts, int seconds)
        {
            if (!map.IsOpen)
            {
                return;
            }
            if (seconds <= 0)
            {
                map.Close();
                pc.Notify(GetString("OutOfAbilityUsesDoMoreTasks"));
                opts.Mode = MapOptions.Modes.Normal;
                return;
            }
            UseLimitSeconds[pc.PlayerId] -= 1;
            SendRPC(pc.PlayerId, UseLimitSeconds[pc.PlayerId]);
            _ = new LateTask(() => { MapCountdown(pc, map, opts, seconds - 1); }, 1f, "NiceHackerAbilityCountdown");
        }
        public static string GetHudText(PlayerControl pc)
        {
            if (pc == null) return string.Empty;
            if (!pc.Is(CustomRoles.NiceHacker)) return string.Empty;

            return $"<color=#00ffa5>{GetString("NiceHackerAbilitySecondsLeft")}:</color> <b>{(int)UseLimitSeconds[pc.PlayerId]}</b>s";
        }
        public static string GetProgressText(byte playerId, bool comms)
        {
            if (GetPlayerById(playerId).IsModClient() || !UseLimit.ContainsKey(playerId)) return string.Empty;

            var sb = new StringBuilder();

            var taskState = Main.PlayerStates?[playerId].TaskState;
            UnityEngine.Color TextColor;
            var TaskCompleteColor = UnityEngine.Color.green;
            var NonCompleteColor = UnityEngine.Color.yellow;
            var NormalColor = taskState.IsTaskFinished ? TaskCompleteColor : NonCompleteColor;
            TextColor = comms ? UnityEngine.Color.gray : NormalColor;
            string Completed = comms ? "?" : $"{taskState.CompletedTasksCount}";

            UnityEngine.Color TextColor1;
            if (UseLimit[playerId] < 1) TextColor1 = UnityEngine.Color.red;
            else TextColor1 = UnityEngine.Color.white;

            sb.Append(ColorString(TextColor, $"<color=#777777>-</color> {Completed}/{taskState.AllTasksCount}"));
            sb.Append(ColorString(TextColor1, $" <color=#777777>-</color> {Math.Round(UseLimit[playerId], 1)}"));

            return sb.ToString();
        }
    }
}