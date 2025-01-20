using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using Hazel;

namespace EHR.Coven;

public class PotionMaster : Coven
{
    public static bool On;
    private static List<PotionMaster> Instances = [];

    private static OptionItem AbilityCooldown;
    private static OptionItem KillCooldown;
    private static OptionItem ShieldDuration;

    private byte PotionMasterId;
    private HashSet<byte> RevealedPlayers = [];

    private Dictionary<byte, long> ShieldedPlayers = [];

    protected override NecronomiconReceivePriorities NecronomiconReceivePriority => NecronomiconReceivePriorities.Random;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(650020)
            .AutoSetupOption(ref AbilityCooldown, 30f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref KillCooldown, 30f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref ShieldDuration, 20, new IntegerValueRule(0, 600, 1), OptionFormat.Seconds);
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

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return pc.IsAlive();
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (target.Is(Team.Coven))
        {
            long shieldEndTS = Utils.TimeStamp + ShieldDuration.GetInt();
            ShieldedPlayers[target.PlayerId] = shieldEndTS;
            Utils.SendRPC(CustomRPC.SyncRoleData, PotionMasterId, 1, target.PlayerId, shieldEndTS);
            Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
            Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: target);
            killer.SetKillCooldown(AbilityCooldown.GetFloat());
            return false;
        }

        if (HasNecronomicon) return true;

        RevealedPlayers.Add(target.PlayerId);
        Main.AllAlivePlayerControls.DoIf(x => x.Is(Team.Coven), x => Utils.NotifyRoles(SpecifySeer: x, SpecifyTarget: target));
        killer.SetKillCooldown(AbilityCooldown.GetFloat());
        return false;
    }

    protected override void OnReceiveNecronomicon()
    {
        PotionMasterId.GetPlayer().ResetKillCooldown();
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = HasNecronomicon ? KillCooldown.GetFloat() : AbilityCooldown.GetFloat();
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (GameStates.IsMeeting || ExileController.Instance || !Main.IntroDestroyed || ShieldedPlayers.Count == 0) return;

        long now = Utils.TimeStamp;
        List<byte> toRemove = ShieldedPlayers.Where(x => now >= x.Value).Select(x => x.Key).ToList();

        toRemove.ForEach(x =>
        {
            ShieldedPlayers.Remove(x);
            var player = x.GetPlayer();
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: player);
            Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player);
            Utils.SendRPC(CustomRPC.SyncRoleData, PotionMasterId, 2, x);
        });

        foreach (byte shieldedId in ShieldedPlayers.Keys)
        {
            var player = shieldedId.GetPlayer();
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: player);
            Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player);
        }
    }

    public override bool KnowRole(PlayerControl seer, PlayerControl target)
    {
        if (base.KnowRole(seer, target)) return true;
        return seer.Is(Team.Coven) && RevealedPlayers.Contains(target.PlayerId);
    }

    public static bool OnAnyoneCheckMurder(PlayerControl target) => !Instances.Any(x => x.ShieldedPlayers.ContainsKey(target.PlayerId));

    public void ReceiveRPC(MessageReader reader)
    {
        switch (reader.ReadPackedInt32())
        {
            case 1:
                ShieldedPlayers[reader.ReadByte()] = long.Parse(reader.ReadString());
                break;
            case 2:
                ShieldedPlayers.Remove(reader.ReadByte());
                break;
        }
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId == target.PlayerId && seer.IsModClient() && !hud) return string.Empty;
        if (seer.PlayerId != target.PlayerId && seer.PlayerId != PotionMasterId) return string.Empty;
        if (!ShieldedPlayers.TryGetValue(target.PlayerId, out var shieldExpireTS)) return string.Empty;

        return string.Format(Translator.GetString("PotionMaster.Suffix"), shieldExpireTS - Utils.TimeStamp, Main.CovenColor);
    }
}