using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using UnityEngine;
using static TOHE.Options;
using static TOHE.Translator;
using static TOHE.Utils;

namespace TOHE.Roles.Crewmate
{
    public class PatrollingState(byte sentinelId, int patrolDuration, float patrolRadius, PlayerControl sentinel = null, bool isPatrolling = false, Vector2? startingPosition = null, long patrolStartTimeStamp = 0)
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
        private long LastUpdate;

        public void SetPlayer() => Sentinel = GetPlayerById(SentinelId);

        public void StartPatrolling()
        {
            if (IsPatrolling) return;
            IsPatrolling = true;
            StartingPosition = Sentinel.Pos();
            PatrolStartTimeStamp = TimeStamp;
            foreach (var pc in NearbyKillers) pc.Notify(string.Format(GetString("KillerNotifyPatrol"), PatrolDuration));
            Sentinel.MarkDirtySettings();
        }

        public void Update()
        {
            if (!IsPatrolling) return;

            long now = TimeStamp;
            if (LastUpdate >= now) return;
            LastUpdate = now;

            if (PatrolStartTimeStamp + PatrolDuration < now)
            {
                FinishPatrolling();
            }
        }

        public void OnCheckPlayerPosition(PlayerControl pc)
        {
            if (!IsPatrolling) return;

            long now = TimeStamp;
            if (LastUpdate >= now) return;
            LastUpdate = now;

            var killers = NearbyKillers;

            bool nowInRange = killers.Any(x => x.PlayerId == pc.PlayerId);
            bool wasInRange = LastNearbyKillers.Contains(pc.PlayerId);

            if (wasInRange && !nowInRange)
            {
                pc.Notify(GetString("KillerEscapedFromSentinel"));
                LastNearbyKillers.Remove(pc.PlayerId);
            }
            if (nowInRange)
            {
                pc.Notify(string.Format(GetString("KillerNotifyPatrol"), PatrolDuration - (TimeStamp - PatrolStartTimeStamp)));
                LastNearbyKillers.Add(pc.PlayerId);
            }
        }

        public void FinishPatrolling()
        {
            IsPatrolling = false;
            if (!GameStates.IsInTask) return;
            foreach (var pc in NearbyKillers)
            {
                pc.Suicide(realKiller: Sentinel);
            }
            Sentinel.MarkDirtySettings();
        }
    }

    internal class Sentinel : RoleBase
    {
        private static int Id => 64430;

        public static OptionItem PatrolCooldown;
        private static OptionItem PatrolDuration;
        public static OptionItem LoweredVision;
        private static OptionItem PatrolRadius;

        public static List<PatrollingState> PatrolStates { get; } = [];

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

        public override void Init()
        {
            PatrolStates.Clear();
        }

        public override void Add(byte playerId)
        {
            var newPatrolState = new PatrollingState(playerId, PatrolDuration.GetInt(), PatrolRadius.GetFloat());
            PatrolStates.Add(newPatrolState);
            _ = new LateTask(newPatrolState.SetPlayer, 8f, log: false);
        }

        public override bool IsEnable => PatrolStates.Count > 0 || Randomizer.Exists;

        public static PatrollingState GetPatrollingState(byte playerId) => PatrolStates.FirstOrDefault(x => x.SentinelId == playerId) ?? new(playerId, PatrolDuration.GetInt(), PatrolRadius.GetInt());
        public static bool IsPatrolling(byte playerId) => GetPatrollingState(playerId).IsPatrolling;
        public override void OnEnterVent(PlayerControl pc, Vent vent) => GetPatrollingState(pc.PlayerId)?.StartPatrolling();

        public static bool OnAnyoneCheckMurder(PlayerControl killer)
        {
            if (killer == null || !PatrolStates.Any(x => x.IsPatrolling)) return true;
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

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            AURoleOptions.EngineerCooldown = PatrolCooldown.GetFloat();
            AURoleOptions.EngineerInVentMaxTime = 1f;
        }

        public override void OnFixedUpdate(PlayerControl pc)
        {
            if (!IsEnable) return;
            foreach (PatrollingState state in PatrolStates)
            {
                state.Update();
            }
        }

        public override void OnCheckPlayerPosition(PlayerControl pc)
        {
            if (!IsEnable) return;
            foreach (PatrollingState state in PatrolStates)
            {
                state.OnCheckPlayerPosition(pc);
            }
        }

        public override void OnReportDeadBody()
        {
            if (!IsEnable) return;
            _ = new LateTask(() =>
            {
                foreach (PatrollingState state in PatrolStates)
                {
                    if (state.IsPatrolling)
                    {
                        state.FinishPatrolling();
                    }
                }
            }, 0.1f);
        }
    }
}
