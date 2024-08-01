using System;
using static EHR.Options;

namespace EHR.Impostor
{
    internal class Inhibitor : RoleBase
    {
        public static bool On;
        private byte InhibitorId;
        public override bool IsEnable => On;

        public override void SetupCustomOption()
        {
            SetupRoleOptions(1500, TabGroup.ImpostorRoles, CustomRoles.Inhibitor);
            InhibitorCD = new FloatOptionItem(1510, "KillCooldown", new(0f, 180f, 2.5f), 20f, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Inhibitor])
                .SetValueFormat(OptionFormat.Seconds);
            InhibitorCDAfterMeetings = new FloatOptionItem(1511, "AfterMeetingKillCooldown", new(0f, 180f, 2.5f), 22.5f, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Inhibitor])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public override void Add(byte playerId)
        {
            On = true;
            InhibitorId = playerId;
        }

        public override void Init()
        {
            On = false;
            InhibitorId = byte.MaxValue;
        }

        public override bool CanUseKillButton(PlayerControl pc)
        {
            return base.CanUseKillButton(pc) && !Utils.IsActive(SystemTypes.Electrical) && !Utils.IsActive(SystemTypes.Comms) && !Utils.IsActive(SystemTypes.MushroomMixupSabotage) && !Utils.IsActive(SystemTypes.Laboratory) && !Utils.IsActive(SystemTypes.LifeSupp) && !Utils.IsActive(SystemTypes.Reactor) && !Utils.IsActive(SystemTypes.HeliSabotage);
        }

        public override void SetKillCooldown(byte id)
        {
            Main.AllPlayerKillCooldown[id] = InhibitorCDAfterMeetings.GetFloat();
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (Math.Abs(Main.AllPlayerKillCooldown[killer.PlayerId] - InhibitorCD.GetFloat()) > 0.5f)
            {
                Main.AllPlayerKillCooldown[killer.PlayerId] = InhibitorCD.GetFloat();
                killer.SyncSettings();
            }

            return base.OnCheckMurder(killer, target);
        }

        public override void OnReportDeadBody()
        {
            Main.AllPlayerKillCooldown[InhibitorId] = InhibitorCDAfterMeetings.GetFloat();
        }
    }
}