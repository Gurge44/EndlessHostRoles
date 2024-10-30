using System.Collections.Generic;
using static EHR.Translator;

namespace EHR.Crewmate
{
    internal class NiceEraser : RoleBase
    {
        private const int Id = 5580;
        public static List<byte> PlayerIdList = [];

        private static OptionItem EraseLimitOpt;
        public static OptionItem HideVote;
        public static OptionItem CancelVote;

        private static List<byte> DidVote = [];
        private static List<byte> PlayerToErase = [];
        public static List<byte> ErasedPlayers = [];

        public override bool IsEnable => PlayerIdList.Count > 0;

        public override void SetupCustomOption()
        {
            Options.SetupSingleRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.NiceEraser);
            EraseLimitOpt = new IntegerOptionItem(Id + 2, "EraseLimit", new(1, 15, 1), 1, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.NiceEraser])
                .SetValueFormat(OptionFormat.Times);
            HideVote = new BooleanOptionItem(Id + 3, "NiceEraserHideVote", false, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.NiceEraser]);
            CancelVote = Options.CreateVoteCancellingUseSetting(Id + 4, CustomRoles.NiceEraser, TabGroup.CrewmateRoles);
        }

        public override void Init()
        {
            PlayerIdList = [];
            ErasedPlayers = [];
        }

        public override void Add(byte playerId)
        {
            PlayerIdList.Add(playerId);
            playerId.SetAbilityUseLimit(EraseLimitOpt.GetInt());
        }

        public override bool OnVote(PlayerControl player, PlayerControl target)
        {
            if (player == null || target == null)
            {
                return false;
            }

            if (DidVote.Contains(player.PlayerId) || Main.DontCancelVoteList.Contains(player.PlayerId))
            {
                return false;
            }

            DidVote.Add(player.PlayerId);

            if (player.GetAbilityUseLimit() < 1)
            {
                return false;
            }

            if (target.PlayerId == player.PlayerId)
            {
                Utils.SendMessage(GetString("EraserEraseSelf"), player.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.NiceEraser), GetString("EraserEraseMsgTitle")));
                return false;
            }

            if (target.GetCustomRole().IsNeutral())
            {
                Utils.SendMessage(string.Format(GetString("EraserEraseNeutralNotice"), target.GetRealName()), player.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.NiceEraser), GetString("EraserEraseMsgTitle")));
                return false;
            }

            player.RpcRemoveAbilityUse();

            if (!PlayerToErase.Contains(target.PlayerId))
            {
                PlayerToErase.Add(target.PlayerId);
            }

            Utils.SendMessage(string.Format(GetString("EraserEraseNotice"), target.GetRealName()), player.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.NiceEraser), GetString("EraserEraseMsgTitle")));

            if (GameStates.IsInTask)
            {
                Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: target);
            }

            Main.DontCancelVoteList.Add(player.PlayerId);
            return true;
        }

        public override void OnReportDeadBody()
        {
            PlayerToErase = [];
            DidVote = [];
        }

        public override void AfterMeetingTasks()
        {
            foreach (byte pc in PlayerToErase.ToArray())
            {
                PlayerControl player = Utils.GetPlayerById(pc);
                if (player == null)
                {
                    continue;
                }

                player.RpcSetCustomRole(player.GetCustomRole().GetErasedRole());
                player.Notify(GetString("LostRoleByNiceEraser"));
                Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags()} 被擦除了", "NiceEraser");
                player.MarkDirtySettings();
                ErasedPlayers.Add(pc);
            }
        }
    }
}