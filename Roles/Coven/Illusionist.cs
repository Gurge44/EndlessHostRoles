using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;

namespace EHR.Coven;

public class Illusionist : Coven
{
    public static bool On;

    private static OptionItem AbilityCooldown;
    private static OptionItem ShapeshiftAnimation;

    public byte SampledPlayerId;

    public override NecronomiconReceivePriorities NecronomiconReceivePriority => NecronomiconReceivePriorities.Random;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(650100)
            .AutoSetupOption(ref AbilityCooldown, 30f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref ShapeshiftAnimation, false);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        SampledPlayerId = byte.MaxValue;
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

        target.RpcShapeshift(sampledPlayer, ShapeshiftAnimation.GetBool());
        if (HasNecronomicon) shapeshifter.SetKillCooldown(AbilityCooldown.GetFloat());

        SampledPlayerId = byte.MaxValue;
        Utils.SendRPC(CustomRPC.SyncRoleData, shapeshifter.PlayerId, 2);
        return false;
    }

    public override void AfterMeetingTasks()
    {
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