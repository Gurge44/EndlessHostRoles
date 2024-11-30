using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using EHR.Neutral;

namespace EHR.Crewmate
{
    internal class Transporter : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        public override void SetupCustomOption()
        {
            Options.SetupRoleOptions(6200, TabGroup.CrewmateRoles, CustomRoles.Transporter);

            Options.TransporterTeleportMax = new IntegerOptionItem(6210, "TransporterTeleportMax", new(0, 90, 1), 5, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Transporter])
                .SetValueFormat(OptionFormat.Times);

            Options.OverrideTasksData.Create(6211, TabGroup.CrewmateRoles, CustomRoles.Transporter);
        }

        public override void Add(byte playerId)
        {
            On = true;
        }

        public override void Init()
        {
            On = false;
        }

        public override void OnTaskComplete(PlayerControl player, int completedTaskCount, int totalTaskCount)
        {
            if (player.IsAlive() && completedTaskCount + 1 <= Options.TransporterTeleportMax.GetInt())
            {
                List<PlayerControl> AllAlivePlayer = Main.AllAlivePlayerControls.Where(x => !Pelican.IsEaten(x.PlayerId) && !x.inVent && !x.onLadder).ToList();

                if (AllAlivePlayer.Count >= 2)
                {
                    PlayerControl tar1 = AllAlivePlayer.RandomElement();
                    AllAlivePlayer.Remove(tar1);
                    PlayerControl tar2 = AllAlivePlayer.RandomElement();
                    Vector2 pos = tar1.Pos();
                    tar1.TP(tar2);
                    tar2.TP(pos);
                    tar1.RPCPlayCustomSound("Teleport");
                    tar2.RPCPlayCustomSound("Teleport");
                    tar1.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Transporter), string.Format(Translator.GetString("TeleportedByTransporter"), tar2.GetRealName())));
                    tar2.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Transporter), string.Format(Translator.GetString("TeleportedByTransporter"), tar1.GetRealName())));
                }
                else
                    player.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Impostor), string.Format(Translator.GetString("ErrorTeleport"), player.GetRealName())));
            }
        }
    }
}