using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmongUs.GameOptions;
using EHR.Modules;
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
            SystemTypes.Electrical,
            SystemTypes.Laboratory,
            SystemTypes.LifeSupp,
            SystemTypes.Reactor,
            SystemTypes.HeliSabotage,
            SystemTypes.MushroomMixupSabotage
        ];

        public static ((Color32 Color, string String, PlayerControl Player) LastReportedPlayer, string LastPlayerPressedButtonName, SystemTypes LastSabotage, string LastReporterName, int NumPlayersVotedLastMeeting, string FirstReportedBodyPlayerName, int NumEmergencyMeetings, int NumPlayersDeadThisRound, int NumPlayersDeadFirstRound, int NumSabotages) Data = ((new(), string.Empty, null), string.Empty, default, string.Empty, 0, string.Empty, 0, 0, 0, 0);

        public override void Init()
        {
            On = false;
            Data = ((new(), string.Empty, null), string.Empty, default, string.Empty, 0, string.Empty, 0, 0, 0, 0);

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

        Question GetQuestion()
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

            string title = Translator.GetString($"QuizMaster.Question.{index}");
            string[] answers = index switch
            {
                1 when abc => EnumHelper.GetAllNames<Team>().Select(x => Translator.GetString($"{x}")).ToArray()[1..],
                1 => AllColors.Shuffle(random).Select(x => x.String).Take(3).ToArray()
            };
        }
    }
}
