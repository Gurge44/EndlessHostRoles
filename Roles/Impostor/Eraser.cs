using System.Collections.Generic;
using AmongUs.GameOptions;
using static EHR.Translator;

namespace EHR.Impostor
{
    internal class Eraser : RoleBase
    {
        private const int Id = 16800;
        private static List<byte> PlayerIdList = [];

        private static readonly string[] EraseMode =
        [
            "EKill",
            "EVote"
        ];

        private static readonly string[] WhenTargetIsNeutralAction =
        [
            "E2Block",
            "E2Kill"
        ];

        private static OptionItem EraseLimitOpt;
        private static OptionItem EraseMethod;
        private static OptionItem WhenTargetIsNeutral;
        public static OptionItem HideVote;
        public static OptionItem CancelVote;

        private static List<byte> DidVote = [];
        private static List<byte> PlayerToErase = [];
        public static List<byte> ErasedPlayers = [];

        public override bool IsEnable => PlayerIdList.Count > 0;

        public override void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Eraser);

            EraseMethod = new StringOptionItem(Id + 10, "EraseMethod", EraseMode, 0, TabGroup.ImpostorRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Eraser]);

            WhenTargetIsNeutral = new StringOptionItem(Id + 11, "WhenTargetIsNeutral", WhenTargetIsNeutralAction, 0, TabGroup.ImpostorRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Eraser]);

            EraseLimitOpt = new IntegerOptionItem(Id + 12, "EraseLimit", new(1, 15, 1), 1, TabGroup.ImpostorRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Eraser])
                .SetValueFormat(OptionFormat.Times);

            HideVote = new BooleanOptionItem(Id + 13, "EraserHideVote", false, TabGroup.ImpostorRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Eraser]);

            CancelVote = Options.CreateVoteCancellingUseSetting(Id + 14, CustomRoles.Eraser, TabGroup.ImpostorRoles);
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

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (killer.GetAbilityUseLimit() < 1) return true;

            if (target.PlayerId == killer.PlayerId) return true;

            if (target.GetCustomRole().IsNeutral() && EraseMethod.GetInt() == 0)
            {
                killer.Notify(GetString("EraserEraseNeutralNotice"));
                if (WhenTargetIsNeutral.GetInt() == 1) return true;

                killer.SetKillCooldown(5f);
                return false;
            }

            if (EraseMethod.GetInt() == 0)
            {
                return killer.CheckDoubleTrigger(target, () =>
                {
                    killer.RpcRemoveAbilityUse();
                    killer.SetKillCooldown();
                    killer.Notify(GetString("TargetErasedInRound"));
                    if (!PlayerToErase.Contains(target.PlayerId)) PlayerToErase.Add(target.PlayerId);
                });
            }

            return true;
        }

        public override bool OnVote(PlayerControl player, PlayerControl target)
        {
            if (player == null || target == null || EraseMethod.GetInt() == 0) return false;

            if (DidVote.Contains(player.PlayerId) || Main.DontCancelVoteList.Contains(player.PlayerId)) return false;

            DidVote.Add(player.PlayerId);

            if (player.GetAbilityUseLimit() < 1) return false;

            if (target.PlayerId == player.PlayerId)
            {
                Utils.SendMessage(GetString("EraserEraseSelf"), player.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Eraser), GetString("EraserEraseMsgTitle")));
                return false;
            }

            if (target.GetCustomRole().IsNeutral())
            {
                Utils.SendMessage(string.Format(GetString("EraserEraseNeutralNotice"), target.GetRealName()), player.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Eraser), GetString("EraserEraseMsgTitle")));
                return false;
            }

            player.RpcRemoveAbilityUse();

            if (!PlayerToErase.Contains(target.PlayerId)) PlayerToErase.Add(target.PlayerId);

            Utils.SendMessage(string.Format(GetString("EraserEraseNotice"), target.GetRealName()), player.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Eraser), GetString("EraserEraseMsgTitle")));

            if (GameStates.IsInTask) Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: target);

            Main.DontCancelVoteList.Add(player.PlayerId);
            return true;
        }

        public override void OnReportDeadBody()
        {
            DidVote = [];
        }

        public override void AfterMeetingTasks()
        {
            foreach (byte id in PlayerToErase)
            {
                PlayerControl pc = Utils.GetPlayerById(id);
                if (pc == null || !pc.IsAlive() || pc.Is(CustomRoles.Bloodlust)) continue;

                CustomRoles erasedRole = pc.GetRoleTypes() is RoleTypes.Crewmate or RoleTypes.Engineer or RoleTypes.Noisemaker or RoleTypes.Scientist or RoleTypes.Tracker ? CustomRoles.CrewmateEHR : CustomRoles.ImpostorEHR;
                pc.RpcSetCustomRole(erasedRole);
                pc.RpcChangeRoleBasis(erasedRole);
                pc.Notify(GetString("LostRoleByEraser"));
                Logger.Info($"{pc.GetNameWithRole().RemoveHtmlTags()} lost their role", "Eraser");
                pc.MarkDirtySettings();
                ErasedPlayers.Add(id);
            }

            PlayerToErase = [];
        }
    }
}