using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;

namespace EHR.Coven;

public class Banshee : Coven
{
    public static bool On;
    private static List<Banshee> Instances = [];

    private static OptionItem AbilityCooldown;
    private static OptionItem Radius;

    private byte BansheeId;

    public HashSet<byte> ScreechedPlayers = [];

    public override NecronomiconReceivePriorities NecronomiconReceivePriority => NecronomiconReceivePriorities.Random;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(650090)
            .AutoSetupOption(ref AbilityCooldown, 30f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref Radius, 3f, new FloatValueRule(0.25f, 10f, 0.25f), OptionFormat.Multiplier);
    }

    public override void Init()
    {
        On = false;
        Instances = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        ScreechedPlayers = [];
        BansheeId = playerId;
        Instances.Add(this);
    }

    public override bool OnVanish(PlayerControl pc)
    {
        var radius = Radius.GetFloat();
        var pos = pc.Pos();
        IEnumerable<PlayerControl> nearbyPlayers = Utils.GetPlayersInRadius(radius, pos).Without(pc);

        if (!HasNecronomicon) ScreechedPlayers = nearbyPlayers.Select(x => x.PlayerId).ToHashSet();
        else nearbyPlayers.Do(x => x.Suicide(realKiller: pc));

        if (ScreechedPlayers.Count > 0)
        {
            var w = Utils.CreateRPC(CustomRPC.SyncRoleData);
            w.Write(BansheeId);
            w.WritePacked(1);
            w.WritePacked(ScreechedPlayers.Count);
            ScreechedPlayers.Do(x => w.Write(x));

            Utils.NotifyRoles(SpecifySeer: pc);
        }

        return false;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        AURoleOptions.PhantomCooldown = AbilityCooldown.GetFloat();
        AURoleOptions.PhantomDuration = 1f;
    }

    public override void AfterMeetingTasks()
    {
        ScreechedPlayers.Clear();
        Utils.SendRPC(CustomRPC.SyncRoleData, BansheeId, 2);
    }

    public void ReceiveRPC(MessageReader reader)
    {
        switch (reader.ReadPackedInt32())
        {
            case 1:
                ScreechedPlayers.Clear();
                int count = reader.ReadPackedInt32();
                Loop.Times(count, _ => ScreechedPlayers.Add(reader.ReadByte()));
                break;
            case 2:
                ScreechedPlayers.Clear();
                break;
        }
    }

    public static void OnReceiveChat()
    {
        var screechedPlayers = Instances.SelectMany(x => x.ScreechedPlayers).ToValidPlayers().ToHashSet();
        if (screechedPlayers.Count == 0) return;

        screechedPlayers.Do(ChatManager.ClearChatForSpecificPlayer);
    }
}