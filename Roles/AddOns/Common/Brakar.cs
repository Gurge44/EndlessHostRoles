using static EHR.Options;

namespace EHR.Roles.AddOns.Common
{
    internal class Brakar : IAddon
    {
        public AddonTypes Type => AddonTypes.Mixed;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(14900, CustomRoles.Brakar, canSetNum: true, teamSpawnOptions: true);
        }
    }
}