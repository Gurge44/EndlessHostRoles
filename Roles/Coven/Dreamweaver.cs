using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using Hazel;

namespace EHR.Coven;

public class Dreamweaver : Coven
{
    public static bool On;

    private static OptionItem AbilityCooldown;

    private byte DreamweaverId;

    public HashSet<byte> InsanePlayers = [];

    protected override NecronomiconReceivePriorities NecronomiconReceivePriority => NecronomiconReceivePriorities.Random;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(650080)
            .AutoSetupOption(ref AbilityCooldown, 30f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds);
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
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return pc.IsAlive();
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = AbilityCooldown.GetFloat();
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        InsanePlayers.Add(target.PlayerId);
        Utils.SendRPC(CustomRPC.SyncRoleData, DreamweaverId, 1, target.PlayerId);
        Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
        killer.SetKillCooldown(AbilityCooldown.GetFloat());
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

        var pos = pc.Pos();
        var nearbyPlayers = Utils.GetPlayersInRadius(1.5f, pos).Without(pc).ToArray();
        if (nearbyPlayers.Length == 0) return;

        var nearestPlayer = nearbyPlayers.MinBy(x => Vector2.Distance(x.Pos(), pos));

        RoleBase roleBase = Main.PlayerStates[pc.PlayerId].Role;
        var type = roleBase.GetType();

        if (type.GetMethod("OnCheckMurder")?.DeclaringType == type)
        {
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