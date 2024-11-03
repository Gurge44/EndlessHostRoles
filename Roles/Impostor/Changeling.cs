using System;
using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using static EHR.Options;

namespace EHR.Impostor
{
    internal class Changeling : RoleBase
    {
        private const int Id = 643510;
        public static readonly Dictionary<byte, bool> ChangedRole = [];

        private static OptionItem CanPickPartnerRole;
        private static OptionItem CanKillBeforeRoleChange;
        private static OptionItem AvailableRoles;

        private static readonly string[] AvailableRolesMode =
        [
            "CL.All", // 0
            "CL.AllImp", // 1
            "CL.AllSS", // 2
            "CL.Enabled", // 3
            "CL.EnabledImp", // 4
            "CL.EnabledSS" // 5
        ];

        private static List<CustomRoles> Roles = [];
        public static bool On;
        private byte ChangelingId;

        public CustomRoles CurrentRole;
        public override bool IsEnable => On;

        public override void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Changeling);
            CanPickPartnerRole = new BooleanOptionItem(Id + 10, "CanPickPartnerRole", true, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Changeling]);
            CanKillBeforeRoleChange = new BooleanOptionItem(Id + 11, "CanKillBeforeRoleChange", true, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Changeling]);
            AvailableRoles = new StringOptionItem(Id + 12, "AvailableRoles", AvailableRolesMode, 0, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Changeling]);
        }

        public static List<CustomRoles> GetAvailableRoles(bool check = false)
        {
            try
            {
                CustomRoles[] allRoles = Enum.GetValues<CustomRoles>();

                IEnumerable<CustomRoles> result = AvailableRoles.GetValue() switch
                {
                    0 => allRoles,
                    1 => allRoles.Where(x => x.GetVNRole(true) is CustomRoles.Impostor or CustomRoles.ImpostorEHR),
                    2 => allRoles.Where(x => x.GetVNRole(true) is CustomRoles.Shapeshifter or CustomRoles.ShapeshifterEHR),
                    3 => allRoles.Where(x => x.GetMode() != 0),
                    4 => allRoles.Where(x => x.GetVNRole(true) is CustomRoles.Impostor or CustomRoles.ImpostorEHR && x.GetMode() != 0),
                    5 => allRoles.Where(x => x.GetVNRole(true) is CustomRoles.Shapeshifter or CustomRoles.ShapeshifterEHR && x.GetMode() != 0),
                    _ => allRoles
                };

                if (!CanPickPartnerRole.GetBool() && !check) result = result.Where(x => !CustomRoleSelector.RoleResult.ContainsValue(x));

                List<CustomRoles> rolesList = result.ToList();
                rolesList.Remove(CustomRoles.Changeling);
                rolesList.RemoveAll(x => !x.IsImpostor() || x.IsVanilla() || x.IsAdditionRole());
                return rolesList;
            }
            catch
            {
                return [];
            }
        }

        public override void Add(byte playerId)
        {
            On = true;
            ChangelingId = playerId;
            ChangedRole[playerId] = false;

            try
            {
                CurrentRole = Roles.First();
                Utils.SendRPC(CustomRPC.SyncChangeling, playerId, (int)CurrentRole);
            }
            catch (InvalidOperationException)
            {
                Logger.Error("No roles for Changeling", "Changeling");
                Utils.GetPlayerById(playerId).RpcSetCustomRole(CustomRoles.ImpostorEHR);
            }
        }

        public override void Init()
        {
            On = false;
            ChangedRole.Clear();
            Roles = GetAvailableRoles();
        }

        private void SelectNextRole(PlayerControl pc)
        {
            int currentIndex = Roles.IndexOf(CurrentRole);
            CurrentRole = currentIndex == Roles.Count - 1 ? Roles.First() : Roles[currentIndex + 1];
            Utils.SendRPC(CustomRPC.SyncChangeling, pc.PlayerId, (int)CurrentRole);
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }

        public override bool CanUseKillButton(PlayerControl pc)
        {
            return CanKillBeforeRoleChange.GetBool();
        }

        public override void OnCoEnterVent(PlayerPhysics physics, int ventId)
        {
            SelectNextRole(physics.myPlayer);
        }

        public override void OnPet(PlayerControl pc)
        {
            SelectNextRole(pc);
        }

        public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
        {
            LateTask.New(() =>
            {
                shapeshifter.RpcSetCustomRole(CurrentRole);
                ChangedRole[shapeshifter.PlayerId] = true;
                shapeshifter.RpcResetAbilityCooldown();
                if (!DisableShapeshiftAnimations.GetBool()) LateTask.New(() => { shapeshifter.RpcShapeshift(shapeshifter, false); }, 1f, log: false);
            }, 0.3f, log: false);

            return false;
        }

        public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
        {
            return seer.PlayerId != target.PlayerId || ChangelingId != seer.PlayerId ? string.Empty : string.Format(Translator.GetString("ChangelingCurrentRole"), CurrentRole.ToColoredString());
        }
    }
}