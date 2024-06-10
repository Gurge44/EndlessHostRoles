using static EHR.Options;

namespace EHR.Roles.AddOns.Common
{
    internal class Glow : IAddon
    {
        public AddonTypes Type => AddonTypes.Mixed;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(14020, CustomRoles.Glow, canSetNum: true, teamSpawnOptions: true);
        }
    }
}