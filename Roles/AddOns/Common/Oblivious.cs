using static EHR.Options;

namespace EHR.AddOns.Common;

internal class Oblivious : IAddon
{
    public AddonTypes Type => AddonTypes.Harmful;

    public void SetupCustomOption()
    {
        SetupAdtRoleOptions(15400, CustomRoles.Oblivious, canSetNum: true, teamSpawnOptions: true);

        ObliviousBaitImmune = new BooleanOptionItem(15413, "ObliviousBaitImmune", false, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Oblivious]);
    }
}