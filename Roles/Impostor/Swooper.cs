using System;
using System.Collections.Generic;
using System.Text;
using AmongUs.GameOptions;
using EHR.Crewmate;
using EHR.Modules;
using EHR.Neutral;
using Hazel;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Impostor;

public class Swooper : RoleBase
{
    private const int Id = 4200;
    private static List<byte> PlayerIdList = [];

    private static OptionItem SwooperCooldown;
    private static OptionItem SwooperDuration;
    private static OptionItem SwooperVentNormallyOnCooldown;
    private static OptionItem SwooperLimitOpt;
    public static OptionItem SwooperAbilityUseGainWithEachKill;
    private int CD;

    private float Cooldown;
    private float Duration;

    private long InvisTime;

    private long lastFixedTime;
    private long lastTime;
    private byte SwooperId;

    private CustomRoles UsedRole;
    private int ventedId;
    private bool VentNormallyOnCooldown;

    public override bool IsEnable => PlayerIdList.Count > 0;

    bool CanGoInvis => GameStates.IsInTask && InvisTime == -10 && lastTime == -10;
    bool IsInvis => InvisTime != -10;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Swooper);
        SwooperCooldown = new FloatOptionItem(Id + 2, "SwooperCooldown", new(1f, 60f, 1f), 20f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Swooper])
            .SetValueFormat(OptionFormat.Seconds);
        SwooperDuration = new FloatOptionItem(Id + 3, "SwooperDuration", new(1f, 30f, 1f), 10f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Swooper])
            .SetValueFormat(OptionFormat.Seconds);
        SwooperVentNormallyOnCooldown = new BooleanOptionItem(Id + 4, "SwooperVentNormallyOnCooldown", true, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Swooper]);
        SwooperLimitOpt = new IntegerOptionItem(Id + 5, "AbilityUseLimit", new(0, 5, 1), 1, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Swooper])
            .SetValueFormat(OptionFormat.Times);
        SwooperAbilityUseGainWithEachKill = new FloatOptionItem(Id + 6, "AbilityUseGainWithEachKill", new(0f, 5f, 0.1f), 0.5f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Swooper])
            .SetValueFormat(OptionFormat.Times);
    }

    public override void Init()
    {
        PlayerIdList = [];
        InvisTime = -10;
        lastTime = -10;
        ventedId = -10;
        CD = 0;
        SwooperId = byte.MaxValue;
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        SwooperId = playerId;

        InvisTime = -10;
        lastTime = -10;
        ventedId = -10;
        CD = 0;

        UsedRole = Main.PlayerStates[playerId].MainRole;

        switch (UsedRole)
        {
            case CustomRoles.Swooper:
                playerId.SetAbilityUseLimit(SwooperLimitOpt.GetInt());
                Cooldown = SwooperCooldown.GetFloat();
                Duration = SwooperDuration.GetFloat();
                VentNormallyOnCooldown = SwooperVentNormallyOnCooldown.GetBool();
                break;
            case CustomRoles.Chameleon:
                playerId.SetAbilityUseLimit(Chameleon.UseLimitOpt.GetInt());
                Cooldown = Chameleon.ChameleonCooldown.GetFloat();
                Duration = Chameleon.ChameleonDuration.GetFloat();
                VentNormallyOnCooldown = true;
                break;
            case CustomRoles.Wraith:
                Cooldown = Wraith.WraithCooldown.GetFloat();
                Duration = Wraith.WraithDuration.GetFloat();
                VentNormallyOnCooldown = Wraith.WraithVentNormallyOnCooldown.GetBool();
                break;
        }
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        try
        {
            if (UsedRole == CustomRoles.Chameleon)
            {
                AURoleOptions.EngineerCooldown = Cooldown;
                AURoleOptions.EngineerInVentMaxTime = Duration;
            }
        }
        catch (Exception e)
        {
            Utils.ThrowException(e);
        }
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return pc.Data.RoleType != RoleTypes.Engineer;
    }

    void SendRPC()
    {
        if (!IsEnable || !Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetSwooperTimer, SendOption.Reliable);
        writer.Write(SwooperId);
        writer.Write(InvisTime.ToString());
        writer.Write(lastTime.ToString());
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public void ReceiveRPC(MessageReader reader)
    {
        InvisTime = long.Parse(reader.ReadString());
        lastTime = long.Parse(reader.ReadString());
    }

    public override void AfterMeetingTasks()
    {
        InvisTime = -10;
        lastTime = Utils.TimeStamp;
        SendRPC();
    }

    private int Count;

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!GameStates.IsInTask || !IsEnable || player == null) return;
        
        if (Count++ < 10) return;
        Count = 0;

        var now = Utils.TimeStamp;

        if (lastTime != -10)
        {
            if (!player.IsModClient())
            {
                var cooldown = lastTime + (long)Cooldown - now;
                if ((int)cooldown != CD) player.Notify(string.Format(GetString("CDPT"), cooldown + 1), 1.1f, overrideAll: true);
                CD = (int)cooldown;
            }

            if (lastTime + (long)Cooldown < now)
            {
                lastTime = -10;
                if (!player.IsModClient()) player.Notify(GetString("SwooperCanVent"), 300f);
                SendRPC();
                CD = 0;
            }
        }

        if (lastFixedTime != now && InvisTime != -10)
        {
            lastFixedTime = now;
            bool refresh = false;
            var remainTime = InvisTime + (long)Duration - now;
            switch (remainTime)
            {
                case < 0:
                    lastTime = now;
                    var pos = player.Pos();
                    player.MyPhysics?.RpcBootFromVent(ventedId == -10 ? Main.LastEnteredVent[player.PlayerId].Id : ventedId);
                    player.Notify(GetString("SwooperInvisStateOut"));
                    InvisTime = -10;
                    SendRPC();
                    refresh = true;
                    LateTask.New(() => { player.TP(pos); }, 0.5f, log: false);
                    break;
                case <= 10 when !player.IsModClient():
                    player.Notify(string.Format(GetString("SwooperInvisStateCountdown"), remainTime + 1), overrideAll: true);
                    break;
            }

            if (refresh) SendRPC();
        }
    }

    public override void OnCoEnterVent(PlayerPhysics __instance, int ventId)
    {
        if (!AmongUsClient.Instance.AmHost || IsInvis) return;

        var pc = __instance.myPlayer;
        LateTask.New(() =>
        {
            float limit = pc.GetAbilityUseLimit();
            bool wraith = UsedRole == CustomRoles.Wraith;
            if (CanGoInvis && (wraith || limit >= 1))
            {
                ventedId = ventId;

                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(__instance.NetId, 34, SendOption.Reliable, pc.GetClientId());
                writer.WritePacked(ventId);
                AmongUsClient.Instance.FinishRpcImmediately(writer);

                InvisTime = Utils.TimeStamp;
                if (!wraith) pc.RpcRemoveAbilityUse();
                SendRPC();
                pc.Notify(GetString("SwooperInvisState"), Duration);
            }
            else if (!VentNormallyOnCooldown)
            {
                __instance.RpcBootFromVent(ventId);
                pc.Notify(GetString("SwooperInvisInCooldown"));
            }
        }, 0.5f, "Swooper Vent");
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        if (!IsInvis || InvisTime == Utils.TimeStamp) return;

        InvisTime = -10;
        lastTime = Utils.TimeStamp;
        SendRPC();

        pc?.MyPhysics?.RpcBootFromVent(vent.Id);
        pc.Notify(GetString("SwooperInvisStateOut"));
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (!hud || seer == null || !GameStates.IsInTask || !PlayerControl.LocalPlayer.IsAlive()) return string.Empty;
        if (Main.PlayerStates[seer.PlayerId].Role is not Swooper sw) return string.Empty;

        var str = new StringBuilder();
        if (sw.IsInvis)
        {
            var remainTime = sw.InvisTime + (long)sw.Duration - Utils.TimeStamp;
            str.Append(string.Format(GetString("SwooperInvisStateCountdown"), remainTime + 1));
        }
        else if (sw.lastTime != -10)
        {
            var cooldown = sw.lastTime + (long)sw.Cooldown - Utils.TimeStamp;
            str.Append(string.Format(GetString("SwooperInvisCooldownRemain"), cooldown + 1));
        }
        else
        {
            str.Append(GetString("SwooperCanVent"));
        }

        return str.ToString();
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (Medic.ProtectList.Contains(target.PlayerId)) return false;
        if (target.Is(CustomRoles.Bait)) return true;

        if (!IsInvis) return true;
        killer.SetKillCooldown();
        target.SetRealKiller(killer);
        target.RpcCheckAndMurder(target);
        return false;
    }
}