using System.Collections.Generic;
using AmongUs.GameOptions;
using static TOHE.Options;

namespace TOHE.Roles.Neutral;

public static class NSerialKiller
{
    private static readonly int Id = 12800;
    public static List<byte> playerIdList = [];

    private static OptionItem KillCooldown;
    public static OptionItem CanVent;
    private static OptionItem HasImpostorVision;

    public static void SetupCustomOption()
    {
        SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.NSerialKiller, 1, zeroOne: false);
        KillCooldown = FloatOptionItem.Create(Id + 10, "KillCooldown", new(0f, 180f, 2.5f), 22.5f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.NSerialKiller])
            .SetValueFormat(OptionFormat.Seconds);
        CanVent = BooleanOptionItem.Create(Id + 11, "CanVent", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.NSerialKiller]);
        HasImpostorVision = BooleanOptionItem.Create(Id + 13, "ImpostorVision", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.NSerialKiller]);
    }
    public static void Init()
    {
        playerIdList = [];
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }
    public static bool IsEnable => playerIdList.Count > 0;
    public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    public static void ApplyGameOptions(IGameOptions opt) => opt.SetVision(HasImpostorVision.GetBool());
}
