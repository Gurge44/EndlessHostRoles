using static EHR.Options;

namespace EHR.Roles.AddOns.Common
{
    internal class Lucky : IAddon
    {
        public AddonTypes Type => AddonTypes.Mixed;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(14300, CustomRoles.Lucky, canSetNum: true);
            LuckyProbability = IntegerOptionItem.Create(14310, "LuckyProbability", new(0, 100, 5), 50, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Lucky])
                .SetValueFormat(OptionFormat.Percent);
            ImpCanBeLucky = BooleanOptionItem.Create(14311, "ImpCanBeLucky", true, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Lucky]);
            CrewCanBeLucky = BooleanOptionItem.Create(14312, "CrewCanBeLucky", true, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Lucky]);
            NeutralCanBeLucky = BooleanOptionItem.Create(14313, "NeutralCanBeLucky", true, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Lucky]);
        }
    }
}
