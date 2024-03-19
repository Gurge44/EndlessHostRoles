using System.Collections.Generic;
using System.Linq;
using TOHE.Roles.AddOns.GhostRoles;

namespace TOHE.Modules
{
    internal static class GhostRolesManager
    {
        public static Dictionary<byte, (CustomRoles Role, IGhostRole Instance)> AssignedGhostRoles = [];
        public static List<CustomRoles> GhostRoles = [];

        public static void Initialize()
        {
            AssignedGhostRoles = [];
            if (GhostRoles.Count > 0) return;
            GhostRoles = EnumHelper.GetAllValues<CustomRoles>().Where(x => x.IsGhostRole() && x.IsEnable()).ToList();

            Haunter.AllHauntedPlayers = [];
        }

        public static void AssignGhostRole(PlayerControl pc)
        {
            if (GhostRoles.Count == 0) return;

            var suitableRole = GetSuitableGhostRole(pc);

            pc.RpcSetCustomRole(suitableRole);
            IGhostRole instance = Utils.CreateGhostRoleInstance(suitableRole);
            instance.OnAssign(pc);
            AssignedGhostRoles[pc.PlayerId] = (suitableRole, instance);
        }

        public static void SpecificAssignGhostRole(byte id, CustomRoles role, bool set)
        {
            if (AssignedGhostRoles.Any(x => x.Key == id || x.Value.Role == role)) return;

            if (set) Utils.GetPlayerById(id).RpcSetCustomRole(role);
            IGhostRole instance = Utils.CreateGhostRoleInstance(role);
            instance.OnAssign(Utils.GetPlayerById(id));
            AssignedGhostRoles[id] = (role, instance);
        }

        public static bool ShouldHaveGhostRole(PlayerControl pc)
        {
            if (pc.IsAlive() || pc.GetCountTypes() is CountTypes.Crew or CountTypes.OutOfGame) return false;
            var suitableRole = GetSuitableGhostRole(pc);
            return !AssignedGhostRoles.Any(x => x.Key == pc.PlayerId || x.Value.Role == suitableRole);
        }

        public static CustomRoles GetSuitableGhostRole(PlayerControl pc)
        {
            return GhostRoles.FirstOrDefault(x => AssignedGhostRoles.All(r => r.Value.Role != x) && Utils.CreateGhostRoleInstance(x).Team == pc.GetTeam());
        }
    }
}
