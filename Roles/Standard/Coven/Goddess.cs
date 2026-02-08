using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Modules.Extensions;
using Hazel;
using Il2CppSystem;

namespace EHR.Roles;

public class Goddess : CovenBase
{
    public static bool On;

    private static OptionItem AbilityCooldown;
    private static OptionItem AbilityDuration;
    private static OptionItem CanVentBeforeNecronomicon;
    private static OptionItem CanVentAfterNecronomicon;

    private byte GoddessId;
    private CountdownTimer Timer;

    protected override NecronomiconReceivePriorities NecronomiconReceivePriority => NecronomiconReceivePriorities.Random;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(650060)
            .AutoSetupOption(ref AbilityCooldown, 30f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref AbilityDuration, 10, new IntegerValueRule(1, 60, 1), OptionFormat.Seconds)
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
        GoddessId = playerId;
        Timer = null;
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return HasNecronomicon ? CanVentAfterNecronomicon.GetBool() : CanVentBeforeNecronomicon.GetBool();
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return HasNecronomicon;
    }

    public override bool OnVanish(PlayerControl pc)
    {
        Timer = new CountdownTimer(AbilityDuration.GetInt(), () =>
        {
            Timer = null;
            pc.RpcResetAbilityCooldown();
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }, onTick: () => Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc), onCanceled: () => Timer = null);
        Utils.SendRPC(CustomRPC.SyncRoleData, GoddessId);
        return false;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        AURoleOptions.PhantomCooldown = AbilityCooldown.GetFloat();
        AURoleOptions.PhantomDuration = 0.1f;
    }

    public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
    {
        if (Timer == null || killer.Is(CustomRoles.Pestilence) || !killer.IsAlive()) return true;

        killer.SetRealKiller(target);
        target.Kill(killer);
        return false;
    }

    public void ReceiveRPC(MessageReader reader)
    {
        Timer = new CountdownTimer(AbilityDuration.GetInt(), () => Timer = null, onCanceled: () => Timer = null);
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != GoddessId || seer.PlayerId != target.PlayerId || (seer.IsModdedClient() && !hud) || meeting || Timer == null) return string.Empty;
        return string.Format(Translator.GetString("Goddess.Suffix"), (int)Math.Ceiling(Timer.Remaining.TotalSeconds), Main.CovenColor);
    }
}