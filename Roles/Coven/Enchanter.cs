using System.Collections.Generic;
using EHR.Modules;

namespace EHR.Coven;

public class Enchanter : Coven
{
    public static bool On;

    private static OptionItem AbilityCooldown;
    private static OptionItem AbilityUseLimit;

    public static HashSet<byte> EnchantedPlayers = [];

    protected override NecronomiconReceivePriorities NecronomiconReceivePriority => NecronomiconReceivePriorities.Random;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(650120)
            .AutoSetupOption(ref AbilityCooldown, 30f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref AbilityUseLimit, 2, new IntegerValueRule(1, 10, 1), OptionFormat.Times);
    }

    public override void Init()
    {
        On = false;
        EnchantedPlayers = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetInt());
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