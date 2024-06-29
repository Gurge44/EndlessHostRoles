using static EHR.Options;

namespace EHR.Neutral
{
    internal class Terrorist : ISettingHolder
    {
        public void SetupCustomOption()
        {
            SetupRoleOptions(11500, TabGroup.NeutralRoles, CustomRoles.Terrorist);
            CanTerroristSuicideWin = new BooleanOptionItem(11510, "CanTerroristSuicideWin", false, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Terrorist]);
            TerroristCanGuess = new BooleanOptionItem(11511, "CanGuess", true, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Terrorist]);
            OverrideTasksData.Create(11512, TabGroup.NeutralRoles, CustomRoles.Terrorist);
        }
    }
}