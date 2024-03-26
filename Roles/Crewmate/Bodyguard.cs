using UnityEngine;
using static EHR.Options;

namespace EHR.Roles.Crewmate
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

        public static void SetupCustomOption()
        {
            SetupRoleOptions(8400, TabGroup.CrewmateRoles, CustomRoles.Bodyguard);
            BodyguardProtectRadius = FloatOptionItem.Create(8410, "BodyguardProtectRadius", new(0.5f, 5f, 0.5f), 1.5f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bodyguard])
                .SetValueFormat(OptionFormat.Multiplier);
            BodyguardKillsKiller = BooleanOptionItem.Create(8411, "BodyguardKillsKiller", false, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bodyguard]);
        }

        public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
        {
            if (killer.PlayerId != target.PlayerId)
            {
                float dis = Vector2.Distance(target.Pos(), killer.Pos());
                if (dis > BodyguardProtectRadius.GetFloat()) return true;

                if (target.Is(CustomRoles.Madmate) && killer.Is(Team.Impostor))
                {
                    Logger.Info($"{target.GetRealName()} is a madmate, so they chose to ignore the murder scene", "Bodyguard");
                    return true;
                }

                if (BodyguardKillsKiller.GetBool()) target.Kill(killer);
                else killer.SetKillCooldown();
                target.Suicide(PlayerState.DeathReason.Sacrifice, killer);
                Logger.Info($"{target.GetRealName()} stood up and died for {killer.GetRealName()}", "Bodyguard");
                return false;
            }

            return true;
        }
    }
}
