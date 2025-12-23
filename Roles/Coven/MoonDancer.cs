using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;

namespace EHR.Coven;

public class MoonDancer : Coven
{
    public static bool On;

    public override bool IsEnable => On;

    private static OptionItem SelfAssignAddonCooldown;
    private static OptionItem OthersAssignAddonCooldown;
    private static OptionItem StealAddonCooldown;
    private static OptionItem KillCooldown;
    private static OptionItem CanStealAddonsWithoutNecronomicon;
    private static OptionItem StealAllAddonsOnKill;
    private static OptionItem CanVentBeforeNecronomicon;
    private static OptionItem CanVentAfterNecronomicon;

    protected override NecronomiconReceivePriorities NecronomiconReceivePriority => NecronomiconReceivePriorities.Random;

    private Dictionary<byte, List<CustomRoles>> ToAssign = [];

    public override void SetupCustomOption()
    {
        StartSetup(657300)
            .AutoSetupOption(ref SelfAssignAddonCooldown, 15f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref OthersAssignAddonCooldown, 15f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref StealAddonCooldown, 15f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref KillCooldown, 30f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref CanStealAddonsWithoutNecronomicon, true)
            .AutoSetupOption(ref StealAllAddonsOnKill, true)
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
        ToAssign = [];
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return HasNecronomicon ? CanVentAfterNecronomicon.GetBool() : CanVentBeforeNecronomicon.GetBool();
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return pc.IsAlive();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        AURoleOptions.PhantomCooldown = SelfAssignAddonCooldown.GetFloat();
        AURoleOptions.PhantomDuration = 0.1f;
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = HasNecronomicon ? KillCooldown.GetFloat() : OthersAssignAddonCooldown.GetFloat();
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (!HasNecronomicon && !CanStealAddonsWithoutNecronomicon.GetBool())
        {
            GiveAddon();
            return false;
        }

        if (killer.CheckDoubleTrigger(target, GiveAddon))
        {
            if (HasNecronomicon && !StealAllAddonsOnKill.GetBool()) return true;
            PlayerState state = Main.PlayerStates[target.PlayerId];
            state.SubRoles.FindAll(x => !killer.Is(x) && !x.IsNotAssignableMidGame() && !x.IsConverted() && CustomRolesHelper.CheckAddonConflict(x, killer)).ForEach(x =>
            {
                killer.RpcSetCustomRole(x);
                state.RemoveSubRole(x);
            });
            if (!HasNecronomicon) killer.SetKillCooldown(StealAddonCooldown.GetFloat());
            return HasNecronomicon;
        }

        return false;
        
        void GiveAddon()
        {
            CustomRoles addon = Options.GroupedAddons[target.Is(Team.Coven) ? AddonTypes.Helpful : AddonTypes.Harmful].Where(x => !target.Is(x) && !x.IsNotAssignableMidGame() && !x.IsConverted() && CustomRolesHelper.CheckAddonConflict(x, target)).RandomElement();
            if (addon == default(CustomRoles))
                return;

            if (ToAssign.TryGetValue(target.PlayerId, out var list))
                list.Add(addon);
            else
                ToAssign[target.PlayerId] = [addon];
            
            killer.SetKillCooldown(OthersAssignAddonCooldown.GetFloat());
        }
    }

    public override bool OnVanish(PlayerControl pc)
    {
        CustomRoles addon = Enum.GetValues<CustomRoles>().Where(x => x.IsAdditionRole() && !x.IsGhostRole() && !pc.Is(x) && !x.IsNotAssignableMidGame() && !x.IsConverted()).RandomElement();
        if (addon == default(CustomRoles)) return false;
        pc.RpcSetCustomRole(addon);
        return false;
    }

    public override void AfterMeetingTasks()
    {
        foreach ((byte id, List<CustomRoles> addons) in ToAssign)
        {
            var pc = id.GetPlayer();
            if (pc == null || !pc.IsAlive()) continue;
            
            addons.ForEach(x => pc.RpcSetCustomRole(x));
        }

        ToAssign = [];
    }
}