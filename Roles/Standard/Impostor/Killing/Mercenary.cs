using EHR.Modules;
using EHR.Modules.Extensions;
using Hazel;

namespace EHR.Roles;

public class Mercenary : RoleBase
{
    private const int Id = 1700;
    public static bool On;

    private static OptionItem KillCooldown;
    private static OptionItem TimeLimit;
    private static OptionItem WaitFor1Kill;

    private CountdownTimer Timer;
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
        MercenaryId = serial;
        Timer = null;
        if (!AmongUsClient.Instance.AmHost || WaitFor1Kill.GetBool()) return;
        StartNewTimer(10);
    }

    private void StartNewTimer(int add = 0)
    {
        Timer?.Dispose();
        Timer = new CountdownTimer(TimeLimit.GetInt() + add, () =>
        {
            Timer = null;
            
            var player = MercenaryId.GetPlayer();
            if (player == null || !player.IsAlive()) return;

            player.Suicide();

            if (player.AmOwner)
                Achievements.Type.OutOfTime.Complete();
        }, onTick: () =>
        {
            if (Timer.Remaining.TotalSeconds > 20) return;

            var player = MercenaryId.GetPlayer();

            if (player == null || !player.IsAlive())
            {
                Timer.Dispose();
                Timer = null;
                Utils.SendRPC(CustomRPC.SyncRoleData, MercenaryId, false);
                return;
            }

            Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player);
        }, onCanceled: () => Timer = null);
        Utils.SendRPC(CustomRPC.SyncRoleData, MercenaryId, true);
    }

    public void ReceiveRPC(MessageReader reader)
    {
        Timer = reader.ReadBoolean() ? new CountdownTimer(TimeLimit.GetInt(), () => Timer = null, onCanceled: () => Timer = null) : null;
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        StartNewTimer();
        return true;
    }

    public override void AfterMeetingTasks()
    {
        StartNewTimer();
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != MercenaryId || seer.PlayerId != target.PlayerId || (seer.IsModdedClient() && !hud) || meeting || Timer == null) return string.Empty;
        long remainingTime = (int)Timer.Remaining.TotalSeconds;
        return remainingTime > 20 ? string.Empty : string.Format(Translator.GetString("SerialKillerTimeLeft"), remainingTime);
    }
}