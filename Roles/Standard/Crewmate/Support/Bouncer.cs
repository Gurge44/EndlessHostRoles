using System;
using System.Collections.Generic;
using EHR.Modules.Extensions;

namespace EHR.Roles;

public class Bouncer : RoleBase
{
    public static bool On;

    public static OptionItem AbilityCooldown;
    private static OptionItem AbilityDuration;
    private static OptionItem AbortBouncingIfLeftRoom;
    private static OptionItem WhoGetsBounced;
    private static OptionItem AbilityUseLimit;
    private static OptionItem AbilityUseGainWithEachTaskCompleted;
    private static OptionItem AbilityChargesWhenFinishedTasks;

    private static readonly string[] WhoGetsBouncedOptions =
    [
        "Bouncer.WhoGetsBouncedOptions.Everyone",
        "Bouncer.WhoGetsBouncedOptions.Impostors",
        "Bouncer.WhoGetsBouncedOptions.ImpNKNPNE"
    ];

    public override bool IsEnable => On;

    private Dictionary<byte, Vector2> LastPosition = [];
    private CountdownTimer Timer;
    private PlainShipRoom MarkedRoom;
    private byte BouncerId;

    public override void SetupCustomOption()
    {
        StartSetup(659300)
            .AutoSetupOption(ref AbilityCooldown, 15, new IntegerValueRule(0, 120, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref AbilityDuration, 15, new IntegerValueRule(1, 60, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref AbortBouncingIfLeftRoom, true)
            .AutoSetupOption(ref WhoGetsBounced, 0, WhoGetsBouncedOptions)
            .AutoSetupOption(ref AbilityUseLimit, 1f, new FloatValueRule(0, 20, 0.05f), OptionFormat.Times)
            .AutoSetupOption(ref AbilityUseGainWithEachTaskCompleted, 0.5f, new FloatValueRule(0f, 5f, 0.05f), OptionFormat.Times)
            .AutoSetupOption(ref AbilityChargesWhenFinishedTasks, 0.2f, new FloatValueRule(0f, 5f, 0.05f), OptionFormat.Times);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        BouncerId = playerId;
        LastPosition = [];
        Timer = null;
        MarkedRoom = null;
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetFloat());
    }

    public override void OnPet(PlayerControl pc)
    {
        var room = pc.GetPlainShipRoom();
        if (!room || pc.GetAbilityUseLimit() < 1f) return;

        MarkedRoom = room;
        Timer = new CountdownTimer(AbilityDuration.GetFloat(), () =>
        {
            Timer = null;
            if (!pc || !pc.IsAlive()) return;
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }, onTick: () =>
        {
            if (Timer.Remaining.TotalSeconds >= 6 || !pc || !pc.IsAlive()) return;
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }, onCanceled: () => Timer = null);
        pc.RpcRemoveAbilityUse(notify: false);
        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
    }

    public override void OnCheckPlayerPosition(PlayerControl pc)
    {
        if (Timer == null) return;

        if (pc.PlayerId == BouncerId)
        {
            if (AbortBouncingIfLeftRoom.GetBool() && !pc.IsInRoom(MarkedRoom))
            {
                Timer.Dispose();
                Timer = null;
            }

            return;
        }

        if (!LastPosition.TryGetValue(pc.PlayerId, out Vector2 lastPosition))
            LastPosition[pc.PlayerId] = pc.transform.position;
        else if (pc.IsInRoom(MarkedRoom) && WhoGetsBounced.GetValue() switch
        {
            0 => true,
            1 => pc.Is(Team.Impostor),
            2 => pc.Is(Team.Impostor) || pc.IsNeutralKiller() || pc.IsNeutralPariah() || pc.IsNeutralEvil(),
            _ => false
        })
            pc.TP(lastPosition);
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != BouncerId || seer.PlayerId != target.PlayerId || (seer.IsModdedClient() && !hud) || meeting || Timer == null) return string.Empty;
        int remainingSeconds = (int)Math.Ceiling(Timer.Remaining.TotalSeconds);
        string timerText = seer.IsModdedClient() || remainingSeconds <= 5 ? string.Format(Translator.GetString("Bouncer.Suffix.RemainingSeconds"), remainingSeconds) : string.Empty;
        return string.Format(Translator.GetString("Bouncer.Suffix"), Translator.GetString(MarkedRoom.RoomId)) + timerText;
    }
}