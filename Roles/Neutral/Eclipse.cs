using AmongUs.GameOptions;
using Hazel;
using System.Collections.Generic;
using System.Linq;
using static TOHE.Options;

namespace TOHE.Roles.Neutral;

public static class Eclipse
{
    private static readonly int Id = 128000;
    public static List<byte> playerIdList = [];

    private static OptionItem KillCooldown;
    public static OptionItem CanVent;
    private static OptionItem VisionIncrease;
    private static OptionItem StartVision;
    private static OptionItem MaxVision;

    private static float Vision;

    public static void SetupCustomOption()
    {
        SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Eclipse, 1, zeroOne: false);
        KillCooldown = FloatOptionItem.Create(Id + 10, "KillCooldown", new(0f, 180f, 2.5f), 22.5f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Eclipse])
            .SetValueFormat(OptionFormat.Seconds);
        StartVision = FloatOptionItem.Create(Id + 11, "EclipseStartVision", new(0.1f, 5f, 0.1f), 0.5f, TabGroup.NeutralRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Eclipse])
            .SetValueFormat(OptionFormat.Multiplier);
        VisionIncrease = FloatOptionItem.Create(Id + 12, "EclipseVisionIncrease", new(0.05f, 5f, 0.05f), 0.1f, TabGroup.NeutralRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Eclipse])
            .SetValueFormat(OptionFormat.Multiplier);
        MaxVision = FloatOptionItem.Create(Id + 13, "EclipseMaxVision", new(0.25f, 5f, 0.25f), 1.5f, TabGroup.NeutralRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Eclipse])
            .SetValueFormat(OptionFormat.Multiplier);
        CanVent = BooleanOptionItem.Create(Id + 14, "CanVent", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Eclipse]);
    }
    public static void Init()
    {
        playerIdList = [];
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        Vision = StartVision.GetFloat();
        _ = new LateTask(() => { SendRPC(Vision); }, 8f, "Eclipse Set Vision RPC");

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }
    public static bool IsEnable => playerIdList.Count > 0;
    public static void SendRPC(float vision)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetEclipseVision, SendOption.Reliable, -1);
        writer.Write(vision);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ReceiveRPC(MessageReader reader)
    {
        if (AmongUsClient.Instance.AmHost) return;

        Vision = reader.ReadSingle();
    }
    public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    public static void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetVision(true);
        opt.SetFloat(FloatOptionNames.CrewLightMod, Vision);
        opt.SetFloat(FloatOptionNames.ImpostorLightMod, Vision);
    }
    public static void OnCheckMurder(PlayerControl pc)
    {
        if (pc == null) return;

        float currentVision = Vision;
        Vision += VisionIncrease.GetFloat();
        if (Vision > MaxVision.GetFloat()) Vision = MaxVision.GetFloat();

        if (Vision != currentVision)
        {
            SendRPC(Vision);
            pc.SyncSettings();
        }
    }
}
