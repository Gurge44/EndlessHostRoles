using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TOHE.Roles.Impostor
{
    internal class Trickster : ISettingHolder
    {
        public void SetupCustomOption() => Options.SetupRoleOptions(4300, TabGroup.ImpostorRoles, CustomRoles.Trickster);
    }
}
