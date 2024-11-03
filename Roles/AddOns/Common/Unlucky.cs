using static EHR.Options;

namespace EHR.AddOns.Common
{
    internal class Unlucky : IAddon
    {
        public AddonTypes Type => AddonTypes.Harmful;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(14350, CustomRoles.Unlucky, canSetNum: true, teamSpawnOptions: true);

            UnluckyKillSuicideChance = new IntegerOptionItem(14364, "UnluckyKillSuicideChance", new(0, 100, 1), 2, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Unlucky])
                .SetValueFormat(OptionFormat.Percent);

            UnluckyTaskSuicideChance = new IntegerOptionItem(14365, "UnluckyTaskSuicideChance", new(0, 100, 1), 5, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Unlucky])
                .SetValueFormat(OptionFormat.Percent);

            UnluckyVentSuicideChance = new IntegerOptionItem(14366, "UnluckyVentSuicideChance", new(0, 100, 1), 3, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Unlucky])
                .SetValueFormat(OptionFormat.Percent);

            UnluckyReportSuicideChance = new IntegerOptionItem(14367, "UnluckyReportSuicideChance", new(0, 100, 1), 1, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Unlucky])
                .SetValueFormat(OptionFormat.Percent);

            UnluckySabotageSuicideChance = new IntegerOptionItem(14368, "UnluckySabotageSuicideChance", new(0, 100, 1), 4, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Unlucky])
                .SetValueFormat(OptionFormat.Percent);
        }
    }
}