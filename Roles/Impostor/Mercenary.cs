using System.Collections.Generic;
using EHR.Modules;
using Hazel;
using UnityEngine;

namespace EHR.Impostor;

public class Mercenary : RoleBase
{
    private const int Id = 1700;
    public static List<byte> PlayerIdList = [];

    private static OptionItem KillCooldown;
    private static OptionItem TimeLimit;
    private static OptionItem WaitFor1Kill;

    private float SuicideTimer;
    private int Timer;
    private byte MercenaryId;

    public override bool IsEnable => PlayerIdList.Count > 0;

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
        PlayerIdList = [];
        SuicideTimer = 0f;
        Timer = TimeLimit.GetInt();
    }

    public override void Add(byte serial)
    {
        PlayerIdList.Add(serial);
        Timer = TimeLimit.GetInt();
        SuicideTimer = 0f;
        MercenaryId = serial;
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
        return pc != null && pc.Is(CustomRoles.Mercenary) && pc.IsAlive() && (Main.PlayerStates[pc.PlayerId].GetKillCount(true) > 0 || !WaitFor1Kill.GetBool());
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (!killer.Is(CustomRoles.Mercenary)) return true;

        SuicideTimer = 0f;
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
        if (!GameStates.IsInTask || !Main.IntroDestroyed || ExileController.Instance || AntiBlackout.SkipTasks) return;

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

            if (player.AmOwner)
                Achievements.Type.OutOfTime.Complete();
        }
        else
        {
            SuicideTimer += Time.fixedDeltaTime;
            int tempTimer = Timer;
            Timer = TimeLimit.GetInt() - (int)SuicideTimer;
            
            if (Timer != tempTimer && Timer <= 20 && !player.IsModdedClient())
            {
                Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player);
                Utils.SendRPC(CustomRPC.SyncRoleData, MercenaryId, Timer);
            }
        }
    }

    public override void AfterMeetingTasks()
    {
        SuicideTimer = 0f;
        Timer = TimeLimit.GetInt();
    }

    public void ReceiveRPC(MessageReader reader)
    {
        Timer = reader.ReadPackedInt32();
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != MercenaryId || seer.PlayerId != target.PlayerId || (seer.IsModdedClient() && !hud) || meeting || Timer - 1 > 20) return string.Empty;
        return string.Format(Translator.GetString("SerialKillerTimeLeft"), Timer - 1);
    }
}