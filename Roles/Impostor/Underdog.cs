using MS.Internal.Xml.XPath;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TOHE.Roles.Impostor
{
    internal class Underdog : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        public override void Add(byte playerId)
        {
            On = true;
        }

        public override void Init()
        {
            On = false;
        }

        public override void SetKillCooldown(byte id)
        {
            Main.AllPlayerKillCooldown[id] = Main.AllAlivePlayerControls.Length < Options.UnderdogMaximumPlayersNeededToKill.GetInt() ? Options.UnderdogKillCooldown.GetFloat() : Options.UnderdogKillCooldownWithMorePlayersAlive.GetFloat();
        }

        public override void OnMurder(PlayerControl killer, PlayerControl target)
        {
            SetKillCooldown(killer.PlayerId);
        }
    }
}
