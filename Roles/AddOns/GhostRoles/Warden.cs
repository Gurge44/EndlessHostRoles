using System;

namespace EHR.Roles.AddOns.GhostRoles
{
    internal class Warden : IGhostRole, ISettingHolder
    {
        public Team Team => Team.Crewmate;
        public int Cooldown => 30;

        private static OptionItem ExtraSpeed;
        private static OptionItem ExtraSpeedDuration;

        public void OnProtect(PlayerControl pc, PlayerControl target)
        {
            float speed = Main.AllPlayerSpeed[target.PlayerId];
            if (Math.Abs(speed - (speed + ExtraSpeed.GetFloat())) < 0.1f || speed > speed + ExtraSpeed.GetFloat()) return;
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
            ExtraSpeedDuration = IntegerOptionItem.Create(649202, "ExpressSpeedDur", new(1, 90, 1), 5, TabGroup.OtherRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Warden])
                .SetValueFormat(OptionFormat.Seconds);
            ExtraSpeed = FloatOptionItem.Create(649203, "ExpressSpeed", new(0.5f, 3f, 0.1f), 0.25f, TabGroup.OtherRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Warden])
                .SetValueFormat(OptionFormat.Multiplier);
        }

        public void OnAssign(PlayerControl pc)
        {
        }
    }
}
