using static EHR.Options;

namespace EHR.Roles.Neutral
{
    internal class Terrorist : ISettingHolder
    {
        public void SetupCustomOption()
        {
            SetupRoleOptions(11500, TabGroup.NeutralRoles, CustomRoles.Terrorist);
            CanTerroristSuicideWin = BooleanOptionItem.Create(11510, "CanTerroristSuicideWin", false, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Terrorist]);
            TerroristCanGuess = BooleanOptionItem.Create(11511, "CanGuess", true, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Terrorist]);
            TerroristTasks = OverrideTasksData.Create(11512, TabGroup.NeutralRoles, CustomRoles.Terrorist);
        }
    }
}
