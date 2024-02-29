using System.Collections.Generic;

namespace TOHE.Roles.Impostor
{
    internal class BoobyTrap : RoleBase
    {
        public static List<byte> BoobyTrapBody = [];
        public static List<byte> BoobyTrapKiller = [];
        public static Dictionary<byte, byte> KillerOfBoobyTrapBody = [];

        public static bool On;
        public override bool IsEnable => On;

        public override void Add(byte playerId)
        {
            On = true;
        }

        public override void Init()
        {
            On = false;
            BoobyTrap.BoobyTrapBody = [];
            BoobyTrap.KillerOfBoobyTrapBody = [];
        }

        public override void SetKillCooldown(byte id)
        {
            Main.AllPlayerKillCooldown[id] = Options.BTKillCooldown.GetFloat();
        }

        public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
        {
            if (Options.TrapOnlyWorksOnTheBodyBoobyTrap.GetBool() && !GameStates.IsMeeting)
            {
                BoobyTrapBody.Add(target.PlayerId);
                BoobyTrapKiller.Add(target.PlayerId);
            }

            return true;
        }

        public override void OnMurder(PlayerControl killer, PlayerControl target)
        {
            if (!Options.TrapOnlyWorksOnTheBodyBoobyTrap.GetBool() && killer != target)
            {
                if (!BoobyTrapBody.Contains(target.PlayerId)) BoobyTrapBody.Add(target.PlayerId);
                if (!KillerOfBoobyTrapBody.ContainsKey(target.PlayerId)) KillerOfBoobyTrapBody.Add(target.PlayerId, killer.PlayerId);
                killer.Suicide();
            }
        }
    }
}
