using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TOHE.Roles.Crewmate
{
    internal class Observer : ISettingHolder
    {
        public void SetupCustomOption() => Options.SetupRoleOptions(7500, TabGroup.CrewmateRoles, CustomRoles.Observer);
    }
}
