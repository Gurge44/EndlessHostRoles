using System.Collections.Generic;
using UnityEngine;

namespace EHR.Roles.Impostor;

public class SerialKiller : RoleBase
{
    private const int Id = 1700;
    public static List<byte> playerIdList = [];

    private static OptionItem KillCooldown;
    public static OptionItem TimeLimit;
    public static OptionItem WaitFor1Kill;

    private int Timer;
    public float SuicideTimer;

    public static void SetupCustomOption()
    {
        Options.SetupSingleRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.SerialKiller);
        KillCooldown = FloatOptionItem.Create(Id + 10, "KillCooldown", new(0f, 180f, 2.5f), 22.5f, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.SerialKiller])
            .SetValueFormat(OptionFormat.Seconds);
        TimeLimit = FloatOptionItem.Create(Id + 11, "SerialKillerLimit", new(5f, 180f, 5f), 80f, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.SerialKiller])
            .SetValueFormat(OptionFormat.Seconds);
        WaitFor1Kill = BooleanOptionItem.Create(Id + 12, "WaitFor1Kill", true, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.SerialKiller]);
    }

    public override void Init()
    {
        playerIdList = [];
        SuicideTimer = TimeLimit.GetFloat();
        Timer = TimeLimit.GetInt();
    }

    public override void Add(byte serial)
    {
        playerIdList.Add(serial);
        Timer = TimeLimit.GetInt();
        SuicideTimer = TimeLimit.GetFloat();
    }

    public override bool IsEnable => playerIdList.Count > 0;
    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();

    public static bool HasKilled(PlayerControl pc) => pc != null && pc.Is(CustomRoles.SerialKiller) && pc.IsAlive() && (Main.PlayerStates[pc.PlayerId].GetKillCount(true) > 0 || !WaitFor1Kill.GetBool());

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (!killer.Is(CustomRoles.SerialKiller)) return true;
        SuicideTimer = float.NaN;
        Timer = TimeLimit.GetInt();
        killer.MarkDirtySettings();
        return true;
    }

    public override void OnReportDeadBody()
    {
        SuicideTimer = float.NaN;
        Timer = TimeLimit.GetInt();
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!GameStates.IsInTask) return;
        if (!HasKilled(player))
        {
            SuicideTimer = float.NaN;
            Timer = TimeLimit.GetInt();
            return;
        }

        if (float.IsNaN(SuicideTimer)) return;

        if (SuicideTimer >= TimeLimit.GetFloat())
        {
            player.Suicide();
            SuicideTimer = float.NaN;
            Timer = TimeLimit.GetInt();
        }
        else
        {
            SuicideTimer += Time.fixedDeltaTime;
            int tempTimer = Timer;
            Timer = TimeLimit.GetInt() - (int)SuicideTimer;
            if (Timer != tempTimer && Timer <= 20 && !player.IsModClient()) Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player);
        }
    }

    public override void AfterMeetingTasks()
    {
        SuicideTimer = 0f;
        Timer = TimeLimit.GetInt();
    }
}