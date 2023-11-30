namespace TOHE.Roles.Crewmate
{
    using AmongUs.GameOptions;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;
    using static TOHE.Options;

    public static class Ignitor // Candle Lighter from TOHY
    {
        private static readonly int Id = 5280;
        private static List<byte> playerIdList = [];

        private static OptionItem OptionTaskStartVision;
        private static OptionItem OptionCountStartTime;
        private static OptionItem OptionTaskEndVisionTime;
        private static OptionItem OptionTaskEndVision;
        private static OptionItem OptionTaskTimeMoveMeeting;
        private static OptionItem OptionTasksFinishedVision;

        private static float UpdateTime;
        private static float ElapsedTime;
        private static bool active = true;

        public static void SetupCustomOption()
        {
            SetupSingleRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Ignitor, 1);
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
        public static void Init()
        {
            playerIdList = [];
            active = true;
        }
        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            UpdateTime = 1.0f;
            ElapsedTime = OptionTaskEndVisionTime.GetInt() + OptionCountStartTime.GetInt();
        }
        public static void ApplyGameOptions(IGameOptions opt)
        {
            float Vision;
            if (!active) Vision = OptionTasksFinishedVision.GetFloat();
            else if (ElapsedTime > OptionTaskEndVisionTime.GetInt()) Vision = OptionTaskStartVision.GetFloat();
            else Vision = OptionTaskStartVision.GetFloat() * (ElapsedTime / OptionTaskEndVisionTime.GetInt());

            if (Vision <= OptionTaskEndVision.GetFloat()) Vision = OptionTaskEndVision.GetFloat();

            opt.SetFloat(FloatOptionNames.CrewLightMod, Vision);
            if (Utils.IsActive(SystemTypes.Electrical))
                opt.SetFloat(FloatOptionNames.CrewLightMod, Vision * 5);
        }
        public static bool IsEnable => playerIdList.Count > 0;
        public static bool OnCompleteTask(PlayerControl pc)
        {
            ElapsedTime = OptionTaskEndVisionTime.GetInt();
            return true;
        }
        public static void OnTasksFinished(PlayerControl pc)
        {
            active = false;
            pc.SyncSettings();
        }
        public static void OnFixedUpdate(PlayerControl player)
        {
            if (!GameStates.IsInTask && !OptionTaskTimeMoveMeeting.GetBool()) return;
            if (!active) return;

            UpdateTime -= Time.fixedDeltaTime;
            if (UpdateTime < 0) UpdateTime = 1.0f;

            if (ElapsedTime > 0f)
            {
                ElapsedTime -= Time.fixedDeltaTime;

                if (UpdateTime == 1.0f) player.SyncSettings();
            }
        }
    }
}