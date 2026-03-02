using System.Collections.Generic;
using EHR.Modules;

namespace EHR.Roles;

public class Enchanter : CovenBase
{
    public static bool On;

    private static OptionItem AbilityCooldown;
    private static OptionItem AbilityUseLimit;
    private static OptionItem CanVentBeforeNecronomicon;
    private static OptionItem CanVentAfterNecronomicon;

    public static HashSet<byte> EnchantedPlayers = [];

    protected override NecronomiconReceivePriorities NecronomiconReceivePriority => NecronomiconReceivePriorities.Random;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(650120)
            .AutoSetupOption(ref AbilityCooldown, 30f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref AbilityUseLimit, 2f, new FloatValueRule(0, 20, 1f), OptionFormat.Times)
            .AutoSetupOption(ref CanVentBeforeNecronomicon, false)
            .AutoSetupOption(ref CanVentAfterNecronomicon, true);
    }

    public override void Init()
    {
        On = false;
        EnchantedPlayers = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetFloat());
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return HasNecronomicon ? CanVentAfterNecronomicon.GetBool() : CanVentBeforeNecronomicon.GetBool();
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return pc.IsAlive();
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (EnchantedPlayers.Contains(target.PlayerId)) return HasNecronomicon;
        if (HasNecronomicon) return killer.CheckDoubleTrigger(target, EnchantTarget);
        EnchantTarget();
        return false;

        void EnchantTarget()
        {
            if (killer.GetAbilityUseLimit() < 1) return;
            EnchantedPlayers.Add(target.PlayerId);
            Utils.SendRPC(CustomRPC.SyncRoleData, killer.PlayerId, 1, target.PlayerId);
            Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
            killer.SetKillCooldown(AbilityCooldown.GetFloat());
            killer.RpcRemoveAbilityUse();
        }
    }
}
