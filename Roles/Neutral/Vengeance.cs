using AmongUs.GameOptions;
using Hazel;
using System.Collections.Generic;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Neutral;

public static class Vengeance
{
    private static readonly int Id = 12820;
    public static List<byte> playerIdList = [];

    private static OptionItem KillCooldown;
    public static OptionItem CanVent;
    private static OptionItem HasImpostorVision;
    private static OptionItem RevengeTime;

    private static bool IsRevenge;
    private static int Timer;
    private static bool Success;
    private static byte Killer;
    private static float tempKillTimer;

    public static void SetupCustomOption()
    {
        SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Vengeance, 1, zeroOne: false);
        KillCooldown = FloatOptionItem.Create(Id + 10, "KillCooldown", new(0f, 180f, 2.5f), 22.5f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Vengeance])
            .SetValueFormat(OptionFormat.Seconds);
        RevengeTime = IntegerOptionItem.Create(Id + 11, "VengeanceRevengeTime", new(0, 30, 1), 15, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Vengeance])
            .SetValueFormat(OptionFormat.Seconds);
        CanVent = BooleanOptionItem.Create(Id + 12, "CanVent", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Vengeance]);
        HasImpostorVision = BooleanOptionItem.Create(Id + 13, "ImpostorVision", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Vengeance]);
    }
    public static void Init()
    {
        playerIdList = [];
        IsRevenge = false;
        Success = false;
        Killer = byte.MaxValue;
        tempKillTimer = 0;
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        Timer = RevengeTime.GetInt();
        _ = new LateTask(() => { SendRPCSyncTimer(Timer); }, 8f, "Vengeance Set Timer RPC");

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }
    public static bool IsEnable => playerIdList.Count > 0;
    public static void SendRPCSyncTimer(int timer)
    {
        if (!IsEnable || !Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncVengeanceTimer, SendOption.Reliable, -1);
        writer.Write(timer);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ReceiveRPCSyncTimer(MessageReader reader)
    {
        if (AmongUsClient.Instance.AmHost) return;

        Timer = reader.ReadInt32();
    }
    public static void SendRPC(bool isRevenge, bool success, byte killer, float tempKillTimer)
    {
        if (!IsEnable || !Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncVengeanceData, SendOption.Reliable, -1);
        writer.Write(isRevenge);
        writer.Write(success);
        writer.Write(killer);
        writer.Write(tempKillTimer);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ReceiveRPC(MessageReader reader)
    {
        if (AmongUsClient.Instance.AmHost) return;

        IsRevenge = reader.ReadBoolean();
        Success = reader.ReadBoolean();
        Killer = reader.ReadByte();
        tempKillTimer = reader.ReadSingle();
    }
    public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    public static void ApplyGameOptions(IGameOptions opt) => opt.SetVision(HasImpostorVision.GetBool());
    public static bool OnKillAttempt(PlayerControl killer, PlayerControl target)
    {
        if (killer.PlayerId == target.PlayerId) return true;
        if (killer == null) return false;
        if (target == null) return false;
        if (IsRevenge) return true;

        _ = new LateTask(() => { target.TPtoRndVent(); }, 0.01f);

        Timer = RevengeTime.GetInt();
        Countdown(Timer, target);
        IsRevenge = true;
        killer.SetKillCooldown();
        tempKillTimer = Main.KillTimers[target.PlayerId];
        target.SetKillCooldown(time: 1f);
        Killer = killer.PlayerId;

        SendRPC(IsRevenge, Success, Killer, tempKillTimer);

        return false;
    }
    public static void Countdown(int seconds, PlayerControl player)
    {
        if (!player.IsAlive()) return;
        if (Success)
        {
            Timer = RevengeTime.GetInt();
            Success = false;
            SendRPCSyncTimer(Timer);
            SendRPC(IsRevenge, Success, Killer, tempKillTimer);
            return;
        }
        if ((seconds <= 0 || GameStates.IsMeeting) && player.IsAlive()) { player.Kill(player); return; }
        player.Notify(string.Format(GetString("VengeanceRevenge"), seconds), 1.1f);
        Timer = seconds;
        SendRPCSyncTimer(Timer);

        _ = new LateTask(() => { Countdown(seconds - 1, player); }, 1.01f);
    }
    public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer == null) return false;
        if (target == null) return false;

        if (!IsRevenge) return true;
        else if (target.PlayerId == Killer)
        {
            Success = true;
            killer.Notify(GetString("VengeanceSuccess"));
            killer.SetKillCooldown(KillCooldown.GetFloat() + tempKillTimer);
            IsRevenge = false;
            SendRPC(IsRevenge, Success, Killer, tempKillTimer);
            return true;
        }
        else
        {
            killer.Kill(killer);
            return false;
        }
    }
}
