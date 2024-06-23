using static EHR.Options;

namespace EHR.AddOns.Common
{
    internal class Truant : IAddon
    {
        public AddonTypes Type => AddonTypes.Harmful;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(15437, CustomRoles.Truant, canSetNum: true, teamSpawnOptions: true);
            TruantWaitingTime = new IntegerOptionItem(15443, "TruantWaitingTime", new(1, 90, 1), 3, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Truant])
                .SetValueFormat(OptionFormat.Seconds);
        }
    }
}