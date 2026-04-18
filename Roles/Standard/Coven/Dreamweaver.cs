using System;
using System.Collections.Generic;
using EHR.Modules;
using Hazel;

namespace EHR.Roles;

public class Dreamweaver : CovenBase
{
    public static bool On;

    private static OptionItem AbilityCooldown;
    private static OptionItem AbilityUseLimit;
    private static OptionItem CanVentBeforeNecronomicon;
    private static OptionItem CanVentAfterNecronomicon;

    private byte DreamweaverId;

    public HashSet<byte> InsanePlayers = [];

    protected override NecronomiconReceivePriorities NecronomiconReceivePriority => NecronomiconReceivePriorities.Random;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(650080)
            .AutoSetupOption(ref AbilityCooldown, 30f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref AbilityUseLimit, 5f, new FloatValueRule(0, 20, 1f), OptionFormat.Times)
            .AutoSetupOption(ref CanVentBeforeNecronomicon, false)
            .AutoSetupOption(ref CanVentAfterNecronomicon, false);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        InsanePlayers = [];
        DreamweaverId = playerId;
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetFloat());
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return HasNecronomicon ? CanVentAfterNecronomicon.GetBool() : CanVentBeforeNecronomicon.GetBool();
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return pc.IsAlive() && pc.GetAbilityUseLimit() > 0;
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = AbilityCooldown.GetFloat();
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (Thanos.IsImmune(target)) return false;
        InsanePlayers.Add(target.PlayerId);
        Utils.SendRPC(CustomRPC.SyncRoleData, DreamweaverId, 1, target.PlayerId);
        Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
        killer.SetKillCooldown(AbilityCooldown.GetFloat());
        killer.RpcRemoveAbilityUse();
        return false;
    }

    public override void AfterMeetingTasks()
    {
        InsanePlayers.ToValidPlayers().Do(x => x.RpcSetCustomRole(CustomRoles.Insane));
        InsanePlayers.Clear();
        Utils.SendRPC(CustomRPC.SyncRoleData, DreamweaverId, 2);
    }

    public override void OnGlobalFixedUpdate(PlayerControl pc, bool lowLoad)
    {
        if (lowLoad || GameStates.IsMeeting || ExileController.Instance || !Main.IntroDestroyed || !pc.IsAlive() || !pc.Is(CustomRoles.Insane) || Main.KillTimers[pc.PlayerId] > 0f) return;

        if (!FastVector2.TryGetClosestPlayerInRangeTo(pc, 1.5f, out PlayerControl nearestPlayer)) return;

        RoleBase roleBase = Main.PlayerStates[pc.PlayerId].Role;
        Type type = roleBase.GetType();

        if (type.GetMethod("OnCheckMurder")?.DeclaringType == type)
        {
            Logger.Info($"Explicit OnCheckMurder triggered for {pc.GetNameWithRole()}", "Dreamweaver");
            roleBase.OnCheckMurder(pc, nearestPlayer);
            pc.SetKillCooldown();
        }

        if (HasNecronomicon) pc.Suicide(realKiller: DreamweaverId.GetPlayer());
    }

    public void ReceiveRPC(MessageReader reader)
    {
        switch (reader.ReadPackedInt32())
        {
            case 1:
                InsanePlayers.Add(reader.ReadByte());
                break;
            case 2:
                InsanePlayers.Clear();
                break;
        }
    }
}
