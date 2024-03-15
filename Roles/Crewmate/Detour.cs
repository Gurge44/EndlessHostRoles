using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TOHE.Roles.Crewmate
{
    internal class Detour : ISettingHolder
    {
        public void SetupCustomOption() => Options.SetupRoleOptions(5590, TabGroup.CrewmateRoles, CustomRoles.Detour);
    }
}
