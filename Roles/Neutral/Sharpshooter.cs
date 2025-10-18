using System.Linq;
using AmongUs.GameOptions;
using UnityEngine;

namespace EHR.Neutral;

public class Sharpshooter : RoleBase
{
    public static bool On;

    public static OptionItem AbilityCooldown;
    public static OptionItem AbilityDuration;
    private static OptionItem SpeedIncreasement;
    private static OptionItem KillCooldown;
    private static OptionItem CanVent;
    private static OptionItem ImpostorVision;

    public override bool IsEnable => On;

    private long AbilityEndTS;
    private Vector2 RealPosition;
    private byte SharpshooterId;

    public override void SetupCustomOption()
    {
        StartSetup(655800)
            .AutoSetupOption(ref AbilityCooldown, 30f, new FloatValueRule(0f, 180f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref AbilityDuration, 10f, new FloatValueRule(1f, 30f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref SpeedIncreasement, 1f, new FloatValueRule(0f, 3f, 0.1f), OptionFormat.Multiplier)
            .AutoSetupOption(ref KillCooldown, 22.5f, new FloatValueRule(0f, 180f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref CanVent, true)
            .AutoSetupOption(ref ImpostorVision, true);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        AbilityEndTS = 0;
        RealPosition = Vector2.zero;
        SharpshooterId = playerId;
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        if (AbilityEndTS == 0) opt.SetVision(ImpostorVision.GetBool());

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

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return CanVent.GetBool();
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return AbilityEndTS == 0;
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
        if (AbilityEndTS != 0) return;

        RealPosition = pc.Pos();
        AbilityEndTS = Utils.TimeStamp + AbilityDuration.GetInt();
        Main.AllPlayerSpeed[pc.PlayerId] += SpeedIncreasement.GetFloat();
        Main.PlayerStates[pc.PlayerId].IsBlackOut = true;
        pc.FreezeForOthers();
        pc.MarkDirtySettings();
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!GameStates.IsInTask || ExileController.Instance || AntiBlackout.SkipTasks || AbilityEndTS == 0) return;

        if (!pc.IsAlive())
        {
            RevertAbility();
            pc.MarkDirtySettings();
            pc.RevertFreeze(RealPosition);
        }
        else if (Utils.TimeStamp >= AbilityEndTS)
        {
            RevertAbility();
            pc.MarkDirtySettings();
            pc.RevertFreeze(RealPosition);
            pc.Suicide(PlayerState.DeathReason.Misfire);
        }
        else
        {
            var pos = pc.Pos();
            var killRange = NormalGameOptionsV10.KillDistances[Mathf.Clamp(Main.NormalOptions.KillDistance, 0, 2)];
            var nearPlayers = Main.AllAlivePlayerControls.Without(pc).Select(x => (pc: x, distance: Vector2.Distance(x.Pos(), pos))).Where(x => x.distance <= killRange).ToArray();
            PlayerControl closestPlayer = nearPlayers.Length == 0 ? null : nearPlayers.MinBy(x => x.distance).pc;
            if (closestPlayer == null || !pc.RpcCheckAndMurder(closestPlayer, check: true)) return;
            if (!Options.UsePets.GetBool()) pc.RpcResetAbilityCooldown();
            RevertAbility();
            pc.SetKillCooldown();
            closestPlayer.Suicide(PlayerState.DeathReason.Kill, pc);
            pc.MarkDirtySettings();
            pc.RevertFreeze(RealPosition);
        }
    }

    public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
    {
        if (AbilityEndTS == 0) return true;
        RevertAbility();
        target.MarkDirtySettings();
        target.RevertFreeze(RealPosition);
        return true;
    }

    public override void OnReportDeadBody()
    {
        if (AbilityEndTS == 0) return;
        RevertAbility();
        var pc = SharpshooterId.GetPlayer();
        if (pc == null) return;
        pc.RevertFreeze(RealPosition);
    }

    private void RevertAbility()
    {
        Main.AllPlayerSpeed[SharpshooterId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
        Main.PlayerStates[SharpshooterId].IsBlackOut = false;
        AbilityEndTS = 0;
    }
}