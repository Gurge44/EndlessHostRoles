using System;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using HarmonyLib;
using UnityEngine;

namespace EHR.Roles.Neutral;

public class Tremor : RoleBase
{
    private const int Id = 644600;
    public static bool On;

    private static OptionItem KillCooldown;
    private static OptionItem CanVent;
    private static OptionItem HasImpostorVision;
    private static OptionItem TimerStart;
    private static OptionItem TimerDecrease;
    private static OptionItem DoomTime;
    int Count = 0;
    int DoomTimer;

    long LastUpdate = Utils.TimeStamp;

    int Timer;

    public override bool IsEnable => On;
    public bool IsDoom => Timer <= 0;

    public static void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Tremor);
        KillCooldown = FloatOptionItem.Create(Id + 2, "KillCooldown", new(0f, 180f, 0.5f), 22.5f, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Tremor])
            .SetValueFormat(OptionFormat.Seconds);
        CanVent = BooleanOptionItem.Create(Id + 3, "CanVent", true, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Tremor]);
        HasImpostorVision = BooleanOptionItem.Create(Id + 4, "ImpostorVision", true, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Tremor]);
        TimerStart = IntegerOptionItem.Create(Id + 5, "Tremor.TimerStart", new(0, 600, 5), 180, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Tremor])
            .SetValueFormat(OptionFormat.Seconds);
        TimerDecrease = IntegerOptionItem.Create(Id + 6, "Tremor.TimerDecrease", new(0, 180, 1), 15, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Tremor])
            .SetValueFormat(OptionFormat.Seconds);
        DoomTime = IntegerOptionItem.Create(Id + 7, "Tremor.DoomTime", new(0, 180, 1), 30, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Tremor])
            .SetValueFormat(OptionFormat.Seconds);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        Timer = TimerStart.GetInt() + 8;
        DoomTimer = 0;

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }

    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    public override void ApplyGameOptions(IGameOptions opt, byte id) => opt.SetVision(HasImpostorVision.GetBool());
    public override bool CanUseImpostorVentButton(PlayerControl pc) => CanVent.GetBool();

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!GameStates.IsInTask || !pc.IsAlive()) return;

        bool wasDoom = IsDoom;

        if (!IsDoom && LastUpdate != Utils.TimeStamp)
        {
            Timer--;
            Utils.SendRPC(CustomRPC.SyncTremor, pc.PlayerId, Timer);
            LastUpdate = Utils.TimeStamp;
        }

        if (wasDoom != IsDoom)
        {
            Main.AllAlivePlayerControls.Do(x => x.Notify(Translator.GetString("Tremor.DoomNotify")));
            DoomTimer = DoomTime.GetInt();
        }

        if (IsDoom)
        {
            Count++;

            if (Count % 3 == 0) pc.RpcGuardAndKill();

            if (Count < 15) return;
            Count = 0;

            var pos = pc.Pos();
            Main.AllAlivePlayerControls
                .Where(x => x.PlayerId != pc.PlayerId && Vector2.Distance(pos, x.Pos()) <= 1.5f)
                .Do(pc.Kill);

            if (LastUpdate == Utils.TimeStamp) return;
            LastUpdate = Utils.TimeStamp;

            DoomTimer--;
            Utils.SendRPC(CustomRPC.SyncTremor, pc.PlayerId, DoomTimer);

            if (DoomTimer <= 0) pc.Suicide();
        }
    }

    public override void OnMurder(PlayerControl killer, PlayerControl target)
    {
        Timer = Math.Max(Timer - TimerDecrease.GetInt(), 0);
        Utils.SendRPC(CustomRPC.SyncTremor, killer.PlayerId, Timer);
    }

    public void ReceiveRPC(Hazel.MessageReader reader)
    {
        int value = reader.ReadPackedInt32();
        if (IsDoom) DoomTimer = value;
        else Timer = value;
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != target.PlayerId || (seer.IsModClient() && !hud) || meeting) return string.Empty;
        var color = IsDoom ? Color.yellow : Color.cyan;
        return Utils.ColorString(color, IsDoom ? DoomTimer.ToString() : Timer.ToString());
    }
}