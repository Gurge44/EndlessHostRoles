using static EHR.Options;

namespace EHR.Roles.AddOns.Common
{
    internal class Sunglasses : IAddon
    {
        public AddonTypes Type => AddonTypes.Harmful;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(15450, CustomRoles.Sunglasses, canSetNum: true, teamSpawnOptions: true);
            SunglassesVision = FloatOptionItem.Create(15460, "SunglassesVision", new(0f, 5f, 0.05f), 0.75f, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Sunglasses])
                .SetValueFormat(OptionFormat.Multiplier);
        }
    }
}