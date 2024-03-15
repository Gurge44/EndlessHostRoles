using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TOHE.Roles.Crewmate
{
    internal class Dictator : ISettingHolder
    {
        public void SetupCustomOption() => Options.SetupRoleOptions(9100, TabGroup.CrewmateRoles, CustomRoles.Dictator);
    }
}
