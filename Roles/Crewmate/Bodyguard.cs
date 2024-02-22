namespace TOHE.Roles.Crewmate
{
    internal static class Bodyguard
    {
        public static bool OnCheckMurderAsTarget(PlayerControl pc, PlayerControl killer)
        {
            if (pc.Is(CustomRoles.Madmate) && killer.Is(Team.Impostor))
            {
                Logger.Info($"{pc.GetRealName()} is a madmate, so they chose to ignore the murder scene", "Bodyguard");
            }
            else
            {
                if (Options.BodyguardKillsKiller.GetBool()) pc.Kill(killer);
                else killer.SetKillCooldown();
                pc.Suicide(PlayerState.DeathReason.Sacrifice, killer);
                Logger.Info($"{pc.GetRealName()} stood up and died for {killer.GetRealName()}", "Bodyguard");
                return false;
            }

            return true;
        }
    }
}
