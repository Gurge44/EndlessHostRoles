using AmongUs.GameOptions;
using EHR.Roles.Neutral;

namespace EHR.Roles.Impostor
{
    internal class CursedWolf : RoleBase
    {
        private bool IsJinx;

        private float KillCooldown;
        private bool CanVent;
        private bool HasImpostorVision;
        private bool KillAttacker;

        public static bool On;
        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(1000, TabGroup.ImpostorRoles, CustomRoles.CursedWolf); //TOH_Y
            Options.GuardSpellTimes = IntegerOptionItem.Create(1010, "GuardSpellTimes", new(1, 15, 1), 3, TabGroup.ImpostorRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.CursedWolf])
                .SetValueFormat(OptionFormat.Times);
            Options.killAttacker = BooleanOptionItem.Create(1011, "killAttacker", true, TabGroup.ImpostorRoles, false)
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

            if (!AmongUsClient.Instance.AmHost || !IsJinx) return;
            if (!Main.ResetCamPlayerList.Contains(playerId))
                Main.ResetCamPlayerList.Add(playerId);
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

            var kcd = Main.KillTimers[target.PlayerId] + Main.AllPlayerKillCooldown[target.PlayerId];

            killer.RpcGuardAndKill(target);
            target.RpcRemoveAbilityUse();
            Logger.Info($"{target.GetNameWithRole().RemoveHtmlTags()} : {target.GetAbilityUseLimit()} curses remain", "CursedWolf");

            if (KillAttacker)
            {
                Main.PlayerStates[killer.PlayerId].deathReason = PlayerState.DeathReason.Curse;
                killer.SetRealKiller(target);
                target.Kill(killer);
                _ = new LateTask(() => { target.SetKillCooldown(time: kcd); }, 0.1f, log: false);
            }

            return false;
        }
    }
}
