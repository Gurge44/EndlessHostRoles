using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;

namespace EHR.Coven;

public class Banshee : Coven
{
    public static bool On;
    public static List<Banshee> Instances = [];

    private static OptionItem AbilityCooldown;
    private static OptionItem Radius;
    private static OptionItem CanVentBeforeNecronomicon;
    private static OptionItem CanVentAfterNecronomicon;

    private byte BansheeId;

    public HashSet<byte> ScreechedPlayers = [];

    protected override NecronomiconReceivePriorities NecronomiconReceivePriority => NecronomiconReceivePriorities.Random;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(650090)
            .AutoSetupOption(ref AbilityCooldown, 30f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref Radius, 3f, new FloatValueRule(0.25f, 10f, 0.25f), OptionFormat.Multiplier)
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
        ScreechedPlayers = [];
        BansheeId = playerId;
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

    public override bool OnVanish(PlayerControl pc)
    {
        float radius = Radius.GetFloat();
        Vector2 pos = pc.Pos();
        IEnumerable<PlayerControl> nearbyPlayers = Utils.GetPlayersInRadius(radius, pos).Without(pc);

        if (!HasNecronomicon) ScreechedPlayers = nearbyPlayers.Select(x => x.PlayerId).ToHashSet();
        else nearbyPlayers.Do(x => x.Suicide(PlayerState.DeathReason.Deafened, pc));

        if (ScreechedPlayers.Count > 0)
        {
            if (Utils.DoRPC)
            {
                MessageWriter w = Utils.CreateRPC(CustomRPC.SyncRoleData);
                w.Write(BansheeId);
                w.WritePacked(1);
                w.WritePacked(ScreechedPlayers.Count);
                ScreechedPlayers.Do(x => w.Write(x));
                Utils.EndRPC(w);
            }

            Utils.NotifyRoles(SpecifySeer: pc);
        }

        return false;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        AURoleOptions.PhantomCooldown = AbilityCooldown.GetFloat();
        AURoleOptions.PhantomDuration = 0.1f;
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

    public override void OnReportDeadBody()
    {
        LateTask.New(() => Instances.SelectMany(x => x.ScreechedPlayers).Distinct().ToValidPlayers().DoIf(x => x.IsAlive(), x => x.SetChatVisible(false)), 4f, "Set Chat Hidden For Banshee Victims");

        PlayerControl pc = BansheeId.GetPlayer();
        
        if (pc != null && pc.AmOwner && Instances.SelectMany(x => x.ScreechedPlayers).Distinct().Count() == Main.AllAlivePlayerControls.Count(x => !Instances.Exists(a => a.BansheeId == x.PlayerId)))
            Achievements.Type.GetMuted.CompleteAfterGameEnd();
    }
}