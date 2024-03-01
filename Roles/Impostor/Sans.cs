using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using TOHE.Roles.Neutral;
using static TOHE.Options;

namespace TOHE.Roles.Impostor;

public class Sans : RoleBase
{
    private const int Id = 600;
    public static List<byte> playerIdList = [];

    private static OptionItem DefaultKillCooldown;
    private static OptionItem ReduceKillCooldown;
    private static OptionItem MinKillCooldown;
    public static OptionItem BardChance;

    private float DefaultKCD;
    private float ReduceKCD;
    private float MinKCD;
    private bool ResetKCDOnMeeting;
    private bool HasImpostorVision;
    private bool CanVent;

    private CustomRoles UsedRole;

    private float NowCooldown;

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Sans);
        DefaultKillCooldown = FloatOptionItem.Create(Id + 10, "SansDefaultKillCooldown", new(0f, 180f, 2.5f), 30f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Sans])
            .SetValueFormat(OptionFormat.Seconds);
        ReduceKillCooldown = FloatOptionItem.Create(Id + 11, "SansReduceKillCooldown", new(0f, 30f, 0.5f), 3.5f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Sans])
            .SetValueFormat(OptionFormat.Seconds);
        MinKillCooldown = FloatOptionItem.Create(Id + 12, "SansMinKillCooldown", new(0f, 30f, 2.5f), 10f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Sans])
            .SetValueFormat(OptionFormat.Seconds);
        BardChance = IntegerOptionItem.Create(Id + 13, "BardChance", new(0, 100, 5), 0, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Sans])
            .SetValueFormat(OptionFormat.Percent);
    }

    public override void Init()
    {
        playerIdList = [];
        NowCooldown = DefaultKillCooldown.GetFloat();
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        NowCooldown = DefaultKillCooldown.GetFloat();

        UsedRole = Main.PlayerStates[playerId].MainRole;

        switch (UsedRole)
        {
            case CustomRoles.Sans:
                DefaultKCD = DefaultKillCooldown.GetFloat();
                ReduceKCD = ReduceKillCooldown.GetFloat();
                MinKCD = MinKillCooldown.GetFloat();
                ResetKCDOnMeeting = false;
                HasImpostorVision = true;
                CanVent = true;
                break;
            case CustomRoles.Juggernaut:
                DefaultKCD = Juggernaut.DefaultKillCooldown.GetFloat();
                ReduceKCD = Juggernaut.ReduceKillCooldown.GetFloat();
                MinKCD = Juggernaut.MinKillCooldown.GetFloat();
                ResetKCDOnMeeting = false;
                HasImpostorVision = Juggernaut.HasImpostorVision.GetBool();
                CanVent = Juggernaut.CanVent.GetBool();
                break;
            case CustomRoles.Reckless:
                DefaultKCD = Reckless.DefaultKillCooldown.GetFloat();
                ReduceKCD = Reckless.ReduceKillCooldown.GetFloat();
                MinKCD = Reckless.MinKillCooldown.GetFloat();
                ResetKCDOnMeeting = true;
                HasImpostorVision = Reckless.HasImpostorVision.GetBool();
                CanVent = Reckless.CanVent.GetBool();
                break;
        }

        if (!AmongUsClient.Instance.AmHost || UsedRole == CustomRoles.Sans) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }

    public override bool IsEnable => playerIdList.Count > 0;
    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = NowCooldown;

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        opt.SetVision(HasImpostorVision);
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return CanVent;
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        NowCooldown = Math.Clamp(NowCooldown - ReduceKCD, MinKCD, DefaultKCD);
        killer?.ResetKillCooldown();
        killer?.SyncSettings();
        return base.OnCheckMurder(killer, target);
    }

    public override void OnReportDeadBody()
    {
        if (!ResetKCDOnMeeting) return;
        NowCooldown = DefaultKCD;
    }
}