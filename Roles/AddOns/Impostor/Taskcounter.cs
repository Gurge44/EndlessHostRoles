using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TOHE.Roles.AddOns.Impostor
{
    internal class Taskcounter : IAddon
    {
        public AddonTypes Type => AddonTypes.ImpOnly;

        public void SetupCustomOption()
        {
            Options.SetupAdtRoleOptions(14370, CustomRoles.Taskcounter, canSetNum: true);
        }
    }
}
