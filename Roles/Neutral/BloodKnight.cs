using System.Collections.Generic;
using System.Text;
using AmongUs.GameOptions;
using Hazel;
using static TOHE.Options;

namespace TOHE.Roles.Neutral;

public static class BloodKnight
{
    private static readonly int Id = 11800;
    public static List<byte> playerIdList = [];

    private static OptionItem KillCooldown;
    public static OptionItem CanVent;
    private static OptionItem HasImpostorVision;
    private static OptionItem ProtectDuration;

    private static Dictionary<byte, long> TimeStamp = [];

    public static void SetupCustomOption()
    {
        SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.BloodKnight, 1, zeroOne: false);
        KillCooldown = FloatOptionItem.Create(Id + 10, "KillCooldown", new(0f, 180f, 2.5f), 22.5f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.BloodKnight])
            .SetValueFormat(OptionFormat.Seconds);
        CanVent = BooleanOptionItem.Create(Id + 11, "CanVent", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.BloodKnight]);
        HasImpostorVision = BooleanOptionItem.Create(Id + 13, "ImpostorVision", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.BloodKnight]);
        ProtectDuration = FloatOptionItem.Create(Id + 14, "BKProtectDuration", new(1f, 30f, 1f), 15f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.BloodKnight])
            .SetValueFormat(OptionFormat.Seconds);
    }
    public static void Init()
    {
        playerIdList = [];
        TimeStamp = [];
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        TimeStamp.TryAdd(playerId, 0);

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }
    public static bool IsEnable => playerIdList.Count > 0;
    private static void SendRPC(byte playerId)
    {
        if (!IsEnable || !Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetBKTimer, SendOption.Reliable);
        writer.Write(playerId);
        writer.Write(TimeStamp[playerId].ToString());
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ReceiveRPC(MessageReader reader)
    {
        byte PlayerId = reader.ReadByte();
        string Time = reader.ReadString();
        TimeStamp.TryAdd(PlayerId, long.Parse(Time));
        TimeStamp[PlayerId] = long.Parse(Time);
    }
    public static bool InProtect(byte playerId) => TimeStamp.TryGetValue(playerId, out var time) && time > Utils.TimeStamp;
    public static void OnMurderPlayer(PlayerControl killer, PlayerControl target)
    {
        if (killer.PlayerId == target.PlayerId) return;
        TimeStamp[killer.PlayerId] = Utils.TimeStamp + (long)ProtectDuration.GetFloat();
        SendRPC(killer.PlayerId);
        killer.Notify(Translator.GetString("BKInProtect"));
    }
    public static void OnFixedUpdate(PlayerControl pc)
    {
        if (!GameStates.IsInTask || !pc.Is(CustomRoles.BloodKnight)) return;
        if (TimeStamp[pc.PlayerId] < Utils.TimeStamp && TimeStamp[pc.PlayerId] != 0)
        {
            TimeStamp[pc.PlayerId] = 0;
            pc.Notify(Translator.GetString("BKProtectOut"));
        }
    }
    public static string GetHudText(PlayerControl pc)
    {
        if (pc == null || !GameStates.IsInTask || !PlayerControl.LocalPlayer.IsAlive()) return string.Empty;
        var str = new StringBuilder();
        if (InProtect(pc.PlayerId))
        {
            var remainTime = TimeStamp[pc.PlayerId] - Utils.TimeStamp;
            str.Append(string.Format(Translator.GetString("BKSkillTimeRemain"), remainTime));
        }
        else
        {
            str.Append(Translator.GetString("BKSkillNotice"));
        }
        return str.ToString();
    }

    public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    public static void ApplyGameOptions(IGameOptions opt) => opt.SetVision(HasImpostorVision.GetBool());
}