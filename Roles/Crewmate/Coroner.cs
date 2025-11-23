using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EHR.Crewmate;

using static Options;
using static Translator;

public class Coroner : RoleBase
{
    private const int Id = 6400;
    private static List<byte> PlayerIdList = [];

    public static List<byte> UnreportablePlayers = [];

    public static OptionItem ArrowsPointingToDeadBody;
    public static OptionItem UseLimitOpt;
    public static OptionItem LeaveDeadBodyUnreportable;
    public static OptionItem NotifyKiller;
    public static OptionItem CoronerAbilityUseGainWithEachTaskCompleted;
    public static OptionItem AbilityChargesWhenFinishedTasks;

    private byte CoronerId;
    private List<byte> CoronerTargets = [];

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override bool SeesArrowsToDeadBodies => ArrowsPointingToDeadBody.GetBool();

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Coroner);

        ArrowsPointingToDeadBody = new BooleanOptionItem(Id + 10, "CoronerArrowsPointingToDeadBody", false, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Coroner]);

        LeaveDeadBodyUnreportable = new BooleanOptionItem(Id + 11, "CoronerLeaveDeadBodyUnreportable", false, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Coroner]);

        NotifyKiller = new BooleanOptionItem(Id + 14, "CoronerNotifyKiller", false, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Coroner]);

        UseLimitOpt = new IntegerOptionItem(Id + 12, "AbilityUseLimit", new(0, 20, 1), 1, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Coroner])
            .SetValueFormat(OptionFormat.Times);

        CoronerAbilityUseGainWithEachTaskCompleted = new FloatOptionItem(Id + 13, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Coroner])
            .SetValueFormat(OptionFormat.Times);

        AbilityChargesWhenFinishedTasks = new FloatOptionItem(Id + 15, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Coroner])
            .SetValueFormat(OptionFormat.Times);
    }

    public override void Init()
    {
        PlayerIdList = [];
        UnreportablePlayers = [];
        CoronerTargets = [];
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        playerId.SetAbilityUseLimit(UseLimitOpt.GetFloat());
        CoronerTargets = [];
        CoronerId = playerId;
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override void OnReportDeadBody()
    {
        foreach (byte id in PlayerIdList)
        {
            TargetArrow.RemoveAllTarget(id);
            LocateArrow.RemoveAllTarget(id);
        }

        CoronerTargets.Clear();
    }

    public override void AfterMeetingTasks()
    {
        TargetArrow.RemoveAllTarget(CoronerId);
        LocateArrow.RemoveAllTarget(CoronerId);
    }

    public override bool CheckReportDeadBody(PlayerControl pc, NetworkedPlayerInfo target, PlayerControl killer)
    {
        if (killer != null && !target.Object.Is(CustomRoles.Disregarded))
        {
            if (CoronerTargets.Contains(killer.PlayerId)) return false;

            Vector2 pos = target.Object.Pos();
            LocateArrow.Remove(pc.PlayerId, pos);

            if (pc.GetAbilityUseLimit() >= 1)
            {
                CoronerTargets.Add(killer.PlayerId);
                TargetArrow.Add(pc.PlayerId, killer.PlayerId);

                pc.Notify(GetString("CoronerTrackRecorded"));
                pc.RpcRemoveAbilityUse();

                if (LeaveDeadBodyUnreportable.GetBool()) UnreportablePlayers.Add(target.PlayerId);

                if (NotifyKiller.GetBool()) killer.Notify(GetString("CoronerKillerNotify"));
            }
            else
                pc.Notify(GetString("OutOfAbilityUsesDoMoreTasks"));
        }
        else
            pc.Notify(GetString("CoronerNoTrack"));

        return false;
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (target != null && seer.PlayerId != target.PlayerId || GameStates.IsMeeting || seer.PlayerId != CoronerId || hud || Main.PlayerStates[seer.PlayerId].Role is not Coroner cn) return string.Empty;
        return cn.CoronerTargets.Count > 0 ? cn.CoronerTargets.Select(targetId => TargetArrow.GetArrows(seer, targetId)).Aggregate(string.Empty, (current, arrow) => current + Utils.ColorString(seer.GetRoleColor(), arrow)) : Utils.ColorString(Color.white, LocateArrow.GetArrows(seer));
    }
}