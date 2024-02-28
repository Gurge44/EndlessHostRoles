using AmongUs.GameOptions;
using Hazel;
using System;
using System.Collections.Generic;
using System.Text;
using TOHE.Roles.Crewmate;
using TOHE.Roles.Neutral;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Impostor;

public class Swooper : RoleBase
{
    private const int Id = 4200;
    private static List<byte> playerIdList = [];

    public static OptionItem SwooperCooldown;
    private static OptionItem SwooperDuration;
    private static OptionItem SwooperVentNormallyOnCooldown;
    private static OptionItem SwooperLimitOpt;
    public static OptionItem SwooperAbilityUseGainWithEachKill;

    private float Cooldown;
    private float Duration;
    private bool VentNormallyOnCooldown;

    private CustomRoles UsedRole;

    private long InvisTime;
    public long lastTime;
    private int ventedId;
    private int CD;
    private byte SwooperId;

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Swooper);
        SwooperCooldown = FloatOptionItem.Create(Id + 2, "SwooperCooldown", new(1f, 60f, 1f), 20f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Swooper])
            .SetValueFormat(OptionFormat.Seconds);
        SwooperDuration = FloatOptionItem.Create(Id + 3, "SwooperDuration", new(1f, 30f, 1f), 10f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Swooper])
            .SetValueFormat(OptionFormat.Seconds);
        SwooperVentNormallyOnCooldown = BooleanOptionItem.Create(Id + 4, "SwooperVentNormallyOnCooldown", true, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Swooper]);
        SwooperLimitOpt = IntegerOptionItem.Create(Id + 5, "AbilityUseLimit", new(0, 5, 1), 1, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Swooper])
            .SetValueFormat(OptionFormat.Times);
        SwooperAbilityUseGainWithEachKill = FloatOptionItem.Create(Id + 6, "AbilityUseGainWithEachKill", new(0f, 5f, 0.1f), 0.5f, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Swooper])
            .SetValueFormat(OptionFormat.Times);
    }

    public override void Init()
    {
        playerIdList = [];
        InvisTime = -10;
        lastTime = -10;
        ventedId = -10;
        CD = 0;
        SwooperId = byte.MaxValue;
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        SwooperId = playerId;

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

        if (!AmongUsClient.Instance.AmHost || UsedRole == CustomRoles.Swooper) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
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

    public override bool IsEnable => playerIdList.Count > 0;

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

    bool CanGoInvis => GameStates.IsInTask && InvisTime == -10 && lastTime == -10;
    bool IsInvis => InvisTime != -10;

    private long lastFixedTime;

    public override void AfterMeetingTasks()
    {
        InvisTime = -10;
        lastTime = Utils.TimeStamp;
        SendRPC();
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!GameStates.IsInTask || !IsEnable || player == null) return;

        var now = Utils.TimeStamp;

        if (lastTime != -10 && !player.IsModClient())
        {
            var cooldown = lastTime + (long)Cooldown - now;
            if ((int)cooldown != CD) player.Notify(string.Format(GetString("CDPT"), cooldown + 1), 1.1f);
            CD = (int)cooldown;
        }

        if (lastTime + (long)Cooldown < now)
        {
            lastTime = -10;
            if (!player.IsModClient()) player.Notify(GetString("SwooperCanVent"));
            SendRPC();
            CD = 0;
        }

        if (lastFixedTime != now)
        {
            lastFixedTime = now;
            bool refresh = false;
            var remainTime = InvisTime + (long)Duration - now;
            switch (remainTime)
            {
                case < 0:
                    lastTime = now;
                    player.MyPhysics?.RpcBootFromVent(ventedId == -10 ? Main.LastEnteredVent[player.PlayerId].Id : ventedId);
                    player.Notify(GetString("SwooperInvisStateOut"));
                    SendRPC();
                    refresh = true;
                    break;
                case <= 10 when !player.IsModClient():
                    player.Notify(string.Format(GetString("SwooperInvisStateCountdown"), remainTime + 1));
                    break;
            }

            if (refresh) SendRPC();
        }
    }

    public override void OnCoEnterVent(PlayerPhysics __instance, int ventId)
    {
        if (!AmongUsClient.Instance.AmHost || IsInvis) return;

        var pc = __instance.myPlayer;
        _ = new LateTask(() =>
        {
            if (CanGoInvis && pc.GetAbilityUseLimit() >= 1)
            {
                ventedId = ventId;

                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(__instance.NetId, 34, SendOption.Reliable, pc.GetClientId());
                writer.WritePacked(ventId);
                AmongUsClient.Instance.FinishRpcImmediately(writer);

                InvisTime = Utils.TimeStamp;
                pc.RpcRemoveAbilityUse();
                SendRPC();
                pc.Notify(GetString("SwooperInvisState"), Duration);
            }
            else
            {
                if (!VentNormallyOnCooldown)
                {
                    __instance.RpcBootFromVent(vent.Id);
                    pc.Notify(GetString("SwooperInvisInCooldown"));
                }
            }
        }, 0.5f, "Swooper Vent");
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        if (!IsInvis) return;

        InvisTime = -10;
        lastTime = Utils.TimeStamp;
        SendRPC();

        pc?.MyPhysics?.RpcBootFromVent(vent.Id);
        pc.Notify(GetString("SwooperInvisStateOut"));
    }

    public static string GetHudText(PlayerControl pc)
    {
        if (pc == null || !GameStates.IsInTask || !PlayerControl.LocalPlayer.IsAlive()) return string.Empty;
        if (Main.PlayerStates[pc.PlayerId].Role is not Swooper sw) return string.Empty;

        var str = new StringBuilder();
        if (sw.IsInvis)
        {
            var remainTime = sw.InvisTime + (long)sw.Duration - Utils.TimeStamp;
            str.Append(string.Format(GetString("SwooperInvisStateCountdown"), remainTime + 1));
        }
        else if (sw.lastTime != -10)
        {
            var cooldown = sw.lastTime + (long)sw.Cooldown - Utils.TimeStamp;
            str.Append(string.Format(GetString("SwooperInvisCooldownRemain"), cooldown + 2));
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