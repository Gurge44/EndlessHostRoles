using static EHR.Options;

namespace EHR.AddOns.Common;

internal class Schizophrenic : IAddon
{
    public AddonTypes Type => AddonTypes.Mixed;

    public void SetupCustomOption()
    {
        SetupAdtRoleOptions(14700, CustomRoles.Schizophrenic, canSetNum: true, teamSpawnOptions: true);

        DualVotes = new BooleanOptionItem(14712, "DualVotes", true, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Schizophrenic]);
    }
}