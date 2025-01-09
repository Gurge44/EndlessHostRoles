using AmongUs.GameOptions;

namespace EHR.Coven;

public class CovenLeader : Coven
{
    public static bool On;

    private static OptionItem KillCooldown;

    private byte CovenLeaderId;

    protected override NecronomiconReceivePriorities NecronomiconReceivePriority => NecronomiconReceivePriorities.First;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(650000)
            .AutoSetupOption(ref KillCooldown, 30f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        CovenLeaderId = playerId;
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return pc.IsAlive();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        opt.SetInt(Int32OptionNames.KillDistance, HasNecronomicon ? 2 : 0);
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = HasNecronomicon ? Options.DefaultKillCooldown / 2f : Options.DefaultKillCooldown;
    }

    protected override void OnReceiveNecronomicon()
    {
        CovenLeaderId.GetPlayer().ResetKillCooldown();
    }
}