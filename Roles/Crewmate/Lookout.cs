using System.Text;

namespace EHR.Crewmate
{
    internal class Lookout : RoleBase
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

        public override void SetupCustomOption()
        {
            Options.SetupRoleOptions(9150, TabGroup.CrewmateRoles, CustomRoles.Lookout);
        }

        public override void OnPet(PlayerControl pc)
        {
            var AllAlivePlayers = Main.AllAlivePlayerControls;
            var sb = new StringBuilder();
            for (int i = 0; i < AllAlivePlayers.Length; i++)
                if (i % 3 == 0)
                    sb.AppendLine();
            for (int i = 0; i < AllAlivePlayers.Length; i++)
            {
                PlayerControl player = AllAlivePlayers[i];
                if (player == null) continue;
                if (i != 0) sb.Append("; ");
                string name = player.GetRealName();
                byte id = player.PlayerId;
                if (Main.PlayerColors.TryGetValue(id, out var color)) name = Utils.ColorString(color, name);
                sb.Append($"{name} {id}");
                if (i % 3 == 0 && i != AllAlivePlayers.Length - 1) sb.AppendLine();
            }

            pc.Notify(sb.ToString());
        }
    }
}