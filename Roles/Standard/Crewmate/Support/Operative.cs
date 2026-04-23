using AmongUs.GameOptions;

namespace EHR.Roles;

public class Operative : RoleBase
{
    public static bool On;

    public static OptionItem AbilityCooldown;
    private static OptionItem AbilityUseLimit;
    private static OptionItem AbilityUseGainWithEachTaskCompleted;
    private static OptionItem AbilityChargesWhenFinishedTasks;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(658800)
            .AutoSetupOption(ref AbilityCooldown, 15, new IntegerValueRule(0, 120, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref AbilityUseLimit, 1f, new FloatValueRule(0, 20, 0.05f), OptionFormat.Times)
            .AutoSetupOption(ref AbilityUseGainWithEachTaskCompleted, 0.4f, new FloatValueRule(0f, 5f, 0.05f), OptionFormat.Times)
            .AutoSetupOption(ref AbilityChargesWhenFinishedTasks, 0.2f, new FloatValueRule(0f, 5f, 0.05f), OptionFormat.Times);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetFloat());
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        if (!Options.UsePets.GetBool())
        {
            AURoleOptions.EngineerInVentMaxTime = 1f;
            AURoleOptions.EngineerCooldown = AbilityCooldown.GetFloat();
        }
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        if (Options.UsePets.GetBool()) return;
        UseAbility();
    }

    public override void OnPet(PlayerControl pc)
    {
        UseAbility();
    }

    static void UseAbility()
    {
        SabotageSystemType sabotageSystemType = ShipStatus.Instance.Systems[SystemTypes.Sabotage].CastFast<SabotageSystemType>();
        sabotageSystemType.Timer = 30f;
        sabotageSystemType.IsDirty = true;
    }
}