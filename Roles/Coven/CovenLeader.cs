using AmongUs.GameOptions;

namespace EHR.Coven;

public class CovenLeader : Coven
{
    public static bool On;

    private byte CovenLeaderId;

    protected override NecronomiconReceivePriorities NecronomiconReceivePriority => NecronomiconReceivePriorities.First;

    public override bool IsEnable => On;

    public override void SetupCustomOption() { }

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
        var kcd = Options.CovenLeaderKillCooldown.GetFloat();
        Main.AllPlayerKillCooldown[id] = HasNecronomicon ? kcd / 2f : kcd;
    }

    protected override void OnReceiveNecronomicon()
    {
        CovenLeaderId.GetPlayer().ResetKillCooldown();
    }
}