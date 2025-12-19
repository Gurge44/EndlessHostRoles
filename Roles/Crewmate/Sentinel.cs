using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using static EHR.Options;
using static EHR.Translator;
using static EHR.Utils;

namespace EHR.Crewmate;

public class PatrollingState(byte sentinelId, int patrolDuration, float patrolRadius, PlayerControl sentinel = null, bool isPatrolling = false, Vector2? startingPosition = null, long patrolStartTimeStamp = 0)
{
    private readonly Dictionary<byte, long> LastPostitionCheck = [];
    private List<byte> LastNearbyKillers = [];
    private long LastUpdate;

    public byte SentinelId => sentinelId;

    public PlayerControl Sentinel { get; private set; } = sentinel;

    public bool IsPatrolling { get; private set; } = isPatrolling;

    private Vector2 StartingPosition { get; set; } = startingPosition ?? Vector2.zero;

    private long PatrolStartTimeStamp { get; set; } = patrolStartTimeStamp;

    private int PatrolDuration => patrolDuration;

    private float PatrolRadius => patrolRadius;

    public PlayerControl[] NearbyKillers => GetPlayersInRadius(PatrolRadius, StartingPosition).Where(x => !x.Is(Team.Crewmate) && (!Sentinel.IsMadmate() || !x.Is(Team.Impostor)) && SentinelId != x.PlayerId).ToArray();

    public void SetPlayer()
    {
        Sentinel = GetPlayerById(SentinelId);
    }

    public void StartPatrolling()
    {
        if (IsPatrolling) return;

        IsPatrolling = true;
        StartingPosition = Sentinel.Pos();
        PatrolStartTimeStamp = TimeStamp;
        foreach (PlayerControl pc in NearbyKillers) pc.Notify(string.Format(GetString("KillerNotifyPatrol"), PatrolDuration));

        Sentinel.MarkDirtySettings();
    }

    public void Update()
    {
        if (!IsPatrolling) return;

        long now = TimeStamp;
        if (LastUpdate >= now) return;
        LastUpdate = now;

        if (PatrolStartTimeStamp + PatrolDuration < now)
            FinishPatrolling();
    }

    public void OnCheckPlayerPosition(PlayerControl pc)
    {
        if (!IsPatrolling) return;

        long now = TimeStamp;
        LastPostitionCheck.TryAdd(pc.PlayerId, now);
        if (LastPostitionCheck[pc.PlayerId] >= now) return;
        LastPostitionCheck[pc.PlayerId] = now;

        PlayerControl[] killers = NearbyKillers;

        bool nowInRange = killers.Any(x => x.PlayerId == pc.PlayerId);
        bool wasInRange = LastNearbyKillers.Contains(pc.PlayerId);

        long timeLeft = PatrolDuration - (TimeStamp - PatrolStartTimeStamp);

        if (wasInRange && !nowInRange) pc.Notify(GetString("KillerEscapedFromSentinel"));
        if (nowInRange && timeLeft >= 0) pc.Notify(string.Format(GetString("KillerNotifyPatrol"), timeLeft), 3f, true);

        LastNearbyKillers = killers.Select(x => x.PlayerId).ToList();
    }

    public void FinishPatrolling()
    {
        IsPatrolling = false;
        if (!GameStates.IsInTask) return;

        foreach (PlayerControl pc in NearbyKillers)
            pc.Suicide(PlayerState.DeathReason.Patrolled, Sentinel);

        Sentinel.MarkDirtySettings();
    }
}

internal class Sentinel : RoleBase
{
    public static OptionItem PatrolCooldown;
    private static OptionItem PatrolDuration;
    public static OptionItem LoweredVision;
    private static OptionItem PatrolRadius;
    private static int Id => 64430;

    public static List<PatrollingState> PatrolStates { get; } = [];

    public override bool IsEnable => PatrolStates.Count > 0 || Randomizer.Exists;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Sentinel);

        PatrolCooldown = CreateCDSetting(Id + 2, TabGroup.CrewmateRoles, CustomRoles.Sentinel);

        PatrolDuration = new IntegerOptionItem(Id + 3, "SentinelPatrolDuration", new(1, 90, 1), 5, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Sentinel])
            .SetValueFormat(OptionFormat.Seconds);

        LoweredVision = new FloatOptionItem(Id + 4, "FFA_LowerVision", new(0.05f, 3f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Sentinel])
            .SetValueFormat(OptionFormat.Multiplier);

        PatrolRadius = new FloatOptionItem(Id + 5, "SentinelPatrolRadius", new(0.1f, 25f, 0.1f), 5f, TabGroup.CrewmateRoles)
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
        LateTask.New(newPatrolState.SetPlayer, 8f, log: false);
    }

    public override void Remove(byte playerId)
    {
        PatrolStates.RemoveAll(x => x.SentinelId == playerId);
    }

    private static PatrollingState GetPatrollingState(byte playerId)
    {
        return PatrolStates.FirstOrDefault(x => x.SentinelId == playerId) ?? new(playerId, PatrolDuration.GetInt(), PatrolRadius.GetInt());
    }

    public static bool IsPatrolling(byte playerId)
    {
        return GetPatrollingState(playerId)?.IsPatrolling == true;
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        if (UsePets.GetBool()) return;
        GetPatrollingState(pc.PlayerId)?.StartPatrolling();
    }

    public override void OnPet(PlayerControl pc)
    {
        GetPatrollingState(pc.PlayerId)?.StartPatrolling();
    }

    public static bool OnAnyoneCheckMurder(PlayerControl killer)
    {
        if (killer == null || !PatrolStates.Any(x => x.IsPatrolling)) return true;

        foreach (PatrollingState state in PatrolStates)
        {
            if (!state.IsPatrolling) continue;

            if (state.NearbyKillers.Any(x => x.PlayerId == killer.PlayerId))
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
            state.Update();
    }

    public override void OnCheckPlayerPosition(PlayerControl pc)
    {
        if (!IsEnable) return;

        foreach (PatrollingState state in PatrolStates)
            state.OnCheckPlayerPosition(pc);
    }

    public override void OnReportDeadBody()
    {
        if (!IsEnable) return;

        LateTask.New(() =>
        {
            foreach (PatrollingState state in PatrolStates)
            {
                if (state.IsPatrolling)
                    state.FinishPatrolling();
            }
        }, 0.1f, "SentinelFinishPatrol");
    }

    public override bool CanUseVent(PlayerControl pc, int ventId)
    {
        return !IsThisRole(pc) || pc.Is(CustomRoles.Nimble) || pc.GetClosestVent()?.Id == ventId;
    }

    public override void ManipulateGameEndCheckCrew(PlayerState playerState, out bool keepGameGoing, out int countsAs)
    {
        if (playerState.IsDead)
        {
            base.ManipulateGameEndCheckCrew(playerState, out keepGameGoing, out countsAs);
            return;
        }

        keepGameGoing = true;
        countsAs = 1;
    }
}