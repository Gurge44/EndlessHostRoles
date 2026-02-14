using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Gamemodes;
using EHR.Modules;
using EHR.Patches;
using static EHR.Options;

namespace EHR.Roles;

internal class QuizMaster : RoleBase
{
    private const int Id = 10890;
    public static bool On;

    private static OptionItem MarkCooldown;
    private static OptionItem CanVent;
    private static OptionItem HasImpostorVision;
    private static OptionItem CanKillWithDoubleClick;
    private static OptionItem KillCooldown;
    private static OptionItem EnableCustomQuestionsOpt;
    private static OptionItem CustomQuestionChance;

    private static bool EnableCustomQuestions;
    private static Question[] CustomQuestions = [];

    public static Dictionary<byte, string> MessagesToSend = [];
    public static List<QuizMaster> QuizMasters = [];

    private static List<string> AllColors = [];

    public static readonly SystemTypes[] AllSabotages =
    [
        SystemTypes.Comms,
        SystemTypes.Reactor,
        SystemTypes.Electrical,
        SystemTypes.LifeSupp,
        SystemTypes.MushroomMixupSabotage,
        SystemTypes.Laboratory,
        SystemTypes.HeliSabotage,
        (SystemTypes)SubmergedCompatibility.SubmergedSystemTypes.Ballast
    ];

    public static ((string ColorString, PlayerControl Player) LastReportedPlayer, string LastPlayerPressedButtonName, SystemTypes LastSabotage, string LastReporterName, int NumPlayersVotedLastMeeting, string FirstReportedBodyPlayerName, int NumEmergencyMeetings, int NumPlayersDeadThisRound, int NumPlayersDeadFirstRound, int NumSabotages, int NumMeetings) Data = ((string.Empty, null), string.Empty, default(SystemTypes), string.Empty, 0, string.Empty, 0, 0, 0, 0, 0);

    private Question CurrentQuestion;
    public byte QuizMasterId;
    public byte Target;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.QuizMaster);

        MarkCooldown = new FloatOptionItem(Id + 2, "QuizMaster.MarkCooldown", new(0f, 180f, 1f), 1f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.QuizMaster])
            .SetValueFormat(OptionFormat.Seconds);

        CanVent = new BooleanOptionItem(Id + 3, "CanVent", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.QuizMaster]);

        HasImpostorVision = new BooleanOptionItem(Id + 4, "ImpostorVision", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.QuizMaster]);

        CanKillWithDoubleClick = new BooleanOptionItem(Id + 5, "CanKillWithDoubleClick", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.QuizMaster]);

        KillCooldown = new FloatOptionItem(Id + 6, "KillCooldown", new(0f, 60f, 0.5f), 22.5f, TabGroup.NeutralRoles)
            .SetParent(CanKillWithDoubleClick)
            .SetValueFormat(OptionFormat.Seconds);

        EnableCustomQuestionsOpt = new BooleanOptionItem(Id + 7, "QuizMaster.EnableCustomQuestions", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.QuizMaster]);

        CustomQuestionChance = new FloatOptionItem(Id + 8, "QuizMaster.CustomQuestionChance", new(0f, 100f, 5f), 50f, TabGroup.NeutralRoles)
            .SetParent(EnableCustomQuestionsOpt)
            .SetValueFormat(OptionFormat.Percent);
    }

    public override void Init()
    {
        On = false;
        Target = byte.MaxValue;
        CurrentQuestion = null;

        QuizMasters = [];

        Data = ((string.Empty, null), string.Empty, default(SystemTypes), string.Empty, 0, string.Empty, 0, 0, 0, 0, 0);

        AllColors = [];

        LateTask.New(() =>
        {
            foreach (PlayerControl pc in Main.EnumeratePlayerControls())
            {
                int colorId = pc.Data.DefaultOutfit.ColorId;
                AllColors.Add(Palette.GetColorName(colorId));
            }
        }, 10f, log: false);

        CustomQuestions = [];
        EnableCustomQuestions = EnableCustomQuestionsOpt.GetBool() && File.Exists($"{Main.DataPath}/EHR_DATA/QuizMasterQuestions.txt");

        if (EnableCustomQuestions)
        {
            string[] lines = File.ReadAllLines($"{Main.DataPath}/EHR_DATA/QuizMasterQuestions.txt");

            IEnumerable<Question> questions = lines.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x =>
            {
                try
                {
                    string[] line = x.Split(';');
                    return new Question(line[0], line[1].Split(','), int.Parse(line[2]));
                }
                catch (Exception e)
                {
                    Utils.ThrowException(e);
                    return null;
                }
            }).Where(x => x != null);

            CustomQuestions = questions.ToArray();

            EnableCustomQuestions &= CustomQuestions.Length > 0;
        }
    }

    public override void Add(byte playerId)
    {
        On = true;
        QuizMasters.Add(this);
        QuizMasterId = playerId;
    }

    public override void Remove(byte playerId)
    {
        QuizMasters.Remove(this);
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = CanKillWithDoubleClick.GetBool() ? KillCooldown.GetFloat() : MarkCooldown.GetFloat();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        opt.SetVision(HasImpostorVision.GetBool());
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return CanVent.GetBool();
    }

    public override void AfterMeetingTasks()
    {
        Data.NumPlayersDeadThisRound = 0;

        if (Target != byte.MaxValue)
        {
            CheckForEndVotingPatch.TryAddAfterMeetingDeathPlayers(PlayerState.DeathReason.WrongAnswer, Target);
            Target = byte.MaxValue;
        }

        MessagesToSend = [];
        CurrentQuestion = null;
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (!base.OnCheckMurder(killer, target)) return false;

        if (!CanKillWithDoubleClick.GetBool())
        {
            Target = target.PlayerId;
            return false;
        }

        return killer.CheckDoubleTrigger(target, () =>
        {
            Target = target.PlayerId;
            killer.RPCPlayCustomSound("Clothe");
            killer.SetKillCooldown(MarkCooldown.GetFloat());
        });
    }

    private static Question GetQuestion()
    {
        List<int> allowedIndexes = [];
        List<int> allowedABCIndexes = [];

        for (var i = 1; i <= 14; i++)
        {
            switch (i)
            {
                case 1 when Data.LastReportedPlayer.ColorString != string.Empty:
                case 2 when Data.LastPlayerPressedButtonName != string.Empty:
                case 3 when Data.LastSabotage != default(SystemTypes):
                case 4 when Main.LastVotedPlayerInfo != null:
                case 5 when Data.LastReporterName != string.Empty:
                case 6 when Data.NumPlayersVotedLastMeeting != 0:
                case 7:
                case 8 when Data.FirstReportedBodyPlayerName != string.Empty:
                case 9 or 10 or 11 or 12 or 13 or 14:
                    allowedIndexes.Add(i);
                    break;
            }
        }

        for (var i = 1; i <= 6; i++)
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

        if (EnableCustomQuestions && random.Next(100) < CustomQuestionChance.GetInt()) return CustomQuestions.RandomElement();

        bool abc = random.Next(4) == 0;
        List<int> indexes = abc ? allowedABCIndexes : allowedIndexes;
        int index = indexes.RandomElement();

        CustomRoles randomRole = Main.CustomRoleValues.Where(x => x.IsEnable() && !x.IsAdditionRole() && !CustomHnS.AllHnSRoles.Contains(x) && !x.IsForOtherGameMode()).RandomElement();

        string title = index switch
        {
            14 => string.Format(Translator.GetString("QuizMaster.Question.14"), randomRole.ToColoredString()),
            5 or 6 when abc => string.Format(Translator.GetString($"QuizMaster.Question.ABC.{index}"), randomRole.ToColoredString()),
            _ when abc => Translator.GetString($"QuizMaster.Question.ABC.{index}"),
            _ => Translator.GetString($"QuizMaster.Question.{index}")
        };

        (IEnumerable<string> WrongAnswers, string CorrectAnswer) answers = index switch
        {
            1 when abc => (Enum.GetValues<Team>().Skip(1).Where(x => x != Data.LastReportedPlayer.Player.GetTeam()).Select(x => Translator.GetString($"{x}")), Translator.GetString($"{Data.LastReportedPlayer.Player.GetTeam()}")),
            1 => (AllColors.Without(Data.LastReportedPlayer.ColorString).Shuffle().Take(2), Data.LastReportedPlayer.ColorString),
            2 when abc => (new[] { PlayerState.DeathReason.Suicide, PlayerState.DeathReason.Kill, PlayerState.DeathReason.etc }.Without(Main.PlayerStates[Data.LastReportedPlayer.Player!.PlayerId].deathReason).Select(x => Translator.GetString($"DeathReason.{x}")), Translator.GetString($"DeathReason.{Main.PlayerStates[Data.LastReportedPlayer.Player!.PlayerId].deathReason}")),
            2 => (GetTwoRandomNames(Data.LastPlayerPressedButtonName), Data.LastPlayerPressedButtonName),
            3 when abc => (["Town Of Us", "Town Of Host Enhanced"], "Endless Host Roles"),
            3 => (AllSabotages.Take(2).Select(x => Translator.GetString($"{x}")), Translator.GetString($"{Data.LastSabotage}")),
            4 when abc => (["tukasa0001", "0xDrMoe"], "Gurge44"),
            4 => (CustomRoleSelector.RoleResult.Values.Without(Main.LastVotedPlayerInfo!.Object.GetCustomRole()).Shuffle().Take(2).Select(x => x.ToColoredString()), Main.LastVotedPlayerInfo!.Object.GetCustomRole().ToColoredString()),
            5 when abc => (Enum.GetValues<Team>().Skip(1).Where(x => x != randomRole.GetTeam()).Select(x => Translator.GetString($"{x}")), Translator.GetString($"{randomRole.GetTeam()}")),
            5 => (GetTwoRandomNames(Data.LastReporterName), Data.LastReporterName),
            6 when abc => (Enum.GetValues<CustomRoleTypes>().Where(x => x != randomRole.GetCustomRoleTypes()).Shuffle().Take(2).Select(x => Translator.GetString($"{x}")), Translator.GetString($"{randomRole.GetCustomRoleTypes()}")),
            6 => (GetTwoRandomNumbers(Data.NumPlayersVotedLastMeeting, 0, 15), Data.NumPlayersVotedLastMeeting.ToString()),
            7 => (GetTwoRandomNumbers(Data.NumMeetings, Math.Max(Data.NumMeetings - 3, 0), Data.NumMeetings + 3), Data.NumMeetings.ToString()),
            8 => (GetTwoRandomNames(Data.FirstReportedBodyPlayerName), Data.FirstReportedBodyPlayerName),
            9 => (GetTwoRandomNumbers(Data.NumEmergencyMeetings, Math.Max(Data.NumEmergencyMeetings - 3, 0), Data.NumEmergencyMeetings + 3), Data.NumEmergencyMeetings.ToString()),
            10 => (GetTwoRandomNumbers(Data.NumPlayersDeadThisRound, Math.Max(Data.NumPlayersDeadThisRound - 3, 0), Data.NumPlayersDeadThisRound + 3), Data.NumPlayersDeadThisRound.ToString()),
            11 => (GetTwoRandomNumbers(Data.NumPlayersDeadFirstRound, Math.Max(Data.NumPlayersDeadFirstRound - 3, 0), Data.NumPlayersDeadFirstRound + 3), Data.NumPlayersDeadFirstRound.ToString()),
            12 => (GetTwoRandomNumbers(Data.NumSabotages, Math.Max(Data.NumSabotages - 3, 0), Data.NumSabotages + 3), Data.NumSabotages.ToString()),
            13 => (GetTwoRandomNumbers(GameData.Instance.CompletedTasks, 0, GameData.Instance.TotalTasks), GameData.Instance.CompletedTasks.ToString()),
            14 => (Enum.GetValues<RoleTypes>().Without(RoleTypes.CrewmateGhost).Without(RoleTypes.ImpostorGhost).Where(x => x != randomRole.GetRoleTypes()).Shuffle().Take(2).Select(x => Translator.GetString($"{x}")), Translator.GetString($"{randomRole.GetRoleTypes()}")),

            _ => ([], string.Empty)
        };

        List<string> allAnswersList = answers.WrongAnswers.Append(answers.CorrectAnswer).Shuffle();
        int correctIndex = allAnswersList.IndexOf(answers.CorrectAnswer);

        return new(title, allAnswersList.ToArray(), correctIndex);

        IEnumerable<string> GetTwoRandomNames(string except) => Main.EnumeratePlayerControls().Select(x => x?.GetRealName()).Without(except).Shuffle().TakeLast(2);

        IEnumerable<string> GetTwoRandomNumbers(params int[] nums) => IRandom.SequenceUnique(3, nums[1], nums[2] + 1).Without(nums[0]).Take(2).Select(x => x.ToString());
    }

    public override void OnReportDeadBody()
    {
        if (Target == byte.MaxValue) return;

        CurrentQuestion = GetQuestion();

        string msgTitle = CurrentQuestion.QuestionText;
        string answers = string.Join('\n', CurrentQuestion.Answers.Select((x, i) => $"{(char)(65 + i)}: <b>{x}</b>"));
        string sample = string.Format(Translator.GetString("QuizMaster.QuestionSample"), msgTitle, answers);

        MessagesToSend[Target] = sample;

        RPC.PlaySoundRPC(Target, Sounds.TaskUpdateSound);
        Logger.Info($"Question for {Utils.GetPlayerById(Target)?.GetNameWithRole()}: {msgTitle} - Choices: {string.Join('/', CurrentQuestion.Answers)} - Correct Answer: {CurrentQuestion.Answers[CurrentQuestion.CorrectAnswerIndex]}", "QuizMaster");
    }

    public void Answer(string answer)
    {
        int index = -1;

        try
        {
            index = answer[0] - 'A';

            PlayerControl pc = Utils.GetPlayerById(Target);
            string name = pc?.GetNameWithRole();
            if (index != -1) Logger.Info($"Player {name} answered {CurrentQuestion.Answers[index]}", "QuizMaster");

            if (CurrentQuestion.CorrectAnswerIndex == index)
            {
                if (pc != null) RPC.PlaySoundRPC(pc.PlayerId, Sounds.TaskComplete);
                Utils.SendMessage(Translator.GetString("QuizMaster.AnswerCorrect"), Target, Translator.GetString("QuizMaster.Title"), importance: MessageImportance.High);
                Utils.SendMessage(string.Format(Translator.GetString("QuizMaster.AnswerCorrect.Self"), CurrentQuestion.Answers[CurrentQuestion.CorrectAnswerIndex]), QuizMasterId, Translator.GetString("QuizMaster.Title"), importance: MessageImportance.High);

                Logger.Info($"Player {name} answered correctly", "QuizMaster");
            }
            else if (index != -1)
            {
                Utils.SendMessage(string.Format(Translator.GetString("QuizMaster.AnswerIncorrect"), CurrentQuestion.Answers[CurrentQuestion.CorrectAnswerIndex]), Target, Translator.GetString("QuizMaster.Title"), importance: MessageImportance.High);
                Utils.SendMessage(string.Format(Translator.GetString("QuizMaster.AnswerIncorrect.Self"), CurrentQuestion.Answers[index], CurrentQuestion.Answers[CurrentQuestion.CorrectAnswerIndex]), QuizMasterId, Translator.GetString("QuizMaster.Title"), importance: MessageImportance.High);

                if (pc.Is(CustomRoles.Pestilence)) return;
                Main.PlayerStates[Target].deathReason = PlayerState.DeathReason.WrongAnswer;
                pc?.RpcGuesserMurderPlayer();
                Utils.AfterPlayerDeathTasks(pc, true);

                Logger.Info($"Player {name} was killed for answering incorrectly", "QuizMaster");
            }
        }
        catch (IndexOutOfRangeException) { }
        catch (Exception e) { Utils.ThrowException(e); }

        if (index == -1) return;

        CurrentQuestion = null;
        Target = byte.MaxValue;
    }

    private class Question(string questionText, string[] answers, int correctAnswerIndex)
    {
        public readonly string[] Answers = answers;
        public readonly int CorrectAnswerIndex = correctAnswerIndex;
        public readonly string QuestionText = questionText;
    }
}
