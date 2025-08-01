﻿using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;

namespace EHR.Neutral;

public class Tank : RoleBase
{
    public static bool On;

    private static OptionItem Speed;
    public static OptionItem CanBeGuessed;
    private static OptionItem CanBeKilled;
    private static OptionItem VentCooldown;

    private static HashSet<int> AllVents = [];
    private HashSet<int> EnteredVents;
    private byte TankId;

    public override bool IsEnable => On;
    public bool IsWon => EnteredVents.Count >= AllVents.Count;

    public override void SetupCustomOption()
    {
        StartSetup(646950)
            .AutoSetupOption(ref Speed, 0.9f, new FloatValueRule(0.1f, 3f, 0.1f), OptionFormat.Multiplier)
            .AutoSetupOption(ref CanBeGuessed, false)
            .AutoSetupOption(ref CanBeKilled, true)
            .AutoSetupOption(ref VentCooldown, 15f, new FloatValueRule(0f, 60f, 0.5f), OptionFormat.Seconds);
    }

    public override void Init()
    {
        On = false;
        if (ShipStatus.Instance == null) return;

        AllVents = ShipStatus.Instance.AllVents.Select(x => x.Id).ToHashSet();
    }

    public override void Add(byte playerId)
    {
        On = true;
        TankId = playerId;
        EnteredVents = [];
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        AURoleOptions.EngineerCooldown = VentCooldown.GetFloat();
        AURoleOptions.EngineerInVentMaxTime = 0.3f;
        Main.AllPlayerSpeed[playerId] = Speed.GetFloat();
    }

    public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
    {
        return CanBeKilled.GetBool();
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        EnteredVents.Add(vent.Id);
        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
    }

    public override string GetProgressText(byte playerId, bool comms)
    {
        var progress = $"{Utils.ColorString(Utils.GetRoleColor(CustomRoles.Tank), $"{EnteredVents.Count}")}/{AllVents.Count}";
        if (IsWon) progress = $"<#00ff00>{progress}</color>";

        return base.GetProgressText(playerId, comms) + progress;
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != target.PlayerId || seer.PlayerId != TankId || meeting || (seer.IsModdedClient() && !hud) || IsWon) return string.Empty;

        string randomVentName = ShipStatus.Instance?.AllVents?.FirstOrDefault(x => x.Id == AllVents.Except(EnteredVents).FirstOrDefault())?.name ?? string.Empty;
        return randomVentName == string.Empty ? string.Empty : string.Format(Translator.GetString("Tank.Suffix"), randomVentName);
    }

    public override bool CanUseVent(PlayerControl pc, int ventId)
    {
        return !IsThisRole(pc) || (!EnteredVents.Contains(ventId) && pc.GetClosestVent()?.Id == ventId);
    }
}