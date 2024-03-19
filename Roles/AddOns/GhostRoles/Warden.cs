using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TOHE.Roles.AddOns.GhostRoles
{
    internal class Warden : IGhostRole, ISettingHolder
    {
        public Team Team => Team.Crewmate;

        private static OptionItem ExtraSpeed;
        private static OptionItem ExtraSpeedDuration;

        public void OnProtect(PlayerControl pc, PlayerControl target)
        {
            float speed = Main.AllPlayerSpeed[target.PlayerId];
            Main.AllPlayerSpeed[target.PlayerId] += ExtraSpeed.GetFloat();
            target.MarkDirtySettings();
            target.Notify(Translator.GetString("WardenNotify"));

            _ = new LateTask(() =>
            {
                Main.AllPlayerSpeed[target.PlayerId] = speed;
                target.MarkDirtySettings();
            }, ExtraSpeedDuration.GetFloat(), "Remove Warden Speed Boost");
        }

        public void SetupCustomOption()
        {
            Options.SetupRoleOptions(649200, TabGroup.OtherRoles, CustomRoles.Warden);
            ExtraSpeedDuration = IntegerOptionItem.Create(649202, "ExpressSpeedDur", new(1, 90, 1), 5, TabGroup.OtherRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Warden])
                .SetValueFormat(OptionFormat.Seconds);
            ExtraSpeed = FloatOptionItem.Create(649203, "ExpressSpeed", new(0.5f, 3f, 0.1f), 1.5f, TabGroup.OtherRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Warden])
                .SetValueFormat(OptionFormat.Multiplier);
        }

        public void OnAssign(PlayerControl pc)
        {
        }
    }
}
