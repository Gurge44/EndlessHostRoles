using static TOHE.Options;

namespace TOHE.Roles.Crewmate
{
    internal static class CyberStar
    {
        public static void SetupCustomOption()
        {
            SetupRoleOptions(5300, TabGroup.CrewmateRoles, CustomRoles.CyberStar);
            ImpKnowCyberStarDead = BooleanOptionItem.Create(5400, "ImpKnowCyberStarDead", false, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.CyberStar]);
            NeutralKnowCyberStarDead = BooleanOptionItem.Create(5500, "NeutralKnowCyberStarDead", false, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.CyberStar]);
        }

        public static void OnDeath(PlayerControl seer)
        {
        }
    }
}
