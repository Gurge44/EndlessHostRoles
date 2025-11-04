using System.Collections.Generic;
using System.Runtime.CompilerServices;
using EHR.Modules;
using Hazel;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.AddOns.Crewmate;

public class Stressed : IAddon
{
    private const int Id = 14686;

    private static OptionItem StressedExtraTimeAfterTaskComplete;
    private static OptionItem StressedExtraTimeAfterMeeting;
    private static OptionItem StressedExtraTimeAfterImpostorDeath;
    private static OptionItem StressedExtraTimeAfterImpostorEjected;
    private static OptionItem StressedExtraTimeAfterFixingSabotage;
    private static OptionItem StressedExtraTimeAfterReporting;
    private static OptionItem StressedTimePenaltyAfterCrewmateEjected;
    private static OptionItem StressedStartingTime;

    private static int TimeAfterTaskComplete;
    private static int TimeAfterMeeting;
    private static int TimeAfterImpDead;
    private static int TimeAfterImpEject;
    private static int TimeAfterSaboFix;
    private static int TimeAfterReport;
    private static int TimeMinusAfterCrewEject;
    private static int StartingTime;

    private static Dictionary<byte, int> Timers = [];
    private static Dictionary<byte, long> LastUpdates = [];

    public static bool CountRepairSabotage;

    private static bool IsEnable => Timers.Count > 0;
    public AddonTypes Type => AddonTypes.Harmful;

    public void SetupCustomOption()
    {
        SetupAdtRoleOptions(Id, CustomRoles.Stressed, canSetNum: true);

        StressedExtraTimeAfterTaskComplete = new IntegerOptionItem(Id + 11, "StressedExtraTimeAfterTask", new(0, 60, 1), 30, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Stressed])
            .SetValueFormat(OptionFormat.Seconds);

        StressedExtraTimeAfterMeeting = new IntegerOptionItem(Id + 4, "DamoclesExtraTimeAfterMeeting", new(0, 60, 1), 15, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Stressed])
            .SetValueFormat(OptionFormat.Seconds);

        StressedExtraTimeAfterImpostorDeath = new IntegerOptionItem(Id + 5, "StressedExtraTimeAfterImpostorDeath", new(0, 60, 1), 10, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Stressed])
            .SetValueFormat(OptionFormat.Seconds);

        StressedExtraTimeAfterImpostorEjected = new IntegerOptionItem(Id + 6, "StressedExtraTimeAfterImpostorEjected", new(0, 60, 1), 25, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Stressed])
            .SetValueFormat(OptionFormat.Seconds);

        StressedExtraTimeAfterFixingSabotage = new IntegerOptionItem(Id + 7, "StressedExtraTimeAfterFixingSabotage", new(0, 60, 1), 20, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Stressed])
            .SetValueFormat(OptionFormat.Seconds);

        StressedExtraTimeAfterReporting = new IntegerOptionItem(Id + 8, "StressedExtraTimeAfterReporting", new(0, 60, 1), 10, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Stressed])
            .SetValueFormat(OptionFormat.Seconds);

        StressedTimePenaltyAfterCrewmateEjected = new IntegerOptionItem(Id + 9, "StressedTimePenaltyAfterCrewmateEjected", new(0, 60, 1), 15, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Stressed])
            .SetValueFormat(OptionFormat.Seconds);

        StressedStartingTime = new IntegerOptionItem(Id + 10, "DamoclesStartingTime", new(0, 60, 1), 30, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Stressed])
            .SetValueFormat(OptionFormat.Seconds);
    }

    public static void Init()
    {
        TimeAfterTaskComplete = StressedExtraTimeAfterTaskComplete.GetInt();
        TimeAfterMeeting = StressedExtraTimeAfterMeeting.GetInt();
        StartingTime = StressedStartingTime.GetInt();
        TimeAfterImpDead = StressedExtraTimeAfterImpostorDeath.GetInt();
        TimeAfterImpEject = StressedExtraTimeAfterImpostorEjected.GetInt();
        TimeAfterSaboFix = StressedExtraTimeAfterFixingSabotage.GetInt();
        TimeAfterReport = StressedExtraTimeAfterReporting.GetInt();
        TimeMinusAfterCrewEject = StressedTimePenaltyAfterCrewmateEjected.GetInt();

        Timers = [];
        LastUpdates = [];
        CountRepairSabotage = true;
    }

    public static void Add()
    {
        long now = Utils.TimeStamp;

        LateTask.New(() =>
        {
            foreach (PlayerControl pc in Main.AllAlivePlayerControls)
            {
                if (pc.Is(CustomRoles.Stressed))
                {
                    if (!pc.GetTaskState().HasTasks)
                    {
                        Main.PlayerStates[pc.PlayerId].RemoveSubRole(CustomRoles.Stressed);
                        continue;
                    }

                    Timers[pc.PlayerId] = StartingTime;
                    LastUpdates[pc.PlayerId] = now + 1;
                }
            }

            LogTimer();
        }, 8f, log: false);
    }

    public static void Update(PlayerControl pc)
    {
        long now = Utils.TimeStamp;
        if (pc == null || !LastUpdates.TryGetValue(pc.PlayerId, out long x) || x >= now || !Timers.ContainsKey(pc.PlayerId) || !IsEnable || !GameStates.IsInTask || !pc.Is(CustomRoles.Stressed)) return;

        LastUpdates[pc.PlayerId] = now;

        TaskState ts = pc.GetTaskState();

        if (ts.IsTaskFinished || !ts.HasTasks || !pc.IsAlive())
        {
            Main.PlayerStates[pc.PlayerId].RemoveSubRole(CustomRoles.Stressed);
            Timers.Remove(pc.PlayerId);
            LastUpdates.Remove(pc.PlayerId);
            return;
        }

        Timers[pc.PlayerId]--;

        if (Timers[pc.PlayerId] < 0)
        {
            Timers[pc.PlayerId] = 0;
            pc.Suicide();

            if (pc.AmOwner)
                Achievements.Type.OutOfTime.Complete();
        }

        if (pc.IsNonHostModdedClient()) SendRPC(pc.PlayerId, Timers[pc.PlayerId], LastUpdates[pc.PlayerId]);

        if (!pc.IsModdedClient()) Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
    }

    public static void SendRPC(byte id, int time, long lastUpdate)
    {
        if (!Utils.DoRPC || !IsEnable) return;

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncStressedTimer, SendOption.Reliable);
        writer.Write(id);
        writer.Write(time);
        writer.Write(lastUpdate.ToString());
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        if (!IsEnable) return;

        byte id = reader.ReadByte();
        int time = reader.ReadInt32();
        long lastUpdate = long.Parse(reader.ReadString());
        Timers[id] = time;
        LastUpdates[id] = lastUpdate;
    }

    private static void LogTimer(byte id = byte.MaxValue, [CallerMemberName] string action = "")
    {
        if (Timers.TryGetValue(id, out int time))
            Logger.Info($"{action} - Timer: {time} for {id.ColoredPlayerName()}", "Stressed");
        else
            Timers.Do(x => Logger.Info($"{action} - Timer: {x.Value} for {x.Key.ColoredPlayerName()}", "Stressed"));
    }

    public static void OnTaskComplete(PlayerControl pc)
    {
        if (!IsEnable) return;

        Timers[pc.PlayerId] += TimeAfterTaskComplete;
        LogTimer(pc.PlayerId);
    }

    public static void AfterMeetingTasks()
    {
        if (!IsEnable) return;

        CountRepairSabotage = true;
    }

    public static void OnNonCrewmateDead()
    {
        if (!IsEnable) return;

        AdjustTime(TimeAfterImpDead);
        LogTimer();
    }

    public static void OnNonCrewmateEjected()
    {
        if (!IsEnable) return;

        AdjustTime(TimeAfterImpEject);
        LogTimer();
    }

    public static void OnCrewmateEjected()
    {
        if (!IsEnable) return;

        AdjustTime(TimeMinusAfterCrewEject);
        LogTimer();
    }

    public static void OnRepairSabotage(PlayerControl pc)
    {
        if (!IsEnable) return;

        Timers[pc.PlayerId] += TimeAfterSaboFix;
        LogTimer(pc.PlayerId);
    }

    public static void OnReport(PlayerControl pc)
    {
        if (!IsEnable) return;

        Timers[pc.PlayerId] += TimeAfterReport;
        LogTimer(pc.PlayerId);
    }

    public static void OnMeetingStart()
    {
        if (!IsEnable) return;

        AdjustTime(TimeAfterMeeting + 9);
        LogTimer();
    }

    public static string GetProgressText(byte playerId)
    {
        return Timers.TryGetValue(playerId, out int x) ? string.Format(GetString("DamoclesTimeLeft"), x) : string.Empty;
    }

    private static void AdjustTime(int change)
    {
        if (!IsEnable) return;

        Timers.AdjustAllValues(x => x + change);
    }
}