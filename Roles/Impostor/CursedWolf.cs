using AmongUs.GameOptions;
using EHR.Neutral;

namespace EHR.Impostor;

internal class CursedWolf : RoleBase
{
    public static bool On;
    private bool CanVent;
    private bool HasImpostorVision;
    private bool IsJinx;
    private bool KillAttacker;

    private float KillCooldown;
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(1000, TabGroup.ImpostorRoles, CustomRoles.CursedWolf); // From TOH_Y

        Options.GuardSpellTimes = new IntegerOptionItem(1010, "GuardSpellTimes", new(1, 15, 1), 1, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.CursedWolf])
            .SetValueFormat(OptionFormat.Times);

        Options.killAttacker = new BooleanOptionItem(1011, "killAttacker", true, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.CursedWolf]);
    }

    public override void Add(byte playerId)
    {
        On = true;
        IsJinx = Main.PlayerStates[playerId].MainRole == CustomRoles.Jinx;
        playerId.SetAbilityUseLimit(IsJinx ? Jinx.JinxSpellTimes.GetInt() : Options.GuardSpellTimes.GetInt());

        if (IsJinx)
        {
            KillCooldown = Jinx.KillCooldown.GetFloat();
            CanVent = Jinx.CanVent.GetBool();
            HasImpostorVision = Jinx.HasImpostorVision.GetBool();
            KillAttacker = Jinx.KillAttacker.GetBool();
        }
        else
        {
            KillCooldown = Options.DefaultKillCooldown;
            CanVent = true;
            HasImpostorVision = true;
            KillAttacker = Options.killAttacker.GetBool();
        }
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        opt.SetVision(HasImpostorVision);
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return CanVent;
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown;
    }

    public override void Init()
    {
        On = false;
    }

    public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
    {
        if (target.GetAbilityUseLimit() <= 0) return true;

        if (killer.Is(CustomRoles.Pestilence)) return true;

        if (killer == target) return true;

        float kcd = Main.KillTimers[target.PlayerId] + Main.AllPlayerKillCooldown[target.PlayerId];

        killer.RpcGuardAndKill(target);
        target.RpcRemoveAbilityUse();
        Logger.Info($"{target.GetNameWithRole().RemoveHtmlTags()} : {target.GetAbilityUseLimit()} curses remain", "CursedWolf");

        if (KillAttacker)
        {
            Main.PlayerStates[killer.PlayerId].deathReason = PlayerState.DeathReason.Curse;
            killer.SetRealKiller(target);
            target.Kill(killer);
            LateTask.New(() => { target.SetKillCooldown(kcd); }, 0.1f, log: false);
        }

        return false;
    }
}