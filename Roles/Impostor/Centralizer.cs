using System.Linq;
using AmongUs.GameOptions;

namespace EHR.Impostor;

public class Centralizer : RoleBase
{
    public static bool On;

    public override bool IsEnable => On;

    private static OptionItem NumPlayersTeleported;
    public static OptionItem AbilityCooldown;
    private static OptionItem AbilityUseLimit;
    private static OptionItem AbilityUseGainWithEachKill;

    public Vector2? MarkedPosition;

    public override void SetupCustomOption()
    {
        StartSetup(655700)
            .AutoSetupOption(ref NumPlayersTeleported, 4, new IntegerValueRule(1, 15, 1), OptionFormat.Players)
            .AutoSetupOption(ref AbilityCooldown, 30, new IntegerValueRule(0, 120, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref AbilityUseLimit, 1f, new FloatValueRule(0, 20, 0.1f), OptionFormat.Times)
            .AutoSetupOption(ref AbilityUseGainWithEachKill, 0.6f, new FloatValueRule(0f, 5f, 0.1f), OptionFormat.Times);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        MarkedPosition = null;
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetFloat());
    }
    
    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        if (Options.UsePhantomBasis.GetBool())
            AURoleOptions.PhantomCooldown = AbilityCooldown.GetFloat();
        else
        {
            if (Options.UsePets.GetBool()) return;

            AURoleOptions.ShapeshifterCooldown = AbilityCooldown.GetFloat();
            AURoleOptions.ShapeshifterDuration = 1f;
        }
    }
    
    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (!shapeshifting) return true;
        UseAbility(shapeshifter);
        return false;
    }

    public override bool OnVanish(PlayerControl pc)
    {
        UseAbility(pc);
        return false;
    }

    public override void OnPet(PlayerControl pc)
    {
        UseAbility(pc);
    }

    void UseAbility(PlayerControl pc)
    {
        if (MarkedPosition.HasValue)
        {
            if (pc.GetAbilityUseLimit() < 1) return;
            Main.AllAlivePlayerControls.Shuffle().Take(NumPlayersTeleported.GetInt()).MassTP(MarkedPosition.Value);
            MarkedPosition = null;
            pc.RpcRemoveAbilityUse();
        }
        else
        {
            MarkedPosition = pc.Pos();
            pc.Notify(Translator.GetString("MarkDone"));
        }
    }
}