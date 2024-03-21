using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace EHR.Roles.Crewmate
{
    using static Options;
    using static Translator;
    using static Utils;

    public class NiceHacker : RoleBase
    {
        private const int Id = 641000;
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
            SetupSingleRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.NiceHacker);
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

        public override void Init()
        {
            playerIdList = [];
            UseLimit = [];
            UseLimitSeconds = [];
            LastUpdate = [];
        }

        public override void Add(byte playerId)
        {
            playerIdList.TryAdd(playerId, GetPlayerById(playerId).IsModClient());
            if (!GetPlayerById(playerId).IsModClient()) UseLimit.Add(playerId, UseLimitOpt.GetInt());
            else UseLimitSeconds.Add(playerId, UseLimitOpt.GetInt() * ModdedClientAbilityUseSecondsMultiplier.GetInt());
        }

        public override bool IsEnable => playerIdList.Count > 0;

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            if (UsePets.GetBool()) return;
            AURoleOptions.EngineerCooldown = AbilityCD.GetFloat();
            AURoleOptions.EngineerInVentMaxTime = 1f;
        }

        public static void SendRPC(byte playerId, float secondsLeft)
        {
            if (!DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetNiceHackerLimit, SendOption.Reliable);
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

        public override void OnEnterVent(PlayerControl pc, Vent vent)
        {
            UseAbility(pc);
        }

        public override void OnPet(PlayerControl pc)
        {
            UseAbility(pc);
        }

        private static void UseAbility(PlayerControl pc)
        {
            if (pc == null) return;
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

        public override void OnFixedUpdate(PlayerControl pc)
        {
            if (pc == null) return;
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
            return !pc.Is(CustomRoles.NiceHacker) ? string.Empty : $"<color=#00ffa5>{GetString("NiceHackerAbilitySecondsLeft")}:</color> <b>{(int)UseLimitSeconds[pc.PlayerId]}</b>s";
        }

        public override string GetProgressText(byte playerId, bool comms)
        {
            if (playerId.IsPlayerModClient() || !UseLimit.ContainsKey(playerId)) return string.Empty;

            var sb = new StringBuilder();

            sb.Append(GetTaskCount(playerId, comms));
            sb.Append(ColorString(UseLimit[playerId] < 1 ? Color.red : Color.white, $" <color=#777777>-</color> {Math.Round(UseLimit[playerId], 1)}"));

            return sb.ToString();
        }
    }
}