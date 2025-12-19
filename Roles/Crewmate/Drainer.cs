using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using static EHR.Options;

namespace EHR.Crewmate;

public class Drainer : RoleBase
{
    private const int Id = 642500;
    private static List<byte> PlayerIdList = [];

    private static OptionItem VentCD;
    private static OptionItem UseLimit;
    public static OptionItem DrainerAbilityUseGainWithEachTaskCompleted;
    public static OptionItem AbilityChargesWhenFinishedTasks;

    public static Dictionary<byte, int> PlayersInVents = [];

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Drainer);

        VentCD = new IntegerOptionItem(Id + 10, "VentCooldown", new(1, 60, 1), 30, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Drainer])
            .SetValueFormat(OptionFormat.Seconds);

        UseLimit = new FloatOptionItem(Id + 11, "AbilityUseLimit", new(0, 20, 0.05f), 1, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Drainer])
            .SetValueFormat(OptionFormat.Times);

        DrainerAbilityUseGainWithEachTaskCompleted = new FloatOptionItem(Id + 12, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 0.5f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Drainer])
            .SetValueFormat(OptionFormat.Times);

        AbilityChargesWhenFinishedTasks = new FloatOptionItem(Id + 13, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Drainer])
            .SetValueFormat(OptionFormat.Times);
    }

    public override void Init()
    {
        PlayerIdList = [];
        PlayersInVents = [];
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        playerId.SetAbilityUseLimit(UseLimit.GetFloat());
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        AURoleOptions.EngineerCooldown = VentCD.GetFloat();
        AURoleOptions.EngineerInVentMaxTime = 1f;
    }

    public static void OnAnyoneExitVent(PlayerControl pc)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        if (pc != null) PlayersInVents.Remove(pc.PlayerId);
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        if (pc.GetAbilityUseLimit() <= 0) return;

        pc.RpcRemoveAbilityUse();

        Vent[] vents = vent.NearbyVents.Where(v => v != null).Append(vent).ToArray();
        foreach (Vent ventToDrain in vents) KillPlayersInVent(pc, ventToDrain);
    }

    public static void OnAnyoneEnterVent(PlayerControl pc, Vent vent)
    {
        if (!AmongUsClient.Instance.AmHost || pc == null || vent == null || pc.Is(CustomRoles.Drainer)) return;

        PlayersInVents[pc.PlayerId] = vent.Id;
    }

    private void KillPlayersInVent(PlayerControl pc, Vent vent)
    {
        if (!IsEnable) return;

        int ventId = vent.Id;

        if (!PlayersInVents.ContainsValue(ventId)) return;

        foreach (KeyValuePair<byte, int> venterId in PlayersInVents)
        {
            if (venterId.Value == ventId)
            {
                PlayerControl venter = Utils.GetPlayerById(venterId.Key);
                if (venter == null) continue;

                if (pc != null && pc.RpcCheckAndMurder(venter, true))
                {
                    venter.MyPhysics.RpcBootFromVent(ventId);

                    LateTask.New(() =>
                    {
                        venter.Suicide(PlayerState.DeathReason.Demolished, pc);
                        Logger.Info($"Killed venter {venter.GetNameWithRole()} (was inside {vent.name}, ID {ventId})", "Drainer");
                    }, 0.55f, "Drainer-KillPlayerInVent");
                }
            }
        }
    }

    public override void OnReportDeadBody()
    {
        if (!IsEnable) return;

        PlayersInVents.Clear();
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