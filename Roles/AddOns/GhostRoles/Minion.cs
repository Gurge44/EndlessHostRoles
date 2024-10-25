using System.Collections.Generic;

namespace EHR.AddOns.GhostRoles
{
    internal class Minion : IGhostRole
    {
        public static HashSet<byte> BlindPlayers = [];

        private static OptionItem BlindDuration;
        private static OptionItem CD;

        public Team Team => Team.Impostor;
        public int Cooldown => CD.GetInt();

        public void OnProtect(PlayerControl pc, PlayerControl target)
        {
            if (!BlindPlayers.Add(target.PlayerId)) return;
            target.MarkDirtySettings();

            LateTask.New(() =>
            {
                if (BlindPlayers.Remove(target.PlayerId))
                {
                    target.MarkDirtySettings();
                }
            }, BlindDuration.GetFloat(), "Remove Minion Blindness");
        }

        public void OnAssign(PlayerControl pc)
        {
        }

        public void SetupCustomOption()
        {
            Options.SetupRoleOptions(649000, TabGroup.OtherRoles, CustomRoles.Minion);
            BlindDuration = new IntegerOptionItem(649002, "MinionBlindDuration", new(1, 90, 1), 5, TabGroup.OtherRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Minion])
                .SetValueFormat(OptionFormat.Seconds);
            CD = new IntegerOptionItem(649003, "AbilityCooldown", new(0, 60, 1), 30, TabGroup.OtherRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Minion])
                .SetValueFormat(OptionFormat.Seconds);
        }
    }
}