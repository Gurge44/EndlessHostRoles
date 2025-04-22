using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using HarmonyLib;
using Hazel;
using UnityEngine;
using static EHR.Translator;

namespace EHR;

public static class Quiz
{
    public static readonly HashSet<string> HasPlayedFriendCodes = [];

    private static readonly Dictionary<Difficulty, HashSet<int>> AskedQuestions = new()
    {
        [Difficulty.Easy] = [],
        [Difficulty.Medium] = [],
        [Difficulty.Hard] = []
    };

    private static readonly Dictionary<Difficulty, int> TotalQuestions = new()
    {
        [Difficulty.Easy] = 223,
        [Difficulty.Medium] = 123,
        [Difficulty.Hard] = 133
    };

    private static readonly Dictionary<MapNames, Dictionary<char, SystemTypes>> UsedRooms = new()
    {
        [MapNames.Skeld] = new()
        {
            ['A'] = SystemTypes.Reactor,
            ['B'] = SystemTypes.UpperEngine,
            ['C'] = SystemTypes.Security,
            ['D'] = SystemTypes.LowerEngine,
            ['E'] = SystemTypes.Hallway
        },
        [MapNames.MiraHQ] = new()
        {
            ['A'] = SystemTypes.Office,
            ['B'] = SystemTypes.Greenhouse,
            ['C'] = SystemTypes.Admin,
            ['D'] = SystemTypes.Comms,
            ['E'] = SystemTypes.LockerRoom
        },
        [MapNames.Polus] = new()
        {
            ['A'] = SystemTypes.Security,
            ['B'] = SystemTypes.Electrical,
            ['C'] = SystemTypes.Outside,
            ['D'] = SystemTypes.LifeSupp,
            ['E'] = SystemTypes.BoilerRoom
        },
        [MapNames.Airship] = new()
        {
            ['A'] = SystemTypes.Cockpit,
            ['B'] = SystemTypes.Comms,
            ['C'] = SystemTypes.Engine,
            ['D'] = SystemTypes.Armory,
            ['E'] = SystemTypes.Kitchen
        },
        [MapNames.Fungle] = new()
        {
            ['A'] = SystemTypes.Cafeteria,
            ['B'] = SystemTypes.Dropship,
            ['C'] = SystemTypes.Storage,
            ['D'] = SystemTypes.MeetingRoom,
            ['E'] = SystemTypes.Kitchen
        }
    };

    private static Dictionary<byte, Dictionary<Difficulty, int[]>> NumCorrectAnswers;
    private static List<byte> DyingPlayers;
    private static long QuestionTimeLimitEndTS;
    private static (string Question, List<string> Answers, int CorrectAnswerIndex) CurrentQuestion;
    private static Difficulty CurrentDifficulty;
    private static int Round;
    private static int QuestionsAsked;
    private static long FFAEndTS;
    private static bool NoSuffix;

    public static bool AllowKills;

    private static OptionItem FFAEventLength;
    private static readonly Dictionary<Difficulty, (OptionItem Rounds, OptionItem QuestionsAsked, OptionItem CorrectRequirement, OptionItem TimeLimit)> Settings = [];

    static Quiz()
    {
        UsedRooms[MapNames.Dleks] = UsedRooms[MapNames.Skeld];
    }

    public static void SetupCustomOption()
    {
        var id = 69_220_001;
        var color = Utils.GetRoleColor(CustomRoles.QuizMaster);
        const CustomGameMode gameMode = CustomGameMode.Quiz;

        FFAEventLength = new IntegerOptionItem(id++, "Quiz.Settings.FFAEventLength", new(5, 120, 1), 30, TabGroup.GameSettings)
            .SetValueFormat(OptionFormat.Seconds)
            .SetHeader(true)
            .SetColor(color)
            .SetGameMode(gameMode);

        foreach (Difficulty difficulty in Enum.GetValues<Difficulty>()[1..])
        {
            var rounds = new IntegerOptionItem(id++, $"Quiz.Settings.Rounds.{difficulty}", new(1, 50, 1), 3, TabGroup.GameSettings)
                .SetHidden(difficulty == Difficulty.Hard)
                .SetHeader(true)
                .SetColor(color)
                .SetGameMode(gameMode);

            var questionsAsked = new IntegerOptionItem(id++, $"Quiz.Settings.QuestionsAsked.{difficulty}", new(1, 50, 1), 3, TabGroup.GameSettings)
                .SetColor(color)
                .SetGameMode(gameMode);

            var correctRequirement = new IntegerOptionItem(id++, $"Quiz.Settings.CorrectRequirement.{difficulty}", new(1, 50, 1), 4 - (int)difficulty, TabGroup.GameSettings)
                .SetColor(color)
                .SetGameMode(gameMode);

            var timeLimit = new IntegerOptionItem(id++, $"Quiz.Settings.TimeLimit.{difficulty}", new(5, 120, 1), 10 + 5 * (int)difficulty, TabGroup.GameSettings)
                .SetValueFormat(OptionFormat.Seconds)
                .SetColor(color)
                .SetGameMode(gameMode);

            Settings[difficulty] = (rounds, questionsAsked, correctRequirement, timeLimit);
        }
    }

    public static string GetStatistics(byte id)
    {
        if (!NumCorrectAnswers.TryGetValue(id, out var data)) return string.Empty;
        string str = string.Join(" | ", data.Select(x => $"{x.Key.ToString()[0]}-{x.Value.Sum()}"));
        return GetString("Quiz.EndResults.CorrectAnswerNum") + str;
    }

    public static bool KnowTargetRoleColor(PlayerControl target, ref string color)
    {
        if (NoSuffix || FFAEndTS != 0 || QuestionTimeLimitEndTS != 0) return false;

        color = DyingPlayers.Contains(target.PlayerId) ? "#ff0000" : "#00ff00";
        return true;
    }

    public static string GetSuffix(PlayerControl seer)
    {
        if (NoSuffix) return string.Empty;

        bool wasWrong = DyingPlayers.Contains(seer.PlayerId);

        if (FFAEndTS == 0)
        {
            if (QuestionTimeLimitEndTS != 0)
            {
                string answers = string.Empty;

                for (int i = 0; i < CurrentQuestion.Answers.Count; i++)
                {
                    string answer = CurrentQuestion.Answers[i];
                    char letter = (char)('A' + i);
                    SystemTypes room = UsedRooms[Main.CurrentMap][letter];

                    PlainShipRoom psr = seer.GetPlainShipRoom();
                    bool isInThisRoom = room == SystemTypes.Outside ? psr == null : psr != null && psr.RoomId == room;

                    string prefix = isInThisRoom ? "\u27a1 <u>" : string.Empty;
                    string suffix = isInThisRoom ? "</u>" : string.Empty;
                    string add = $"{prefix}{letter}: {answer} ({GetString(room.ToString())}){suffix}\n";

                    answers += add;
                }

                string question = CurrentQuestion.Question;

                for (var i = 50; i < question.Length; i += 50)
                {
                    int index = question.LastIndexOf(' ', i);
                    if (index != -1) question = question.Insert(index + 1, "\n");
                }

                return $"<#ffff44>{question}</color>\n{answers}<#CF2472>{QuestionTimeLimitEndTS - Utils.TimeStamp}</color>";
            }

            string correctAnswer = CurrentQuestion.Answers[CurrentQuestion.CorrectAnswerIndex];
            char correctAnswerLetter = (char)('A' + CurrentQuestion.CorrectAnswerIndex);
            string correctRoom = GetString(UsedRooms[Main.CurrentMap][correctAnswerLetter].ToString());
            string color = wasWrong ? "#FF0000" : "#00FF00";
            string str = string.Format(GetString("Quiz.Notify.CorrectAnswer"), color, correctAnswerLetter, correctAnswer, correctRoom);

            int numPlayers = Main.AllAlivePlayerControls.Length;
            if (DyingPlayers.Count == numPlayers) str += "\n" + GetString("Quiz.Notify.AllWrong");

            if (CurrentDifficulty > Difficulty.Test && Settings[CurrentDifficulty].QuestionsAsked.GetInt() <= QuestionsAsked)
            {
                int requiredCorrect = Settings[CurrentDifficulty].CorrectRequirement.GetInt();
                int gotCorrect = NumCorrectAnswers[seer.PlayerId][CurrentDifficulty][Round];
                bool failed = gotCorrect < requiredCorrect;

                if (seer.IsAlive()) str += "\n\n" + string.Format(Utils.ColorString(failed ? Color.red : Color.green, GetString("Quiz.Notify.CorrectAnswerNum")), gotCorrect, QuestionsAsked, requiredCorrect);

                str += "\n" + NumCorrectAnswers.Count(x => x.Value[CurrentDifficulty][Round] < requiredCorrect) switch
                {
                    0 => GetString("Quiz.Notify.AllCorrect"),
                    var x when x == numPlayers => GetString("Quiz.Notify.AllDie"),
                    1 => failed ? GetString("Quiz.Notify.OnlyYouWrong") : GetString("Quiz.Notify.OneWrong"),
                    _ => failed ? GetString("Quiz.Notify.IncorrectFFA") : GetString("Quiz.Notify.CorrectFFA")
                };
            }

            return str;
        }

        if (!AllowKills) return string.Empty;

        var ffaTimeLeft = FFAEndTS - Utils.TimeStamp;
        var rndRoom = Main.AllAlivePlayerControls.Without(seer).Select(x => x.GetPlainShipRoom()).Where(x => x != null).Select(x => x.RoomId).Distinct().Without(SystemTypes.Hallway).RandomElement();
        return string.Format(GetString(wasWrong ? "Quiz.Notify.FFAOngoing" : "Quiz.Notify.FFASpectating"), GetString(rndRoom.ToString()), ffaTimeLeft);
    }

    public static void Init()
    {
        NumCorrectAnswers = [];
        DyingPlayers = [];
        QuestionTimeLimitEndTS = 0;
        CurrentQuestion = (string.Empty, [], 0);
        CurrentDifficulty = Difficulty.Easy;
        Round = 0;
        QuestionsAsked = 0;
        FFAEndTS = 0;
        AllowKills = false;
        NoSuffix = true;

        foreach (PlayerControl pc in Main.AllPlayerControls)
        {
            NumCorrectAnswers[pc.PlayerId] = new Dictionary<Difficulty, int[]>
            {
                [Difficulty.Easy] = new int[Settings[Difficulty.Easy].Rounds.GetInt()],
                [Difficulty.Medium] = new int[Settings[Difficulty.Medium].Rounds.GetInt()],
                [Difficulty.Hard] = new int[100] // Surely there won't be more than 100 hard rounds, right?
            };
        }
    }

    public static IEnumerator OnGameStart()
    {
        NoSuffix = true;
        PlayerControl[] aapc = Main.AllAlivePlayerControls;
        bool showTutorial = aapc.ExceptBy(HasPlayedFriendCodes, x => x.FriendCode).Count() > aapc.Length / 2;

        var usedRooms = string.Join('\n', UsedRooms[Main.CurrentMap].Select(x => $"{x.Key}: {GetString(x.Value.ToString())}"));
        aapc.NotifyPlayers(string.Format(GetString("Quiz.Tutorial.Basics"), usedRooms), 11f);
        yield return new WaitForSeconds(showTutorial ? 9f : 5f);
        if (GameStates.IsMeeting || ExileController.Instance || !GameStates.InGame || GameStates.IsLobby) yield break;
        NameNotifyManager.Reset();

        if (showTutorial)
        {
            aapc.NotifyPlayers(GetString("Quiz.Tutorial.TestRound"));
            yield return new WaitForSeconds(3f);
            if (GameStates.IsMeeting || ExileController.Instance || !GameStates.InGame || GameStates.IsLobby) yield break;
            NameNotifyManager.Reset();

            NoSuffix = false;
            CurrentDifficulty = Difficulty.Test;
            QuestionTimeLimitEndTS = Utils.TimeStamp + 30;
            CurrentQuestion = GetQuestion(0);

            while (QuestionTimeLimitEndTS != 0)
            {
                yield return new WaitForSeconds(1f);
                if (GameStates.IsMeeting || ExileController.Instance || !GameStates.InGame || GameStates.IsLobby) yield break;
            }

            yield return new WaitForSeconds(3f);
            NoSuffix = true;

            aapc.NotifyPlayers(GetString("Quiz.Tutorial.OnWrongAnswer"), 10f);
            yield return new WaitForSeconds(3f);
            if (GameStates.IsMeeting || ExileController.Instance || !GameStates.InGame || GameStates.IsLobby) yield break;
            NameNotifyManager.Reset();

            aapc.NotifyPlayers(GetString("Quiz.Tutorial.OnCorrectAnswer"));
            yield return new WaitForSeconds(2f);
            if (GameStates.IsMeeting || ExileController.Instance || !GameStates.InGame || GameStates.IsLobby) yield break;
            NameNotifyManager.Reset();
        }

        yield return NewQuestion(increaseDifficulty: showTutorial, newRound: true);
    }

    private static IEnumerator NewQuestion(bool increaseDifficulty = true, bool newRound = false)
    {
        if (increaseDifficulty && CurrentDifficulty != Difficulty.Hard)
        {
            Round = 0;
            CurrentDifficulty++;
            NoSuffix = true;
            var settings = Settings[CurrentDifficulty];
            Main.AllAlivePlayerControls.NotifyPlayers(string.Format(GetString("Quiz.Notify.NextStage"), (int)CurrentDifficulty, settings.Rounds.GetInt(), settings.QuestionsAsked.GetInt(), settings.CorrectRequirement.GetInt(), settings.QuestionsAsked.GetInt(), settings.TimeLimit.GetInt()), 8f);
            yield return new WaitForSeconds(7f);
            if (GameStates.IsMeeting || ExileController.Instance || !GameStates.InGame || GameStates.IsLobby) yield break;
            NameNotifyManager.Reset();
        }

        if (newRound)
        {
            if (CurrentDifficulty == Difficulty.Hard && Round >= 100) NumCorrectAnswers.Values.Do(x => x[CurrentDifficulty][Round] = 0);

            NoSuffix = true;
            Main.AllAlivePlayerControls.NotifyPlayers(string.Format(GetString("Quiz.Notify.NextRound"), Round + 1));
            yield return new WaitForSeconds(3f);
            if (GameStates.IsMeeting || ExileController.Instance || !GameStates.InGame || GameStates.IsLobby) yield break;
            NameNotifyManager.Reset();
        }

        NoSuffix = false;
        int time = Settings[CurrentDifficulty].TimeLimit.GetInt();
        int questionIndex = GetRandomQuestionIndex();
        CurrentQuestion = GetQuestion(questionIndex);
        time += CurrentQuestion.Question.Length / 20;
        time += CurrentQuestion.Answers.Sum(x => x.Length / 15);
        if (Main.CurrentMap is MapNames.Skeld or MapNames.Dleks) time -= 3;
        QuestionTimeLimitEndTS = Utils.TimeStamp + time;

        Logger.Info($"New question: {CurrentQuestion.Question} | {string.Join(", ", CurrentQuestion.Answers)} | {CurrentQuestion.Answers[CurrentQuestion.CorrectAnswerIndex]}", "Quiz");
        Logger.Info($"Time limit: {time}", "Quiz");
    }

    private static int GetRandomQuestionIndex()
    {
        HashSet<int> asked = AskedQuestions[CurrentDifficulty];
        int max = TotalQuestions[CurrentDifficulty];
        int index = Enumerable.Range(1, max).Except(asked).RandomElement();
        asked.Add(index);
        if (asked.Count >= max) asked.Clear();
        return index;
    }

    private static (string Question, List<string> Answers, int CorrectAnswerIndex) GetQuestion(int questionIndex)
    {
        string baseString = $"Quiz.Questions.{CurrentDifficulty}.{questionIndex}";
        string question = GetString(baseString);
        Dictionary<char, string> answers = new[] { 'A', 'B', 'C', 'D', 'E' }.ToDictionary(x => x, x => GetString($"{baseString}.Answer.{x}"));

        string correctAnswer;

        if (CurrentDifficulty == Difficulty.Test)
        {
            MapNames map = Main.CurrentMap;
            if (map == MapNames.Dleks) map = MapNames.Skeld;
            if (map > MapNames.Dleks) map--;
            correctAnswer = answers[(char)((int)map + 65)];
        }
        else
            correctAnswer = answers[GetString($"{baseString}.CorrectAnswer")[0]];

        List<string> shuffledAnswers = answers.Values.Shuffle();
        int correctAnswerIndex = shuffledAnswers.IndexOf(correctAnswer);
        return (question, shuffledAnswers, correctAnswerIndex);
    }

    private static IEnumerator EndQuestion()
    {
        QuestionsAsked++;
        QuestionTimeLimitEndTS = 0;
        PlayerControl[] aapc = Main.AllAlivePlayerControls;
        SystemTypes correctRoom = UsedRooms[Main.CurrentMap][(char)('A' + CurrentQuestion.CorrectAnswerIndex)];
        DyingPlayers = aapc.Select(x => (ID: x.PlayerId, Room: x.GetPlainShipRoom())).Where(x => correctRoom == SystemTypes.Outside ? x.Room != null : x.Room == null || x.Room.RoomId != correctRoom).Select(x => x.ID).ToList();
        bool everyoneWasWrong = DyingPlayers.Count == aapc.Length;
        if (!everyoneWasWrong) NumCorrectAnswers.DoIf(x => !DyingPlayers.Contains(x.Key), x => x.Value[CurrentDifficulty][Round]++);
        Logger.Info($"{(everyoneWasWrong ? "Everyone" : "Players who")} got the question wrong: {string.Join(", ", DyingPlayers.Select(x => Main.AllPlayerNames.GetValueOrDefault(x, $"Someone (ID {x})")))}", "Quiz");
        Logger.Info($"Number of correct answers for everyone currently: {string.Join(", ", NumCorrectAnswers.Select(x => $"{Main.AllPlayerNames.GetValueOrDefault(x.Key, string.Empty)}: {x.Value[CurrentDifficulty][Round]}"))}", "Quiz");

        if (everyoneWasWrong) QuestionsAsked--;
        else Utils.NotifyRoles();

        yield return new WaitForSeconds(everyoneWasWrong ? 11f : 7f);
        if (GameStates.IsMeeting || ExileController.Instance || !GameStates.InGame || GameStates.IsLobby) yield break;

        var settings = Settings[CurrentDifficulty];

        if (settings.QuestionsAsked.GetInt() > QuestionsAsked)
        {
            yield return NewQuestion(increaseDifficulty: false);
            yield break;
        }

        yield return new WaitForSeconds(3f);

        var correctRequirement = settings.CorrectRequirement.GetInt();
        List<byte> dyingPlayers = NumCorrectAnswers.Where(x => x.Value[CurrentDifficulty][Round] < correctRequirement).Select(x => x.Key).ToList();
        Logger.Info($"Round {Round + 1} of {CurrentDifficulty} difficulty ended. Dying players: {dyingPlayers.Count} | {string.Join(", ", dyingPlayers.Select(x => Main.AllPlayerNames[x]))}", "Quiz");

        Round++;
        QuestionsAsked = 0;

        switch (dyingPlayers.Count)
        {
            case 0:
                yield return NewQuestion(increaseDifficulty: settings.Rounds.GetInt() <= Round, newRound: true);
                yield break;
            case 1:
                var pc = dyingPlayers[0].GetPlayer();
                if (pc != null) pc.Suicide();
                goto case 0;
            case var x when x == aapc.Length:
                Round--;
                NumCorrectAnswers.Values.Do(d => d[CurrentDifficulty][Round] = 0);
                goto case 0;
            default:
                yield return new WaitForSeconds(3f);
                AllowKills = true;
                FFAEndTS = Utils.TimeStamp + FFAEventLength.GetInt();
                var spectators = aapc.ExceptBy(dyingPlayers, x => x.PlayerId).ToArray();
                var sender = CustomRpcSender.Create("Quiz.FFA-Event-RpcExileV2", SendOption.Reliable);
                spectators.Do(sender.RpcExileV2);
                sender.SendMessage();
                Main.PlayerStates.Values.IntersectBy(spectators.Select(x => x.PlayerId), x => x.Player.PlayerId).Do(x => x.SetDead());
                var stillLiving = dyingPlayers.ToValidPlayers().FindAll(x => x.IsAlive());
                stillLiving.ForEach(x => x.RpcChangeRoleBasis(CustomRoles.NSerialKiller));
                Utils.SendRPC(CustomRPC.QuizSync, AllowKills);

                while (Utils.TimeStamp < FFAEndTS)
                {
                    yield return new WaitForSeconds(1f);
                    if (GameStates.IsMeeting || ExileController.Instance || !GameStates.InGame || GameStates.IsLobby) yield break;
                    Utils.NotifyRoles();
                    stillLiving.RemoveAll(x => x == null || !x.IsAlive());
                    if (stillLiving.Count <= 1) break;
                }

                spectators.Do(x => x.RpcRevive());
                AllowKills = false;
                Utils.SendRPC(CustomRPC.QuizSync, AllowKills);

                var location = RandomSpawn.SpawnMap.GetSpawnMap().Positions.IntersectBy(UsedRooms[Main.CurrentMap].Values, x => x.Key).RandomElement().Value;
                sender = CustomRpcSender.Create("Quiz.FFA-Event-TP", SendOption.Reliable);
                spectators.Do(x => sender.TP(x, location));
                sender.SendMessage();

                switch (stillLiving.Count)
                {
                    case 0:
                        break;
                    case 1:
                        stillLiving[0].RpcChangeRoleBasis(CustomRoles.QuizPlayer);
                        break;
                    default:
                        stillLiving.ForEach(x => x.Suicide());
                        break;
                }

                Main.AllPlayerSpeed.SetAllValues(Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod));
                Utils.MarkEveryoneDirtySettings();

                yield return new WaitForSeconds(3f);
                if (GameStates.IsMeeting || ExileController.Instance || !GameStates.InGame || GameStates.IsLobby) yield break;

                FFAEndTS = 0;
                yield return NewQuestion(increaseDifficulty: settings.Rounds.GetInt() <= Round, newRound: true);
                yield break;
        }
    }

    enum Difficulty
    {
        Test,
        Easy,
        Medium,
        Hard
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    private static class FixedUpdatePatch
    {
        private static long LastUpdate;

        [SuppressMessage("ReSharper", "UnusedMember.Local")]
        public static void Postfix()
        {
            if (!AmongUsClient.Instance.AmHost || !GameStates.IsInTask || !CustomGameMode.Quiz.IsActiveOrIntegrated() || !Main.IntroDestroyed) return;

            long now = Utils.TimeStamp;
            if (LastUpdate == now) return;
            LastUpdate = now;

            if (QuestionTimeLimitEndTS != 0)
            {
                if (QuestionTimeLimitEndTS <= now)
                {
                    Logger.Info("Question timer ended", "Quiz");

                    if (CurrentDifficulty == Difficulty.Test)
                    {
                        SystemTypes correctRoom = UsedRooms[Main.CurrentMap][(char)('A' + CurrentQuestion.CorrectAnswerIndex)];
                        DyingPlayers = Main.AllAlivePlayerControls.Select(x => (ID: x.PlayerId, Room: x.GetPlainShipRoom())).Where(x => correctRoom == SystemTypes.Outside ? x.Room != null : x.Room == null || x.Room.RoomId != correctRoom).Select(x => x.ID).ToList();
                        QuestionTimeLimitEndTS = 0;
                    }
                    else
                        Main.Instance.StartCoroutine(EndQuestion());
                }
                else
                    Utils.NotifyRoles();
            }
        }
    }
}