using System.Collections.Generic;

namespace EHR.Crewmate
{
    public class TimeManager : RoleBase
    {
        private const int Id = 8200;
        public static List<byte> PlayerIdList = [];
        private static OptionItem IncreaseMeetingTime;
        public static OptionItem MeetingTimeLimit;
        public static OptionItem MadMinMeetingTimeLimit;

        public override bool IsEnable => PlayerIdList.Count > 0;

        public override void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.TimeManager);

            IncreaseMeetingTime = new IntegerOptionItem(Id + 10, "TimeManagerIncreaseMeetingTime", new(1, 30, 1), 5, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.TimeManager])
                .SetValueFormat(OptionFormat.Seconds);

            MeetingTimeLimit = new IntegerOptionItem(Id + 11, "TimeManagerLimitMeetingTime", new(100, 500, 10), 150, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.TimeManager])
                .SetValueFormat(OptionFormat.Seconds);

            MadMinMeetingTimeLimit = new IntegerOptionItem(Id + 12, "MadTimeManagerLimitMeetingTime", new(5, 150, 5), 50, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.TimeManager])
                .SetValueFormat(OptionFormat.Seconds);

            Options.OverrideTasksData.Create(Id + 13, TabGroup.CrewmateRoles, CustomRoles.TimeManager);
        }

        public override void Init()
        {
            PlayerIdList = [];
        }

        public override void Add(byte playerId)
        {
            PlayerIdList.Add(playerId);
        }

        private static int AdditionalTime(byte id)
        {
            PlayerControl pc = Utils.GetPlayerById(id);
            return PlayerIdList.Contains(id) && pc.IsAlive() ? IncreaseMeetingTime.GetInt() * pc.GetTaskState().CompletedTasksCount : 0;
        }

        public static int TotalIncreasedMeetingTime()
        {
            var sec = 0;

            foreach (byte playerId in PlayerIdList.ToArray())
            {
                if (Utils.GetPlayerById(playerId).Is(CustomRoles.Madmate))
                    sec -= AdditionalTime(playerId);
                else
                    sec += AdditionalTime(playerId);
            }

            Logger.Info($"{sec}second", "TimeManager.TotalIncreasedMeetingTime");
            return sec;
        }
    }
}