using System.Collections.Generic;
using EHR.Modules;

namespace EHR.Impostor;

public class SerialKiller : RoleBase
{
    private const int Id = 1700;
    public static List<byte> PlayerIdList = [];

    private static OptionItem KillCooldown;
    private static OptionItem TimeLimit;
    private static OptionItem WaitFor1Kill;

    private int Timer;
    private long LastUpdate;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.SerialKiller);

        KillCooldown = new FloatOptionItem(Id + 10, "KillCooldown", new(0f, 180f, 0.5f), 22.5f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.SerialKiller])
            .SetValueFormat(OptionFormat.Seconds);

        TimeLimit = new FloatOptionItem(Id + 11, "SerialKillerLimit", new(5f, 180f, 5f), 40f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.SerialKiller])
            .SetValueFormat(OptionFormat.Seconds);

        WaitFor1Kill = new BooleanOptionItem(Id + 12, "WaitFor1Kill", true, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.SerialKiller]);
    }

    public override void Init()
    {
        PlayerIdList = [];
        Timer = TimeLimit.GetInt();
    }

    public override void Add(byte serial)
    {
        PlayerIdList.Add(serial);
        Timer = TimeLimit.GetInt();
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    private static bool HasKilled(PlayerControl pc)
    {
        return pc != null && pc.Is(CustomRoles.SerialKiller) && pc.IsAlive() && (Main.PlayerStates[pc.PlayerId].GetKillCount(true) > 0 || !WaitFor1Kill.GetBool());
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (!killer.Is(CustomRoles.SerialKiller)) return true;

        Timer = TimeLimit.GetInt();
        killer.MarkDirtySettings();
        return true;
    }

    public override void OnReportDeadBody()
    {
        Timer = TimeLimit.GetInt();
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!GameStates.IsInTask || ExileController.Instance || AntiBlackout.SkipTasks || !player.IsAlive()) return;

        long now = Utils.TimeStamp;
        if (now == LastUpdate) return;
        LastUpdate = now;

        if (!HasKilled(player))
        {
            Timer = int.MaxValue;
            return;
        }

        if (Timer < 0)
        {
            player.Suicide();
            Timer = int.MaxValue;

            if (player.IsLocalPlayer())
                Achievements.Type.OutOfTime.Complete();
        }
        else
        {
            Timer--;

            if (Timer <= 20)
                player.Notify(string.Format(Translator.GetString("SerialKillerTimeLeft"), Timer), 3f, true, false);
        }
    }

    public override void AfterMeetingTasks()
    {
        Timer = TimeLimit.GetInt();
    }
}