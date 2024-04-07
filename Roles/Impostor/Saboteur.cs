using System;
using static EHR.Options;

namespace EHR.Roles.Impostor
{
    internal class Saboteur : RoleBase
    {
        private byte SaboteurId;
        public static bool On;
        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(10005, TabGroup.ImpostorRoles, CustomRoles.Saboteur);
            SaboteurCD = FloatOptionItem.Create(10015, "KillCooldown", new(0f, 180f, 2.5f), 17.5f, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Saboteur])
                .SetValueFormat(OptionFormat.Seconds);
            SaboteurCDAfterMeetings = FloatOptionItem.Create(10016, "AfterMeetingKillCooldown", new(0f, 180f, 2.5f), 25f, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Saboteur])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public override void Add(byte playerId)
        {
            On = true;
            SaboteurId = playerId;
        }

        public override void Init()
        {
            On = false;
            SaboteurId = byte.MaxValue;
        }

        public override bool CanUseKillButton(PlayerControl pc)
        {
            return base.CanUseKillButton(pc) && (Utils.IsActive(SystemTypes.Electrical) || Utils.IsActive(SystemTypes.Comms) || Utils.IsActive(SystemTypes.MushroomMixupSabotage) || Utils.IsActive(SystemTypes.Laboratory) || Utils.IsActive(SystemTypes.LifeSupp) || Utils.IsActive(SystemTypes.Reactor) || Utils.IsActive(SystemTypes.HeliSabotage));
        }

        public override void SetKillCooldown(byte id)
        {
            Main.AllPlayerKillCooldown[id] = SaboteurCDAfterMeetings.GetFloat();
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (Math.Abs(Main.AllPlayerKillCooldown[killer.PlayerId] - SaboteurCD.GetFloat()) > 0.5f)
            {
                Main.AllPlayerKillCooldown[killer.PlayerId] = SaboteurCD.GetFloat();
                killer.SyncSettings();
            }

            return base.OnCheckMurder(killer, target);
        }

        public override void OnReportDeadBody()
        {
            Main.AllPlayerKillCooldown[SaboteurId] = SaboteurCDAfterMeetings.GetFloat();
        }
    }
}