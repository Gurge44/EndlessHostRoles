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
        private static List<byte> playerIdList = new();

        private static OptionItem OptionTaskStartVision;
        private static OptionItem OptionCountStartTime;
        private static OptionItem OptionTaskEndVisionTime;
        private static OptionItem OptionTaskEndVision;
        private static OptionItem OptionTaskTimeMoveMeeting;

        private static float UpdateTime;
        static float ElapsedTime;

        public static void SetupCustomOption()
        {
            SetupSingleRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Ignitor, 1);
            OptionTaskStartVision = FloatOptionItem.Create(Id + 10, "CandleLighterStartVision", new(0.5f, 5f, 0.1f), 2.0f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Ignitor])
                .SetValueFormat(OptionFormat.Multiplier);
            OptionCountStartTime = IntegerOptionItem.Create(Id + 11, "CandleLighterCountStartTime", new(0, 300, 10), 0, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Ignitor])
                .SetValueFormat(OptionFormat.Seconds);
            OptionTaskEndVisionTime = IntegerOptionItem.Create(Id + 12, "CandleLighterEndVisionTime", new(60, 1200, 60), 480, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Ignitor])
                .SetValueFormat(OptionFormat.Seconds);
            OptionTaskEndVision = FloatOptionItem.Create(Id + 13, "CandleLighterEndVision", new(0f, 0.5f, 0.05f), 0.1f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Ignitor])
                .SetValueFormat(OptionFormat.Multiplier);
            OptionTaskTimeMoveMeeting = BooleanOptionItem.Create(Id + 14, "CandleLighterTimeMoveMeeting", false, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Ignitor]);
        }
        public static void Init()
        {
            playerIdList = new();
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
            if (ElapsedTime > OptionTaskEndVisionTime.GetInt()) Vision = OptionTaskStartVision.GetFloat();
            else Vision = OptionTaskStartVision.GetFloat() * (ElapsedTime / OptionTaskEndVisionTime.GetInt());

            if (Vision <= OptionTaskEndVision.GetFloat()) Vision = OptionTaskEndVision.GetFloat();

            opt.SetFloat(FloatOptionNames.CrewLightMod, Vision);
            if (Utils.IsActive(SystemTypes.Electrical))
                opt.SetFloat(FloatOptionNames.CrewLightMod, Vision * 5);
        }
        public static bool IsEnable => playerIdList.Any();
        public static bool OnCompleteTask(PlayerControl pc)
        {
            if (pc.IsAlive()) ElapsedTime = OptionTaskEndVisionTime.GetInt();
            return true;
        }
        public static void OnFixedUpdate(PlayerControl player)
        {
            if (!GameStates.IsInTask && !OptionTaskTimeMoveMeeting.GetBool()) return;

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