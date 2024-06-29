using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using static EHR.Options;

namespace EHR.Neutral;

public class Eclipse : RoleBase
{
    private const int Id = 648200;
    public static List<byte> playerIdList = [];

    private static OptionItem KillCooldown;
    public static OptionItem CanVent;
    private static OptionItem VisionIncrease;
    private static OptionItem StartVision;
    private static OptionItem MaxVision;

    private float Vision;

    public override bool IsEnable => playerIdList.Count > 0;

    public static void SetupCustomOption()
    {
        SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Eclipse);
        KillCooldown = new FloatOptionItem(Id + 10, "KillCooldown", new(0f, 180f, 0.5f), 22.5f, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Eclipse])
            .SetValueFormat(OptionFormat.Seconds);
        StartVision = new FloatOptionItem(Id + 11, "EclipseStartVision", new(0.1f, 5f, 0.1f), 0.5f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Eclipse])
            .SetValueFormat(OptionFormat.Multiplier);
        VisionIncrease = new FloatOptionItem(Id + 12, "EclipseVisionIncrease", new(0.05f, 5f, 0.05f), 0.1f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Eclipse])
            .SetValueFormat(OptionFormat.Multiplier);
        MaxVision = new FloatOptionItem(Id + 13, "EclipseMaxVision", new(0.25f, 5f, 0.25f), 1.5f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Eclipse])
            .SetValueFormat(OptionFormat.Multiplier);
        CanVent = new BooleanOptionItem(Id + 14, "CanVent", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Eclipse]);
    }

    public override void Init()
    {
        playerIdList = [];
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        Vision = StartVision.GetFloat();
    }

    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    public override bool CanUseImpostorVentButton(PlayerControl pc) => CanVent.GetBool();

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