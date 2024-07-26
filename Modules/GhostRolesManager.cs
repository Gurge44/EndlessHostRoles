﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AmongUs.GameOptions;
using EHR.AddOns.GhostRoles;
using EHR.Neutral;
using HarmonyLib;
using UnityEngine;

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

            IGhostRole instance = CreateGhostRoleInstance(suitableRole);
            pc.RpcSetCustomRole(suitableRole);
            pc.RpcSetRole(RoleTypes.GuardianAngel);
            instance.OnAssign(pc);
            Main.ResetCamPlayerList.Add(pc.PlayerId);
            AssignedGhostRoles[pc.PlayerId] = (suitableRole, instance);

            if (suitableRole == CustomRoles.Haunter) GhostRoles.Remove(suitableRole);

            NotifyAboutGhostRole(pc, true);
        }

        public static void SpecificAssignGhostRole(byte id, CustomRoles role, bool set)
        {
            if (AssignedGhostRoles.Any(x => x.Key == id || x.Value.Role == role)) return;

            var pc = Utils.GetPlayerById(id);

            IGhostRole instance = CreateGhostRoleInstance(role);
            if (set) pc.RpcSetRole(RoleTypes.GuardianAngel);
            instance.OnAssign(pc);
            Main.ResetCamPlayerList.Add(pc.PlayerId);
            AssignedGhostRoles[id] = (role, instance);
        }

        public static void NotifyAboutGhostRole(PlayerControl pc, bool first = false)
        {
            if (!AssignedGhostRoles.TryGetValue(pc.PlayerId, out var ghostRole)) return;
            if (!first && pc.IsModClient()) return;

            CustomRoles role = ghostRole.Role;
            var info = GetMessage(Translator.GetString($"{role}InfoLong").Split("\n")[1..].Join(delimiter: "\n"));
            var text = $"{Translator.GetString("GotGhostRoleNotify")}\n<size=80%>{info.Message}</size>";
            var notifyText = $"{Translator.GetString("GotGhostRoleNotify")}\n<size=80%>{info.Split}</size>";
            Utils.SendMessage(title: text, sendTo: pc.PlayerId, text: "\n");
            pc.Notify(notifyText, 5 * text.Count(x => x == '\n'));
            return;

            (string Split, string Message) GetMessage(string baseMessage)
            {
                var message = baseMessage;
                for (int i = 50; i < message.Length; i += 50)
                {
                    int index = message.LastIndexOf(' ', i);
                    if (index != -1)
                    {
                        message = message.Insert(index + 1, "\n");
                    }
                }

                return (ApplyFormat(message), ApplyFormat(baseMessage));

                string ApplyFormat(string m) => Utils.ColorString(Color.white, m.Replace(role.ToString(), role.ToColoredString()));
            }
        }

        public static bool ShouldHaveGhostRole(PlayerControl pc)
        {
            try
            {
                if (Options.CurrentGameMode != CustomGameMode.Standard) return false;
                if (AssignedGhostRoles.Count >= GhostRoles.Count) return false;
                if (pc.IsAlive() || pc.GetCountTypes() is CountTypes.None or CountTypes.OutOfGame || pc.Is(CustomRoles.EvilSpirit)) return false;

                var suitableRole = GetSuitableGhostRole(pc);
                return suitableRole switch
                {
                    CustomRoles.Specter when IsPartnerPickedRole() => false,
                    _ => suitableRole.IsGhostRole() && !AssignedGhostRoles.Any(x => x.Key == pc.PlayerId || x.Value.Role == suitableRole)
                };

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