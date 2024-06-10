using System.Collections.Generic;

namespace EHR.Roles.AddOns.GhostRoles
{
    internal class GA : IGhostRole, ISettingHolder
    {
        private static OptionItem ProtectDuration;
        private static OptionItem CD;

        public HashSet<byte> ProtectionList = [];

        public Team Team => Team.Crewmate;
        public int Cooldown => CD.GetInt();

        public void OnProtect(PlayerControl pc, PlayerControl target)
        {
            if (ProtectionList.Add(target.PlayerId))
                LateTask.New(() => ProtectionList.Remove(target.PlayerId), ProtectDuration.GetFloat());
        }

        public void OnAssign(PlayerControl pc)
        {
            ProtectionList = [];
        }

        public void SetupCustomOption()
        {
            Options.SetupRoleOptions(649600, TabGroup.OtherRoles, CustomRoles.GA, zeroOne: true);
            ProtectDuration = new IntegerOptionItem(649602, "BKProtectDuration", new(1, 90, 1), 5, TabGroup.OtherRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.GA])
                .SetValueFormat(OptionFormat.Seconds);
            CD = new IntegerOptionItem(649603, "AbilityCooldown", new(0, 60, 1), 30, TabGroup.OtherRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.GA])
                .SetValueFormat(OptionFormat.Seconds);
        }
    }
}