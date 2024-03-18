using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TOHE.Modules
{
    internal static class GhostRolesManager
    {
        public static Dictionary<CustomRoles, byte> AssignedGhostRoles = [];
        public static List<CustomRoles> GhostRoles = [];

        public static void Initialize()
        {
            AssignedGhostRoles = [];
            GhostRoles = EnumHelper.GetAllValues<CustomRoles>().Where(x => x.IsGhostRole() && x.IsEnable()).ToList();
        }

        public static void AssignGhostRole(PlayerControl pc)
        {
            GhostRoles.RemoveAll(AssignedGhostRoles.ContainsKey);
            if (GhostRoles.Count == 0) return;

            var suitableRole = GetSuitableGhostRole(pc);

            pc.RpcSetCustomRole(suitableRole);
            AssignedGhostRoles[suitableRole] = pc.PlayerId;
        }

        public static void SpecificAssignGhostRole(byte id, CustomRoles role, bool set)
        {
            if (AssignedGhostRoles.ContainsKey(role) || AssignedGhostRoles.ContainsValue(id)) return;

            if (set) Utils.GetPlayerById(id).RpcSetCustomRole(role);
            AssignedGhostRoles[role] = id;
        }

        public static bool ShouldHaveGhostRole(PlayerControl pc)
        {
            if (pc.IsAlive() || pc.GetCountTypes() is CountTypes.Crew or CountTypes.OutOfGame) return false;
            if (AssignedGhostRoles.ContainsValue(pc.PlayerId)) return false;
            return !AssignedGhostRoles.ContainsKey(GetSuitableGhostRole(pc));
        }

        public static CustomRoles GetSuitableGhostRole(PlayerControl pc)
        {
            return GhostRoles.FirstOrDefault(x => Utils.CreateGhostRoleInstance(x).Team == pc.GetTeam());
        }
    }
}
