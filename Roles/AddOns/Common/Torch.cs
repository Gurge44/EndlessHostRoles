using static EHR.Options;

namespace EHR.AddOns.Common;

internal class Torch : IAddon
{
    public AddonTypes Type => AddonTypes.Helpful;

    public void SetupCustomOption()
    {
        SetupAdtRoleOptions(14200, CustomRoles.Torch, canSetNum: true, teamSpawnOptions: true);

        TorchVision = new FloatOptionItem(14210, "TorchVision", new(0.5f, 5f, 0.25f), 1.25f, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Torch])
            .SetValueFormat(OptionFormat.Multiplier);

        TorchAffectedByLights = new BooleanOptionItem(14220, "TorchAffectedByLights", false, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Torch]);
    }
}