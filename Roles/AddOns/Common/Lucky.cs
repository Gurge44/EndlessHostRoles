using static EHR.Options;

namespace EHR.AddOns.Common
{
    internal class Lucky : IAddon
    {
        public AddonTypes Type => AddonTypes.Mixed;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(14300, CustomRoles.Lucky, canSetNum: true, teamSpawnOptions: true);

            LuckyProbability = new IntegerOptionItem(14310, "LuckyProbability", new(0, 100, 5), 50, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Lucky])
                .SetValueFormat(OptionFormat.Percent);
        }
    }
}