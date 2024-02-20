namespace TOHE.Roles.Impostor
{
    internal class CursedWolf : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        public override void Add(byte playerId)
        {
            On = true;
            playerId.SetAbilityUseLimit(Options.GuardSpellTimes.GetInt());
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
            RPC.SendRPCCursedWolfSpellCount(target.PlayerId);
            if (Options.killAttacker.GetBool())
            {
                Logger.Info($"{target.GetNameWithRole().RemoveHtmlTags()} : {target.GetAbilityUseLimit()} curses remain", "CursedWolf");
                Main.PlayerStates[killer.PlayerId].deathReason = PlayerState.DeathReason.Curse;
                killer.SetRealKiller(target);
                target.Kill(killer);
            }

            target.SetKillCooldown(time: kcd);
            return false;
        }
    }
}
