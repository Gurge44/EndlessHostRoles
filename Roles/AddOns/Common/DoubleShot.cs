using System.Collections.Generic;
using static TOHE.Options;

namespace TOHE.Roles.AddOns.Common
{
    public class DoubleShot : IAddon
    {
        public static List<byte> IsActive = [];

        public static void Init()
        {
            IsActive = [];
        }

        public AddonTypes Type => AddonTypes.Helpful;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(13900, CustomRoles.DoubleShot, canSetNum: true, tab: TabGroup.Addons);
            ImpCanBeDoubleShot = BooleanOptionItem.Create(13910, "ImpCanBeDoubleShot", true, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.DoubleShot]);
            CrewCanBeDoubleShot = BooleanOptionItem.Create(13911, "CrewCanBeDoubleShot", true, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.DoubleShot]);
            NeutralCanBeDoubleShot = BooleanOptionItem.Create(13912, "NeutralCanBeDoubleShot", true, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.DoubleShot]);
        }
    }
}