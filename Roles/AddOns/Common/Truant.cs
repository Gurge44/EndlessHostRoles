using static EHR.Options;

namespace EHR.Roles.AddOns.Common
{
    internal class Truant : IAddon
    {
        public AddonTypes Type => AddonTypes.Harmful;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(15435, CustomRoles.Truant, canSetNum: true);
            TruantWaitingTime = IntegerOptionItem.Create(15438, "TruantWaitingTime", new(1, 90, 1), 3, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Truant])
                .SetValueFormat(OptionFormat.Seconds);
        }
    }
}
