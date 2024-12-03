using System.Collections.Generic;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Crewmate
{
    public class Oracle : RoleBase
    {
        private const int Id = 7600;
        private static List<byte> PlayerIdList = [];

        public static OptionItem CheckLimitOpt;
        public static OptionItem HideVote;
        public static OptionItem FailChance;
        public static OptionItem OracleAbilityUseGainWithEachTaskCompleted;
        public static OptionItem AbilityChargesWhenFinishedTasks;
        public static OptionItem CancelVote;

        public static readonly List<byte> DidVote = [];

        public override bool IsEnable => PlayerIdList.Count > 0;

        public override void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Oracle);

            CheckLimitOpt = new IntegerOptionItem(Id + 10, "OracleSkillLimit", new(0, 10, 1), 0, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Oracle])
                .SetValueFormat(OptionFormat.Times);

            HideVote = new BooleanOptionItem(Id + 12, "OracleHideVote", false, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Oracle]);

            FailChance = new IntegerOptionItem(Id + 13, "FailChance", new(0, 100, 5), 20, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Oracle])
                .SetValueFormat(OptionFormat.Percent);

            OracleAbilityUseGainWithEachTaskCompleted = new FloatOptionItem(Id + 14, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Oracle])
                .SetValueFormat(OptionFormat.Times);

            AbilityChargesWhenFinishedTasks = new FloatOptionItem(Id + 15, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Oracle])
                .SetValueFormat(OptionFormat.Times);

            CancelVote = CreateVoteCancellingUseSetting(Id + 11, CustomRoles.Oracle, TabGroup.CrewmateRoles);
        }

        public override void Init()
        {
            PlayerIdList = [];
        }

        public override void Add(byte playerId)
        {
            PlayerIdList.Add(playerId);
            playerId.SetAbilityUseLimit(CheckLimitOpt.GetInt());
        }

        public override bool OnVote(PlayerControl player, PlayerControl target)
        {
            if (player == null || target == null) return false;

            if (DidVote.Contains(player.PlayerId) || Main.DontCancelVoteList.Contains(player.PlayerId)) return false;

            DidVote.Add(player.PlayerId);

            if (player.GetAbilityUseLimit() < 1)
            {
                Utils.SendMessage(GetString("OracleCheckReachLimit"), player.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Oracle), GetString("OracleCheckMsgTitle")));
                return false;
            }

            player.RpcRemoveAbilityUse();

            if (player.PlayerId == target.PlayerId)
            {
                Utils.SendMessage(GetString("OracleCheckSelfMsg") + "\n\n" + string.Format(GetString("OracleCheckLimit"), player.GetAbilityUseLimit()), player.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Oracle), GetString("OracleCheckMsgTitle")));
                return false;
            }

            string text;

            if (target.GetCustomRole().IsImpostor())
                text = "Imp";
            else if (target.GetCustomRole().IsNeutral())
                text = "Neut";
            else
                text = "Crew";

            if (IRandom.Instance.Next(100) < FailChance.GetInt())
            {
                int next = IRandom.Instance.Next(1, 3);

                text = text switch
                {
                    "Crew" => next switch
                    {
                        1 => "Neut",
                        2 => "Imp",
                        _ => text
                    },
                    "Neut" => next switch
                    {
                        1 => "Crew",
                        2 => "Imp",
                        _ => text
                    },
                    "Imp" => next switch
                    {
                        1 => "Neut",
                        2 => "Crew",
                        _ => text
                    },
                    _ => text
                };
            }

            string msg = string.Format(GetString("OracleCheck." + text), target.GetRealName());

            Utils.SendMessage($"{GetString("OracleCheck")}\n{msg}\n\n{string.Format(GetString("OracleCheckLimit"), player.GetAbilityUseLimit())}", player.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Oracle), GetString("OracleCheckMsgTitle")));

            Main.DontCancelVoteList.Add(player.PlayerId);
            return true;
        }
    }
}