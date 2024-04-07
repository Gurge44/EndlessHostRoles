using System;
using AmongUs.GameOptions;
using EHR.Roles.Crewmate;
using EHR.Roles.Impostor;

namespace EHR.Modules;

public class MeetingTimeManager
{
    private static int DiscussionTime;
    private static int VotingTime;
    private static int DefaultDiscussionTime;
    private static int DefaultVotingTime;

    public static void Init()
    {
        DefaultDiscussionTime = Main.RealOptionsData.GetInt(Int32OptionNames.DiscussionTime);
        DefaultVotingTime = Main.RealOptionsData.GetInt(Int32OptionNames.VotingTime);
        Logger.Info($"DefaultDiscussionTime: {DefaultDiscussionTime}s, DefaultVotingTime: {DefaultVotingTime}s", "MeetingTimeManager.Init");
        ResetMeetingTime();
    }

    public static void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetInt(Int32OptionNames.DiscussionTime, DiscussionTime);
        opt.SetInt(Int32OptionNames.VotingTime, VotingTime);
    }

    private static void ResetMeetingTime()
    {
        DiscussionTime = DefaultDiscussionTime;
        VotingTime = DefaultVotingTime;
    }

    public static void OnReportDeadBody()
    {
        if (Options.AllAliveMeeting.GetBool() && Utils.IsAllAlive)
        {
            DiscussionTime = 0;
            VotingTime = Options.AllAliveMeetingTime.GetInt();
            Logger.Info($"Discussion Time: {DiscussionTime}s, Voting Time: {VotingTime}s", "MeetingTimeManager.OnReportDeadBody");
            return;
        }

        ResetMeetingTime();
        int BonusMeetingTime = 0;
        int MeetingTimeMinTimeThief = 0;
        int MeetingTimeMinTimeManager = 0;
        int MeetingTimeMax = 300;

        if (TimeThief.playerIdList.Count > 0)
        {
            MeetingTimeMinTimeThief = TimeThief.LowerLimitVotingTime.GetInt();
            BonusMeetingTime += TimeThief.TotalDecreasedMeetingTime();
        }

        if (TimeManager.playerIdList.Count > 0)
        {
            MeetingTimeMinTimeManager = TimeManager.MadMinMeetingTimeLimit.GetInt();
            MeetingTimeMax = TimeManager.MeetingTimeLimit.GetInt();
            BonusMeetingTime += TimeManager.TotalIncreasedMeetingTime();
        }

        int TotalMeetingTime = DiscussionTime + VotingTime;

        if (TimeManager.playerIdList.Count > 0) BonusMeetingTime = Math.Clamp(TotalMeetingTime + BonusMeetingTime, MeetingTimeMinTimeManager, MeetingTimeMax) - TotalMeetingTime;
        if (TimeThief.playerIdList.Count > 0) BonusMeetingTime = Math.Clamp(TotalMeetingTime + BonusMeetingTime, MeetingTimeMinTimeThief, MeetingTimeMax) - TotalMeetingTime;
        if (TimeManager.playerIdList.Count == 0 && TimeThief.playerIdList.Count == 0) BonusMeetingTime = Math.Clamp(TotalMeetingTime + BonusMeetingTime, MeetingTimeMinTimeThief, MeetingTimeMax) - TotalMeetingTime;

        if (BonusMeetingTime >= 0)
        {
            VotingTime += BonusMeetingTime;
        }
        else
        {
            DiscussionTime += BonusMeetingTime;
            if (DiscussionTime < 0)
            {
                VotingTime += DiscussionTime;
                DiscussionTime = 0;
            }
        }

        Logger.Info($"Discussion Time: {DiscussionTime}s, Voting Time: {VotingTime}s", "MeetingTimeManager.OnReportDeadBody");
    }
}