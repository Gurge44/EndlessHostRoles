using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TOHE.Roles.Crewmate
{
    internal class Needy : ISettingHolder
    {
        public void SetupCustomOption() => Options.SetupRoleOptions(5700, TabGroup.CrewmateRoles, CustomRoles.Needy);
    }
}
