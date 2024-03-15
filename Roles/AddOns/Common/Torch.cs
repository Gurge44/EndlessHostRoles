using static TOHE.Options;

namespace TOHE.Roles.AddOns.Common
{
    internal class Torch : IAddon
    {
        public AddonTypes Type => AddonTypes.Helpful;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(14200, CustomRoles.Torch, canSetNum: true);
            TorchVision = FloatOptionItem.Create(14210, "TorchVision", new(0.5f, 5f, 0.25f), 1.25f, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Torch])
                .SetValueFormat(OptionFormat.Multiplier);
            TorchAffectedByLights = BooleanOptionItem.Create(14220, "TorchAffectedByLights", false, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Torch]);
        }
    }
}
