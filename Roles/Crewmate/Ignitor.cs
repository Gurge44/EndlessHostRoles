using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using UnityEngine;

namespace EHR.Crewmate;

using static Options;

public class Ignitor : RoleBase // Candle Lighter from TOHY
{
    private const int Id = 5280;
    private static List<byte> PlayerIdList = [];

    private static OptionItem OptionTaskStartVision;
    private static OptionItem OptionCountStartTime;
    private static OptionItem OptionTaskEndVisionTime;
    private static OptionItem OptionTaskEndVision;
    private static OptionItem OptionTaskTimeMoveMeeting;
    private static OptionItem OptionTasksFinishedVision;
    private bool Active = true;
    private float ElapsedTime;

    private float UpdateTime;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Ignitor);

        OptionTaskStartVision = new FloatOptionItem(Id + 2, "IgnitorStartVision", new(0.5f, 5f, 0.1f), 0.8f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Ignitor])
            .SetValueFormat(OptionFormat.Multiplier);

        OptionCountStartTime = new IntegerOptionItem(Id + 3, "IgnitorCountStartTime", new(0, 50, 5), 0, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Ignitor])
            .SetValueFormat(OptionFormat.Seconds);

        OptionTaskEndVisionTime = new IntegerOptionItem(Id + 4, "IgnitorEndVisionTime", new(20, 200, 10), 50, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Ignitor])
            .SetValueFormat(OptionFormat.Seconds);

        OptionTaskEndVision = new FloatOptionItem(Id + 5, "IgnitorEndVision", new(0f, 0.5f, 0.05f), 0.1f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Ignitor])
            .SetValueFormat(OptionFormat.Multiplier);

        OptionTaskTimeMoveMeeting = new BooleanOptionItem(Id + 6, "IgnitorTimeMoveMeeting", false, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Ignitor]);

        OptionTasksFinishedVision = new FloatOptionItem(Id + 7, "IgnitorTasksFinishedVision", new(0.5f, 5f, 0.1f), 0.5f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Ignitor])
            .SetValueFormat(OptionFormat.Multiplier);

        OverrideTasksData.Create(Id + 8, TabGroup.CrewmateRoles, CustomRoles.Ignitor);
    }

    public override void Init()
    {
        PlayerIdList = [];
        Active = true;
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        Active = true;
        UpdateTime = 1.0f;
        ElapsedTime = OptionTaskEndVisionTime.GetInt() + OptionCountStartTime.GetInt();
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        float Vision;

        if (!Active)
            Vision = OptionTasksFinishedVision.GetFloat();
        else if (ElapsedTime > OptionTaskEndVisionTime.GetInt())
            Vision = OptionTaskStartVision.GetFloat();
        else
            Vision = OptionTaskStartVision.GetFloat() * (ElapsedTime / OptionTaskEndVisionTime.GetInt());

        if (Vision <= OptionTaskEndVision.GetFloat()) Vision = OptionTaskEndVision.GetFloat();

        opt.SetFloat(FloatOptionNames.CrewLightMod, Vision);
        if (Utils.IsActive(SystemTypes.Electrical)) opt.SetFloat(FloatOptionNames.CrewLightMod, Vision * 5);
    }

    public override void OnTaskComplete(PlayerControl pc, int completedTaskCount, int totalTaskCount)
    {
        if (completedTaskCount + 1 >= totalTaskCount)
            Active = false;
        else
            ElapsedTime = OptionTaskEndVisionTime.GetInt();

        pc.MarkDirtySettings();
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!GameStates.IsInTask && !OptionTaskTimeMoveMeeting.GetBool()) return;

        if (!Active) return;

        UpdateTime -= Time.fixedDeltaTime;
        if (UpdateTime < 0) UpdateTime = 1.0f;

        if (ElapsedTime > 0f)
        {
            ElapsedTime -= Time.fixedDeltaTime;

            if (Math.Abs(UpdateTime - 1.0f) < 0.01f) player.MarkDirtySettings();
        }
    }
}