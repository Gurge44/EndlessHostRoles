using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TOHE.Roles.AddOns.GhostRoles
{
    internal class Minion : IGhostRole, ISettingHolder
    {
        public Team Team => Team.Impostor;

        public static HashSet<byte> BlindPlayers = [];

        private static OptionItem BlindDuration;

        public void OnProtect(PlayerControl pc, PlayerControl target)
        {
            if (!BlindPlayers.Add(target.PlayerId)) return;
            target.MarkDirtySettings();

            _ = new LateTask(() =>
            {
                if (BlindPlayers.Remove(target.PlayerId))
                {
                    target.MarkDirtySettings();
                }
            }, BlindDuration.GetFloat(), "Remove Minion Blindness");
        }

        public void SetupCustomOption()
        {
            Options.SetupRoleOptions(649000, TabGroup.OtherRoles, CustomRoles.Minion);
            BlindDuration = IntegerOptionItem.Create(649002, "MinionBlindDuration", new(1, 90, 1), 5, TabGroup.OtherRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Minion])
                .SetValueFormat(OptionFormat.Seconds);
        }
    }
}
