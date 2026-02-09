using System;
using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using EHR.Modules.Extensions;
using Hazel;

namespace EHR.Roles;

public class PotionMaster : CovenBase
{
    public static bool On;
    private static List<PotionMaster> Instances = [];

    private static OptionItem AbilityCooldown;
    private static OptionItem KillCooldown;
    private static OptionItem ShieldDuration;
    private static OptionItem CanVentBeforeNecronomicon;
    private static OptionItem CanVentAfterNecronomicon;

    private byte PotionMasterId;
    private HashSet<byte> RevealedPlayers = [];

    private Dictionary<byte, CountdownTimer> ShieldedPlayers = [];

    protected override NecronomiconReceivePriorities NecronomiconReceivePriority => NecronomiconReceivePriorities.Random;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(650020)
            .AutoSetupOption(ref AbilityCooldown, 30f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref KillCooldown, 30f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref ShieldDuration, 20, new IntegerValueRule(0, 600, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref CanVentBeforeNecronomicon, false)
            .AutoSetupOption(ref CanVentAfterNecronomicon, true);
    }

    public override void Init()
    {
        On = false;
        Instances = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        ShieldedPlayers = [];
        RevealedPlayers = [];
        PotionMasterId = playerId;
        Instances.Add(this);
    }

    public override void Remove(byte playerId)
    {
        Instances.Remove(this);
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
        if (target.Is(Team.Coven))
        {
            ShieldedPlayers[target.PlayerId] = new CountdownTimer(ShieldDuration.GetInt(), () =>
            {
                ShieldedPlayers.Remove(target.PlayerId);
                Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
                Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: target);
                Utils.SendRPC(CustomRPC.SyncRoleData, PotionMasterId, 2, target.PlayerId);
            }, onTick: () =>
            {
                Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
                Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: target);
            }, onCanceled: () =>
            {
                ShieldedPlayers.Remove(target.PlayerId);
                Utils.SendRPC(CustomRPC.SyncRoleData, PotionMasterId, 2, target.PlayerId);
            });
            Utils.SendRPC(CustomRPC.SyncRoleData, PotionMasterId, 1, target.PlayerId);
            Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
            Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: target);
            killer.SetKillCooldown(AbilityCooldown.GetFloat());
            return false;
        }

        if (HasNecronomicon) return true;

        RevealedPlayers.Add(target.PlayerId);
        Utils.SendRPC(CustomRPC.SyncRoleData, PotionMasterId, 3, target.PlayerId);
        Main.EnumerateAlivePlayerControls().DoIf(x => x.Is(Team.Coven), x => Utils.NotifyRoles(SpecifySeer: x, SpecifyTarget: target));
        killer.SetKillCooldown(AbilityCooldown.GetFloat());
        return false;
    }

    public override void OnReceiveNecronomicon()
    {
        PotionMasterId.GetPlayer()?.ResetKillCooldown();
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = HasNecronomicon ? KillCooldown.GetFloat() : AbilityCooldown.GetFloat();
    }

    public override bool KnowRole(PlayerControl seer, PlayerControl target)
    {
        if (base.KnowRole(seer, target)) return true;
        return seer.Is(Team.Coven) && RevealedPlayers.Contains(target.PlayerId);
    }

    public static bool OnAnyoneCheckMurder(PlayerControl target)
    {
        return !Instances.Any(x => x.ShieldedPlayers.ContainsKey(target.PlayerId));
    }

    public void ReceiveRPC(MessageReader reader)
    {
        switch (reader.ReadPackedInt32())
        {
            case 1:
                ShieldedPlayers[reader.ReadByte()] = new CountdownTimer(ShieldDuration.GetInt());
                break;
            case 2:
                ShieldedPlayers.Remove(reader.ReadByte());
                break;
            case 3:
                RevealedPlayers.Add(reader.ReadByte());
                break;
        }
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId == target.PlayerId && seer.IsModdedClient() && !hud) return string.Empty;
        if (seer.PlayerId != target.PlayerId && seer.PlayerId != PotionMasterId) return string.Empty;
        if (!ShieldedPlayers.TryGetValue(target.PlayerId, out CountdownTimer timer)) return string.Empty;

        return string.Format(Translator.GetString("PotionMaster.Suffix"), (int)Math.Ceiling(timer.Remaining.TotalSeconds), Main.CovenColor);
    }
}
