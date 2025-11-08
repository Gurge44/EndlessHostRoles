using System;
using AmongUs.GameOptions;
using EHR.Coven;
using EHR.Crewmate;
using EHR.Impostor;
using HarmonyLib;

namespace EHR.Modules;

public static class MeetingTimeManager
{
    private static int DiscussionTime;
    private static int VotingTime;
    private static int DefaultDiscussionTime;
    private static int DefaultVotingTime;

    public static int VotingTimeLeft;

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
        try
        {
            if (Options.AllAliveMeeting.GetBool() && Utils.IsAllAlive)
            {
                DiscussionTime = 0;
                VotingTime = Options.AllAliveMeetingTime.GetInt();
                Logger.Info($"Discussion Time: {DiscussionTime}s, Voting Time: {VotingTime}s", "MeetingTimeManager.OnReportDeadBody");
                return;
            }

            ResetMeetingTime();
            var BonusMeetingTime = 0;
            var MeetingTimeMinTimeThief = 0;
            var MeetingTimeMinTimeManager = 0;
            var MeetingTimeMax = 300;

            if (TimeThief.PlayerIdList.Count > 0)
            {
                MeetingTimeMinTimeThief = TimeThief.LowerLimitVotingTime.GetInt();
                BonusMeetingTime += TimeThief.TotalDecreasedMeetingTime();
            }

            if (Timelord.On)
                BonusMeetingTime += -Timelord.GetTotalStolenTime();

            if (TimeManager.PlayerIdList.Count > 0)
            {
                MeetingTimeMinTimeManager = TimeManager.MadMinMeetingTimeLimit.GetInt();
                MeetingTimeMax = TimeManager.MeetingTimeLimit.GetInt();
                BonusMeetingTime += TimeManager.TotalIncreasedMeetingTime();
            }

            int TotalMeetingTime = DiscussionTime + VotingTime;

            if (TimeManager.PlayerIdList.Count > 0) BonusMeetingTime = Math.Clamp(TotalMeetingTime + BonusMeetingTime, MeetingTimeMinTimeManager, MeetingTimeMax) - TotalMeetingTime;
            if (TimeThief.PlayerIdList.Count > 0) BonusMeetingTime = Math.Clamp(TotalMeetingTime + BonusMeetingTime, MeetingTimeMinTimeThief, MeetingTimeMax) - TotalMeetingTime;
            if (TimeManager.PlayerIdList.Count == 0 && TimeThief.PlayerIdList.Count == 0) BonusMeetingTime = Math.Clamp(TotalMeetingTime + BonusMeetingTime, MeetingTimeMinTimeThief, MeetingTimeMax) - TotalMeetingTime;

            if (BonusMeetingTime >= 0)
                VotingTime += BonusMeetingTime;
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
        catch (Exception e) { Utils.ThrowException(e); }
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.UpdateTimerText))]
internal static class MeetingHudUpdateTimerTextPatch
{
    public static void Postfix([HarmonyArgument(1)] int value)
    {
        MeetingTimeManager.VotingTimeLeft = value;
    }
}