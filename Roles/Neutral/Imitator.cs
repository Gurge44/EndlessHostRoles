using AmongUs.GameOptions;
using Hazel;
using System.Collections.Generic;
using System.Linq;

namespace TOHE;

// 来源：https://github.com/Yumenopai/TownOfHost_Y
public static class Imitator
{
    private static readonly int Id = 11950;
    public static List<byte> playerIdList = new();

    private static OptionItem OddKillCooldown;
    private static OptionItem EvenKillCooldown;
    private static OptionItem AfterMeetingKillCooldown;
    public static OptionItem CanVent;
    private static OptionItem HasImpostorVision;

    public static Dictionary<byte, bool> IsOdd = new();

    public static void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Imitator);
        OddKillCooldown = FloatOptionItem.Create(Id + 10, "OddKillCooldown", new(0f, 60f, 2.5f), 27.5f, TabGroup.NeutralRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Imitator])
            .SetValueFormat(OptionFormat.Seconds);
        EvenKillCooldown = FloatOptionItem.Create(Id + 11, "EvenKillCooldown", new(0f, 30f, 2.5f), 15f, TabGroup.NeutralRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Imitator])
            .SetValueFormat(OptionFormat.Seconds);
        AfterMeetingKillCooldown = FloatOptionItem.Create(Id + 12, "AfterMeetingKillCooldown", new(0f, 30f, 2.5f), 22.5f, TabGroup.NeutralRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Imitator])
            .SetValueFormat(OptionFormat.Seconds);
        CanVent = BooleanOptionItem.Create(Id + 13, "CanVent", true, TabGroup.NeutralRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Imitator]);
        HasImpostorVision = BooleanOptionItem.Create(Id + 14, "ImpostorVision", true, TabGroup.NeutralRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Imitator]);
    }
    public static void Init()
    {
        playerIdList = new();
        IsOdd = new();
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        IsOdd.Add(playerId, true);

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }
    public static bool IsEnable() => playerIdList.Any();

    private static void SendRPC(byte playerId)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetImitatorOE, SendOption.Reliable, -1);
        writer.Write(playerId);
        writer.Write(IsOdd[playerId]);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        byte playerId = reader.ReadByte();
        IsOdd[playerId] = reader.ReadBoolean();
    }

    public static void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = OddKillCooldown.GetFloat();
    }
    public static void ApplyGameOptions(IGameOptions opt) => opt.SetVision(HasImpostorVision.GetBool());
    public static void OnReportDeadBody()
    {
        foreach (var pc in Main.AllAlivePlayerControls.Where(x => playerIdList.Contains(x.PlayerId)))
        {
            IsOdd[pc.PlayerId] = true;
            SendRPC(pc.PlayerId);
            Main.AllPlayerKillCooldown[pc.PlayerId] = AfterMeetingKillCooldown.GetFloat();
        }
    }
    public static void OnCheckMurder(PlayerControl killer)
    {
        switch (IsOdd[killer.PlayerId])
        {
            case true:
                Logger.Info($"{killer?.Data?.PlayerName}:奇数击杀冷却", "Imitator");
                Main.AllPlayerKillCooldown[killer.PlayerId] = EvenKillCooldown.GetFloat();
                break;
            case false:
                Logger.Info($"{killer?.Data?.PlayerName}:偶数击杀冷却", "Imitator");
                Main.AllPlayerKillCooldown[killer.PlayerId] = OddKillCooldown.GetFloat();
                break;
        }
        IsOdd[killer.PlayerId] = !IsOdd[killer.PlayerId];
        //RPCによる同期
        SendRPC(killer.PlayerId);
        killer.SyncSettings();//キルクール処理を同期
    }
}