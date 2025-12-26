using System.Diagnostics;
using EHR.Modules;
using EHR.Modules.Extensions;
using Hazel;

namespace EHR.Impostor;

public class Mercenary : RoleBase
{
    private const int Id = 1700;
    public static bool On;

    private static OptionItem KillCooldown;
    private static OptionItem TimeLimit;
    private static OptionItem WaitFor1Kill;

    private Stopwatch Timer;
    private long LastNotify;
    private byte MercenaryId;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Mercenary);

        KillCooldown = new FloatOptionItem(Id + 10, "KillCooldown", new(0f, 180f, 0.5f), 22.5f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Mercenary])
            .SetValueFormat(OptionFormat.Seconds);

        TimeLimit = new FloatOptionItem(Id + 11, "MercenaryLimit", new(5f, 180f, 5f), 40f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Mercenary])
            .SetValueFormat(OptionFormat.Seconds);

        WaitFor1Kill = new BooleanOptionItem(Id + 12, "WaitFor1Kill", true, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Mercenary]);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte serial)
    {
        On = true;
        Timer = new();
        MercenaryId = serial;
        if (!WaitFor1Kill.GetBool()) LateTask.New(() => Timer.Start(), 10f, log: false);
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        Timer.Restart();
        Utils.SendRPC(CustomRPC.SyncRoleData, MercenaryId, 1);
        return true;
    }

    public override void OnReportDeadBody()
    {
        Timer.Reset();
        Utils.SendRPC(CustomRPC.SyncRoleData, MercenaryId, 2);
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!GameStates.IsInTask || !Main.IntroDestroyed || ExileController.Instance || AntiBlackout.SkipTasks) return;

        if (!Timer.IsRunning || (MeetingStates.FirstMeeting && WaitFor1Kill.GetBool())) return;

        long remainingTime = Timer.GetRemainingTime(TimeLimit.GetInt());

        if (remainingTime <= 0)
        {
            player.Suicide();
            Timer.Reset();

            if (player.AmOwner)
                Achievements.Type.OutOfTime.Complete();
        }
        else
        {
            long now = Utils.TimeStamp;
            if (now == LastNotify || remainingTime > 20) return;
            LastNotify = now;
            Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player);
            Utils.SendRPC(CustomRPC.SyncRoleData, MercenaryId, Timer);
        }
    }

    public override void AfterMeetingTasks()
    {
        Timer.Restart();
        Utils.SendRPC(CustomRPC.SyncRoleData, MercenaryId, 1);
    }

    public void ReceiveRPC(MessageReader reader)
    {
        switch (reader.ReadPackedInt32())
        {
            case 1:
                Timer = Stopwatch.StartNew();
                break;
            case 2:
                Timer.Reset();
                break;
        }
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != MercenaryId || seer.PlayerId != target.PlayerId || (seer.IsModdedClient() && !hud) || meeting) return string.Empty;
        long remainingTime = Timer.GetRemainingTime(TimeLimit.GetInt());
        return !Timer.IsRunning || remainingTime > 20 ? string.Empty : string.Format(Translator.GetString("SerialKillerTimeLeft"), remainingTime - 1);
    }
}