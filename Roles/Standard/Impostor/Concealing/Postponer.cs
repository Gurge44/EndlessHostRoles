using EHR.Modules;

namespace EHR.Roles;

public class Postponer : RoleBase
{
    public static bool On;

    public override bool IsEnable => On;

    private static OptionItem KillCooldown;
    private static OptionItem BodyDelay;

    public override void SetupCustomOption()
    {
        StartSetup(655000)
            .AutoSetupOption(ref KillCooldown, 30f, new FloatValueRule(0.5f, 120f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref BodyDelay, 10, new IntegerValueRule(1, 120, 1), OptionFormat.Seconds);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (!base.OnCheckMurder(killer, target) || !killer.RpcCheckAndMurder(target, check: true)) return false;
        
        if (target.Is(CustomRoles.Bait)) return true;
        
        RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
        killer.RpcRemoveAbilityUse();
        target.SetRealKiller(killer);
        Main.PlayerStates[target.PlayerId].deathReason = PlayerState.DeathReason.Kill;
        target.RpcExileV2();
        target.Data.IsDead = true;
        Main.PlayerStates[target.PlayerId].SetDead();
        Utils.AfterPlayerDeathTasks(target);
        target.SetRealKiller(killer);
        killer.SetKillCooldown(KillCooldown.GetFloat());
        
        Vector2 position = target.Pos();
        byte colorId = (byte)target.Data.DefaultOutfit.ColorId;
        
        LateTask.New(() =>
        {
            if (!GameStates.IsInTask || ExileController.Instance || AntiBlackout.SkipTasks) return;
            Utils.RpcCreateDeadBody(position, colorId, target);
        }, BodyDelay.GetInt(), "Postponer Body Delay");
        
        return false;
    }
}