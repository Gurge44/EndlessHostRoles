using System.Collections.Generic;
using EHR.Modules;

namespace EHR.Coven;

public class Enchanter : Coven
{
    public static bool On;

    private static OptionItem AbilityCooldown;

    public static HashSet<byte> EnchantedPlayers = [];

    public override NecronomiconReceivePriorities NecronomiconReceivePriority => NecronomiconReceivePriorities.Random;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(650120)
            .AutoSetupOption(ref AbilityCooldown, 30f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        EnchantedPlayers = [];
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
            EnchantedPlayers.Add(target.PlayerId);
            Utils.SendRPC(CustomRPC.SyncRoleData, killer.PlayerId, 1, target.PlayerId);
            Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
            killer.SetKillCooldown(AbilityCooldown.GetFloat());
        }
    }
}