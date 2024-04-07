using static EHR.Options;

namespace EHR.Roles.AddOns.Common
{
    internal class Trapper : IAddon
    {
        public AddonTypes Type => AddonTypes.Mixed;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(13800, CustomRoles.Trapper, canSetNum: true);
            ImpCanBeTrapper = BooleanOptionItem.Create(13810, "ImpCanBeTrapper", true, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Trapper]);
            CrewCanBeTrapper = BooleanOptionItem.Create(13811, "CrewCanBeTrapper", true, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Trapper]);
            NeutralCanBeTrapper = BooleanOptionItem.Create(13812, "NeutralCanBeTrapper", true, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Trapper]);
            TrapperBlockMoveTime = FloatOptionItem.Create(13813, "TrapperBlockMoveTime", new(0f, 180f, 1f), 5f, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Trapper])
                .SetValueFormat(OptionFormat.Seconds);
        }
    }
}