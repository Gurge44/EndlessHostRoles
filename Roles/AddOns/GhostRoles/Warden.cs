﻿using System;

namespace EHR.Roles.AddOns.GhostRoles
{
    internal class Warden : IGhostRole, ISettingHolder
    {
        private static OptionItem ExtraSpeed;
        private static OptionItem ExtraSpeedDuration;
        private static OptionItem CD;

        public Team Team => Team.Crewmate;
        public int Cooldown => CD.GetInt();

        public void OnProtect(PlayerControl pc, PlayerControl target)
        {
            float speed = Main.AllPlayerSpeed[target.PlayerId];
            float targetSpeed = speed + ExtraSpeed.GetFloat();
            if (Math.Abs(speed - targetSpeed) < 0.1f || speed > targetSpeed) return;
            Main.AllPlayerSpeed[target.PlayerId] += ExtraSpeed.GetFloat();
            target.MarkDirtySettings();
            target.Notify(Translator.GetString("WardenNotify"));

            LateTask.New(() =>
            {
                Main.AllPlayerSpeed[target.PlayerId] = speed;
                target.MarkDirtySettings();
            }, ExtraSpeedDuration.GetFloat(), "Remove Warden Speed Boost");
        }

        public void OnAssign(PlayerControl pc)
        {
        }

        public void SetupCustomOption()
        {
            Options.SetupRoleOptions(649200, TabGroup.OtherRoles, CustomRoles.Warden, zeroOne: true);
            ExtraSpeedDuration = new IntegerOptionItem(649202, "ExpressSpeedDur", new(1, 90, 1), 5, TabGroup.OtherRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Warden])
                .SetValueFormat(OptionFormat.Seconds);
            ExtraSpeed = new FloatOptionItem(649203, "WardenAdditionalSpeed", new(0.5f, 3f, 0.1f), 0.25f, TabGroup.OtherRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Warden])
                .SetValueFormat(OptionFormat.Multiplier);
            CD = new IntegerOptionItem(649204, "AbilityCooldown", new(0, 60, 1), 30, TabGroup.OtherRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Warden])
                .SetValueFormat(OptionFormat.Seconds);
        }
    }
}