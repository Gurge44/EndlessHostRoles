using System;
using System.Collections.Generic;
using System.Linq;
using static TOHE.Options;

namespace TOHE.Roles.Impostor
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

        private CustomRoles CurrentRole;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Changeling);
            CanPickPartnerRole = BooleanOptionItem.Create(Id + 10, "CanPickPartnerRole", true, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Changeling]);
            CanKillBeforeRoleChange = BooleanOptionItem.Create(Id + 11, "CanKillBeforeRoleChange", true, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Changeling]);
            AvailableRoles = StringOptionItem.Create(Id + 12, "AvailableRoles", AvailableRolesMode, 0, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Changeling]);
        }

        public static List<CustomRoles> GetAvailableRoles(bool check = false)
        {
            CustomRoles[] allRoles = EnumHelper.GetAllValues<CustomRoles>();

            IEnumerable<CustomRoles> result = AvailableRoles.GetValue() switch
            {
                0 => allRoles,
                1 => allRoles.Where(x => x.GetVNRole() is CustomRoles.Impostor or CustomRoles.ImpostorTOHE),
                2 => allRoles.Where(x => x.GetVNRole() is CustomRoles.Shapeshifter or CustomRoles.ShapeshifterTOHE),
                3 => allRoles.Where(x => x.GetMode() != 0),
                4 => allRoles.Where(x => x.GetVNRole() is CustomRoles.Impostor or CustomRoles.ImpostorTOHE && x.GetMode() != 0),
                5 => allRoles.Where(x => x.GetVNRole() is CustomRoles.Shapeshifter or CustomRoles.ShapeshifterTOHE && x.GetMode() != 0),
                _ => allRoles
            };

            if (!CanPickPartnerRole.GetBool() && !check)
            {
                result = result.Where(x => !Main.AllPlayerControls.Any(p => p.Is(x)));
            }

            var rolesList = result.ToList();
            rolesList.Remove(CustomRoles.Changeling);
            rolesList.RemoveAll(x => !x.IsImpostor() || x.IsVanilla());
            return rolesList;
        }

        public static bool On;
        public override bool IsEnable => On;

        public override void Add(byte playerId)
        {
            On = true;
            ChangedRole[playerId] = false;
            try
            {
                CurrentRole = Roles.First();
            }
            catch (InvalidOperationException)
            {
                Logger.Error("No roles for Changeling", "Changeling");
                Utils.GetPlayerById(playerId).RpcSetCustomRole(CustomRoles.ImpostorTOHE);
            }
        }

        public override void Init()
        {
            On = false;
            ChangedRole.Clear();
            Roles = GetAvailableRoles();
        }

        void SelectNextRole(PlayerControl pc)
        {
            var currentIndex = Roles.IndexOf(CurrentRole);
            CurrentRole = currentIndex == Roles.Count - 1 ? Roles.First() : Roles[currentIndex + 1];
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }

        public override bool CanUseKillButton(PlayerControl pc) => CanKillBeforeRoleChange.GetBool();

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
            shapeshifter.RpcSetCustomRole(CurrentRole);
            return false;
        }

        public static string GetSuffix(PlayerControl seer) => Main.PlayerStates[seer.PlayerId].Role is not Changeling { IsEnable: true } cl ? string.Empty : string.Format(Translator.GetString("ChangelingCurrentRole"), Utils.ColorString(Utils.GetRoleColor(cl.CurrentRole), Translator.GetString($"{cl.CurrentRole}")));
    }
}
