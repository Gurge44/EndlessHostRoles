using Hazel;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static TOHE.Options;
using static TOHE.Translator;
using static TOHE.Utils;

namespace TOHE.Roles.Crewmate
{
    class PatrollingState(byte sentinelId, int patrolDuration, float patrolRadius, PlayerControl sentinel = null, bool isPatrolling = false, Vector2? startingPosition = null, long patrolStartTimeStamp = 0)
    {
        public byte SentinelId { get => sentinelId; set => sentinelId = value; }
        public PlayerControl Sentinel { get => sentinel; set => sentinel = value; }
        public bool IsPatrolling { get => isPatrolling; set => isPatrolling = value; }
        public Vector2 StartingPosition { get => startingPosition ?? Vector2.zero; set => startingPosition = value; }
        public long PatrolStartTimeStamp { get => patrolStartTimeStamp; set => patrolStartTimeStamp = value; }
        public int PatrolDuration { get => patrolDuration; set => patrolDuration = value; }
        public float PatrolRadius { get => patrolRadius; set => patrolRadius = value; }

        public PlayerControl[] NearbyKillers => GetPlayersInRadius(PatrolRadius, StartingPosition).Where(x => !x.Is(Team.Crewmate) && SentinelId != x.PlayerId).ToArray();
        private readonly List<byte> LastNearbyKillers = [];
        private long LastUpdate = 0;

        public void SetPlayer() => Sentinel = GetPlayerById(SentinelId);

        public void StartPatrolling()
        {
            if (IsPatrolling) return;
            IsPatrolling = true;
            StartingPosition = Sentinel.Pos();
            PatrolStartTimeStamp = GetTimeStamp();
            foreach (var pc in NearbyKillers) pc.Notify(string.Format(GetString("KillerNotifyPatrol"), PatrolDuration));
            Sentinel.MarkDirtySettings();
            SendRPC();
        }

        public void Update()
        {
            if (!IsPatrolling) return;

            long now = GetTimeStamp();
            if (LastUpdate >= now) return;
            LastUpdate = now;

            if (PatrolStartTimeStamp + PatrolDuration < now)
            {
                FinishPatrolling();
                return;
            }
        }

        public void OnCheckPlayerPosition(PlayerControl pc)
        {
            var killers = NearbyKillers;

            bool nowInRange = killers.Any(x => x.PlayerId == pc.PlayerId);
            bool wasInRange = LastNearbyKillers.Contains(pc.PlayerId);

            if (wasInRange && !nowInRange)
            {
                pc.Notify(GetString("KillerEscapedFromSentinel"));
            }
            if (nowInRange)
            {
                pc.Notify(string.Format(GetString("KillerNotifyPatrol"), PatrolDuration - (GetTimeStamp() - PatrolStartTimeStamp)));
            }
        }

        public void FinishPatrolling()
        {
            IsPatrolling = false;
            foreach (var pc in NearbyKillers)
            {
                pc.Suicide(realKiller: Sentinel);
            }
            Sentinel.MarkDirtySettings();
            SendRPC();
        }

        public void SendRPC()
        {
            if (!DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncSentinel, SendOption.Reliable, -1);
            writer.Write(SentinelId);
            writer.Write(IsPatrolling);
            writer.Write(StartingPosition.x);
            writer.Write(StartingPosition.y);
            writer.Write(PatrolStartTimeStamp.ToString());
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public void ReceiveRPC(bool ispatrolling, float x, float y, string ts)
        {
            IsPatrolling = ispatrolling;
            StartingPosition = new(x, y);
            PatrolStartTimeStamp = long.Parse(ts);
        }
    }
    internal class Sentinel
    {
        private static int Id => 64430;

        public static OptionItem PatrolCooldown;
        private static OptionItem PatrolDuration;
        public static OptionItem LoweredVision;
        private static OptionItem PatrolRadius;

        private static readonly List<PatrollingState> PatrolStates = [];
        private static readonly List<byte> AffectedKillers = [];

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Sentinel);
            PatrolCooldown = CreateCDSetting(Id + 2, TabGroup.CrewmateRoles, CustomRoles.Sentinel);
            PatrolDuration = IntegerOptionItem.Create(Id + 3, "SentinelPatrolDuration", new(1, 90, 1), 5, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Sentinel])
                .SetValueFormat(OptionFormat.Seconds);
            LoweredVision = FloatOptionItem.Create(Id + 4, "FFA_LowerVision", new(0.05f, 3f, 0.05f), 0.2f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Sentinel])
                .SetValueFormat(OptionFormat.Multiplier);
            PatrolRadius = FloatOptionItem.Create(Id + 5, "SentinelPatrolRadius", new(0.1f, 25f, 0.1f), 5f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Sentinel])
                .SetValueFormat(OptionFormat.Multiplier);
        }

        public static void Init()
        {
            PatrolStates.Clear();
            AffectedKillers.Clear();
        }
        public static void Add(byte playerId)
        {
            var newPatrolState = new PatrollingState(playerId, PatrolDuration.GetInt(), PatrolRadius.GetFloat());
            PatrolStates.Add(newPatrolState);
            _ = new LateTask(newPatrolState.SetPlayer, 8f, log: false);
        }
        public static void ReceiveRPC(MessageReader reader)
        {
            byte id = reader.ReadByte();
            bool ispatrolling = reader.ReadBoolean();
            float x = reader.ReadSingle();
            float y = reader.ReadSingle();
            string ts = reader.ReadString();
            PatrolStates.FirstOrDefault(x => x.SentinelId == id).ReceiveRPC(ispatrolling, x, y, ts);
        }
        public static bool IsEnable => PatrolStates.Count > 0;
        public static PatrollingState GetPatrollingState(byte playerId) => PatrolStates.FirstOrDefault(x => x.SentinelId == playerId) ?? new(playerId, PatrolDuration.GetInt(), PatrolRadius.GetInt());
        public static bool IsPatrolling(byte playerId) => GetPatrollingState(playerId).IsPatrolling;
        public static void StartPatrolling(PlayerControl pc) => GetPatrollingState(pc.PlayerId)?.StartPatrolling();
        public static bool OnAnyoneCheckMurder(PlayerControl killer)
        {
            if (killer == null || !IsEnable || !PatrolStates.Any(x => x.IsPatrolling)) return true;
            foreach (PatrollingState state in PatrolStates)
            {
                if (!state.IsPatrolling) continue;
                if (state.NearbyKillers.Contains(killer))
                {
                    state.Sentinel.RpcCheckAndMurder(killer);
                    return false;
                }
            }
            return true;
        }
        public static void OnFixedUpdate()
        {
            if (!IsEnable) return;
            foreach (PatrollingState state in PatrolStates)
            {
                state.Update();
            }
        }
        public static void OnCheckPlayerPosition(PlayerControl pc)
        {
            if (!IsEnable) return;
            foreach (PatrollingState state in PatrolStates)
            {
                state.OnCheckPlayerPosition(pc);
            }
        }
    }
}
