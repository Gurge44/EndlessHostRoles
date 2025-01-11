using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;

namespace EHR.Coven;

public class Illusionist : Coven
{
    public static bool On;

    private static OptionItem AbilityCooldown;
    private static OptionItem ShapeshiftAnimation;
    private static OptionItem UnmorphPrevious;
    private HashSet<byte> ForceMorhpedPlayers = [];

    public byte SampledPlayerId;

    protected override NecronomiconReceivePriorities NecronomiconReceivePriority => NecronomiconReceivePriorities.Random;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(650100)
            .AutoSetupOption(ref AbilityCooldown, 30f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref ShapeshiftAnimation, false)
            .AutoSetupOption(ref UnmorphPrevious, false);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        SampledPlayerId = byte.MaxValue;
        ForceMorhpedPlayers = [];
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
        if (HasNecronomicon && SampledPlayerId == target.PlayerId)
            return true;

        SampledPlayerId = target.PlayerId;
        Utils.SendRPC(CustomRPC.SyncRoleData, killer.PlayerId, 1, target.PlayerId);
        Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
        if (!HasNecronomicon) killer.SetKillCooldown(AbilityCooldown.GetFloat());
        return false;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        AURoleOptions.ShapeshifterCooldown = 1f;
        AURoleOptions.ShapeshifterDuration = 1f;
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        var sampledPlayer = SampledPlayerId.GetPlayer();
        if (sampledPlayer == null) return false;

        bool shouldAnimate = ShapeshiftAnimation.GetBool();
        ForceMorhpedPlayers.ToValidPlayers().Do(x => x.RpcShapeshift(x, shouldAnimate));

        target.RpcShapeshift(sampledPlayer, shouldAnimate);
        if (HasNecronomicon) shapeshifter.SetKillCooldown(AbilityCooldown.GetFloat());
        if (UnmorphPrevious.GetBool()) ForceMorhpedPlayers.Add(sampledPlayer.PlayerId);

        SampledPlayerId = byte.MaxValue;
        Utils.SendRPC(CustomRPC.SyncRoleData, shapeshifter.PlayerId, 2);
        return false;
    }

    public override void AfterMeetingTasks()
    {
        ForceMorhpedPlayers = [];
        SampledPlayerId = byte.MaxValue;
        Utils.SendRPC(CustomRPC.SyncRoleData, byte.MaxValue, 2);
    }

    public void ReceiveRPC(MessageReader reader)
    {
        SampledPlayerId = reader.ReadPackedInt32() switch
        {
            1 => reader.ReadByte(),
            2 => byte.MaxValue,
            _ => SampledPlayerId
        };
    }
}