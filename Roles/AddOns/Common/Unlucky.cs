using static EHR.Options;

namespace EHR.Roles.AddOns.Common
{
    internal class Unlucky : IAddon
    {
        public AddonTypes Type => AddonTypes.Harmful;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(14350, CustomRoles.Unlucky, canSetNum: true);
            UnluckyKillSuicideChance = IntegerOptionItem.Create(14364, "UnluckyKillSuicideChance", new(0, 100, 1), 2, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Unlucky])
                .SetValueFormat(OptionFormat.Percent);
            UnluckyTaskSuicideChance = IntegerOptionItem.Create(14365, "UnluckyTaskSuicideChance", new(0, 100, 1), 5, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Unlucky])
                .SetValueFormat(OptionFormat.Percent);
            UnluckyVentSuicideChance = IntegerOptionItem.Create(14366, "UnluckyVentSuicideChance", new(0, 100, 1), 3, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Unlucky])
                .SetValueFormat(OptionFormat.Percent);
            UnluckyReportSuicideChance = IntegerOptionItem.Create(14367, "UnluckyReportSuicideChance", new(0, 100, 1), 1, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Unlucky])
                .SetValueFormat(OptionFormat.Percent);
            UnluckySabotageSuicideChance = IntegerOptionItem.Create(14368, "UnluckySabotageSuicideChance", new(0, 100, 1), 4, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Unlucky])
                .SetValueFormat(OptionFormat.Percent);
            ImpCanBeUnlucky = BooleanOptionItem.Create(14361, "ImpCanBeUnlucky", true, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Unlucky]);
            CrewCanBeUnlucky = BooleanOptionItem.Create(14362, "CrewCanBeUnlucky", true, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Unlucky]);
            NeutralCanBeUnlucky = BooleanOptionItem.Create(14363, "NeutralCanBeUnlucky", true, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Unlucky]);
        }
    }
}
