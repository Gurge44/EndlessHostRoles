using static EHR.Options;

namespace EHR.Crewmate
{
    internal class CyberStar : ISettingHolder
    {
        public void SetupCustomOption()
        {
            SetupRoleOptions(5300, TabGroup.CrewmateRoles, CustomRoles.CyberStar);
            ImpKnowCyberStarDead = new BooleanOptionItem(5400, "ImpKnowCyberStarDead", false, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.CyberStar]);
            NeutralKnowCyberStarDead = new BooleanOptionItem(5500, "NeutralKnowCyberStarDead", false, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.CyberStar]);
        }
    }
}