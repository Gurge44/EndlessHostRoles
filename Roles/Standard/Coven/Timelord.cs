using UnityEngine;

namespace EHR.Roles;

public class Timelord : CovenBase
{
    public static bool On;

    private static OptionItem KillCooldown;
    private static OptionItem TimeStolenWithEachKill;
    private static OptionItem CanVentBeforeNecronomicon;
    private static OptionItem CanVentAfterNecronomicon;

    private static Color32 ShadeColor;
    protected override NecronomiconReceivePriorities NecronomiconReceivePriority => NecronomiconReceivePriorities.Random;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(650110)
            .AutoSetupOption(ref KillCooldown, 30f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref TimeStolenWithEachKill, 10, new IntegerValueRule(1, 60, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref CanVentBeforeNecronomicon, false)
            .AutoSetupOption(ref CanVentAfterNecronomicon, true);
    }

    public override void Init()
    {
        On = false;
        ShadeColor = Team.Coven.GetColor().ShadeColor(0.25f);
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
        return true;
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
        int totalKills = 0;
        var states = Main.PlayerStates.Values;

        foreach (var state in states)
        {
            if (state.MainRole != CustomRoles.Timelord) continue;

            totalKills += state.GetKillCount();
        }

        int perKill = TimeStolenWithEachKill.GetInt();
        return totalKills * perKill;
    }

    public override void GetProgressText(byte playerId, bool comms, StringBuilder resultText)
    {
        base.GetProgressText(playerId, comms, resultText);
        if (!HasNecronomicon) return;

        resultText.Append(Utils.ColorPrefix(ShadeColor))
            .Append(" -")
            .Append(GetTotalStolenTime())
            .Append("s</color>");
    }
}
