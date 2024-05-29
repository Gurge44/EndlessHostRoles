using static EHR.Options;

namespace EHR.Roles.AddOns.Common
{
    internal class Trapper : IAddon
    {
        public AddonTypes Type => AddonTypes.Mixed;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(13800, CustomRoles.Trapper, canSetNum: true, teamSpawnOptions: true);
            TrapperBlockMoveTime = FloatOptionItem.Create(13813, "TrapperBlockMoveTime", new(0f, 180f, 1f), 5f, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Trapper])
                .SetValueFormat(OptionFormat.Seconds);
        }
    }
}