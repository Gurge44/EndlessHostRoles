using AmongUs.GameOptions;
using Hazel;

namespace EHR.Neutral;

public class Explosivist : RoleBase
{
    public static bool On;

    public static OptionItem AbilityCooldown;
    private static OptionItem ImpostorVision;
    public static OptionItem ExplosionDelay;
    private static OptionItem ExplosionRadius;

    public override bool IsEnable => On;

    private TNT Explosive;
    private Vector2 RealPosition;
    private long ExplodeTS;
    private byte ExplosivistId;

    public override void SetupCustomOption()
    {
        StartSetup(654300)
            .AutoSetupOption(ref AbilityCooldown, 22.5f, new FloatValueRule(0f, 180f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref ImpostorVision, true)
            .AutoSetupOption(ref ExplosionDelay, 10, new IntegerValueRule(1, 60, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref ExplosionRadius, 4f, new FloatValueRule(0.25f, 10f, 0.25f), OptionFormat.Multiplier);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        Explosive = null;
        RealPosition = Vector2.zero;
        ExplodeTS = 0;
        ExplosivistId = playerId;
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = AbilityCooldown.GetFloat();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        opt.SetVision(ImpostorVision.GetBool());

        if (Options.UsePhantomBasis.GetBool() && Options.UsePhantomBasisForNKs.GetBool())
        {
            AURoleOptions.PhantomCooldown = AbilityCooldown.GetFloat();
            AURoleOptions.PhantomDuration = 1f;
        }
        else if (!Options.UsePets.GetBool())
        {
            AURoleOptions.ShapeshifterCooldown = AbilityCooldown.GetFloat();
            AURoleOptions.ShapeshifterDuration = 1f;
        }
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return false;
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

    void UseAbility(PlayerControl player)
    {
        if (Explosive != null) return;
        
        Vector2 pos = player.Pos();
        Explosive = new TNT(pos);
        RealPosition = pos;

        player.FreezeForOthers();

        ExplodeTS = Utils.TimeStamp + ExplosionDelay.GetInt();
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!GameStates.IsInTask || ExileController.Instance || AntiBlackout.SkipTasks) return;

        Explosive?.TP(pc.transform.position);

        if (Explosive != null && (ExplodeTS <= Utils.TimeStamp || !pc.IsAlive()))
        {
            Utils.GetPlayersInRadius(ExplosionRadius.GetFloat(), Explosive.Position).Without(pc).Do(x => x.Suicide(PlayerState.DeathReason.Bombed, pc));

            pc.RevertFreeze(RealPosition);
            
            if (!Options.UsePets.GetBool() || (Options.UsePhantomBasis.GetBool() && Options.UsePhantomBasisForNKs.GetBool()))
                pc.RpcResetAbilityCooldown();
            
            Explosive.Despawn();
            Explosive = null;
            RealPosition = Vector2.zero;
            ExplodeTS = 0;
        }
    }

    public override void OnReportDeadBody()
    {
        if (Explosive != null)
        {
            PlayerControl pc = ExplosivistId.GetPlayer();
            pc.RevertFreeze(RealPosition);
            Explosive.Despawn();
            Explosive = null;
            RealPosition = Vector2.zero;
            ExplodeTS = 0;
        }
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != ExplosivistId || seer.PlayerId != target.PlayerId || (seer.IsModdedClient() && !hud) || meeting || ExplodeTS == 0) return string.Empty;
        return string.Format(Translator.GetString("ExplosivistSuffix"), ExplodeTS - Utils.TimeStamp);
    }
}