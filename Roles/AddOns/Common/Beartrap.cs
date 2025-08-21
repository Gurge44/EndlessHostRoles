using static EHR.Options;

namespace EHR.AddOns.Common;

internal class Beartrap : IAddon
{
    public AddonTypes Type => AddonTypes.Mixed;

    public void SetupCustomOption()
    {
        SetupAdtRoleOptions(13800, CustomRoles.Beartrap, canSetNum: true, teamSpawnOptions: true);

        BeartrapBlockMoveTime = new FloatOptionItem(13813, "BeartrapBlockMoveTime", new(0f, 180f, 1f), 5f, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Beartrap])
            .SetValueFormat(OptionFormat.Seconds);
    }
}