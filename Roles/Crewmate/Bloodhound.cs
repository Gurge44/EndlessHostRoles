﻿using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EHR.Crewmate;

using static Options;
using static Translator;

public class Bloodhound : RoleBase
{
    private const int Id = 6400;
    private static List<byte> PlayerIdList = [];

    public static List<byte> UnreportablePlayers = [];

    public static OptionItem ArrowsPointingToDeadBody;
    public static OptionItem UseLimitOpt;
    public static OptionItem LeaveDeadBodyUnreportable;
    public static OptionItem NotifyKiller;
    public static OptionItem BloodhoundAbilityUseGainWithEachTaskCompleted;
    public static OptionItem AbilityChargesWhenFinishedTasks;

    private byte BloodhoundId;
    private List<byte> BloodhoundTargets = [];

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override bool SeesArrowsToDeadBodies => ArrowsPointingToDeadBody.GetBool();

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Bloodhound);

        ArrowsPointingToDeadBody = new BooleanOptionItem(Id + 10, "BloodhoundArrowsPointingToDeadBody", false, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Bloodhound]);

        LeaveDeadBodyUnreportable = new BooleanOptionItem(Id + 11, "BloodhoundLeaveDeadBodyUnreportable", false, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Bloodhound]);

        NotifyKiller = new BooleanOptionItem(Id + 14, "BloodhoundNotifyKiller", false, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Bloodhound]);

        UseLimitOpt = new IntegerOptionItem(Id + 12, "AbilityUseLimit", new(0, 20, 1), 1, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Bloodhound])
            .SetValueFormat(OptionFormat.Times);

        BloodhoundAbilityUseGainWithEachTaskCompleted = new FloatOptionItem(Id + 13, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Bloodhound])
            .SetValueFormat(OptionFormat.Times);

        AbilityChargesWhenFinishedTasks = new FloatOptionItem(Id + 15, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Bloodhound])
            .SetValueFormat(OptionFormat.Times);
    }

    public override void Init()
    {
        PlayerIdList = [];
        UnreportablePlayers = [];
        BloodhoundTargets = [];
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        playerId.SetAbilityUseLimit(UseLimitOpt.GetFloat());
        BloodhoundTargets = [];
        BloodhoundId = playerId;
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

        BloodhoundTargets.Clear();
    }

    public override void AfterMeetingTasks()
    {
        TargetArrow.RemoveAllTarget(BloodhoundId);
        LocateArrow.RemoveAllTarget(BloodhoundId);
    }

    public override bool CheckReportDeadBody(PlayerControl pc, NetworkedPlayerInfo target, PlayerControl killer)
    {
        if (killer != null && !target.Object.Is(CustomRoles.Unreportable))
        {
            if (BloodhoundTargets.Contains(killer.PlayerId)) return false;

            Vector2 pos = target.Object.Pos();
            LocateArrow.Remove(pc.PlayerId, pos);

            if (pc.GetAbilityUseLimit() >= 1)
            {
                BloodhoundTargets.Add(killer.PlayerId);
                TargetArrow.Add(pc.PlayerId, killer.PlayerId);

                pc.Notify(GetString("BloodhoundTrackRecorded"));
                pc.RpcRemoveAbilityUse();

                if (LeaveDeadBodyUnreportable.GetBool()) UnreportablePlayers.Add(target.PlayerId);

                if (NotifyKiller.GetBool()) killer.Notify(GetString("BloodhoundKillerNotify"));
            }
            else
                pc.Notify(GetString("OutOfAbilityUsesDoMoreTasks"));
        }
        else
            pc.Notify(GetString("BloodhoundNoTrack"));

        return false;
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (target != null && seer.PlayerId != target.PlayerId) return string.Empty;

        if (GameStates.IsMeeting || seer.PlayerId != BloodhoundId || hud) return string.Empty;

        if (Main.PlayerStates[seer.PlayerId].Role is not Bloodhound bh) return string.Empty;

        return bh.BloodhoundTargets.Count > 0 ? bh.BloodhoundTargets.Select(targetId => TargetArrow.GetArrows(seer, targetId)).Aggregate(string.Empty, (current, arrow) => current + Utils.ColorString(seer.GetRoleColor(), arrow)) : Utils.ColorString(Color.white, LocateArrow.GetArrows(seer));
    }
}