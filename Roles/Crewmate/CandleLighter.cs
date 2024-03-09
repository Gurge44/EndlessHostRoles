using AmongUs.GameOptions;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TOHE.Roles.Crewmate
{
    using static Options;

    public class Ignitor : RoleBase // Candle Lighter from TOHY
    {
        private const int Id = 5280;
        private static List<byte> playerIdList = [];

        private static OptionItem OptionTaskStartVision;
        private static OptionItem OptionCountStartTime;
        private static OptionItem OptionTaskEndVisionTime;
        private static OptionItem OptionTaskEndVision;
        private static OptionItem OptionTaskTimeMoveMeeting;
        private static OptionItem OptionTasksFinishedVision;

        private float UpdateTime;
        private float ElapsedTime;
        private bool Active = true;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Ignitor);
            OptionTaskStartVision = FloatOptionItem.Create(Id + 2, "CandleLighterStartVision", new(0.5f, 5f, 0.1f), 0.8f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Ignitor])
                .SetValueFormat(OptionFormat.Multiplier);
            OptionCountStartTime = IntegerOptionItem.Create(Id + 3, "CandleLighterCountStartTime", new(0, 50, 5), 0, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Ignitor])
                .SetValueFormat(OptionFormat.Seconds);
            OptionTaskEndVisionTime = IntegerOptionItem.Create(Id + 4, "CandleLighterEndVisionTime", new(20, 200, 10), 50, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Ignitor])
                .SetValueFormat(OptionFormat.Seconds);
            OptionTaskEndVision = FloatOptionItem.Create(Id + 5, "CandleLighterEndVision", new(0f, 0.5f, 0.05f), 0.1f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Ignitor])
                .SetValueFormat(OptionFormat.Multiplier);
            OptionTaskTimeMoveMeeting = BooleanOptionItem.Create(Id + 6, "CandleLighterTimeMoveMeeting", false, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Ignitor]);
            OptionTasksFinishedVision = FloatOptionItem.Create(Id + 7, "CandleLighterTasksFinishedVision", new(0.5f, 5f, 0.1f), 0.5f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Ignitor])
                .SetValueFormat(OptionFormat.Multiplier);
            OverrideTasksData.Create(Id + 8, TabGroup.CrewmateRoles, CustomRoles.Ignitor);
        }

        public override void Init()
        {
            playerIdList = [];
            Active = true;
        }

        public override void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            Active = true;
            UpdateTime = 1.0f;
            ElapsedTime = OptionTaskEndVisionTime.GetInt() + OptionCountStartTime.GetInt();
        }

        public override void ApplyGameOptions(IGameOptions opt, byte id)
        {
            float Vision;
            if (!Active) Vision = OptionTasksFinishedVision.GetFloat();
            else if (ElapsedTime > OptionTaskEndVisionTime.GetInt()) Vision = OptionTaskStartVision.GetFloat();
            else Vision = OptionTaskStartVision.GetFloat() * (ElapsedTime / OptionTaskEndVisionTime.GetInt());

            if (Vision <= OptionTaskEndVision.GetFloat()) Vision = OptionTaskEndVision.GetFloat();

            opt.SetFloat(FloatOptionNames.CrewLightMod, Vision);
            if (Utils.IsActive(SystemTypes.Electrical))
                opt.SetFloat(FloatOptionNames.CrewLightMod, Vision * 5);
        }

        public override bool IsEnable => playerIdList.Count > 0;

        public override void OnTaskComplete(PlayerControl pc, int completedTaskCount, int totalTaskCount)
        {
            if ((completedTaskCount + 1) >= totalTaskCount) Active = false;
            else ElapsedTime = OptionTaskEndVisionTime.GetInt();
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
}