using EHR.Modules;
using Hazel;

namespace EHR.Crewmate;

public class Adrenaline : RoleBase
{
    public static bool On;

    private static OptionItem Time;
    private static OptionItem MaxSurvives;
    private static OptionItem MinTasksRequired;
    private static OptionItem CanCallMeetingDuringTimer;
    private static OptionItem SpeedIncreaseDuringTimer;
    public static OptionItem AdrenalineAbilityUseGainWithEachTaskCompleted;
    public static OptionItem AbilityChargesWhenFinishedTasks;
    
    private byte AdrenalineId;
    private float DefaultSpeed;
    private long LastUpdate;
    private int Timer;
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        var id = 648300;
        Options.SetupRoleOptions(id++, TabGroup.CrewmateRoles, CustomRoles.Adrenaline);

        Time = new IntegerOptionItem(++id, "Adrenaline.Time", new(1, 120, 1), 15, TabGroup.CrewmateRoles)
            .SetValueFormat(OptionFormat.Seconds)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Adrenaline]);

        MaxSurvives = new IntegerOptionItem(++id, "Adrenaline.MaxSurvives", new(1, 5, 1), 1, TabGroup.CrewmateRoles)
            .SetValueFormat(OptionFormat.Times)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Adrenaline]);

        MinTasksRequired = new IntegerOptionItem(++id, "Adrenaline.MinTasksRequired", new(1, 5, 1), 1, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Adrenaline]);

        CanCallMeetingDuringTimer = new BooleanOptionItem(++id, "Adrenaline.CanCallMeetingDuringTimer", true, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Adrenaline]);

        SpeedIncreaseDuringTimer = new FloatOptionItem(++id, "Adrenaline.SpeedDuringTimer", new(0f, 3f, 0.05f), 0.5f, TabGroup.CrewmateRoles)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Adrenaline]);
        
        AdrenalineAbilityUseGainWithEachTaskCompleted = new FloatOptionItem(++id, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 0.4f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Adrenaline])
            .SetValueFormat(OptionFormat.Times);
        AbilityChargesWhenFinishedTasks = new FloatOptionItem(++id, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Adrenaline])
            .SetValueFormat(OptionFormat.Times);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        AdrenalineId = playerId;
        Timer = 0;
        Utils.SendRPC(CustomRPC.SyncRoleData, playerId, Timer);
        DefaultSpeed = Main.AllPlayerSpeed[playerId];
        playerId.SetAbilityUseLimit(MaxSurvives.GetFloat());
    }

    public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
    {
        if (!base.OnCheckMurderAsTarget(killer, target) || target.GetAbilityUseLimit() < 1 || target.GetTaskState().CompletedTasksCount < MinTasksRequired.GetInt()) return true;

        target.RpcRemoveAbilityUse();
        Timer = Time.GetInt();
        Utils.SendRPC(CustomRPC.SyncRoleData, target.PlayerId, Timer);
        Main.AllPlayerSpeed[target.PlayerId] += SpeedIncreaseDuringTimer.GetFloat();
        target.MarkDirtySettings();
        return false;
    }

    public override void OnReportDeadBody()
    {
        Timer = 0;
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (Timer == 0 || !pc.IsAlive()) return;

        if (GameStates.IsMeeting)
        {
            Timer = 0;
            Main.AllPlayerSpeed[pc.PlayerId] = DefaultSpeed;
            pc.MarkDirtySettings();
            return;
        }

        long now = Utils.TimeStamp;
        if (now == LastUpdate) return;
        LastUpdate = now;

        Timer--;

        if (Timer <= 0)
        {
            Timer = 0;
            pc.Suicide();

            if (pc.AmOwner)
                Achievements.Type.OutOfTime.Complete();
        }

        Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, Timer);
        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
    }

    public static bool CanCallMeeting(PlayerControl pc)
    {
        return Main.PlayerStates[pc.PlayerId].Role is Adrenaline an && (an.Timer == 0 || CanCallMeetingDuringTimer.GetBool());
    }

    public void ReceiveRPC(MessageReader reader)
    {
        Timer = reader.ReadPackedInt32();
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != target.PlayerId || seer.PlayerId != AdrenalineId || meeting || (seer.IsModdedClient() && !hud) || Timer == 0) return string.Empty;

        return string.Format(Translator.GetString("Adrenaline.Suffix"), Timer);
    }
}