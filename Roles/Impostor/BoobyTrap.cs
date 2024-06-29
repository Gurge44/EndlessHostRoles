using System.Collections.Generic;
using EHR.Modules;
using static EHR.Options;

namespace EHR.Impostor
{
    internal class BoobyTrap : RoleBase
    {
        private static List<byte> BoobyTrapBody = [];
        private static List<byte> BoobyTrapKiller = [];
        private static Dictionary<byte, byte> KillerOfBoobyTrapBody = [];

        public static bool On;

        private static OptionItem BTKillCooldown;
        private static OptionItem TrapOnlyWorksOnTheBodyBoobyTrap;
        private static OptionItem TrapConsecutiveBodies;
        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(16500, TabGroup.ImpostorRoles, CustomRoles.BoobyTrap);
            BTKillCooldown = new FloatOptionItem(16510, "KillCooldown", new(2.5f, 180f, 2.5f), 20f, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.BoobyTrap])
                .SetValueFormat(OptionFormat.Seconds);
            TrapOnlyWorksOnTheBodyBoobyTrap = new BooleanOptionItem(16511, "TrapOnlyWorksOnTheBodyBoobyTrap", true, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.BoobyTrap]);
            TrapConsecutiveBodies = new BooleanOptionItem(16512, "TrapConsecutiveBodies", true, TabGroup.ImpostorRoles)
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

        public override bool CheckReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target, PlayerControl killer)
        {
            if (BoobyTrapBody.Contains(target.PlayerId) && reporter.IsAlive())
            {
                if (!TrapOnlyWorksOnTheBodyBoobyTrap.GetBool())
                {
                    var killerID = KillerOfBoobyTrapBody[target.PlayerId];

                    reporter.Suicide(PlayerState.DeathReason.Bombed, Utils.GetPlayerById(killerID));
                    RPC.PlaySoundRPC(killerID, Sounds.KillSound);

                    if (!BoobyTrapBody.Contains(reporter.PlayerId) && TrapConsecutiveBodies.GetBool()) BoobyTrapBody.Add(reporter.PlayerId);
                    KillerOfBoobyTrapBody.TryAdd(reporter.PlayerId, killerID);
                    return false;
                }

                var killerID2 = target.PlayerId;

                reporter.Suicide(PlayerState.DeathReason.Bombed, Utils.GetPlayerById(killerID2));
                RPC.PlaySoundRPC(killerID2, Sounds.KillSound);
                return false;
            }

            return true;
        }
    }
}