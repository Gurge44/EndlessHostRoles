using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TOHE.Roles.AddOns.GhostRoles
{
    public interface IGhostRole
    {
        public Team Team { get; }
        public void OnProtect(PlayerControl pc, PlayerControl target);
    }
}
