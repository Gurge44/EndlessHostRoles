using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmongUs.GameOptions;
using EHR.Modules;
using Il2CppSystem.Globalization;
using UnityEngine;
using static EHR.Options;

namespace EHR.Roles.Neutral
{
    internal class QuizMaster : RoleBase
    {
        public static bool On;
        private const int Id = 10890;

        private static OptionItem KillCooldown;
        private static OptionItem CanVent;
        private static OptionItem HasImpostorVision;

        public byte QuizMasterId;
        public byte Target;
        private Question CurrentQuestion;

        public static Dictionary<byte, string> MessagesToSend = [];
        public static List<QuizMaster> QuizMasters = [];

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.QuizMaster);
            KillCooldown = FloatOptionItem.Create(Id + 2, "QuizMaster.MarkCooldown", new(0f, 180f, 2.5f), 22.5f, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.QuizMaster])
                .SetValueFormat(OptionFormat.Seconds);
            CanVent = BooleanOptionItem.Create(Id + 3, "CanVent", true, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.QuizMaster]);
            HasImpostorVision = BooleanOptionItem.Create(Id + 4, "ImpostorVision", true, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.QuizMaster]);
        }

        class Question(string questionText, string[] answers, int correctAnswerIndex)
        {
            public string QuestionText = questionText;
            public string[] Answers = answers;
            public int CorrectAnswerIndex = correctAnswerIndex;
        }

        private static List<(Color32 Color, string String)> AllColors = [];

        public static readonly SystemTypes[] AllSabotages =
        [
            SystemTypes.Comms,
            SystemTypes.Reactor,
            SystemTypes.Electrical,
            SystemTypes.LifeSupp,
            SystemTypes.MushroomMixupSabotage,
            SystemTypes.Laboratory,
            SystemTypes.HeliSabotage
        ];

        public static ((Color32 Color, string String, PlayerControl Player) LastReportedPlayer, string LastPlayerPressedButtonName, SystemTypes LastSabotage, string LastReporterName, int NumPlayersVotedLastMeeting, string FirstReportedBodyPlayerName, int NumEmergencyMeetings, int NumPlayersDeadThisRound, int NumPlayersDeadFirstRound, int NumSabotages, int NumMeetings) Data = ((new(), string.Empty, null), string.Empty, default, string.Empty, 0, string.Empty, 0, 0, 0, 0, 0);

        public override void Init()
        {
            On = false;
            Target = byte.MaxValue;
            CurrentQuestion = default;

            QuizMasters = [];

            Data = ((new(), string.Empty, null), string.Empty, default, string.Empty, 0, string.Empty, 0, 0, 0, 0, 0);

            AllColors = [];
            _ = new LateTask(() =>
            {
                foreach (var kvp in Main.PlayerColors)
                {
                    var info = Utils.GetPlayerInfoById(kvp.Key);
                    if (info == null) continue;

                    AllColors.Add((kvp.Value, info.GetPlayerColorString()));
                }
            }, 10f, log: false);
        }

        public override void Add(byte playerId)
        {
            On = true;
            QuizMasters.Add(this);
            QuizMasterId = playerId;

            if (!AmongUsClient.Instance.AmHost) return;
            if (!Main.ResetCamPlayerList.Contains(playerId))
                Main.ResetCamPlayerList.Add(playerId);
        }

        public override bool IsEnable => On;
        public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
        public override void ApplyGameOptions(IGameOptions opt, byte id) => opt.SetVision(HasImpostorVision.GetBool());
        public override bool CanUseImpostorVentButton(PlayerControl pc) => CanVent.GetBool();

        public override void AfterMeetingTasks()
        {
            Data.NumPlayersDeadThisRound = 0;
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (!base.OnCheckMurder(killer, target)) return false;

            Target = target.PlayerId;
            return false;
        }

        static Question GetQuestion()
        {
            List<int> allowedIndexes = [];
            List<int> allowedABCIndexes = [];

            for (int i = 1; i <= 14; i++)
            {
                switch (i)
                {
                    case 1 when Data.LastReportedPlayer.String != string.Empty:
                    case 2 when Data.LastPlayerPressedButtonName != string.Empty:
                    case 3 when Data.LastSabotage != default:
                    case 4 when Main.LastVotedPlayerInfo != null:
                    case 5 when Data.LastReporterName != string.Empty:
                    case 6 when Data.NumPlayersVotedLastMeeting != 0:
                    case 7:
                    case 8 when Data.FirstReportedBodyPlayerName != string.Empty:
                    case 9:
                    case 10:
                    case 11:
                    case 12:
                    case 13:
                    case 14:
                        allowedIndexes.Add(i);
                        break;
                }
            }

            for (int i = 1; i <= 6; i++)
            {
                switch (i)
                {
                    case 1 or 2 when Data.LastReportedPlayer.Player != null:
                    case 3 or 4 or 5 or 6:
                        allowedABCIndexes.Add(i);
                        break;
                }
            }

            var random = IRandom.Instance;
            int whichList = random.Next(4);
            bool abc = whichList == 0;
            List<int> indexes = abc ? allowedABCIndexes : allowedIndexes;
            int index = indexes[random.Next(indexes.Count)];

            var randomRole = EnumHelper.GetAllValues<CustomRoles>().Where(x => x.IsEnable()).Shuffle(random).First();

            string title = Translator.GetString($"QuizMaster.Question.{index}");
            (IEnumerable<string> WrongAnswers, string CorrectAnswer) answers = index switch
            {
                1 when abc => (EnumHelper.GetAllValues<Team>().Skip(1).Where(x => x != Data.LastReportedPlayer.Player.GetTeam()).Select(x => Translator.GetString($"{x}")), Translator.GetString($"{Data.LastReportedPlayer.Player.GetTeam()}")),
                1 => (AllColors.Shuffle(random).Take(2).Select(x => x.String), Data.LastReportedPlayer.String),
                2 when abc => ((new[] { PlayerState.DeathReason.Suicide, PlayerState.DeathReason.Kill, PlayerState.DeathReason.etc }).Select(x => Translator.GetString($"DeathReason.{x}")), Translator.GetString($"DeathReason.{Main.PlayerStates[Data.LastReportedPlayer.Player!.PlayerId].deathReason}")),
                2 => (GetTwoRandomNames(), Data.LastPlayerPressedButtonName),
                3 when abc => (new[] { "Town Of Us", "Town Of Host Enhanced" }, "Endless Host Roles"),
                3 => (AllSabotages[..1].Select(x => Translator.GetString($"{x}")), Translator.GetString($"{Data.LastSabotage}")),
                4 when abc => (new[] { "tukasa0001", "0xDrMoe" }, "Gurge44"),
                4 => (CustomRoleSelector.AllRoles.Take(2).Select(x => x.ToColoredString()), Main.LastVotedPlayerInfo!.Object.GetCustomRole().ToColoredString()),
                5 when abc => (EnumHelper.GetAllValues<Team>().Skip(1).Where(x => x != randomRole.GetTeam()).Select(x => Translator.GetString($"{x}")), Translator.GetString($"{randomRole.GetTeam()}")),
                5 => (GetTwoRandomNames(), Data.LastReporterName),
                6 when abc => (EnumHelper.GetAllValues<CustomRoleTypes>().Where(x => x != randomRole.GetCustomRoleTypes()).Take(2).Select(x => Translator.GetString($"{x}")), Translator.GetString($"{randomRole.GetCustomRoleTypes()}")),
                6 => (GetTwoRandomNumbers(Data.NumPlayersVotedLastMeeting, 0, 15), Data.NumPlayersVotedLastMeeting.ToString()),
                7 => (GetTwoRandomNumbers(Data.NumMeetings, Math.Max(Data.NumMeetings - 2, 0), Data.NumMeetings + 2), Data.NumMeetings.ToString()),
                8 => (GetTwoRandomNames(), Data.FirstReportedBodyPlayerName),
                9 => (GetTwoRandomNumbers(Data.NumEmergencyMeetings, Math.Max(Data.NumEmergencyMeetings - 2, 0), Data.NumEmergencyMeetings + 2), Data.NumEmergencyMeetings.ToString()),
                10 => (GetTwoRandomNumbers(Data.NumPlayersDeadThisRound, Math.Max(Data.NumPlayersDeadThisRound - 2, 0), Data.NumPlayersDeadThisRound + 2), Data.NumPlayersDeadThisRound.ToString()),
                11 => (GetTwoRandomNumbers(Data.NumPlayersDeadFirstRound, Math.Max(Data.NumPlayersDeadFirstRound - 2, 0), Data.NumPlayersDeadFirstRound + 2), Data.NumPlayersDeadFirstRound.ToString()),
                12 => (GetTwoRandomNumbers(Data.NumSabotages, Math.Max(Data.NumSabotages - 2, 0), Data.NumSabotages + 2), Data.NumSabotages.ToString()),
                13 => (GetTwoRandomNumbers(GameData.Instance.CompletedTasks, 0, GameData.Instance.TotalTasks), GameData.Instance.CompletedTasks.ToString()),
                14 => (EnumHelper.GetAllValues<RoleTypes>().Where(x => x != randomRole.GetRoleTypes()).Take(2).Select(x => Translator.GetString($"{x}")), Translator.GetString($"{randomRole.GetRoleTypes()}")),

                _ => ([], string.Empty)
            };

            var allAnswersList = answers.WrongAnswers.Append(answers.CorrectAnswer).Shuffle(random).ToList();
            var correctIndex = allAnswersList.IndexOf(answers.CorrectAnswer);

            return new(title, allAnswersList.ToArray(), correctIndex);

            IEnumerable<string> GetTwoRandomNames() => Main.AllPlayerControls.Shuffle(random).TakeLast(2).Select(x => x.GetRealName());
            IEnumerable<string> GetTwoRandomNumbers(params int[] nums) => Enumerable.Range(nums[1], nums[2]).Where(x => x != nums[0]).Shuffle(random).Take(2).Select(x => x.ToString());
        }

        public override void OnReportDeadBody()
        {
            var target = Utils.GetPlayerById(Target);
            if (target == null) return;

            CurrentQuestion = GetQuestion();
        }

        public void Answer(string answer)
        {
            var index = answer switch
            {
                "A" => 0,
                "B" => 1,
                "C" => 2,
                _ => -1
            };

            try
            {
                if (CurrentQuestion.CorrectAnswerIndex == index)
                {
                    Utils.SendMessage(Translator.GetString("QuizMaster.AnswerCorrect"), Target, Translator.GetString("QuizMaster.Title"));
                    Utils.SendMessage(string.Format(Translator.GetString("QuizMaster.AnswerCorrect.Self"), CurrentQuestion.Answers[CurrentQuestion.CorrectAnswerIndex]), QuizMasterId, Translator.GetString("QuizMaster.Title"));
                }
                else
                {
                    Utils.SendMessage(string.Format(Translator.GetString("QuizMaster.AnswerIncorrect"), CurrentQuestion.Answers[CurrentQuestion.CorrectAnswerIndex]), Target, Translator.GetString("QuizMaster.Title"));
                    Utils.SendMessage(string.Format(Translator.GetString("QuizMaster.AnswerIncorrect.Self"), CurrentQuestion.Answers[index], CurrentQuestion.Answers[CurrentQuestion.CorrectAnswerIndex]), QuizMasterId, Translator.GetString("QuizMaster.Title"));
                }
            }
            catch (IndexOutOfRangeException)
            {
            }
            catch (Exception e)
            {
                Utils.ThrowException(e);
            }

            CurrentQuestion = default;
            Target = byte.MaxValue;
        }

        // Left to do: Send message on meeting start, kill target if wrong answer, let target answer from chat commands patch
    }
}
