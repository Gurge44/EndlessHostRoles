﻿using System.Linq;

namespace EHR.Coven;

public class Timelord : Coven
{
    public static bool On;

    private static OptionItem KillCooldown;
    private static OptionItem TimeStolenWithEachKill;
    private static OptionItem CanVentBeforeNecronomicon;
    private static OptionItem CanVentAfterNecronomicon;

    protected override NecronomiconReceivePriorities NecronomiconReceivePriority => NecronomiconReceivePriorities.Random;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(650110)
            .AutoSetupOption(ref KillCooldown, 30f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref TimeStolenWithEachKill, 10, new IntegerValueRule(0, 60, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref CanVentBeforeNecronomicon, false)
            .AutoSetupOption(ref CanVentAfterNecronomicon, true);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return HasNecronomicon ? CanVentAfterNecronomicon.GetBool() : CanVentBeforeNecronomicon.GetBool();
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return pc.IsAlive();
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (target.Is(Team.Coven)) target.SetKillCooldown(0.01f);
        else target.SetKillCooldown();

        if (!HasNecronomicon) killer.SetKillCooldown(KillCooldown.GetFloat());
        return HasNecronomicon;
    }

    public static int GetTotalStolenTime()
    {
        return Main.PlayerStates.Values.Where(x => x.MainRole == CustomRoles.Timelord).Sum(x => x.GetKillCount()) * TimeStolenWithEachKill.GetInt();
    }

    public override string GetProgressText(byte playerId, bool comms)
    {
        string baseText = base.GetProgressText(playerId, comms);
        if (!HasNecronomicon) return baseText;

        string stolen = Utils.ColorString(Team.Coven.GetColor().ShadeColor(0.25f), $" -{GetTotalStolenTime()}s");
        return baseText + stolen;
    }
}