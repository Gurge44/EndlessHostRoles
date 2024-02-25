using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using static TOHE.Options;

namespace TOHE.Roles.Neutral;

public class Eclipse : RoleBase
{
    private const int Id = 128000;
    public static List<byte> playerIdList = [];

    private static OptionItem KillCooldown;
    public static OptionItem CanVent;
    private static OptionItem VisionIncrease;
    private static OptionItem StartVision;
    private static OptionItem MaxVision;

    private float Vision;

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

    public override void Init()
    {
        playerIdList = [];
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        Vision = StartVision.GetFloat();

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }

    public override bool IsEnable => playerIdList.Count > 0;
    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        opt.SetVision(true);
        opt.SetFloat(FloatOptionNames.CrewLightMod, Vision);
        opt.SetFloat(FloatOptionNames.ImpostorLightMod, Vision);
    }

    public override void OnMurder(PlayerControl pc, PlayerControl target)
    {
        if (pc == null) return;

        float currentVision = Vision;
        Vision += VisionIncrease.GetFloat();
        if (Vision > MaxVision.GetFloat()) Vision = MaxVision.GetFloat();

        if (Math.Abs(Vision - currentVision) > 0.1f)
        {
            pc.MarkDirtySettings();
        }
    }
}
