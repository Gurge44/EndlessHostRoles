using UnityEngine;

namespace TOHE.Roles.Crewmate
{
    internal class Bodyguard : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        public override void Init()
        {
            On = false;
        }

        public override void Add(byte playerId)
        {
            On = true;
        }

        public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
        {
            if (killer.PlayerId != target.PlayerId)
            {
                float dis = Vector2.Distance(target.Pos(), killer.Pos());
                if (dis > Options.BodyguardProtectRadius.GetFloat()) return true;

                if (target.Is(CustomRoles.Madmate) && killer.Is(Team.Impostor))
                {
                    Logger.Info($"{target.GetRealName()} is a madmate, so they chose to ignore the murder scene", "Bodyguard");
                    return true;
                }

                if (Options.BodyguardKillsKiller.GetBool()) target.Kill(killer);
                else killer.SetKillCooldown();
                target.Suicide(PlayerState.DeathReason.Sacrifice, killer);
                Logger.Info($"{target.GetRealName()} stood up and died for {killer.GetRealName()}", "Bodyguard");
                return false;
            }

            return true;
        }
    }
}
