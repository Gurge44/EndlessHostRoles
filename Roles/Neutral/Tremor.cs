using System;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using UnityEngine;

namespace EHR.Neutral;

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

    private int Count;
    private int DoomTimer;
    private long LastUpdate = Utils.TimeStamp;
    private int Timer;
    private byte TremorId;

    public override bool IsEnable => On;
    public bool IsDoom => Timer <= 0;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Tremor);

        KillCooldown = new FloatOptionItem(Id + 2, "KillCooldown", new(0f, 180f, 0.5f), 22.5f, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Tremor])
            .SetValueFormat(OptionFormat.Seconds);

        CanVent = new BooleanOptionItem(Id + 3, "CanVent", true, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Tremor]);

        HasImpostorVision = new BooleanOptionItem(Id + 4, "ImpostorVision", true, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Tremor]);

        TimerStart = new IntegerOptionItem(Id + 5, "Tremor.TimerStart", new(0, 600, 5), 180, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Tremor])
            .SetValueFormat(OptionFormat.Seconds);

        TimerDecrease = new IntegerOptionItem(Id + 6, "Tremor.TimerDecrease", new(0, 180, 1), 15, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Tremor])
            .SetValueFormat(OptionFormat.Seconds);

        DoomTime = new IntegerOptionItem(Id + 7, "Tremor.DoomTime", new(0, 180, 1), 30, TabGroup.NeutralRoles)
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
        TremorId = playerId;
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        opt.SetVision(HasImpostorVision.GetBool());
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return CanVent.GetBool();
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!GameStates.IsInTask || ExileController.Instance || !pc.IsAlive()) return;

        bool wasDoom = IsDoom;

        if (!IsDoom && LastUpdate != Utils.TimeStamp)
        {
            Timer--;
            Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, Timer);
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

            Vector2 pos = pc.Pos();

            Main.AllAlivePlayerControls
                .Where(x => x.PlayerId != pc.PlayerId && Vector2.Distance(pos, x.Pos()) <= 1.5f)
                .Do(pc.Kill);

            if (LastUpdate == Utils.TimeStamp) return;

            LastUpdate = Utils.TimeStamp;

            DoomTimer--;
            Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, DoomTimer);

            if (DoomTimer <= 0)
            {
                pc.Suicide();

                if (PlayerControl.LocalPlayer.IsAlive())
                    Achievements.Type.Armageddon.Complete();
            }
        }
    }

    public override void OnMurder(PlayerControl killer, PlayerControl target)
    {
        Timer = Math.Max(Timer - TimerDecrease.GetInt(), 0);
        Utils.SendRPC(CustomRPC.SyncRoleData, killer.PlayerId, Timer);
    }

    public void ReceiveRPC(MessageReader reader)
    {
        int value = reader.ReadPackedInt32();

        if (IsDoom)
            DoomTimer = value;
        else
            Timer = value;
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != TremorId || seer.PlayerId != target.PlayerId || (seer.IsModClient() && !hud) || meeting) return string.Empty;

        Color color = IsDoom ? Color.yellow : Color.cyan;
        string text = IsDoom ? DoomTimer.ToString() : Timer.ToString();
        if (hud) text = $"<size=130%><b>{text}</b></size>";

        return Utils.ColorString(color, text);
    }
}