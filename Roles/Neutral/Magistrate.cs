using AmongUs.GameOptions;
using EHR.Modules;

namespace EHR.Neutral;

public class Magistrate : RoleBase
{
    public static bool On;

    public static OptionItem ExtraVotes;

    public static bool CallCourtNextMeeting;
    private AbilityTriggers AbilityTrigger;

    private byte MagistrateID;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(645400)
            .AutoSetupOption(ref ExtraVotes, 3, new IntegerValueRule(0, 30, 1), OptionFormat.Votes);
    }

    public override void Init()
    {
        On = false;
        CallCourtNextMeeting = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        MagistrateID = playerId;
        playerId.SetAbilityUseLimit(1);
        AbilityTrigger = Options.UsePhantomBasis.GetBool() && Options.UsePhantomBasisForNKs.GetBool() ? AbilityTriggers.Vanish : Options.UsePets.GetBool() ? AbilityTriggers.Pet : AbilityTriggers.Vent;
    }

    public override void AfterMeetingTasks()
    {
        CallCourtNextMeeting = false;
        Main.AllPlayerControls.Do(x => Camouflage.RpcSetSkin(x, notCommsOrCamo: true));
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        if (AbilityTrigger != AbilityTriggers.Vent) return;
        AURoleOptions.EngineerCooldown = 0.1f;
        AURoleOptions.EngineerInVentMaxTime = 1f;
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return pc.IsAlive() && AbilityTrigger == AbilityTriggers.Vent && pc.GetAbilityUseLimit() > 0;
    }

    public override bool CanUseVent(PlayerControl pc, int ventId)
    {
        return !IsThisRole(pc) || pc.PlayerId != MagistrateID || (AbilityTrigger == AbilityTriggers.Vent && pc.GetAbilityUseLimit() > 0);
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        if (AbilityTrigger != AbilityTriggers.Vent || pc.GetAbilityUseLimit() < 1) return;
        UseAbility(pc);
    }

    public override void OnPet(PlayerControl pc)
    {
        if (AbilityTrigger == AbilityTriggers.Pet)
            UseAbility(pc);
    }

    public override bool OnVanish(PlayerControl pc)
    {
        if (AbilityTrigger != AbilityTriggers.Vanish) return false;
        UseAbility(pc);
        return false;
    }

    private static void UseAbility(PlayerControl pc)
    {
        pc.RPCPlayCustomSound("Line");
        pc.RpcRemoveAbilityUse();
        CallCourtNextMeeting = true;
    }

    private enum AbilityTriggers
    {
        Vent,
        Pet,
        Vanish
    }
}
