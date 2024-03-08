using System.Collections.Generic;

namespace TOHE.Roles.Crewmate
{
    internal class Markseeker : RoleBase
    {
        public static List<byte> PlayerIdList = [];
        public static bool On;
        public override bool IsEnable => On;

        private const int Id = 643550;
        public static OptionItem CancelVote;

        public byte MarkedId;
        public bool TargetRevealed;

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Markseeker);
            CancelVote = Options.CreateVoteCancellingUseSetting(Id + 2, CustomRoles.Markseeker, TabGroup.CrewmateRoles);
        }

        public override void Add(byte playerId)
        {
            On = true;
            MarkedId = byte.MaxValue;
            TargetRevealed = false;
            PlayerIdList.Add(playerId);
        }

        public override void Init()
        {
            On = false;
            PlayerIdList = [];
        }

        public static bool OnVote(PlayerControl player, PlayerControl target)
        {
            if (player == null || target == null || player.PlayerId == target.PlayerId || Main.PlayerStates[player.PlayerId].Role is not Markseeker { IsEnable: true, MarkedId: byte.MaxValue } ms || Main.DontCancelVoteList.Contains(player.PlayerId)) return false;

            ms.MarkedId = target.PlayerId;

            Main.DontCancelVoteList.Add(player.PlayerId);
            return true;
        }

        public static void OnDeath(PlayerControl player)
        {
            if (Main.PlayerStates[player.PlayerId].Role is not Markseeker { IsEnable: true } ms || ms.MarkedId == byte.MaxValue) return;

            ms.TargetRevealed = true;
        }
    }
}
