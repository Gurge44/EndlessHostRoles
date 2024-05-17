﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AmongUs.GameOptions;
using EHR.Roles.AddOns.GhostRoles;
using EHR.Roles.Neutral;
using HarmonyLib;

namespace EHR.Modules
{
    internal static class GhostRolesManager
    {
        public static Dictionary<byte, (CustomRoles Role, IGhostRole Instance)> AssignedGhostRoles = [];
        private static List<CustomRoles> GhostRoles = [];

        public static void Initialize()
        {
            AssignedGhostRoles = [];
            GhostRoles = Enum.GetValues<CustomRoles>().Where(x => x != CustomRoles.EvilSpirit && x.IsGhostRole() && x.IsEnable()).ToList();

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
            IGhostRole instance = CreateGhostRoleInstance(suitableRole);
            instance.OnAssign(pc);
            AssignedGhostRoles[pc.PlayerId] = (suitableRole, instance);
        }

        public static void SpecificAssignGhostRole(byte id, CustomRoles role, bool set)
        {
            if (AssignedGhostRoles.Any(x => x.Key == id || x.Value.Role == role)) return;

            var pc = Utils.GetPlayerById(id);
            if (set) pc.RpcSetRole(RoleTypes.GuardianAngel);

            IGhostRole instance = CreateGhostRoleInstance(role);
            instance.OnAssign(pc);
            AssignedGhostRoles[id] = (role, instance);
        }

        public static bool ShouldHaveGhostRole(PlayerControl pc)
        {
            try
            {
                if (AssignedGhostRoles.Count >= GhostRoles.Count) return false;
                if (pc.IsAlive() || pc.GetCountTypes() is CountTypes.None or CountTypes.OutOfGame || pc.Is(CustomRoles.EvilSpirit)) return false;

                var suitableRole = GetSuitableGhostRole(pc);
                switch (suitableRole)
                {
                    case CustomRoles.Specter when IsPartnerPickedRole():
                        return false;
                    case CustomRoles.Haunter:
                        GhostRoles.Remove(CustomRoles.Haunter);
                        break;
                }

                return suitableRole.IsGhostRole() && !AssignedGhostRoles.Any(x => x.Key == pc.PlayerId || x.Value.Role == suitableRole);

                bool IsPartnerPickedRole() => Main.PlayerStates[pc.PlayerId].Role switch
                {
                    Romantic when Romantic.HasPickedPartner => true,
                    Totocalcio tc when tc.BetPlayer != byte.MaxValue => true,
                    _ => false
                };
            }
            catch (Exception e)
            {
                Utils.ThrowException(e);
                return false;
            }
        }

        private static CustomRoles GetSuitableGhostRole(PlayerControl pc)
        {
            return GhostRoles.FirstOrDefault(x => AssignedGhostRoles.All(r => r.Value.Role != x) && (CreateGhostRoleInstance(x)?.Team & pc.GetTeam()) != 0);
        }

        public static IGhostRole CreateGhostRoleInstance(CustomRoles ghostRole, bool check = false)
        {
            try
            {
                var ghostRoleClass = Assembly.GetExecutingAssembly().GetTypes().First(x => typeof(IGhostRole).IsAssignableFrom(x) && !x.IsInterface && x.Name == $"{ghostRole}");
                var ghostRoleInstance = (IGhostRole)Activator.CreateInstance(ghostRoleClass);
                return ghostRoleInstance;
            }
            catch (InvalidOperationException)
            {
                if (!check) Logger.Error($"Ghost role {ghostRole} not found", "CreateGhostRoleInstance");
                return null;
            }
            catch (Exception e)
            {
                if (!check) Utils.ThrowException(e);
                return null;
            }
        }
    }
}