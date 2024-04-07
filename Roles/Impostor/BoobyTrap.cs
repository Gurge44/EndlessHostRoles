using System.Collections.Generic;
using static EHR.Options;

namespace EHR.Roles.Impostor
{
    internal class BoobyTrap : RoleBase
    {
        public static List<byte> BoobyTrapBody = [];
        public static List<byte> BoobyTrapKiller = [];
        public static Dictionary<byte, byte> KillerOfBoobyTrapBody = [];

        public static bool On;
        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(16500, TabGroup.ImpostorRoles, CustomRoles.BoobyTrap);
            BTKillCooldown = FloatOptionItem.Create(16510, "KillCooldown", new(2.5f, 180f, 2.5f), 20f, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.BoobyTrap])
                .SetValueFormat(OptionFormat.Seconds);
            TrapOnlyWorksOnTheBodyBoobyTrap = BooleanOptionItem.Create(16511, "TrapOnlyWorksOnTheBodyBoobyTrap", true, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.BoobyTrap]);
        }

        public override void Add(byte playerId)
        {
            On = true;
        }

        public override void Init()
        {
            On = false;
            BoobyTrapBody = [];
            KillerOfBoobyTrapBody = [];
        }

        public override void SetKillCooldown(byte id)
        {
            Main.AllPlayerKillCooldown[id] = BTKillCooldown.GetFloat();
        }

        public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
        {
            if (TrapOnlyWorksOnTheBodyBoobyTrap.GetBool() && !GameStates.IsMeeting)
            {
                BoobyTrapBody.Add(target.PlayerId);
                BoobyTrapKiller.Add(target.PlayerId);
            }

            return true;
        }

        public override void OnMurder(PlayerControl killer, PlayerControl target)
        {
            if (!TrapOnlyWorksOnTheBodyBoobyTrap.GetBool() && killer != target)
            {
                if (!BoobyTrapBody.Contains(target.PlayerId)) BoobyTrapBody.Add(target.PlayerId);
                if (!KillerOfBoobyTrapBody.ContainsKey(target.PlayerId)) KillerOfBoobyTrapBody.Add(target.PlayerId, killer.PlayerId);
                killer.Suicide();
            }
        }
    }
}