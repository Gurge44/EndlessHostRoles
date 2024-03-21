using static EHR.Options;

namespace EHR.Roles.AddOns.Common
{
    internal class Glow : IAddon
    {
        public AddonTypes Type => AddonTypes.Mixed;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(14020, CustomRoles.Glow, canSetNum: true);
            ImpCanBeGlow = BooleanOptionItem.Create(14030, "ImpCanBeGlow", true, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Glow]);
            CrewCanBeGlow = BooleanOptionItem.Create(14031, "CrewCanBeGlow", true, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Glow]);
            NeutralCanBeGlow = BooleanOptionItem.Create(14032, "NeutralCanBeGlow", true, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Glow]);
        }
    }
}