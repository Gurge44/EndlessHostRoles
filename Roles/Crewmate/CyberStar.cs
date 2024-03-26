using static EHR.Options;

namespace EHR.Roles.Crewmate
{
    internal class CyberStar : ISettingHolder
    {
        public void SetupCustomOption()
        {
            SetupRoleOptions(5300, TabGroup.CrewmateRoles, CustomRoles.CyberStar);
            ImpKnowCyberStarDead = BooleanOptionItem.Create(5400, "ImpKnowCyberStarDead", false, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.CyberStar]);
            NeutralKnowCyberStarDead = BooleanOptionItem.Create(5500, "NeutralKnowCyberStarDead", false, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.CyberStar]);
        }
    }
}
