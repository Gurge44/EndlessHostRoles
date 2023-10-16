using AmongUs.GameOptions;
using System;
using System.Collections.Generic;
using System.Linq;
using static TOHE.Options;

namespace TOHE.Roles.Neutral;

public static class Reckless
{
    private static readonly int Id = 640500;
    public static List<byte> playerIdList = new();

    private static OptionItem DefaultKillCooldown;
    private static OptionItem ReduceKillCooldown;
    private static OptionItem MinKillCooldown;
    public static OptionItem HasImpostorVision;
    public static OptionItem CanVent;

    private static Dictionary<byte, float> NowCooldown;

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Reckless);
        DefaultKillCooldown = FloatOptionItem.Create(Id + 10, "SansDefaultKillCooldown", new(0f, 180f, 2.5f), 22.5f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Reckless])
            .SetValueFormat(OptionFormat.Seconds);
        ReduceKillCooldown = FloatOptionItem.Create(Id + 11, "SansReduceKillCooldown", new(0f, 30f, 0.5f), 5f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Reckless])
            .SetValueFormat(OptionFormat.Seconds);
        MinKillCooldown = FloatOptionItem.Create(Id + 12, "SansMinKillCooldown", new(0f, 30f, 2.5f), 10f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Reckless])
            .SetValueFormat(OptionFormat.Seconds);
        HasImpostorVision = BooleanOptionItem.Create(Id + 13, "ImpostorVision", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Reckless]);
        CanVent = BooleanOptionItem.Create(Id + 14, "CanVent", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Reckless]);
    }
    public static void Init()
    {
        playerIdList = new();
        NowCooldown = new();
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        NowCooldown.TryAdd(playerId, DefaultKillCooldown.GetFloat());

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }
    public static bool IsEnable => playerIdList.Any();
    public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = NowCooldown[id];
    public static void ApplyGameOptions(IGameOptions opt) => opt.SetVision(HasImpostorVision.GetBool());
    public static void OnCheckMurder(PlayerControl killer)
    {
        NowCooldown[killer.PlayerId] = Math.Clamp(NowCooldown[killer.PlayerId] - ReduceKillCooldown.GetFloat(), MinKillCooldown.GetFloat(), DefaultKillCooldown.GetFloat());
        killer.ResetKillCooldown();
        killer.SyncSettings();
    }
    public static void OnReportDeadBody()
    {
        for (int i = 0; i < playerIdList.Count; i++)
        {
            byte id = playerIdList[i];
            NowCooldown[Utils.GetPlayerById(id).PlayerId] = DefaultKillCooldown.GetFloat();
        }
    }
}
