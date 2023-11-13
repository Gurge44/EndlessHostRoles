using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TOHE.Roles.Crewmate
{
    public static class GuessManagerRole
    {
        public static List<byte> playerIdList = new();
        public static void SetupCustomOption() => Options.SetupRoleOptions(642640, TabGroup.CrewmateRoles, CustomRoles.GuessManager);
        public static void Init() => playerIdList = new();
        public static void Add(byte playerId) => playerIdList.Add(playerId);

    }
}
