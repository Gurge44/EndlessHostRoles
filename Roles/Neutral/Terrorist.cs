using static TOHE.Options;

namespace TOHE.Roles.Neutral
{
    internal static class Terrorist
    {
        public static void SetupCustomOption()
        {
            SetupRoleOptions(11500, TabGroup.NeutralRoles, CustomRoles.Terrorist);
            CanTerroristSuicideWin = BooleanOptionItem.Create(11510, "CanTerroristSuicideWin", false, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Terrorist]);
            TerroristCanGuess = BooleanOptionItem.Create(11511, "CanGuess", true, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Terrorist]);
            TerroristTasks = OverrideTasksData.Create(11512, TabGroup.NeutralRoles, CustomRoles.Terrorist);
        }
    }
}
