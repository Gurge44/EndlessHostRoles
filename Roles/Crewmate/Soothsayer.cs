using System.Collections.Generic;

namespace EHR.Crewmate
{
    public class Soothsayer : RoleBase
    {
        public static bool On;
        private static List<Soothsayer> Instances = [];

        public static OptionItem CancelVote;

        private byte Target;
        public override bool IsEnable => On;

        public override void SetupCustomOption()
        {
            const int id = 649450;
            const TabGroup tab = TabGroup.CrewmateRoles;
            const CustomRoles role = CustomRoles.Soothsayer;

            Options.SetupRoleOptions(id, tab, role);
            CancelVote = Options.CreateVoteCancellingUseSetting(id + 2, role, tab);
        }

        public override void Init()
        {
            On = false;
            Instances = [];
        }

        public override void Add(byte playerId)
        {
            On = true;
            Instances.Add(this);
            Target = byte.MaxValue;
        }

        public override bool OnVote(PlayerControl player, PlayerControl target)
        {
            if (player == null || target == null || player.PlayerId == target.PlayerId)
            {
                return false;
            }

            if (Target != byte.MaxValue || Main.DontCancelVoteList.Contains(player.PlayerId))
            {
                return false;
            }

            Target = target.PlayerId;

            Main.DontCancelVoteList.Add(player.PlayerId);
            return true;
        }

        public static void OnAnyoneDeath(PlayerControl killer, PlayerControl target)
        {
            Instances.DoIf(x => x.Target == target.PlayerId, _ => Main.AllAlivePlayerControls.Do(p => p.Notify(string.Format(Translator.GetString("SoothsayerDiedNotify"), Utils.ColorString(Main.PlayerColors.GetValueOrDefault(killer.PlayerId), killer.GetRealName())), 10f)));
        }
    }
}