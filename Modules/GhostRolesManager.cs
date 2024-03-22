using AmongUs.GameOptions;
using EHR.Roles.AddOns.GhostRoles;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;

namespace EHR.Modules
{
    internal static class GhostRolesManager
    {
        public static Dictionary<byte, (CustomRoles Role, IGhostRole Instance)> AssignedGhostRoles = [];
        public static List<CustomRoles> GhostRoles = [];

        public static void Initialize()
        {
            AssignedGhostRoles = [];
            if (GhostRoles.Count > 0) return;
            GhostRoles = EnumHelper.GetAllValues<CustomRoles>().Where(x => x != CustomRoles.EvilSpirit && x.IsGhostRole() && x.IsEnable()).ToList();

            Logger.Msg($"Ghost roles: {GhostRoles.Join()}", "GhostRoles");
            Haunter.AllHauntedPlayers = [];
        }

        public static void AssignGhostRole(PlayerControl pc)
        {
            if (GhostRoles.Count == 0) return;

            var suitableRole = GetSuitableGhostRole(pc);
            Logger.Warn($"Assigning Ghost Role: {pc.GetNameWithRole()} => {suitableRole}", "GhostRolesManager");

            pc.RpcSetCustomRole(suitableRole);
            pc.RpcSetRole(RoleTypes.GuardianAngel);
            IGhostRole instance = Utils.CreateGhostRoleInstance(suitableRole);
            instance.OnAssign(pc);
            AssignedGhostRoles[pc.PlayerId] = (suitableRole, instance);
        }

        public static void SpecificAssignGhostRole(byte id, CustomRoles role, bool set)
        {
            if (AssignedGhostRoles.Any(x => x.Key == id || x.Value.Role == role)) return;

            var pc = Utils.GetPlayerById(id);
            if (set) pc.RpcSetRole(RoleTypes.GuardianAngel);

            IGhostRole instance = Utils.CreateGhostRoleInstance(role);
            instance.OnAssign(pc);
            AssignedGhostRoles[id] = (role, instance);
        }

        public static bool ShouldHaveGhostRole(PlayerControl pc)
        {
            try
            {
                if (AssignedGhostRoles.Count >= GhostRoles.Count) return false;
                if (pc.IsAlive() || pc.GetCountTypes() is CountTypes.None or CountTypes.OutOfGame) return false;
                var suitableRole = GetSuitableGhostRole(pc);
                return !AssignedGhostRoles.Any(x => x.Key == pc.PlayerId || x.Value.Role == suitableRole);
            }
            catch (System.Exception e)
            {
                Utils.ThrowException(e);
                return false;
            }
        }

        public static CustomRoles GetSuitableGhostRole(PlayerControl pc)
        {
            return GhostRoles.FirstOrDefault(x => AssignedGhostRoles.All(r => r.Value.Role != x) && (Utils.CreateGhostRoleInstance(x)?.Team & pc.GetTeam()) != 0);
        }
    }
}
