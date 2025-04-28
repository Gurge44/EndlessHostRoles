using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AmongUs.GameOptions;
using Assets.CoreScripts;
using EHR.AddOns.Common;
using EHR.Crewmate;
using EHR.Impostor;
using EHR.Modules;
using EHR.Neutral;
using HarmonyLib;
using Hazel;
using UnityEngine;
using static EHR.Translator;

// ReSharper disable InconsistentNaming


namespace EHR;

internal class Command(string[] commandForms, string arguments, string description, Command.UsageLevels usageLevel, Command.UsageTimes usageTime, Action<PlayerControl, string, string[]> action, bool isCanceled, bool alwaysHidden, string[] argsDescriptions = null)
{
    public enum UsageLevels
    {
        Everyone,
        Modded,
        Host,
        HostOrModerator
    }

    public enum UsageTimes
    {
        Always,
        InLobby,
        InGame,
        InMeeting,
        AfterDeath,
        AfterDeathOrLobby
    }

    public string[] CommandForms => commandForms;
    public string Arguments => arguments;
    public string Description => description;
    public string[] ArgsDescriptions => argsDescriptions ?? [];
    public UsageLevels UsageLevel => usageLevel;
    public UsageTimes UsageTime => usageTime;
    public Action<PlayerControl, string, string[]> Action => action;
    public bool IsCanceled => isCanceled;
    public bool AlwaysHidden => alwaysHidden;

    public bool IsThisCommand(string text)
    {
        if (!text.StartsWith('/')) return false;

        text = text.ToLower().Trim().TrimStart('/');
        return CommandForms.Any(text.Split(' ')[0].Equals);
    }

    public bool CanUseCommand(PlayerControl pc, bool checkTime = true, bool sendErrorMessage = false)
    {
        if (UsageLevel == UsageLevels.Everyone && UsageTime == UsageTimes.Always && !Lovers.PrivateChat.GetBool()) return true;

        if (Lovers.PrivateChat.GetBool() && GameStates.IsInTask && pc.IsAlive()) return false;

        switch (UsageLevel)
        {
            case UsageLevels.Host when !pc.IsHost():
            case UsageLevels.Modded when !pc.IsModdedClient():
            case UsageLevels.HostOrModerator when !pc.IsHost() && (AmongUsClient.Instance.AmHost && !ChatCommands.IsPlayerModerator(pc.FriendCode)):
                if (sendErrorMessage) Utils.SendMessage("\n", pc.PlayerId, GetString($"Commands.NoAccess.Level.{UsageLevel}"));
                return false;
        }

        if (!checkTime) return true;

        switch (UsageTime)
        {
            case UsageTimes.InLobby when !GameStates.IsLobby:
            case UsageTimes.InGame when !GameStates.InGame:
            case UsageTimes.InMeeting when !GameStates.IsMeeting:
            case UsageTimes.AfterDeath when pc.IsAlive():
            case UsageTimes.AfterDeathOrLobby when pc.IsAlive() && !GameStates.IsLobby:
                if (sendErrorMessage) Utils.SendMessage("\n", pc.PlayerId, GetString($"Commands.NoAccess.Time.{UsageTime}"));
                return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(ChatController), nameof(ChatController.SendChat))]
internal static class ChatCommands
{
    public static readonly List<string> ChatHistory = [];
    public static readonly Dictionary<byte, long> LastSentCommand = [];
    public static HashSet<Command> AllCommands = [];

    private static readonly Dictionary<char, int> PollVotes = [];
    private static readonly Dictionary<char, string> PollAnswers = [];
    private static readonly List<byte> PollVoted = [];
    private static float PollTimer = 45f;

    public static readonly Dictionary<byte, (long MuteTimeStamp, int Duration)> MutedPlayers = [];

    public static Dictionary<byte, List<CustomRoles>> DraftRoles = [];
    public static Dictionary<byte, CustomRoles> DraftResult = [];

    public static readonly HashSet<byte> Spectators = [];
    public static readonly HashSet<byte> LastSpectators = [];

    private static HashSet<byte> ReadyPlayers = [];

    private static string CurrentAnagram = string.Empty;

    public static void LoadCommands()
    {
        AllCommands =
        [
            new(["lt", "лт", "大厅关闭时间"], "", GetString("CommandDescription.LT"), Command.UsageLevels.Everyone, Command.UsageTimes.InLobby, LTCommand, false, false),
            new(["dump", "дамп", "лог", "导出日志"], "", GetString("CommandDescription.Dump"), Command.UsageLevels.Modded, Command.UsageTimes.Always, DumpCommand, false, false),
            new(["v", "version", "в", "версия", "检查版本", "versão"], "", GetString("CommandDescription.Version"), Command.UsageLevels.Modded, Command.UsageTimes.Always, VersionCommand, false, false),
            new(["cs", "changesetting", "измнастр", "修改设置", "mudarconfig", "mudarconfiguração"], "{name} {?} [?]", GetString("CommandDescription.ChangeSetting"), Command.UsageLevels.Host, Command.UsageTimes.InLobby, ChangeSettingCommand, true, false, [GetString("CommandArgs.ChangeSetting.Name"), GetString("CommandArgs.ChangeSetting.UnknownValue"), GetString("CommandArgs.ChangeSetting.UnknownValue")]),
            new(["win", "winner", "победители", "获胜者", "vencedor"], "", GetString("CommandDescription.Winner"), Command.UsageLevels.Everyone, Command.UsageTimes.InLobby, WinnerCommand, true, false),
            new(["l", "lastresult", "л", "对局职业信息", "resultados", "ultimoresultado"], "", GetString("CommandDescription.LastResult"), Command.UsageLevels.Everyone, Command.UsageTimes.InLobby, LastResultCommand, true, false),
            new(["rn", "rename", "рн", "ренейм", "переименовать", "修改名称", "renomear"], "{name}", GetString("CommandDescription.Rename"), Command.UsageLevels.Everyone, Command.UsageTimes.InLobby, RenameCommand, true, false, [GetString("CommandArgs.Rename.Name")]),
            new(["hn", "hidename", "хн", "спрник", "隐藏姓名", "semnome", "escondernome"], "", GetString("CommandDescription.HideName"), Command.UsageLevels.Host, Command.UsageTimes.InLobby, HideNameCommand, true, false),
            new(["level", "лвл", "уровень", "修改等级", "nível"], "{level}", GetString("CommandDescription.Level"), Command.UsageLevels.Host, Command.UsageTimes.InLobby, LevelCommand, true, false, [GetString("CommandArgs.Level.Level")]),
            new(["n", "now", "н", "当前设置", "atual"], "", GetString("CommandDescription.Now"), Command.UsageLevels.Everyone, Command.UsageTimes.Always, NowCommand, true, false),
            new(["dis", "disconnect", "дис", "断连"], "{team}", GetString("CommandDescription.Disconnect"), Command.UsageLevels.Host, Command.UsageTimes.InGame, DisconnectCommand, true, false, [GetString("CommandArgs.Disconnect.Team")]),
            new(["r", "р", "função"], "[role]", GetString("CommandDescription.R"), Command.UsageLevels.Everyone, Command.UsageTimes.Always, RCommand, true, false, [GetString("CommandArgs.R.Role")]),
            new(["up", "指定"], "{role}", GetString("CommandDescription.Up"), Command.UsageLevels.Host, Command.UsageTimes.InLobby, UpCommand, true, false, [GetString("CommandArgs.Up.Role")]),
            new(["setrole", "setaddon", "сетроль", "预设职业", "definir-função"], "{id} {role}", GetString("CommandDescription.SetRole"), Command.UsageLevels.Host, Command.UsageTimes.InLobby, SetRoleCommand, true, false, [GetString("CommandArgs.SetRole.Id"), GetString("CommandArgs.SetRole.Role")]),
            new(["h", "help", "хэлп", "хелп", "помощь", "帮助", "ajuda"], "", GetString("CommandDescription.Help"), Command.UsageLevels.Everyone, Command.UsageTimes.Always, HelpCommand, true, false),
            new(["gamestate", "gstate", "gs", "kcount", "kc", "кубийц", "гс", "статигры", "对局状态", "estadojogo", "status"], "", GetString("CommandDescription.KCount"), Command.UsageLevels.Everyone, Command.UsageTimes.InGame, KCountCommand, true, false),
            new(["addmod", "добмодера", "指定协管", "moderador-add"], "{id}", GetString("CommandDescription.AddMod"), Command.UsageLevels.Host, Command.UsageTimes.Always, AddModCommand, true, false, [GetString("CommandArgs.AddMod.Id")]),
            new(["deletemod", "убрмодера", "удмодера", "убратьмодера", "удалитьмодера", "移除协管", "moderador-remover"], "{id}", GetString("CommandDescription.DeleteMod"), Command.UsageLevels.Host, Command.UsageTimes.Always, DeleteModCommand, true, false, [GetString("CommandArgs.DeleteMod.Id")]),
            new(["combo", "комбо", "设置不会同时出现的职业", "combinação", "combinar"], "{mode} {role} {addon} [all]", GetString("CommandDescription.Combo"), Command.UsageLevels.Host, Command.UsageTimes.Always, ComboCommand, true, false, [GetString("CommandArgs.Combo.Mode"), GetString("CommandArgs.Combo.Role"), GetString("CommandArgs.Combo.Addon"), GetString("CommandArgs.Combo.All")]),
            new(["eff", "effect", "эффект", "效果", "efeito"], "{effect}", GetString("CommandDescription.Effect"), Command.UsageLevels.Host, Command.UsageTimes.InGame, EffectCommand, true, false, [GetString("CommandArgs.Effect.Effect")]),
            new(["afkexempt", "освафк", "афкосв", "挂机检测器不会检测", "afk-isentar"], "{id}", GetString("CommandDescription.AFKExempt"), Command.UsageLevels.Host, Command.UsageTimes.Always, AFKExemptCommand, true, false, [GetString("CommandArgs.AFKExempt.Id")]),
            new(["m", "myrole", "м", "мояроль", "我的职业", "minhafunção"], "", GetString("CommandDescription.MyRole"), Command.UsageLevels.Everyone, Command.UsageTimes.InGame, MyRoleCommand, true, false),
            new(["tpout", "тпаут", "传送出"], "", GetString("CommandDescription.TPOut"), Command.UsageLevels.Everyone, Command.UsageTimes.InLobby, TPOutCommand, true, false),
            new(["tpin", "тпин", "传送进"], "", GetString("CommandDescription.TPIn"), Command.UsageLevels.Everyone, Command.UsageTimes.InLobby, TPInCommand, true, false),
            new(["t", "template", "т", "темплейт", "模板"], "{tag}", GetString("CommandDescription.Template"), Command.UsageLevels.Everyone, Command.UsageTimes.Always, TemplateCommand, true, false, [GetString("CommandArgs.Template.Tag")]),
            new(["mw", "messagewait", "мв", "медленныйрежим", "消息冷却", "espera-mensagens"], "{duration}", GetString("CommandDescription.MessageWait"), Command.UsageLevels.Host, Command.UsageTimes.Always, MessageWaitCommand, true, false, [GetString("CommandArgs.MessageWait.Duration")]),
            new(["death", "d", "д", "смерть", "死亡原因", "abate"], "", GetString("CommandDescription.Death"), Command.UsageLevels.Everyone, Command.UsageTimes.AfterDeath, DeathCommand, true, false),
            new(["say", "s", "сказать", "с", "说", "falar", "dizer"], "{message}", GetString("CommandDescription.Say"), Command.UsageLevels.HostOrModerator, Command.UsageTimes.Always, SayCommand, true, true, [GetString("CommandArgs.Say.Message")]),
            new(["vote", "голос", "投票给", "votar"], "{id}", GetString("CommandDescription.Vote"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, VoteCommand, true, true, [GetString("CommandArgs.Vote.Id")]),
            new(["ask", "спр", "спросить", "数学家提问", "perguntar"], "{number1} {number2}", GetString("CommandDescription.Ask"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, AskCommand, true, true, [GetString("CommandArgs.Ask.Number1"), GetString("CommandArgs.Ask.Number2")]),
            new(["ans", "answer", "отв", "ответить", "回答数学家问题", "responder"], "{number}", GetString("CommandDescription.Answer"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, AnswerCommand, true, false, [GetString("CommandArgs.Answer.Number")]),
            new(["qa", "вопротв", "回答测验大师问题", "questão-responder"], "{letter}", GetString("CommandDescription.QA"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, QACommand, true, false, [GetString("CommandArgs.QA.Letter")]),
            new(["qs", "вопрпоказать", "检查测验大师问题", "questão-ver"], "", GetString("CommandDescription.QS"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, QSCommand, true, false),
            new(["target", "цель", "腹语者标记", "alvo"], "{id}", GetString("CommandDescription.Target"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, TargetCommand, true, true, [GetString("CommandArgs.Target.Id")]),
            new(["chat", "сообщение", "腹语者发送消息"], "{message}", GetString("CommandDescription.Chat"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, ChatCommand, true, true, [GetString("CommandArgs.Chat.Message")]),
            new(["check", "проверить", "检查", "veificar"], "{id} {role}", GetString("CommandDescription.Check"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, CheckCommand, true, true, [GetString("CommandArgs.Check.Id"), GetString("CommandArgs.Check.Role")]),
            new(["ban", "kick", "бан", "кик", "забанить", "кикнуть", "封禁", "踢出", "banir", "expulsar"], "{id}", GetString("CommandDescription.Ban"), Command.UsageLevels.HostOrModerator, Command.UsageTimes.Always, BanKickCommand, true, false, [GetString("CommandArgs.Ban.Id")]),
            new(["exe", "выкинуть", "驱逐", "executar"], "{id}", GetString("CommandDescription.Exe"), Command.UsageLevels.Host, Command.UsageTimes.Always, ExeCommand, true, false, [GetString("CommandArgs.Exe.Id")]),
            new(["kill", "убить", "击杀", "matar"], "{id}", GetString("CommandDescription.Kill"), Command.UsageLevels.Host, Command.UsageTimes.Always, KillCommand, true, false, [GetString("CommandArgs.Kill.Id")]),
            new(["colour", "color", "цвет", "更改颜色", "cor"], "{color}", GetString("CommandDescription.Colour"), Command.UsageLevels.Everyone, Command.UsageTimes.InLobby, ColorCommand, true, false, [GetString("CommandArgs.Colour.Color")]),
            new(["id", "guesslist", "айди", "ID列表"], "", GetString("CommandDescription.ID"), Command.UsageLevels.Everyone, Command.UsageTimes.Always, IDCommand, true, false),
            new(["changerole", "измроль", "修改职业", "mudar-função"], "{role}", GetString("CommandDescription.ChangeRole"), Command.UsageLevels.Host, Command.UsageTimes.InGame, ChangeRoleCommand, true, false, [GetString("CommandArgs.ChangeRole.Role")]),
            new(["end", "завершить", "结束游戏", "encerrar", "finalizar", "fim"], "", GetString("CommandDescription.End"), Command.UsageLevels.Host, Command.UsageTimes.InGame, EndCommand, true, false),
            new(["cosid", "костюм", "одежда", "服装ID"], "", GetString("CommandDescription.CosID"), Command.UsageLevels.Modded, Command.UsageTimes.Always, CosIDCommand, true, false),
            new(["mt", "hy", "собрание", "开会/结束会议"], "", GetString("CommandDescription.MTHY"), Command.UsageLevels.Host, Command.UsageTimes.InGame, MTHYCommand, true, false),
            new(["csd", "кзвук", "自定义播放声音"], "{sound}", GetString("CommandDescription.CSD"), Command.UsageLevels.Modded, Command.UsageTimes.Always, CSDCommand, true, false, [GetString("CommandArgs.CSD.Sound")]),
            new(["sd", "взвук", "游戏中播放声音"], "{sound}", GetString("CommandDescription.SD"), Command.UsageLevels.Modded, Command.UsageTimes.Always, SDCommand, true, false, [GetString("CommandArgs.SD.Sound")]),
            new(["gno", "гно", "猜数字"], "{number}", GetString("CommandDescription.GNO"), Command.UsageLevels.Everyone, Command.UsageTimes.AfterDeathOrLobby, GNOCommand, true, false, [GetString("CommandArgs.GNO.Number")]),
            new(["poll", "опрос", "发起调查", "enquete"], "{question} {answerA} {answerB} [answerC] [answerD]", GetString("CommandDescription.Poll"), Command.UsageLevels.HostOrModerator, Command.UsageTimes.Always, PollCommand, true, false, [GetString("CommandArgs.Poll.Question"), GetString("CommandArgs.Poll.AnswerA"), GetString("CommandArgs.Poll.AnswerB"), GetString("CommandArgs.Poll.AnswerC"), GetString("CommandArgs.Poll.AnswerD")]),
            new(["pv", "проголосовать", "选择调查选项"], "{vote}", GetString("CommandDescription.PV"), Command.UsageLevels.Everyone, Command.UsageTimes.Always, PVCommand, false, false, [GetString("CommandArgs.PV.Vote")]),
            new(["hm", "мс", "мессенджер", "送信"], "{id}", GetString("CommandDescription.HM"), Command.UsageLevels.Everyone, Command.UsageTimes.AfterDeath, HMCommand, true, false, [GetString("CommandArgs.HM.Id")]),
            new(["decree", "указ", "总统命令", "decretar"], "{number}", GetString("CommandDescription.Decree"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, DecreeCommand, true, true, [GetString("CommandArgs.Decree.Number")]),
            new(["addvip", "добавитьвип", "добвип", "指定会员", "vip-add"], "{id}", GetString("CommandDescription.AddVIP"), Command.UsageLevels.Host, Command.UsageTimes.Always, AddVIPCommand, true, false, [GetString("CommandArgs.AddVIP.Id")]),
            new(["deletevip", "удвип", "убрвип", "удалитьвип", "убратьвип", "删除会员", "vip-remover"], "{id}", GetString("CommandDescription.DeleteVIP"), Command.UsageLevels.Host, Command.UsageTimes.Always, DeleteVIPCommand, true, false, [GetString("CommandArgs.DeleteVIP.Id")]),
            new(["assume", "предположить", "传销头目预测投票", "assumir"], "{id} {number}", GetString("CommandDescription.Assume"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, AssumeCommand, true, true, [GetString("CommandArgs.Assume.Id"), GetString("CommandArgs.Assume.Number")]),
            new(["note", "заметка", "记者管理笔记", "nota", "anotar"], "{action} [?]", GetString("CommandDescription.Note"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, NoteCommand, true, true, [GetString("CommandArgs.Note.Action"), GetString("CommandArgs.Note.UnknownValue")]),
            new(["os", "optionset", "шансроли", "设置职业生成概率"], "{chance} {role}", GetString("CommandDescription.OS"), Command.UsageLevels.Host, Command.UsageTimes.InLobby, OSCommand, true, false, [GetString("CommandArgs.OS.Chance"), GetString("CommandArgs.OS.Role")]),
            new(["negotiation", "neg", "наказание", "谈判方式", "negociar", "negociação"], "{number}", GetString("CommandDescription.Negotiation"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, NegotiationCommand, true, false, [GetString("CommandArgs.Negotiation.Number")]),
            new(["mute", "мут", "禁言", "mutar", "silenciar"], "{id} [duration]", GetString("CommandDescription.Mute"), Command.UsageLevels.HostOrModerator, Command.UsageTimes.AfterDeathOrLobby, MuteCommand, true, false, [GetString("CommandArgs.Mute.Id"), GetString("CommandArgs.Mute.Duration")]),
            new(["unmute", "размут", "解禁", "desmutar", "desilenciar"], "{id}", GetString("CommandDescription.Unmute"), Command.UsageLevels.Host, Command.UsageTimes.Always, UnmuteCommand, true, false, [GetString("CommandArgs.Unmute.Id")]),
            new(["draftstart", "ds", "драфтстарт", "启用草稿", "todosescolhem-iniciar"], "", GetString("CommandDescription.DraftStart"), Command.UsageLevels.Host, Command.UsageTimes.InLobby, DraftStartCommand, true, false),
            new(["dd", "draftdesc", "draftdescription", "драфтописание", "草稿描述", "todosescolhem-descricao"], "{index}", GetString("CommandDescription.DraftDescription"), Command.UsageLevels.Everyone, Command.UsageTimes.InLobby, DraftDescriptionCommand, false, false, [GetString("CommandArgs.DraftDescription.Index")]),
            new(["draft", "драфт", "选择草稿", "todosescolhem-escolher"], "{number}", GetString("CommandDescription.Draft"), Command.UsageLevels.Everyone, Command.UsageTimes.InLobby, DraftCommand, false, false, [GetString("CommandArgs.Draft.Number")]),
            new(["rc", "readycheck", "проверитьготовность", "准备检测", "verificação-de-prontidão"], "", GetString("CommandDescription.ReadyCheck"), Command.UsageLevels.Host, Command.UsageTimes.InLobby, ReadyCheckCommand, true, false),
            new(["ready", "готов", "准备", "pronto"], "", GetString("CommandDescription.Ready"), Command.UsageLevels.Everyone, Command.UsageTimes.InLobby, ReadyCommand, true, false),
            new(["enableallroles", "всероли", "启用所有职业", "habilitar-todas-as-funções"], "", GetString("CommandDescription.EnableAllRoles"), Command.UsageLevels.Host, Command.UsageTimes.InLobby, EnableAllRolesCommand, true, false),
            new(["achievements", "достижения", "成就", "conquistas"], "", GetString("CommandDescription.Achievements"), Command.UsageLevels.Modded, Command.UsageTimes.Always, AchievementsCommand, true, false),
            new(["dn", "deathnote", "заметкамертвого", "死亡笔记"], "{name}", GetString("CommandDescription.DeathNote"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, DeathNoteCommand, true, true, [GetString("CommandArgs.DeathNote.Name")]),
            new(["w", "whisper", "шепот", "ш", "私聊", "sussurrar"], "{id} {message}", GetString("CommandDescription.Whisper"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, WhisperCommand, true, true, [GetString("CommandArgs.Whisper.Id"), GetString("CommandArgs.Whisper.Message")]),
            new(["spectate", "спектейт", "观战", "espectar"], "", GetString("CommandDescription.Spectate"), Command.UsageLevels.Everyone, Command.UsageTimes.InLobby, SpectateCommand, false, false),
            new(["anagram", "анаграмма"], "", GetString("CommandDescription.Anagram"), Command.UsageLevels.Everyone, Command.UsageTimes.Always, AnagramCommand, true, false),
            new(["rl", "rolelist", "роли"], "", GetString("CommandDescription.RoleList"), Command.UsageLevels.Everyone, Command.UsageTimes.Always, RoleListCommand, true, false),
            new(["jt", "jailtalk", "тюремныйразговор", "监狱谈话"], "{message}", GetString("CommandDescription.JailTalk"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, JailTalkCommand, true, true, [GetString("CommandArgs.JailTalk.Message")]),
            new(["gm", "gml", "gamemodes", "gamemodelist", "режимы", "模式列表"], "", GetString("CommandDescription.GameModeList"), Command.UsageLevels.Everyone, Command.UsageTimes.Always, GameModeListCommand, true, false),
            new(["gmp", "gmpoll", "pollgm", "gamemodepoll", "режимголосование", "模式投票"], "", GetString("CommandDescription.GameModePoll"), Command.UsageLevels.HostOrModerator, Command.UsageTimes.InLobby, GameModePollCommand, true, false),
            new(["8ball", "шар", "八球"], "[question]", GetString("CommandDescription.EightBall"), Command.UsageLevels.Everyone, Command.UsageTimes.Always, EightBallCommand, false, false, [GetString("CommandArgs.EightBall.Question")]),
            new(["addtag", "добавитьтег", "添加标签", "adicionartag"], "{id} {color} {tag}", GetString("CommandDescription.AddTag"), Command.UsageLevels.Host, Command.UsageTimes.InLobby, AddTagCommand, true, false, [GetString("CommandArgs.AddTag.Id"), GetString("CommandArgs.AddTag.Color"), GetString("CommandArgs.AddTag.Tag")]),
            new(["deletetag", "удалитьтег", "删除标签"], "{id}", GetString("CommandDescription.DeleteTag"), Command.UsageLevels.Host, Command.UsageTimes.InLobby, DeleteTagCommand, true, false, [GetString("CommandArgs.DeleteTag.Id")]),
            new(["daybreak", "db", "дейбрейк", "破晓"], "", GetString("CommandDescription.DayBreak"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, DayBreakCommand, true, true),
            new(["fix", "blackscreenfix", "fixblackscreen", "ф", "исправить", "修复"], "{id}", GetString("CommandDescription.Fix"), Command.UsageLevels.HostOrModerator, Command.UsageTimes.InGame, FixCommand, true, false, [GetString("CommandArgs.Fix.Id")]),
            new(["xor", "异或命令"], "{role} {role}", GetString("CommandDescription.XOR"), Command.UsageLevels.Host, Command.UsageTimes.InLobby, XORCommand, true, false, [GetString("CommandArgs.XOR.Role"), GetString("CommandArgs.XOR.Role")]),
            new(["ci", "chemistinfo", "химик", "化学家"], "", GetString("CommandDescription.ChemistInfo"), Command.UsageLevels.Everyone, Command.UsageTimes.Always, ChemistInfoCommand, true, false),
            new(["forge"], "{id} {role}", GetString("CommandDescription.Forge"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, ForgeCommand, true, true, [GetString("CommandArgs.Forge.Id"), GetString("CommandArgs.Forge.Role")]),
            new(["choose", "pick", "выбрать", "选择", "escolher"], "{role}", GetString("CommandDescription.Choose"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, ChooseCommand, true, true, [GetString("CommandArgs.Choose.Role")]),
            
            // Commands with action handled elsewhere
            new(["shoot", "guess", "bet", "bt", "st", "угадать", "бт", "猜测", "赌", "adivinhar"], "{id} {role}", GetString("CommandDescription.Guess"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, (_, _, _) => { }, true, false, [GetString("CommandArgs.Guess.Id"), GetString("CommandArgs.Guess.Role")]),
            new(["tl", "sp", "jj", "trial", "суд", "засудить", "审判", "判", "julgar"], "{id}", GetString("CommandDescription.Trial"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, (_, _, _) => { }, true, false, [GetString("CommandArgs.Trial.Id")]),
            new(["sw", "swap", "st", "свап", "свапнуть", "换票", "trocar"], "{id}", GetString("CommandDescription.Swap"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, (_, _, _) => { }, true, false, [GetString("CommandArgs.Swap.Id")]),
            new(["compare", "cp", "cmp", "сравнить", "ср", "检查", "comparar"], "{id1} {id2}", GetString("CommandDescription.Compare"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, (_, _, _) => { }, true, false, [GetString("CommandArgs.Compare.Id1"), GetString("CommandArgs.Compare.Id2")]),
            new(["ms", "mediumship", "medium", "медиум", "回答"], "{answer}", GetString("CommandDescription.Medium"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, (_, _, _) => { }, true, false, [GetString("CommandArgs.Medium.Answer")]),
            new(["rv", "месть", "отомстить", "复仇"], "{id}", GetString("CommandDescription.Revenge"), Command.UsageLevels.Everyone, Command.UsageTimes.AfterDeath, (_, _, _) => { }, true, false, [GetString("CommandArgs.Revenge.Id")])
        ];
    }

    // Function to check if a Player is Moderator
    public static bool IsPlayerModerator(string friendCode)
    {
        friendCode = friendCode.Replace(':', '#');

        if (friendCode == "" || friendCode == string.Empty || !Options.ApplyModeratorList.GetBool()) return false;

        const string friendCodesFilePath = "./EHR_DATA/Moderators.txt";

        if (!File.Exists(friendCodesFilePath))
        {
            File.WriteAllText(friendCodesFilePath, string.Empty);
            return false;
        }

        string[] friendCodes = File.ReadAllLines(friendCodesFilePath);
        return friendCodes.Any(code => code.Contains(friendCode, StringComparison.OrdinalIgnoreCase));
    }

    // Function to check if a player is a VIP
    public static bool IsPlayerVIP(string friendCode)
    {
        friendCode = friendCode.Replace(':', '#');

        if (friendCode == "" || friendCode == string.Empty || !Options.ApplyVIPList.GetBool()) return false;

        const string friendCodesFilePath = "./EHR_DATA/VIPs.txt";

        if (!File.Exists(friendCodesFilePath))
        {
            File.WriteAllText(friendCodesFilePath, string.Empty);
            return false;
        }

        string[] friendCodes = File.ReadAllLines(friendCodesFilePath);
        return friendCodes.Any(code => code.Contains(friendCode, StringComparison.OrdinalIgnoreCase));
    }

    public static bool Prefix(ChatController __instance)
    {
        if (__instance.quickChatField.visible) return true;
        if (__instance.freeChatField.textArea.text == string.Empty) return false;
        __instance.timeSinceLastMessage = 3f;

        string text = __instance.freeChatField.textArea.text.Trim();
        var cancelVal = string.Empty;

        if (GameStates.InGame && (Silencer.ForSilencer.Contains(PlayerControl.LocalPlayer.PlayerId) || (Main.PlayerStates[PlayerControl.LocalPlayer.PlayerId].Role is Dad { IsEnable: true } dad && dad.UsingAbilities.Contains(Dad.Ability.GoForMilk))) && PlayerControl.LocalPlayer.IsAlive()) goto Canceled;

        CheckAnagramGuess(PlayerControl.LocalPlayer.PlayerId, text);

        if (ChatHistory.Count == 0 || ChatHistory[^1] != text)
            ChatHistory.Add(text);

        ChatControllerUpdatePatch.CurrentHistorySelection = ChatHistory.Count;

        string[] args = text.Split(' ');
        var canceled = false;
        Main.IsChatCommand = true;

        Logger.Info(text, "SendChat");

        if (!Starspawn.IsDayBreak)
        {
            if (GuessManager.GuesserMsg(PlayerControl.LocalPlayer, text)) goto Canceled;
            if (Judge.TrialMsg(PlayerControl.LocalPlayer, text)) goto Canceled;
            if (NiceSwapper.SwapMsg(PlayerControl.LocalPlayer, text)) goto Canceled;
            if (ParityCop.ParityCheckMsg(PlayerControl.LocalPlayer, text)) goto Canceled;
            if (Councillor.MurderMsg(PlayerControl.LocalPlayer, text)) goto Canceled;
            if (Mediumshiper.MsMsg(PlayerControl.LocalPlayer, text)) goto Canceled;
            if (Mafia.MafiaMsgCheck(PlayerControl.LocalPlayer, text)) goto Canceled;
        }

        Main.IsChatCommand = false;

        if (text.StartsWith('/'))
        {
            foreach (Command command in AllCommands)
            {
                if (!command.IsThisCommand(text)) continue;

                Logger.Info($" Recognized command: {text}", "ChatCommand");
                Main.IsChatCommand = true;

                if (!command.CanUseCommand(PlayerControl.LocalPlayer, sendErrorMessage: true))
                    goto Canceled;

                command.Action(PlayerControl.LocalPlayer, text, args);
                if (command.IsCanceled) goto Canceled;

                break;
            }

            Statistics.HasUsedAnyCommand = true;
        }

        if (CheckMute(PlayerControl.LocalPlayer.PlayerId)) goto Canceled;

        if (GameStates.IsInGame && (PlayerControl.LocalPlayer.IsAlive() || ExileController.Instance) && Lovers.PrivateChat.GetBool() && (ExileController.Instance || !GameStates.IsMeeting) && Options.CurrentGameMode == CustomGameMode.Standard)
        {
            if (PlayerControl.LocalPlayer.Is(CustomRoles.Lovers) || PlayerControl.LocalPlayer.GetCustomRole() is CustomRoles.LovingCrewmate or CustomRoles.LovingImpostor)
            {
                PlayerControl otherLover = Main.LoversPlayers.First(x => x.PlayerId != PlayerControl.LocalPlayer.PlayerId);
                string title = PlayerControl.LocalPlayer.GetRealName();
                ChatUpdatePatch.LoversMessage = true;
                var sender = CustomRpcSender.Create("LoversMessage", SendOption.Reliable);
                sender = Utils.SendMessage(text, otherLover.PlayerId, title, writer: sender, multiple: true);
                sender = Utils.SendMessage(text, PlayerControl.LocalPlayer.PlayerId, title, writer: sender, multiple: true);
                sender.Notify(otherLover, $"<size=80%><{Main.RoleColors[CustomRoles.Lovers]}>[\u2665]</color> {text}</size>", 8f);
                sender.SendMessage();
                LateTask.New(() => ChatUpdatePatch.LoversMessage = false, Math.Max(AmongUsClient.Instance.Ping / 1000f * 2f, Main.MessageWait.Value + 0.5f), log: false);
            }

            goto Canceled;
        }

        goto Skip;
        Canceled:
        Main.IsChatCommand = false;
        canceled = true;
        Skip:

        if (canceled)
        {
            Logger.Info("Command Canceled", "ChatCommand");
            __instance.freeChatField.textArea.Clear();
            __instance.freeChatField.textArea.SetText(cancelVal);
        }
        else
        {
            if (GameStates.IsLobby && AmongUsClient.Instance.AmHost)
            {
                if (!Main.AllPlayerNames.TryGetValue(PlayerControl.LocalPlayer.PlayerId, out string name))
                    Utils.ApplySuffix(PlayerControl.LocalPlayer, out name);

                Utils.SendMessage(text.Insert(0, new('\n', name.Count(x => x == '\n'))), title: name, addtoHistory: false);

                canceled = true;
                __instance.freeChatField.textArea.Clear();
                __instance.freeChatField.textArea.SetText(string.Empty);
            }

            ChatManager.SendMessage(PlayerControl.LocalPlayer, text);
        }

        if (text.Contains("666") && PlayerControl.LocalPlayer.Is(CustomRoles.Gamer))
            Achievements.Type.WhatTheHell.Complete();

        return !canceled;
    }

    private static void CheckAnagramGuess(byte id, string text)
    {
        if (CurrentAnagram != string.Empty && text.Contains(CurrentAnagram))
        {
            Utils.SendMessage("\n", title: string.Format(GetString("Anagram.CorrectGuess"), id.ColoredPlayerName(), CurrentAnagram));
            CurrentAnagram = string.Empty;
        }
    }

    private static void RequestCommandProcessingFromHost(string methodName, string text, bool modCommand = false)
    {
        PlayerControl pc = PlayerControl.LocalPlayer;
        MessageWriter w = AmongUsClient.Instance.StartRpc(pc.NetId, (byte)CustomRPC.RequestCommandProcessing);
        w.Write(methodName);
        w.Write(pc.PlayerId);
        w.Write(text);
        w.Write(modCommand);
        w.EndMessage();
    }

    // ---------------------------------------------------------------------------------------------------------------------------------------------

    private static void ChooseCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(ChooseCommand), text);
            return;
        }
        
        if (!player.IsAlive() || !player.Is(CustomRoles.Pawn) || !player.AllTasksCompleted()) return;
        if (args.Length < 2 || !GetRoleByName(string.Join(' ', args[1..]), out var role) || role.GetMode() == 0) return;
        
        if (!player.IsLocalPlayer()) ChatManager.SendPreviousMessagesToAll();
        
        player.RpcSetCustomRole(role);
        player.RpcChangeRoleBasis(role);
    }
    
    private static void ForgeCommand(PlayerControl player, string text, string[] args)
    {
        if (Starspawn.IsDayBreak) return;

        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(ForgeCommand), text);
            return;
        }

        if (!player.IsAlive() || !player.Is(CustomRoles.Forger) || player.GetAbilityUseLimit() < 1) return;
        if (args.Length < 3 || !GuessManager.MsgToPlayerAndRole(text[6..], out byte targetId, out CustomRoles forgeRole, out _)) return;

        if (!player.IsLocalPlayer()) ChatManager.SendPreviousMessagesToAll();
        
        Forger.Forges[targetId] = forgeRole;

        player.RpcRemoveAbilityUse();
    }

    private static void ChemistInfoCommand(PlayerControl player, string text, string[] args)
    {
        Utils.SendMessage(Chemist.GetProcessesInfo(), player.PlayerId, CustomRoles.Chemist.ToColoredString());
    }

    private static void XORCommand(PlayerControl player, string text, string[] args)
    {
        if (args.Length < 3 || !GetRoleByName(args[1], out CustomRoles role1) || !GetRoleByName(args[2], out CustomRoles role2)) return;

        if (Main.XORRoles.Remove((role1, role2)) || Main.XORRoles.Remove((role2, role1)))
        {
            Utils.SendMessage("\n", player.PlayerId, string.Format(GetString("XORRemoved"), role1.ToColoredString(), role2.ToColoredString()));
            return;
        }

        if (role1 == role2)
        {
            Utils.SendMessage("\n", player.PlayerId, GetString("XORSameRole"));
            return;
        }

        if (role1.IsAdditionRole() || role2.IsAdditionRole())
        {
            Utils.SendMessage("\n", player.PlayerId, GetString("XORAdditionRole"));
            return;
        }

        Main.XORRoles.Add((role1, role2));
        Utils.SendMessage("\n", player.PlayerId, string.Format(GetString("XORAdded"), role1.ToColoredString(), role2.ToColoredString()));
    }

    private static void FixCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(FixCommand), text, true);
            return;
        }

        if (args.Length < 2 || !byte.TryParse(args[1], out byte id)) return;

        var pc = id.GetPlayer();
        if (pc == null) return;

        pc.FixBlackScreen();

        if (Main.AllPlayerControls.All(x => x.IsAlive()))
            Logger.SendInGame(GetString("FixBlackScreenWaitForDead"));
    }

    private static void DayBreakCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(DayBreakCommand), text);
            return;
        }

        if (!player.IsAlive() || Main.PlayerStates[player.PlayerId].Role is not Starspawn sp || sp.HasUsedDayBreak) return;

        if (!player.IsLocalPlayer()) ChatManager.SendPreviousMessagesToAll();

        Starspawn.IsDayBreak = true;
        sp.HasUsedDayBreak = true;

        Utils.SendMessage("\n", title: string.Format(GetString("StarspawnUsedDayBreak"), CustomRoles.Starspawn.ToColoredString()));
    }

    private static void AddTagCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(AddTagCommand), text);
            return;
        }

        if (args.Length < 4 || !byte.TryParse(args[1], out byte id)) return;

        PlayerControl pc = id.GetPlayer();
        if (pc == null) return;

        Color color = ColorUtility.TryParseHtmlString($"#{args[2].ToLower()}", out Color c) ? c : Color.red;
        string tag = Utils.ColorString(color, string.Join(' ', args[3..]));
        PrivateTagManager.AddTag(pc.FriendCode, tag);

        Utils.SendMessage("\n", player.PlayerId, string.Format(GetString("AddTagSuccess"), tag, id.ColoredPlayerName(), id));
    }

    private static void DeleteTagCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(DeleteTagCommand), text);
            return;
        }

        if (args.Length < 2 || !byte.TryParse(args[1], out byte id)) return;

        PlayerControl pc = id.GetPlayer();
        if (pc == null) return;

        PrivateTagManager.DeleteTag(pc.FriendCode);
        Utils.SendMessage("\n", player.PlayerId, string.Format(GetString("DeleteTagSuccess"), id.ColoredPlayerName()));
    }

    private static void EightBallCommand(PlayerControl player, string text, string[] args)
    {
        if (Options.Disable8ballCommand.GetBool())
        {
            Utils.SendMessage("\n", player.PlayerId, GetString("EightBallDisabled"), sendOption: SendOption.None);
            return;
        }

        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(EightBallCommand), text);
            return;
        }

        Utils.SendMessage(GetString($"8BallResponse.{IRandom.Instance.Next(20)}"), player.IsAlive() ? byte.MaxValue : player.PlayerId, GetString("8BallResponseTitle"));
    }

    private static void GameModePollCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(GameModePollCommand), text, true);
            return;
        }

        string gmNames = string.Join(' ', Enum.GetNames<CustomGameMode>().SkipLast(1).Select(x => GetString(x).Replace(' ', '_')));
        var msg = $"/poll {GetString("GameModePoll.Question").TrimEnd('?')}? {GetString("GameModePoll.KeepCurrent").Replace(' ', '_')} {gmNames}";
        PollCommand(player, msg, msg.Split(' '));
    }

    private static void GameModeListCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(GameModeListCommand), text);
            return;
        }

        string info = string.Join("\n\n", Enum.GetValues<CustomGameMode>()[1..].SkipLast(1)
            .Select(x => (GameMode: x, Color: Main.RoleColors.GetValueOrDefault(CustomRoleSelector.GameModeRoles.TryGetValue(x, out CustomRoles role) ? role : x == CustomGameMode.HideAndSeek ? CustomRoles.Hider : CustomRoles.Witness, "#000000")))
            .Select(x => $"<{x.Color}><u><b>{GetString($"{x.GameMode}")}</b></u></color><size=75%>\n{GetString($"ModeDescribe.{x.GameMode}").Split("\n\n")[0]}</size>"));

        Utils.SendMessage(info, player.PlayerId, GetString("GameModeListTitle"));
    }

    private static void JailTalkCommand(PlayerControl player, string text, string[] args)
    {
        if (args.Length < 2) return;

        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(JailTalkCommand), text);
            return;
        }

        Jailor jailor = Main.PlayerStates[player.PlayerId].Role as Jailor ?? Main.PlayerStates.Select(x => x.Value.Role as Jailor).FirstOrDefault(x => x != null);
        if (jailor == null) return;

        bool amJailor = Jailor.PlayerIdList.Contains(player.PlayerId);
        bool amJailed = player.PlayerId == jailor.JailorTarget;
        if (!amJailor && !amJailed) return;

        if (!player.IsLocalPlayer()) ChatManager.SendPreviousMessagesToAll();

        string title = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jailor), GetString("JailTalkTitle"));

        string message = string.Join(' ', args[1..]);

        if (amJailor) Utils.SendMessage(message, jailor.JailorTarget, title);
        else Jailor.PlayerIdList.ForEach(x => Utils.SendMessage(message, x, title, sendOption: SendOption.None));
    }

    private static void RoleListCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(RoleListCommand), text);
            return;
        }

        StringBuilder sb = new("<size=70%>");

        Dictionary<Team, RoleOptionType[]> rot = Enum.GetValues<RoleOptionType>()
            .Without(RoleOptionType.Coven_Miscellaneous)
            .GroupBy(x => x.ToString().Split('_')[0])
            .ToDictionary(x => Enum.Parse<Team>(x.Key), x => x.ToArray());

        foreach (Team team in Enum.GetValues<Team>()[1..])
        {
            sb.Append("<u>");
            sb.Append(Utils.ColorString(team.GetColor(), GetString(team.ToString()).ToUpper()));
            sb.Append("</u>");

            int factionMin;
            int factionMax;

            if (Options.FactionMinMaxSettings.TryGetValue(team, out (OptionItem MinSetting, OptionItem MaxSetting) factionLimits))
            {
                factionMin = factionLimits.MinSetting.GetInt();
                factionMax = factionLimits.MaxSetting.GetInt();
            }
            else
            {
                factionMin = Math.Max(0, Main.NormalOptions.MaxPlayers - Options.FactionMinMaxSettings[Team.Neutral].MaxSetting.GetInt() - Options.FactionMinMaxSettings[Team.Impostor].MaxSetting.GetInt());
                factionMax = Math.Max(0, Main.NormalOptions.MaxPlayers - Options.FactionMinMaxSettings[Team.Neutral].MinSetting.GetInt() - Options.FactionMinMaxSettings[Team.Impostor].MinSetting.GetInt());
            }

            sb.Append(' ');
            sb.Append(factionMin);
            sb.Append(" - ");
            sb.Append(factionMax);
            sb.Append("\n\n");

            if (team == Team.Neutral)
            {
                sb.Append(Options.MinNNKs.GetInt());
                sb.Append('-');
                sb.Append(Options.MaxNNKs.GetInt());
                sb.Append(' ');
                sb.Append(GetString("NeutralNonKillingRoles"));
                sb.Append("\n\n");
            }

            if (rot.TryGetValue(team, out RoleOptionType[] subCategories))
            {
                foreach (RoleOptionType subCategory in subCategories)
                {
                    if (Options.RoleSubCategoryLimits.TryGetValue(subCategory, out OptionItem[] limits) && (team == Team.Neutral || limits[0].GetBool()))
                    {
                        int min = limits[1].GetInt();
                        int max = limits[2].GetInt();

                        factionMin -= max;
                        factionMax -= min;

                        sb.Append(min);
                        sb.Append('-');
                        sb.Append(max);
                        sb.Append(' ');
                        sb.Append(Utils.ColorString(subCategory.GetRoleOptionTypeColor(), GetString($"ROT.{subCategory}")[2..]));
                        sb.Append('\n');
                    }
                }

                if (team != Team.Neutral && factionMax > 0)
                {
                    sb.Append(Math.Max(0, factionMin));
                    sb.Append('-');
                    sb.Append(factionMax);
                    sb.Append(' ');
                    sb.Append(GetString("RoleRateNoColor"));
                    sb.Append(' ');
                    sb.Append(GetString("Roles"));
                    sb.Append('\n');
                }

                sb.Append("\n\n");
            }
        }

        Utils.SendMessage("\n", player.PlayerId, sb.ToString().Trim() + "</size>");
    }

    private static void AnagramCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(AnagramCommand), text);
            return;
        }

        Main.Instance.StartCoroutine(Main.GetRandomWord(CreateAnagram));
        return;

        void CreateAnagram(string word)
        {
            string scrambled = new(word.ToLower().ToCharArray().Shuffle());
            CurrentAnagram = word;
            byte sendTo = GameStates.InGame && !player.IsAlive() ? player.PlayerId : byte.MaxValue;
            Utils.SendMessage(string.Format(GetString("Anagram"), scrambled, word[0]), sendTo, GetString("AnagramTitle"));
        }
    }

    private static void SpectateCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(SpectateCommand), text);
            return;
        }

        if (Options.DisableSpectateCommand.GetBool())
        {
            Utils.SendMessage("\n", player.PlayerId, GetString("SpectateDisabled"), sendOption: SendOption.None);
            return;
        }

        if (LastSpectators.Contains(player.PlayerId))
        {
            Utils.SendMessage("\n", player.PlayerId, GetString("SpectateCommand.WasSpectatingLastRound"));
            return;
        }

        if (Spectators.Remove(player.PlayerId))
        {
            Utils.SendMessage("\n", player.PlayerId, GetString("SpectateCommand.Removed"));
            return;
        }

        if (Spectators.Add(player.PlayerId))
            Utils.SendMessage("\n", player.PlayerId, GetString("SpectateCommand.Success"));
    }

    private static void WhisperCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(WhisperCommand), text);
            return;
        }

        if (!player.IsAlive() || Silencer.ForSilencer.Contains(player.PlayerId)) return;

        if (Options.DisableWhisperCommand.GetBool())
        {
            Utils.SendMessage("\n", player.PlayerId, GetString("WhisperDisabled"), sendOption: SendOption.None);
            return;
        }

        if (Magistrate.CallCourtNextMeeting)
        {
            Utils.SendMessage("\n", player.PlayerId, GetString("NoWhisperDuringCourt"), sendOption: SendOption.None);
            return;
        }

        if (args.Length < 3 || !byte.TryParse(args[1], out byte targetId)) return;
        if (!player.IsLocalPlayer()) ChatManager.SendPreviousMessagesToAll();

        PlayerState state = Main.PlayerStates[targetId];
        if (state.IsDead || state.SubRoles.Contains(CustomRoles.Shy)) return;

        string msg = args[2..].Join(delimiter: " ");
        string title = string.Format(GetString("WhisperTitle"), player.PlayerId.ColoredPlayerName(), player.PlayerId);

        Utils.SendMessage(msg, targetId, title);
        ChatUpdatePatch.LastMessages.Add((msg, targetId, title, Utils.TimeStamp));
    }

    private static void DeathNoteCommand(PlayerControl player, string text, string[] args)
    {
        if (Starspawn.IsDayBreak) return;

        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(DeathNoteCommand), text);
            return;
        }

        if (!player.Is(CustomRoles.NoteKiller) || args.Length < 2) return;

        if (!player.IsLocalPlayer()) ChatManager.SendPreviousMessagesToAll();

        if (!NoteKiller.CanGuess)
        {
            Utils.SendMessage(GetString("DeathNoteCommand.CanNotGuess"), player.PlayerId, sendOption: SendOption.None);
            return;
        }

        string guess = args[1].ToLower();
        guess = char.ToUpper(guess[0]) + guess[1..];
        byte deadPlayer = NoteKiller.RealNames.GetKeyByValue(guess);

        if (deadPlayer == 0 && (!NoteKiller.RealNames.TryGetValue(0, out string name) || name != guess))
        {
            NoteKiller.CanGuess = false;
            Utils.SendMessage(GetString("DeathNoteCommand.WrongName"), player.PlayerId);
            return;
        }

        PlayerControl pc = deadPlayer.GetPlayer();

        if (pc == null || !pc.IsAlive())
        {
            NoteKiller.CanGuess = false;
            Utils.SendMessage(GetString("DeathNoteCommand.PlayerNotFoundOrDead"), player.PlayerId);
            return;
        }

        PlayerState state = Main.PlayerStates[pc.PlayerId];
        state.deathReason = PlayerState.DeathReason.Kill;
        state.RealKiller.ID = player.PlayerId;
        state.SetDead();

        pc.RpcExileV2();
        Utils.AfterPlayerDeathTasks(pc, true);
        SoundManager.Instance.PlaySound(pc.KillSfx, false, 0.8f);

        string coloredName = deadPlayer.ColoredPlayerName();
        Utils.SendMessage("\n", player.PlayerId, string.Format(GetString("DeathNoteCommand.Success"), coloredName), sendOption: SendOption.None);
        Utils.SendMessage(string.Format(GetString("DeathNoteCommand.SuccessForOthers"), coloredName));

        NoteKiller.Kills++;
    }

    private static void AchievementsCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(AchievementsCommand), text);
            return;
        }

        Func<Achievements.Type, string> ToAchievementString = x => $"<b>{GetString($"Achievement.{x}")}</b> - {GetString($"Achievement.{x}.Description")}";

        Achievements.Type[] allAchievements = Enum.GetValues<Achievements.Type>();
        Achievements.Type[] union = Achievements.CompletedAchievements.Union(Achievements.WaitingAchievements).ToArray();
        var completedAchievements = $"<size=70%>{union.Join(ToAchievementString, "\n")}</size>";
        var incompleteAchievements = $"<size=70%>{allAchievements.Except(union).Join(ToAchievementString, "\n")}</size>";

        Utils.SendMessage(incompleteAchievements, player.PlayerId, GetString("IncompleteAchievementsTitle"));
        Utils.SendMessage(completedAchievements, player.PlayerId, GetString("CompletedAchievementsTitle") + $" <#00a5ff>(<#00ffa5>{union.Length}</color>/{allAchievements.Length})</color>");
    }

    private static void EnableAllRolesCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(EnableAllRolesCommand), text);
            return;
        }

        Prompt.Show(
            GetString("Promt.EnableAllRoles"),
            () => Options.CustomRoleSpawnChances.Values.DoIf(x => x.GetValue() == 0, x => x.SetValue(1)),
            () => Utils.EnterQuickSetupRoles(false));
    }

    private static void ReadyCheckCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(ReadyCheckCommand), text);
            return;
        }

        Utils.SendMessage(GetString("ReadyCheckMessage"), title: GetString("ReadyCheckTitle"));
        ReadyPlayers = [player.PlayerId];
        ReadyPlayers.UnionWith(Spectators);
        Main.Instance.StopCoroutine(Countdown());
        Main.Instance.StartCoroutine(Countdown());
        return;

        IEnumerator Countdown()
        {
            var timer = 30f;

            while (timer > 0f)
            {
                if (!GameStates.IsLobby) yield break;

                if (Main.AllPlayerControls.Select(x => x.PlayerId).All(ReadyPlayers.Contains)) break;

                timer -= Time.deltaTime;
                yield return null;
            }

            byte[] notReadyPlayers = Main.AllPlayerControls.Select(x => x.PlayerId).Except(ReadyPlayers).ToArray();

            if (notReadyPlayers.Length == 0)
                Utils.SendMessage("\n", player.PlayerId, GetString("EveryoneReadyTitle"));
            else
                Utils.SendMessage(string.Join(", ", notReadyPlayers.Select(x => x.ColoredPlayerName())), player.PlayerId, string.Format(GetString("PlayersNotReadyTitle"), notReadyPlayers.Length));

            if (Spectators.Count > 0) Utils.SendMessage(string.Join(", ", Spectators.Select(x => x.ColoredPlayerName())), player.PlayerId, string.Format(GetString("SpectatorsList"), Spectators.Count));
        }
    }

    private static void ReadyCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(ReadyCommand), text);
            return;
        }

        ReadyPlayers.Add(player.PlayerId);
    }

    private static void DraftStartCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(DraftStartCommand), text);
            return;
        }

        byte[] allPlayerIds = Main.AllPlayerControls.Select(x => x.PlayerId).ToArray();
        List<CustomRoles> allRoles = Enum.GetValues<CustomRoles>().Where(x => x < CustomRoles.NotAssigned && x.IsEnable() && !x.IsForOtherGameMode() && !CustomHnS.AllHnSRoles.Contains(x) && !x.IsVanilla() && x is not CustomRoles.GM and not CustomRoles.Konan).ToList();

        if (allRoles.Count < allPlayerIds.Length)
        {
            Utils.SendMessage(GetString("DraftNotEnoughRoles"), player.PlayerId);
            return;
        }

        IEnumerable<CustomRoles> impRoles = allRoles.Where(x => x.IsImpostor()).Shuffle().Take(Options.FactionMinMaxSettings[Team.Impostor].MaxSetting.GetInt());
        IEnumerable<CustomRoles> nkRoles = allRoles.Where(x => x.IsNK()).Shuffle().Take(Options.RoleSubCategoryLimits[RoleOptionType.Neutral_Killing][2].GetInt());
        IEnumerable<CustomRoles> nnkRoles = allRoles.Where(x => x.IsNonNK()).Shuffle().Take(Options.MaxNNKs.GetInt());
        IEnumerable<CustomRoles> covenRoles = allRoles.Where(x => x.IsCoven()).Shuffle().Take(Options.FactionMinMaxSettings[Team.Coven].MaxSetting.GetInt());

        allRoles.RemoveAll(x => x.IsImpostor());
        allRoles.RemoveAll(x => x.IsNK());
        allRoles.RemoveAll(x => x.IsNonNK());
        allRoles.RemoveAll(x => x.IsCoven());

        DraftRoles = allRoles
            .Take(allPlayerIds.Length * 5)
            .CombineWith(impRoles, nkRoles, nnkRoles, covenRoles)
            .Shuffle()
            .Partition(allPlayerIds.Length)
            .Zip(allPlayerIds)
            .ToDictionary(x => x.Second, x => x.First.Take(5).ToList());

        Main.Instance.StartCoroutine(RepeatedlySendMessage());
        return;

        IEnumerator RepeatedlySendMessage()
        {
            for (var index = 0; index < 3; index++)
            {
                List<Message> messages = [];

                foreach ((byte id, List<CustomRoles> roles) in DraftRoles)
                {
                    IEnumerable<string> roleList = roles.Select((x, i) => $"{i + 1}. {x.ToColoredString()}");
                    string msg = string.Format(GetString(index == 0 ? "DraftStart" : "DraftResend"), string.Join('\n', roleList));
                    messages.Add(new Message(msg, id, GetString("DraftTitle")));
                }

                messages.SendMultipleMessages(index == 0 ? SendOption.Reliable : SendOption.None);

                yield return new WaitForSeconds(20f);
                if (DraftResult.Count >= DraftRoles.Count || GameStates.InGame) yield break;
            }
        }
    }

    private static void DraftDescriptionCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(DraftDescriptionCommand), text);
            return;
        }

        if (DraftRoles.Count == 0 || !DraftRoles.TryGetValue(player.PlayerId, out List<CustomRoles> roles) || args.Length < 2 || !int.TryParse(args[1], out int chosenIndex) || roles.Count < chosenIndex) return;

        CustomRoles role = roles[chosenIndex - 1];
        string roleStr = role.ToColoredString();

        StringBuilder sb = new();
        var title = $"{roleStr} {Utils.GetRoleMode(role)}";
        sb.Append(GetString($"{role}InfoLong").TrimStart());
        string txt = $"<size=90%>{sb}</size>".Replace(role.ToString(), roleStr).Replace(role.ToString().ToLower(), roleStr);
        sb.Clear().Append(txt);
        if (role.PetActivatedAbility()) sb.Append($"<size=50%>{GetString("SupportsPetMessage")}</size>");

        Utils.SendMessage(sb.ToString(), player.PlayerId, title);
    }

    private static void DraftCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(DraftCommand), text);
            return;
        }

        if (DraftRoles.Count == 0 || !DraftRoles.TryGetValue(player.PlayerId, out List<CustomRoles> roles) || args.Length < 2 || !int.TryParse(args[1], out int chosenIndex) || roles.Count < chosenIndex) return;

        CustomRoles role = roles[chosenIndex - 1];
        DraftResult[player.PlayerId] = role;
        Utils.SendMessage(string.Format(GetString("DraftChosen"), role.ToColoredString()), player.PlayerId, GetString("DraftTitle"));
    }

    private static void MuteCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(MuteCommand), text, true);
            return;
        }

        bool host = player.IsHost();
        if (!host && (GameStates.InGame || MutedPlayers.ContainsKey(player.PlayerId))) return;
        if (!byte.TryParse(args[1], out byte id) || id.IsHost() || (!host && IsPlayerModerator(id.GetPlayer()?.FriendCode))) return;

        int duration = args.Length < 3 || !int.TryParse(args[2], out int dur) ? 60 : dur;
        MutedPlayers[id] = (Utils.TimeStamp, duration);

        List<Message> messages =
        [
            new("\n", player.PlayerId, string.Format(GetString("PlayerMuted"), id.ColoredPlayerName(), duration)),
            new("\n", id, string.Format(GetString("YouMuted"), player.PlayerId.ColoredPlayerName(), duration))
        ];
        if (!host) messages.Add(new Message("\n", 0, string.Format(GetString("ModeratorMuted"), player.PlayerId.ColoredPlayerName(), id.ColoredPlayerName(), duration)));
        messages.SendMultipleMessages();
    }

    private static void UnmuteCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(UnmuteCommand), text);
            return;
        }

        if (args.Length < 2 || !byte.TryParse(args[1], out byte id)) return;

        MutedPlayers.Remove(id);
        Utils.SendMessage("\n", player.PlayerId, string.Format(GetString("PlayerUnmuted"), id.ColoredPlayerName()));
        Utils.SendMessage("\n", id, string.Format(GetString("YouUnmuted"), player.PlayerId.ColoredPlayerName()));
    }

    private static void NegotiationCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(NegotiationCommand), text);
            return;
        }

        if (!Negotiator.On || !player.IsAlive() || args.Length < 2 || !int.TryParse(args[1], out int index)) return;

        Negotiator.ReceiveCommand(player, index);
    }

    private static void OSCommand(PlayerControl player, string text, string[] args)
    {
        if (!GameStates.IsLobby || args.Length < 3 || !byte.TryParse(args[1], out byte chance) || chance > 100 || chance % 5 != 0 || !GetRoleByName(string.Join(' ', args[2..]), out CustomRoles role) || !Options.CustomRoleSpawnChances.TryGetValue(role, out StringOptionItem option)) return;

        if (role.IsAdditionRole())
        {
            option.SetValue(chance == 0 ? 0 : 1);
            if (!Options.CustomAdtRoleSpawnRate.TryGetValue(role, out IntegerOptionItem adtOption)) return;

            adtOption.SetValue(chance / 5);
        }
        else
            option.SetValue(chance / 5);
    }

    private static void NoteCommand(PlayerControl player, string text, string[] args)
    {
        if (Starspawn.IsDayBreak) return;

        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(NoteCommand), text);
            return;
        }

        if (player.Is(CustomRoles.Journalist) && player.IsAlive())
        {
            if (PlayerControl.LocalPlayer.PlayerId != player.PlayerId) ChatManager.SendPreviousMessagesToAll();

            Journalist.OnReceiveCommand(player, args);
        }
    }

    private static void AssumeCommand(PlayerControl player, string text, string[] args)
    {
        if (Starspawn.IsDayBreak) return;

        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(AssumeCommand), text);
            return;
        }

        if (args.Length < 3 || !byte.TryParse(args[1], out byte id) || !int.TryParse(args[2], out int num) || !player.Is(CustomRoles.Assumer) || !player.IsAlive()) return;

        if (PlayerControl.LocalPlayer.PlayerId != player.PlayerId) ChatManager.SendPreviousMessagesToAll();

        Assumer.Assume(player.PlayerId, id, num);
    }

    private static void DeleteVIPCommand(PlayerControl player, string text, string[] args)
    {
        if (args.Length < 2 || !byte.TryParse(args[1], out byte VIPId)) return;

        PlayerControl VIPPc = Utils.GetPlayerById(VIPId);
        if (VIPPc == null) return;

        string fc = VIPPc.FriendCode.Replace(':', '#');
        if (!IsPlayerVIP(fc)) Utils.SendMessage(GetString("PlayerNotVIP"), player.PlayerId);

        string[] lines = File.ReadAllLines("./EHR_DATA/VIPs.txt").Where(line => !line.Contains(fc)).ToArray();
        File.WriteAllLines("./EHR_DATA/VIPs.txt", lines);
        Utils.SendMessage(GetString("PlayerRemovedFromVIPList"), player.PlayerId);
    }

    private static void AddVIPCommand(PlayerControl player, string text, string[] args)
    {
        if (args.Length < 2 || !byte.TryParse(args[1], out byte newVIPId)) return;

        PlayerControl newVIPPc = Utils.GetPlayerById(newVIPId);
        if (newVIPPc == null) return;

        string fc = newVIPPc.FriendCode.Replace(':', '#');
        if (IsPlayerVIP(fc)) Utils.SendMessage(GetString("PlayerAlreadyVIP"), player.PlayerId);

        File.AppendAllText("./EHR_DATA/VIPs.txt", $"\n{fc}");
        Utils.SendMessage(GetString("PlayerAddedToVIPList"), player.PlayerId);
    }

    private static void DecreeCommand(PlayerControl player, string text, string[] args)
    {
        if (Starspawn.IsDayBreak) return;

        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(DecreeCommand), text);
            return;
        }

        if (!player.Is(CustomRoles.President)) return;

        if (player.PlayerId != PlayerControl.LocalPlayer.PlayerId) ChatManager.SendPreviousMessagesToAll();

        LateTask.New(() =>
        {
            if (args.Length < 2)
            {
                Utils.SendMessage(President.GetHelpMessage(), player.PlayerId);
                return;
            }

            President.UseDecree(player, args[1]);
        }, 0.2f, log: false);
    }

    private static void HMCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(HMCommand), text);
            return;
        }

        if (Messenger.Sent.Contains(player.PlayerId) || args.Length < 2 || !int.TryParse(args[1], out int id) || id is > 3 or < 1) return;

        Main.Instance.StartCoroutine(SendOnMeeting());
        return;

        IEnumerator SendOnMeeting()
        {
            bool meeting = GameStates.IsMeeting;
            while (!GameStates.IsMeeting && GameStates.InGame) yield return null;
            if (!GameStates.InGame) yield break;

            if (!meeting) yield return new WaitForSeconds(7f);

            PlayerControl killer = player.GetRealKiller();
            if (killer == null && id != 3) yield break;

            Team team = player.GetTeam();

            string message = id switch
            {
                1 => string.Format(GetString("MessengerMessage.1"), GetString(Main.PlayerStates[killer.PlayerId].LastRoom.RoomId.ToString())),
                2 => string.Format(GetString("MessengerMessage.2"), killer.GetCustomRole().ToColoredString()),
                _ => string.Format(GetString("MessengerMessage.3"), Utils.ColorString(team.GetColor(), GetString($"{team}")))
            };

            Utils.SendMessage(message, title: string.Format(GetString("MessengerTitle"), player.PlayerId.ColoredPlayerName()));
            Messenger.Sent.Add(player.PlayerId);
        }
    }

    // Credit: Drakos for the base code
    private static void PollCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(PollCommand), text, true);
            return;
        }

        PollVotes.Clear();
        PollAnswers.Clear();
        PollVoted.Clear();

        if (!args.Any(x => x.Contains('?')))
        {
            Utils.SendMessage(GetString("PollUsage"), player.PlayerId);
            return;
        }

        PollTimer = 45f;

        int splitIndex = Array.IndexOf(args, args.First(x => x.Contains('?'))) + 1;
        string[] answers = args.Skip(splitIndex).ToArray();

        string msg = string.Join(" ", args.Take(splitIndex).Skip(1)) + "\n";
        bool gmPoll = msg.Contains(GetString("GameModePoll.Question"));

        for (var i = 0; i < Math.Max(answers.Length, 2); i++)
        {
            var choiceLetter = (char)(i + 65);
            msg += Utils.ColorString(RandomColor(), $"{char.ToUpper(choiceLetter)}) {answers[i]}\n");
            PollVotes[choiceLetter] = 0;
            PollAnswers[choiceLetter] = $"<size=70%>〖 {answers[i]} 〗</size>";
        }

        if (gmPoll) PollVotes['A'] = Main.AllPlayerControls.Length / 4;

        msg += $"\n{GetString("Poll.Begin")}\n<size=60%><i>";
        string title = GetString("Poll.Title");
        Utils.SendMessage(msg + $"{string.Format(GetString("Poll.TimeInfo"), (int)Math.Round(PollTimer))}</i></size>", title: title);

        Main.Instance.StartCoroutine(StartPollCountdown());
        return;

        IEnumerator StartPollCountdown()
        {
            if (PollVotes.Count == 0) yield break;

            bool notEveryoneVoted = Main.AllPlayerControls.Length - 1 > PollVotes.Values.Sum();

            var resendTimer = 0f;

            while ((notEveryoneVoted || gmPoll) && PollTimer > 0f)
            {
                notEveryoneVoted = Main.AllPlayerControls.Length - 1 > PollVotes.Values.Sum();
                PollTimer -= Time.deltaTime;
                resendTimer += Time.deltaTime;

                if (resendTimer >= 15f)
                {
                    resendTimer = 0f;
                    Utils.SendMessage(msg + $"{string.Format(GetString("Poll.TimeInfo"), (int)Math.Round(PollTimer))}</i></size>", title: title, sendOption: SendOption.None);
                }

                yield return null;
            }

            DetermineResults();
        }

        void DetermineResults()
        {
            int maxVotes = PollVotes.Values.Max();
            KeyValuePair<char, int>[] winners = PollVotes.Where(x => x.Value == maxVotes).ToArray();

            string result = winners.Length == 1
                ? string.Format(GetString("Poll.Winner"), winners[0].Key, PollAnswers[winners[0].Key], winners[0].Value) +
                  PollVotes.Where(x => x.Key != winners[0].Key).Aggregate("", (s, t) => s + $"{t.Key} - {t.Value} {PollAnswers[t.Key]}\n")
                : string.Format(GetString("Poll.Tie"), string.Join(" & ", winners.Select(x => $"{x.Key}{PollAnswers[x.Key]}")), maxVotes);

            Utils.SendMessage(result, title: Utils.ColorString(new(0, 255, 165, 255), GetString("PollResultTitle")));

            PollVotes.Clear();
            PollAnswers.Clear();
            PollVoted.Clear();

            if (winners.Length == 1 && gmPoll)
            {
                int winnerIndex = winners[0].Key - 65;
                if (winnerIndex != 0) Options.GameMode.SetValue(winnerIndex - 1);
            }
        }

        static Color32 RandomColor()
        {
            byte[] colors = IRandom.Sequence(3, 0, 160).Select(x => (byte)x).ToArray();
            return new(colors[0], colors[1], colors[2], 255);
        }
    }

    private static void PVCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(PVCommand), text);
            return;
        }

        if (PollVotes.Count == 0)
        {
            Utils.SendMessage(GetString("Poll.Inactive"), player.PlayerId, sendOption: SendOption.None);
            return;
        }

        if (PollVoted.Contains(player.PlayerId))
        {
            Utils.SendMessage(GetString("Poll.AlreadyVoted"), player.PlayerId, sendOption: SendOption.None);
            return;
        }

        if (args.Length != 2 || !char.TryParse(args[1], out char vote) || !PollVotes.ContainsKey(char.ToUpper(vote)))
        {
            Utils.SendMessage(GetString("Poll.VotingInfo"), player.PlayerId);
            return;
        }

        vote = char.ToUpper(vote);

        PollVoted.Add(player.PlayerId);
        PollVotes[vote]++;
        Utils.SendMessage(string.Format(GetString("Poll.YouVoted"), vote, PollVotes[vote]), player.PlayerId);
    }

    private static void HelpCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(HelpCommand), text);
            return;
        }

        Utils.ShowHelp(player.PlayerId);
    }

    private static void DumpCommand(PlayerControl player, string text, string[] args)
    {
        Utils.DumpLog();
    }

    private static void GNOCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(GNOCommand), text);
            return;
        }

        if (!GameStates.IsLobby && player.IsAlive())
        {
            Utils.SendMessage(GetString("GNoCommandInfo"), player.PlayerId, sendOption: SendOption.None);
            return;
        }

        string subArgs = args.Length != 2 ? "" : args[1];

        if (subArgs == "" || !int.TryParse(subArgs, out int guessedNo))
        {
            Utils.SendMessage(GetString("GNoCommandInfo"), player.PlayerId, sendOption: SendOption.None);
            return;
        }

        if (guessedNo is < 0 or > 99)
        {
            Utils.SendMessage(GetString("GNoCommandInfo"), player.PlayerId, sendOption: SendOption.None);
            return;
        }

        int targetNumber = Main.GuessNumber[player.PlayerId][0];

        if (Main.GuessNumber[player.PlayerId][0] == -1)
        {
            var rand = IRandom.Instance;
            Main.GuessNumber[player.PlayerId][0] = rand.Next(0, 100);
            targetNumber = Main.GuessNumber[player.PlayerId][0];
        }

        Main.GuessNumber[player.PlayerId][1]--;

        if (Main.GuessNumber[player.PlayerId][1] == 0 && guessedNo != targetNumber)
        {
            Main.GuessNumber[player.PlayerId][0] = -1;
            Main.GuessNumber[player.PlayerId][1] = 7;
            Utils.SendMessage(string.Format(GetString("GNoLost"), targetNumber), player.PlayerId);
            return;
        }

        if (guessedNo < targetNumber)
        {
            Utils.SendMessage(string.Format(GetString("GNoLow"), Main.GuessNumber[player.PlayerId][1]), player.PlayerId);
            return;
        }

        if (guessedNo > targetNumber)
        {
            Utils.SendMessage(string.Format(GetString("GNoHigh"), Main.GuessNumber[player.PlayerId][1]), player.PlayerId);
            return;
        }

        Utils.SendMessage(string.Format(GetString("GNoWon"), Main.GuessNumber[player.PlayerId][1]), player.PlayerId);
        Main.GuessNumber[player.PlayerId][0] = -1;
        Main.GuessNumber[player.PlayerId][1] = 7;
    }

    private static void SDCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(SDCommand), text);
            return;
        }

        if (args.Length < 1 || !int.TryParse(args[1], out int sound1)) return;

        RPC.PlaySoundRPC(player.PlayerId, (Sounds)sound1);
    }

    private static void CSDCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(CSDCommand), text);
            return;
        }

        string subArgs = text.Remove(0, 3);
        player.RPCPlayCustomSound(subArgs.Trim());
    }

    private static void MTHYCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(MTHYCommand), text);
            return;
        }

        if (GameStates.IsMeeting)
            MeetingHud.Instance.RpcClose();
        else
            player.NoCheckStartMeeting(null, true);
    }

    private static void CosIDCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(CosIDCommand), text);
            return;
        }

        NetworkedPlayerInfo.PlayerOutfit of = player.Data.DefaultOutfit;
        Logger.Warn($"ColorId: {of.ColorId}", "Get Cos Id");
        Logger.Warn($"PetId: {of.PetId}", "Get Cos Id");
        Logger.Warn($"HatId: {of.HatId}", "Get Cos Id");
        Logger.Warn($"SkinId: {of.SkinId}", "Get Cos Id");
        Logger.Warn($"VisorId: {of.VisorId}", "Get Cos Id");
        Logger.Warn($"NamePlateId: {of.NamePlateId}", "Get Cos Id");
    }

    private static void EndCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(EndCommand), text);
            return;
        }

        CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Draw);
        GameManager.Instance.LogicFlow.CheckEndCriteria();
    }

    private static void ChangeRoleCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(ChangeRoleCommand), text);
            return;
        }

        if (GameStates.IsLobby || !player.FriendCode.GetDevUser().up) return;

        string subArgs = text.Remove(0, 8);
        string setRole = FixRoleNameInput(subArgs.Trim());

        foreach (CustomRoles rl in Enum.GetValues<CustomRoles>())
        {
            if (rl.IsVanilla()) continue;

            string roleName = GetString(rl.ToString()).ToLower().Trim();

            if (setRole.Contains(roleName))
            {
                if (!rl.IsAdditionRole()) player.SetRole(rl.GetRoleTypes());

                player.RpcSetCustomRole(rl);

                if (rl.IsGhostRole()) GhostRolesManager.SpecificAssignGhostRole(player.PlayerId, rl, true);

                Main.PlayerStates[player.PlayerId].RemoveSubRole(CustomRoles.NotAssigned);
                Main.ChangedRole = true;
                break;
            }
        }
    }

    private static void IDCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(IDCommand), text);
            return;
        }

        string msgText = GetString("PlayerIdList");
        msgText = Main.AllPlayerControls.Aggregate(msgText, (current, pc) => $"{current}\n{pc.PlayerId} \u2192 {pc.GetRealName()}");

        Utils.SendMessage(msgText, player.PlayerId);
    }

    private static void ColorCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(ColorCommand), text);
            return;
        }

        if (GameStates.IsInGame)
        {
            Utils.SendMessage(GetString("Message.OnlyCanUseInLobby"), player.PlayerId, sendOption: SendOption.None);
            return;
        }

        if (!player.IsHost() && !Options.PlayerCanSetColor.GetBool() && !IsPlayerVIP(player.FriendCode) && !player.FriendCode.GetDevUser().up)
        {
            Utils.SendMessage(GetString("DisableUseCommand"), player.PlayerId, sendOption: SendOption.None);
            return;
        }

        string subArgs = args.Length < 2 ? string.Empty : args[1];
        byte color = Utils.MsgToColor(subArgs, player.IsHost());

        if (color == byte.MaxValue)
        {
            Utils.SendMessage(GetString("IllegalColor"), player.PlayerId, sendOption: SendOption.None);
            return;
        }

        player.RpcSetColor(color);
        Utils.SendMessage(string.Format(GetString("Message.SetColor"), subArgs), player.PlayerId, sendOption: SendOption.None);
    }

    private static void KillCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(KillCommand), text);
            return;
        }

        if (GameStates.IsLobby)
        {
            Utils.SendMessage(GetString("Message.CanNotUseInLobby"), player.PlayerId, sendOption: SendOption.None);
            return;
        }

        if (args.Length < 2 || !int.TryParse(args[1], out int id2)) return;

        PlayerControl target = Utils.GetPlayerById(id2);

        if (target != null)
        {
            target.Kill(target);

            if (target.AmOwner)
                Utils.SendMessage(GetString("HostKillSelfByCommand"), title: $"<color=#ff0000>{GetString("DefaultSystemMessageTitle")}</color>");
            else
                Utils.SendMessage(string.Format(GetString("Message.Executed"), target.Data.PlayerName));
        }
    }

    private static void ExeCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(ExeCommand), text);
            return;
        }

        if (GameStates.IsLobby)
        {
            Utils.SendMessage(GetString("Message.CanNotUseInLobby"), player.PlayerId, sendOption: SendOption.None);
            return;
        }

        if (args.Length < 2 || !int.TryParse(args[1], out int id)) return;

        PlayerControl pc = Utils.GetPlayerById(id);

        if (pc != null)
        {
            pc.Data.IsDead = true;
            Main.PlayerStates[pc.PlayerId].deathReason = PlayerState.DeathReason.etc;
            pc.RpcExileV2();
            Main.PlayerStates[pc.PlayerId].SetDead();
            Utils.AfterPlayerDeathTasks(pc, GameStates.IsMeeting);

            if (pc.AmOwner)
                Utils.SendMessage(GetString("HostKillSelfByCommand"), title: $"<color=#ff0000>{GetString("DefaultSystemMessageTitle")}</color>");
            else
                Utils.SendMessage(string.Format(GetString("Message.Executed"), pc.Data.PlayerName));
        }
    }

    private static void BanKickCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(BanKickCommand), text, true);
            return;
        }

        // Check if the Kick command is enabled in the settings
        if (!Options.ApplyModeratorList.GetBool() && !player.IsHost())
        {
            Utils.SendMessage(GetString("KickCommandDisabled"), player.PlayerId, sendOption: SendOption.None);
            return;
        }

        // Check if the Player has the necessary privileges to use the command
        if (!IsPlayerModerator(player.FriendCode) && !player.IsHost())
        {
            Utils.SendMessage(GetString("KickCommandNoAccess"), player.PlayerId, sendOption: SendOption.None);
            return;
        }

        string subArgs = args.Length < 2 ? string.Empty : args[1];

        if (string.IsNullOrEmpty(subArgs) || !byte.TryParse(subArgs, out byte kickPlayerId))
        {
            Utils.SendMessage(GetString("KickCommandInvalidID"), player.PlayerId, sendOption: SendOption.None);
            return;
        }

        if (kickPlayerId.IsHost())
        {
            Utils.SendMessage(GetString("KickCommandKickHost"), player.PlayerId, sendOption: SendOption.None);
            return;
        }

        PlayerControl kickedPlayer = Utils.GetPlayerById(kickPlayerId);

        if (kickedPlayer == null)
        {
            Utils.SendMessage(GetString("KickCommandInvalidID"), player.PlayerId, sendOption: SendOption.None);
            return;
        }

        // Prevent Moderators from kicking other Moderators
        if (IsPlayerModerator(kickedPlayer.FriendCode))
        {
            Utils.SendMessage(GetString("KickCommandKickMod"), player.PlayerId, sendOption: SendOption.None);
            return;
        }

        // Kick the specified Player
        AmongUsClient.Instance.KickPlayer(kickedPlayer.GetClientId(), args[0] == "/ban");
        string kickedPlayerName = kickedPlayer.GetRealName();
        var textToSend = $"{kickedPlayerName} {GetString("KickCommandKicked")}";
        if (GameStates.IsInGame) textToSend += $"{GetString("KickCommandKickedRole")} {kickedPlayer.GetCustomRole().ToColoredString()}";

        Utils.SendMessage(textToSend, sendOption: GameStates.IsInGame ? SendOption.Reliable : SendOption.None);
    }

    private static void CheckCommand(PlayerControl player, string text, string[] args)
    {
        if (Starspawn.IsDayBreak) return;

        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(CheckCommand), text);
            return;
        }

        if (!player.IsAlive() || !player.Is(CustomRoles.Inquirer) || player.GetAbilityUseLimit() < 1) return;

        if (args.Length < 3 || !GuessManager.MsgToPlayerAndRole(text[6..], out byte checkId, out CustomRoles checkRole, out _)) return;

        bool hasRole = Utils.GetPlayerById(checkId).Is(checkRole);
        if (IRandom.Instance.Next(100) < Inquirer.FailChance.GetInt()) hasRole = !hasRole;

        if (player.PlayerId != PlayerControl.LocalPlayer.PlayerId) ChatManager.SendPreviousMessagesToAll();

        LateTask.New(() => Utils.SendMessage(GetString(hasRole ? "Inquirer.MessageTrue" : "Inquirer.MessageFalse"), player.PlayerId), 0.2f, log: false);
        player.RpcRemoveAbilityUse();
    }

    private static void ChatCommand(PlayerControl player, string text, string[] args)
    {
        if (Starspawn.IsDayBreak) return;

        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(ChatCommand), text);
            return;
        }

        if (!Ventriloquist.On || !player.IsAlive() || !player.Is(CustomRoles.Ventriloquist) || player.PlayerId.GetAbilityUseLimit() < 1) return;

        var vl2 = (Ventriloquist)Main.PlayerStates[player.PlayerId].Role;
        if (vl2.Target == byte.MaxValue) return;

        if (player.PlayerId != PlayerControl.LocalPlayer.PlayerId) ChatManager.SendPreviousMessagesToAll();

        PlayerControl tg = Utils.GetPlayerById(vl2.Target);
        string msg = text[6..];
        LateTask.New(() => tg?.RpcSendChat(msg), 0.2f, log: false);
        ChatManager.AddChatHistory(tg, msg);

        player.RpcRemoveAbilityUse();
    }

    private static void TargetCommand(PlayerControl player, string text, string[] args)
    {
        if (Starspawn.IsDayBreak) return;

        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(TargetCommand), text);
            return;
        }

        if (!Ventriloquist.On || !player.IsAlive() || !player.Is(CustomRoles.Ventriloquist) || player.PlayerId.GetAbilityUseLimit() < 1) return;

        var vl = (Ventriloquist)Main.PlayerStates[player.PlayerId].Role;
        vl.Target = args.Length < 2 ? byte.MaxValue : byte.TryParse(args[1], out byte targetId) ? targetId : byte.MaxValue;
        if (player.PlayerId != PlayerControl.LocalPlayer.PlayerId) ChatManager.SendPreviousMessagesToAll();
    }

    private static void QSCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(QSCommand), text);
            return;
        }

        if (!QuizMaster.On || !player.IsAlive()) return;

        var qm2 = (QuizMaster)Main.PlayerStates.Values.First(x => x.Role is QuizMaster).Role;
        if (qm2.Target != player.PlayerId || !QuizMaster.MessagesToSend.TryGetValue(player.PlayerId, out string msg)) return;

        Utils.SendMessage(msg, player.PlayerId, GetString("QuizMaster.QuestionSample.Title"));
    }

    private static void QACommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(QACommand), text);
            return;
        }

        if (args.Length < 2 || !QuizMaster.On || !player.IsAlive()) return;

        var qm = (QuizMaster)Main.PlayerStates.Values.First(x => x.Role is QuizMaster).Role;
        if (qm.Target != player.PlayerId) return;

        qm.Answer(args[1].ToUpper());
    }

    private static void AnswerCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(AnswerCommand), text);
            return;
        }

        if (args.Length < 2) return;

        Mathematician.Reply(player, args[1]);
    }

    private static void AskCommand(PlayerControl player, string text, string[] args)
    {
        if (Starspawn.IsDayBreak) return;

        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(AskCommand), text);
            return;
        }

        if (args.Length < 3 || !player.Is(CustomRoles.Mathematician)) return;

        if (player.PlayerId != PlayerControl.LocalPlayer.PlayerId) ChatManager.SendPreviousMessagesToAll();

        Mathematician.Ask(player, args[1], args[2]);
    }

    private static void VoteCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(VoteCommand), text);
            return;
        }

        if (text.Length < 6 || !GameStates.IsMeeting) return;

        string toVote = text[6..].Replace(" ", string.Empty);
        if (!byte.TryParse(toVote, out byte voteId) || MeetingHud.Instance.playerStates?.FirstOrDefault(x => x.TargetPlayerId == player.PlayerId)?.DidVote is true or null) return;

        if (voteId > Main.AllPlayerControls.Length) return;

        if (player.PlayerId != PlayerControl.LocalPlayer.PlayerId)
            ChatManager.SendPreviousMessagesToAll();

        PlayerControl votedPlayer = voteId.GetPlayer();
        if (Main.PlayerStates.TryGetValue(player.PlayerId, out PlayerState state) && votedPlayer != null && state.Role.OnVote(player, votedPlayer)) return;

        if (!player.IsHost())
            MeetingHud.Instance.CastVote(player.PlayerId, voteId);
        else
            MeetingHud.Instance.CmdCastVote(player.PlayerId, voteId);
    }

    private static void SayCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(SayCommand), text, true);
            return;
        }

        if (!player.IsHost() && !IsPlayerModerator(player.FriendCode)) return;

        if (args.Length > 1) Utils.SendMessage(args[1..].Join(delimiter: " "), title: $"<color=#ff0000>{GetString(player.IsHost() ? "MessageFromTheHost" : "SayTitle")}</color>");
    }

    private static void DeathCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(DeathCommand), text);
            return;
        }

        if (!GameStates.IsInGame) return;
        if (Main.DiedThisRound.Contains(player.PlayerId) && Utils.IsRevivingRoleAlive()) return;

        PlayerControl killer = player.GetRealKiller();

        if (killer == null)
        {
            Utils.SendMessage("\n", player.PlayerId, GetString("DeathCommandFail"), sendOption: SendOption.None);
            return;
        }

        Utils.SendMessage("\n", player.PlayerId, string.Format(GetString("DeathCommand"), killer.PlayerId.ColoredPlayerName(), (killer.Is(CustomRoles.Bloodlust) ? $"{CustomRoles.Bloodlust.ToColoredString()} " : string.Empty) + killer.GetCustomRole().ToColoredString()));
    }

    private static void MessageWaitCommand(PlayerControl player, string text, string[] args)
    {
        if (args.Length > 1 && int.TryParse(args[1], out int sec))
        {
            Main.MessageWait.Value = sec;
            Utils.SendMessage(string.Format(GetString("Message.SetToSeconds"), sec), 0);
        }
        else
            Utils.SendMessage($"{GetString("Message.MessageWaitHelp")}\n{GetString("ForExample")}:\n{args[0]} 3", 0);
    }

    private static void TemplateCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(TemplateCommand), text);
            return;
        }

        if (player.IsLocalPlayer())
        {
            if (args.Length > 1)
                TemplateManager.SendTemplate(args[1]);
            else
                HudManager.Instance.Chat.AddChat(player, (player.FriendCode.GetDevUser().HasTag() ? "\n" : string.Empty) + $"{GetString("ForExample")}:\n{args[0]} test");
        }
        else
        {
            if (args.Length > 1)
                TemplateManager.SendTemplate(args[1], player.PlayerId);
            else
                Utils.SendMessage($"{GetString("ForExample")}:\n{args[0]} test", player.PlayerId, sendOption: SendOption.None);
        }
    }

    private static void TPInCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(TPInCommand), text);
            return;
        }

        if (!GameStates.IsLobby || !Options.PlayerCanTPInAndOut.GetBool()) return;

        player.TP(new Vector2(-0.2f, 1.3f));
    }

    private static void TPOutCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(TPOutCommand), text);
            return;
        }

        if (!GameStates.IsLobby || !Options.PlayerCanTPInAndOut.GetBool()) return;

        player.TP(new Vector2(0.1f, 3.8f));
    }

    private static void MyRoleCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(MyRoleCommand), text);
            return;
        }

        CustomRoles role = player.GetCustomRole();

        if (GameStates.IsInGame)
        {
            StringBuilder sb = new();
            StringBuilder titleSb = new();
            StringBuilder settings = new();
            settings.Append("<size=70%>");
            titleSb.Append($"{role.ToColoredString()} {Utils.GetRoleMode(role)}");
            sb.Append("<size=90%>");
            sb.Append(player.GetRoleInfo(true).TrimStart());
            if (Options.CustomRoleSpawnChances.TryGetValue(role, out StringOptionItem opt)) Utils.ShowChildrenSettings(opt, ref settings, disableColor: false);

            settings.Append("</size>");
            if (role.PetActivatedAbility()) sb.Append($"<size=50%>{GetString("SupportsPetMessage")}</size>");

            string searchStr = GetString(role.ToString());
            sb.Replace(searchStr, role.ToColoredString());
            sb.Replace(searchStr.ToLower(), role.ToColoredString());
            sb.Append("<size=70%>");

            foreach (CustomRoles subRole in Main.PlayerStates[player.PlayerId].SubRoles)
            {
                sb.Append($"\n\n{subRole.ToColoredString()} {Utils.GetRoleMode(subRole)} {GetString($"{subRole}InfoLong")}");
                string searchSubStr = GetString(subRole.ToString());
                sb.Replace(searchSubStr, subRole.ToColoredString());
                sb.Replace(searchSubStr.ToLower(), subRole.ToColoredString());
            }

            if (settings.Length > 0) Utils.SendMessage("\n", player.PlayerId, settings.ToString());

            Utils.SendMessage(sb.Append("</size>").ToString(), player.PlayerId, titleSb.ToString());
            if (role.UsesPetInsteadOfKill()) Utils.SendMessage("\n", player.PlayerId, GetString("UsesPetInsteadOfKillNotice"));
        }
        else
            Utils.SendMessage((player.FriendCode.GetDevUser().HasTag() ? "\n" : string.Empty) + GetString("Message.CanNotUseInLobby"), player.PlayerId);
    }

    private static void AFKExemptCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(AFKExemptCommand), text);
            return;
        }

        if (args.Length < 2 || !byte.TryParse(args[1], out byte afkId)) return;

        AFKDetector.ExemptedPlayers.Add(afkId);
        Utils.SendMessage("\n", player.PlayerId, string.Format(GetString("PlayerExemptedFromAFK"), afkId.ColoredPlayerName()));
    }

    private static void EffectCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(EffectCommand), text);
            return;
        }

        if (args.Length < 2 || !GameStates.IsInTask || !Randomizer.Exists) return;

        if (Enum.TryParse(args[1], true, out Randomizer.Effect effect)) effect.Apply(player);
    }

    private static void ComboCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(ComboCommand), text);
            return;
        }

        if (args.Length < 4)
        {
            if (Main.AlwaysSpawnTogetherCombos.Count == 0 && Main.NeverSpawnTogetherCombos.Count == 0) return;

            StringBuilder sb = new();
            sb.Append("<size=70%>");

            if (Main.AlwaysSpawnTogetherCombos.TryGetValue(OptionItem.CurrentPreset, out Dictionary<CustomRoles, List<CustomRoles>> alwaysList) && alwaysList.Count > 0)
            {
                sb.AppendLine(GetString("AlwaysComboListTitle"));
                sb.AppendLine(alwaysList.Join(x => $"{x.Key.ToColoredString()} \u00a7 {x.Value.Join(r => r.ToColoredString())}", "\n"));
                sb.AppendLine();
            }

            if (Main.NeverSpawnTogetherCombos.TryGetValue(OptionItem.CurrentPreset, out Dictionary<CustomRoles, List<CustomRoles>> neverList) && neverList.Count > 0)
            {
                sb.AppendLine(GetString("NeverComboListTitle"));
                sb.AppendLine(neverList.Join(x => $"{x.Key.ToColoredString()} \u2194 {x.Value.Join(r => r.ToColoredString())}", "\n"));
                sb.AppendLine();
            }

            sb.Append(GetString("ComboUsage"));

            Utils.SendMessage("\n", player.PlayerId, sb.ToString());
            return;
        }

        switch (args[1])
        {
            case "add":
            case "ban":
                if (GetRoleByName(args[2], out CustomRoles mainRole) && GetRoleByName(args[3], out CustomRoles addOn))
                {
                    if (mainRole.IsAdditionRole() || !addOn.IsAdditionRole() || (addOn == CustomRoles.Lovers && args[1] == "add")) break;

                    if (args[1] == "add")
                    {
                        if (!Main.AlwaysSpawnTogetherCombos.ContainsKey(OptionItem.CurrentPreset)) Main.AlwaysSpawnTogetherCombos[OptionItem.CurrentPreset] = [];

                        if (!Main.AlwaysSpawnTogetherCombos[OptionItem.CurrentPreset].TryGetValue(mainRole, out List<CustomRoles> list1))
                            Main.AlwaysSpawnTogetherCombos[OptionItem.CurrentPreset][mainRole] = [addOn];
                        else if (!list1.Contains(addOn)) list1.Add(addOn);

                        if (text.EndsWith(" all"))
                        {
                            for (var preset = 0; preset < OptionItem.NumPresets; preset++)
                            {
                                if (preset == OptionItem.CurrentPreset) continue;

                                if (!Main.AlwaysSpawnTogetherCombos.ContainsKey(preset)) Main.AlwaysSpawnTogetherCombos[preset] = [];

                                if (!Main.AlwaysSpawnTogetherCombos[preset].TryGetValue(mainRole, out List<CustomRoles> list2))
                                    Main.AlwaysSpawnTogetherCombos[preset][mainRole] = [addOn];
                                else if (!list2.Contains(addOn)) list2.Add(addOn);
                            }
                        }
                    }
                    else
                    {
                        if (!Main.NeverSpawnTogetherCombos.ContainsKey(OptionItem.CurrentPreset)) Main.NeverSpawnTogetherCombos[OptionItem.CurrentPreset] = [];

                        if (!Main.NeverSpawnTogetherCombos[OptionItem.CurrentPreset].TryGetValue(mainRole, out List<CustomRoles> list2))
                            Main.NeverSpawnTogetherCombos[OptionItem.CurrentPreset][mainRole] = [addOn];
                        else if (!list2.Contains(addOn)) list2.Add(addOn);

                        if (text.EndsWith(" all"))
                        {
                            for (var preset = 0; preset < OptionItem.NumPresets; preset++)
                            {
                                if (preset == OptionItem.CurrentPreset) continue;

                                if (!Main.NeverSpawnTogetherCombos.ContainsKey(preset)) Main.NeverSpawnTogetherCombos[preset] = [];

                                if (!Main.NeverSpawnTogetherCombos[preset].TryGetValue(mainRole, out List<CustomRoles> list3))
                                    Main.NeverSpawnTogetherCombos[preset][mainRole] = [addOn];
                                else if (!list3.Contains(addOn)) list3.Add(addOn);
                            }
                        }
                    }

                    Utils.SendMessage(string.Format(args[1] == "add" ? GetString("ComboAdd") : GetString("ComboBan"), GetString(mainRole.ToString()), GetString(addOn.ToString())), player.PlayerId);
                    Utils.SaveComboInfo();
                }

                break;
            case "remove":
            case "allow":
                if (GetRoleByName(args[2], out CustomRoles mainRole2) && GetRoleByName(args[3], out CustomRoles addOn2))
                {
                    if (mainRole2.IsAdditionRole() || !addOn2.IsAdditionRole()) break;

                    if (text.EndsWith(" all"))
                    {
                        for (var preset = 0; preset < OptionItem.NumPresets; preset++)
                        {
                            if (Main.AlwaysSpawnTogetherCombos.TryGetValue(preset, out Dictionary<CustomRoles, List<CustomRoles>> list1))
                            {
                                if (list1.TryGetValue(mainRole2, out List<CustomRoles> list2))
                                {
                                    list2.Remove(addOn2);
                                    if (list2.Count == 0) list1.Remove(mainRole2);

                                    if (list1.Count == 0) Main.AlwaysSpawnTogetherCombos.Remove(preset);
                                }
                            }

                            if (Main.NeverSpawnTogetherCombos.TryGetValue(preset, out Dictionary<CustomRoles, List<CustomRoles>> list3))
                            {
                                if (list3.TryGetValue(mainRole2, out List<CustomRoles> list4))
                                {
                                    list4.Remove(addOn2);
                                    if (list4.Count == 0) list3.Remove(mainRole2);

                                    if (list3.Count == 0) Main.NeverSpawnTogetherCombos.Remove(preset);
                                }
                            }
                        }

                        Utils.SendMessage(string.Format(GetString("ComboRemove"), GetString(mainRole2.ToString()), GetString(addOn2.ToString())), player.PlayerId);
                        Utils.SaveComboInfo();
                    }
                    else
                    {
                        if (args[1] == "remove" && Main.AlwaysSpawnTogetherCombos.TryGetValue(OptionItem.CurrentPreset, out Dictionary<CustomRoles, List<CustomRoles>> alwaysList) && alwaysList.TryGetValue(mainRole2, out List<CustomRoles> list3))
                        {
                            list3.Remove(addOn2);
                            if (list3.Count == 0) alwaysList.Remove(mainRole2);

                            Utils.SendMessage(string.Format(GetString("ComboRemove"), GetString(mainRole2.ToString()), GetString(addOn2.ToString())), player.PlayerId);
                            Utils.SaveComboInfo();
                        }
                        else if (Main.NeverSpawnTogetherCombos.TryGetValue(OptionItem.CurrentPreset, out Dictionary<CustomRoles, List<CustomRoles>> neverList) && neverList.TryGetValue(mainRole2, out List<CustomRoles> list4))
                        {
                            list4.Remove(addOn2);
                            if (list4.Count == 0) neverList.Remove(mainRole2);

                            Utils.SendMessage(string.Format(GetString("ComboAllow"), GetString(mainRole2.ToString()), GetString(addOn2.ToString())), player.PlayerId);
                            Utils.SaveComboInfo();
                        }
                    }
                }

                break;
        }
    }

    private static void DeleteModCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(DeleteModCommand), text);
            return;
        }

        if (args.Length < 2 || !byte.TryParse(args[1], out byte remModId)) return;

        PlayerControl remModPc = Utils.GetPlayerById(remModId);
        if (remModPc == null) return;

        string remFc = remModPc.FriendCode.Replace(':', '#');
        if (!IsPlayerModerator(remFc)) Utils.SendMessage(GetString("PlayerNotMod"), player.PlayerId);

        File.WriteAllLines("./EHR_DATA/Moderators.txt", File.ReadAllLines("./EHR_DATA/Moderators.txt").Where(x => !x.Contains(remFc)));
        Utils.SendMessage(GetString("PlayerRemovedFromModList"), player.PlayerId);
    }

    private static void AddModCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(AddModCommand), text);
            return;
        }

        if (args.Length < 2 || !byte.TryParse(args[1], out byte newModId)) return;

        PlayerControl newModPc = Utils.GetPlayerById(newModId);
        if (newModPc == null) return;

        string fc = newModPc.FriendCode.Replace(':', '#');
        if (IsPlayerModerator(fc)) Utils.SendMessage(GetString("PlayerAlreadyMod"), player.PlayerId);

        File.AppendAllText("./EHR_DATA/Moderators.txt", $"\n{fc}");
        Utils.SendMessage(GetString("PlayerAddedToModList"), player.PlayerId);
    }

    private static void KCountCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(KCountCommand), text);
            return;
        }

        if (GameStates.IsLobby || !Options.EnableKillerLeftCommand.GetBool() || Main.AllAlivePlayerControls.Length < Options.MinPlayersForGameStateCommand.GetInt()) return;

        Utils.SendMessage("\n", player.PlayerId, Utils.GetGameStateData());
    }

    private static void SetRoleCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(SetRoleCommand), text);
            return;
        }

        string subArgs = text.Remove(0, 8);

        if (!GameStates.IsLobby)
        {
            Utils.SendMessage(GetString("Message.OnlyCanUseInLobby"), player.PlayerId);
            return;
        }

        if (!GuessManager.MsgToPlayerAndRole(subArgs, out byte resultId, out CustomRoles roleToSet, out _))
        {
            Utils.SendMessage(GetString("InvalidArguments"), player.PlayerId);
            return;
        }

        if (resultId != 0 && !player.FriendCode.GetDevUser().up)
        {
            Utils.SendMessage(GetString("Message.NoPermissionSetRoleOthers"), player.PlayerId);
            return;
        }

        PlayerControl targetPc = Utils.GetPlayerById(resultId);
        if (targetPc == null) return;

        if (roleToSet.IsAdditionRole())
        {
            if (!Main.SetAddOns.ContainsKey(resultId)) Main.SetAddOns[resultId] = [];

            if (Main.SetAddOns[resultId].Contains(roleToSet))
                Main.SetAddOns[resultId].Remove(roleToSet);
            else
                Main.SetAddOns[resultId].Add(roleToSet);
        }
        else
            Main.SetRoles[targetPc.PlayerId] = roleToSet;

        Utils.SendMessage("\n", player.PlayerId, string.Format(GetString("RoleSelected"), resultId.ColoredPlayerName(), roleToSet.ToColoredString()));

        if (roleToSet.OnlySpawnsWithPets() && !Options.UsePets.GetBool())
            Prompt.Show(GetString("Promt.SetRoleRequiresPets"), () => Options.UsePets.SetValue(1), () => { });
    }

    private static void UpCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(UpCommand), text);
            return;
        }

        Utils.SendMessage($"{GetString("UpReplacedMessage")}", player.PlayerId);
    }

    private static void RCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(RCommand), text);
            return;
        }

        string subArgs = text.Remove(0, 2);
        SendRolesInfo(subArgs, player.PlayerId);
    }

    private static void DisconnectCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(DisconnectCommand), text);
            return;
        }

        string subArgs = args.Length < 2 ? string.Empty : args[1];

        switch (subArgs)
        {
            case "crew":
                GameManager.Instance.enabled = false;
                GameManager.Instance.RpcEndGame(GameOverReason.CrewmateDisconnect, false);
                break;

            case "imp":
                GameManager.Instance.enabled = false;
                GameManager.Instance.RpcEndGame(GameOverReason.ImpostorDisconnect, false);
                break;

            default:
                FastDestroyableSingleton<HudManager>.Instance.Chat.AddChat(player, "crew | imp");
                break;
        }

        ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Admin, 0);
    }

    private static void NowCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(NowCommand), text);
            return;
        }

        string subArgs = args.Length < 2 ? string.Empty : args[1];

        switch (subArgs)
        {
            case "r":
            case "roles":
                Utils.ShowActiveRoles(player.PlayerId);
                break;
            case "a":
            case "all":
                Utils.ShowAllActiveSettings(player.PlayerId);
                break;
            default:
                Utils.ShowActiveSettings(player.PlayerId);
                break;
        }
    }

    private static void LevelCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(LevelCommand), text);
            return;
        }

        string subArgs = args.Length < 2 ? string.Empty : args[1];
        Utils.SendMessage(string.Format(GetString("Message.SetLevel"), subArgs), player.PlayerId);
        _ = int.TryParse(subArgs, out int input);

        if (input is < 1 or > 999)
        {
            Utils.SendMessage(GetString("Message.AllowLevelRange"), player.PlayerId);
            return;
        }

        var number = Convert.ToUInt32(input);
        player.RpcSetLevel(number - 1);
    }

    private static void HideNameCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(HideNameCommand), text);
            return;
        }

        Main.HideName.Value = args.Length > 1 ? args.Skip(1).Join(delimiter: " ") : Main.HideName.DefaultValue.ToString();

        GameStartManagerPatch.GameStartManagerStartPatch.HideName.text =
            ColorUtility.TryParseHtmlString(Main.HideColor.Value, out _)
                ? $"<color={Main.HideColor.Value}>{Main.HideName.Value}</color>"
                : $"<color={Main.ModColor}>{Main.HideName.Value}</color>";
    }

    private static void RenameCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(RenameCommand), text);
            return;
        }

        if (args.Length < 2) return;

        if (args[1].Length is > 50 or < 1)
            Utils.SendMessage(GetString("Message.AllowNameLength"), player.PlayerId);
        else
        {
            if (player.IsLocalPlayer())
                Main.NickName = args[1];
            else
            {
                if (!Options.PlayerCanSetName.GetBool() && !IsPlayerVIP(player.FriendCode)) return;

                if (GameStates.IsInGame)
                {
                    Utils.SendMessage(GetString("Message.OnlyCanUseInLobby"), player.PlayerId, sendOption: SendOption.None);
                    return;
                }

                string name = args.Skip(1).Join(delimiter: " ");

                if (name.Length is > 50 or < 1)
                {
                    Utils.SendMessage(GetString("Message.AllowNameLength"), player.PlayerId, sendOption: SendOption.None);
                    return;
                }

                Main.AllPlayerNames[player.PlayerId] = name;
                player.RpcSetName(name);
            }
        }
    }

    private static void LastResultCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(LastResultCommand), text);
            return;
        }

        Utils.ShowKillLog(player.PlayerId);
        Utils.ShowLastAddOns(player.PlayerId);
        Utils.ShowLastRoles(player.PlayerId);
        Utils.ShowLastResult(player.PlayerId);
    }

    private static void WinnerCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(WinnerCommand), text);
            return;
        }

        if (Main.WinnerNameList.Count == 0)
            Utils.SendMessage(GetString("NoInfoExists"), sendOption: SendOption.None);
        else
            Utils.SendMessage("<b><u>Winners:</b></u>\n" + string.Join(", ", Main.WinnerNameList));
    }

    private static void ChangeSettingCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(ChangeSettingCommand), text);
            return;
        }

        string subArgs = args.Length < 2 ? "" : args[1];

        switch (subArgs)
        {
            case "map":
                subArgs = args.Length < 3 ? "" : args[2];

                switch (subArgs)
                {
                    case "theskeld":
                        GameOptionsManager.Instance.CurrentGameOptions.SetByte(ByteOptionNames.MapId, 0);
                        break;
                    case "mirahq":
                        GameOptionsManager.Instance.CurrentGameOptions.SetByte(ByteOptionNames.MapId, 1);
                        break;
                    case "polus":
                        GameOptionsManager.Instance.CurrentGameOptions.SetByte(ByteOptionNames.MapId, 2);
                        break;
                    case "dlekseht":
                        GameOptionsManager.Instance.CurrentGameOptions.SetByte(ByteOptionNames.MapId, 3);
                        break;
                    case "airship":
                        GameOptionsManager.Instance.CurrentGameOptions.SetByte(ByteOptionNames.MapId, 4);
                        break;
                    case "thefungle":
                        GameOptionsManager.Instance.CurrentGameOptions.SetByte(ByteOptionNames.MapId, 5);
                        break;
                    case "custom":
                        subArgs = args.Length < 4 ? "" : args[3];
                        GameOptionsManager.Instance.CurrentGameOptions.SetByte(ByteOptionNames.MapId, byte.Parse(subArgs));
                        break;
                }

                break;
            case "impostors":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.currentNormalGameOptions.SetInt(Int32OptionNames.NumImpostors, int.Parse(subArgs));
                AmongUsClient.Instance.StartGame();
                break;
            case "players":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.CurrentGameOptions.SetInt(Int32OptionNames.MaxPlayers, int.Parse(subArgs));
                AmongUsClient.Instance.StartGame();
                break;
            case "recommended":
                subArgs = args.Length < 3 ? "" : args[2];

                switch (subArgs)
                {
                    case "on":
                        GameOptionsManager.Instance.CurrentGameOptions.SetBool(BoolOptionNames.IsDefaults, true);
                        break;
                    case "off":
                        GameOptionsManager.Instance.CurrentGameOptions.SetBool(BoolOptionNames.IsDefaults, false);
                        break;
                }

                break;
            case "confirmejects":
                subArgs = args.Length < 3 ? "" : args[2];

                switch (subArgs)
                {
                    case "on":
                        GameOptionsManager.Instance.currentNormalGameOptions.SetBool(BoolOptionNames.ConfirmImpostor, true);
                        break;
                    case "off":
                        GameOptionsManager.Instance.currentNormalGameOptions.SetBool(BoolOptionNames.ConfirmImpostor, false);
                        break;
                }

                break;
            case "emergencymeetings":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.currentNormalGameOptions.SetInt(Int32OptionNames.NumEmergencyMeetings, int.Parse(subArgs));
                break;
            case "anonymousvotes":
                subArgs = args.Length < 3 ? "" : args[2];

                switch (subArgs)
                {
                    case "on":
                        GameOptionsManager.Instance.currentNormalGameOptions.SetBool(BoolOptionNames.AnonymousVotes, true);
                        break;
                    case "off":
                        GameOptionsManager.Instance.currentNormalGameOptions.SetBool(BoolOptionNames.AnonymousVotes, false);
                        break;
                }

                break;
            case "emergencycooldown":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.currentNormalGameOptions.SetInt(Int32OptionNames.EmergencyCooldown, int.Parse(subArgs));
                break;
            case "discussiontime":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.currentNormalGameOptions.SetInt(Int32OptionNames.DiscussionTime, int.Parse(subArgs));
                break;
            case "votingtime":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.currentNormalGameOptions.SetInt(Int32OptionNames.VotingTime, int.Parse(subArgs));
                break;
            case "playerspeed":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.currentNormalGameOptions.SetFloat(FloatOptionNames.PlayerSpeedMod, float.Parse(subArgs));
                break;
            case "crewmatevision":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.currentNormalGameOptions.SetFloat(FloatOptionNames.CrewLightMod, float.Parse(subArgs));
                break;
            case "impostorvision":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.currentNormalGameOptions.SetFloat(FloatOptionNames.ImpostorLightMod, float.Parse(subArgs));
                break;
            case "killcooldown":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.currentNormalGameOptions.SetFloat(FloatOptionNames.KillCooldown, float.Parse(subArgs));
                break;
            case "killdistance":
                subArgs = args.Length < 3 ? "" : args[2];

                switch (subArgs)
                {
                    case "short":
                        GameOptionsManager.Instance.currentNormalGameOptions.SetInt(Int32OptionNames.KillDistance, 0);
                        break;
                    case "medium":
                        GameOptionsManager.Instance.currentNormalGameOptions.SetInt(Int32OptionNames.KillDistance, 1);
                        break;
                    case "long":
                        GameOptionsManager.Instance.currentNormalGameOptions.SetInt(Int32OptionNames.KillDistance, 2);
                        break;
                    case "custom":
                        subArgs = args.Length < 4 ? "" : args[3];
                        GameOptionsManager.Instance.currentNormalGameOptions.SetInt(Int32OptionNames.KillDistance, int.Parse(subArgs));
                        break;
                }

                break;
            case "taskbarupdates":
                subArgs = args.Length < 3 ? "" : args[2];

                GameOptionsManager.Instance.currentNormalGameOptions.TaskBarMode = subArgs switch
                {
                    "always" => AmongUs.GameOptions.TaskBarMode.Normal,
                    "meetings" => AmongUs.GameOptions.TaskBarMode.MeetingOnly,
                    "never" => AmongUs.GameOptions.TaskBarMode.Invisible,
                    _ => GameOptionsManager.Instance.currentNormalGameOptions.TaskBarMode
                };

                break;
            case "visualtasks":
                subArgs = args.Length < 3 ? "" : args[2];

                switch (subArgs)
                {
                    case "on":
                        GameOptionsManager.Instance.currentNormalGameOptions.SetBool(BoolOptionNames.VisualTasks, true);
                        break;
                    case "off":
                        GameOptionsManager.Instance.currentNormalGameOptions.SetBool(BoolOptionNames.VisualTasks, false);
                        break;
                }

                break;
            case "commontasks":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.CurrentGameOptions.SetInt(Int32OptionNames.NumCommonTasks, int.Parse(subArgs));
                break;
            case "longtasks":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.CurrentGameOptions.SetInt(Int32OptionNames.NumLongTasks, int.Parse(subArgs));
                break;
            case "shorttasks":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.CurrentGameOptions.SetInt(Int32OptionNames.NumShortTasks, int.Parse(subArgs));
                break;
            case "scientistcount":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.CurrentGameOptions.RoleOptions.SetRoleRate(RoleTypes.Scientist, int.Parse(subArgs), GameOptionsManager.Instance.CurrentGameOptions.RoleOptions.GetChancePerGame(RoleTypes.Scientist));
                break;
            case "scientistchance":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.CurrentGameOptions.RoleOptions.SetRoleRate(RoleTypes.Scientist, GameOptionsManager.Instance.CurrentGameOptions.RoleOptions.GetNumPerGame(RoleTypes.Scientist), int.Parse(subArgs));
                break;
            case "vitalsdisplaycooldown":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.CurrentGameOptions.SetFloat(FloatOptionNames.ScientistCooldown, float.Parse(subArgs));
                break;
            case "batteryduration":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.CurrentGameOptions.SetFloat(FloatOptionNames.ScientistBatteryCharge, float.Parse(subArgs));
                break;
            case "engineercount":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.currentNormalGameOptions.RoleOptions.SetRoleRate(RoleTypes.Engineer, int.Parse(subArgs), GameOptionsManager.Instance.CurrentGameOptions.RoleOptions.GetChancePerGame(RoleTypes.Engineer));
                break;
            case "engineerchance":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.CurrentGameOptions.RoleOptions.SetRoleRate(RoleTypes.Engineer, GameOptionsManager.Instance.CurrentGameOptions.RoleOptions.GetNumPerGame(RoleTypes.Engineer), int.Parse(subArgs));
                break;
            case "ventusecooldown":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.CurrentGameOptions.SetFloat(FloatOptionNames.EngineerCooldown, float.Parse(subArgs));
                break;
            case "maxtimeinvents":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.CurrentGameOptions.SetFloat(FloatOptionNames.EngineerInVentMaxTime, float.Parse(subArgs));
                break;
            case "guardianangelcount":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.CurrentGameOptions.RoleOptions.SetRoleRate(RoleTypes.GuardianAngel, int.Parse(subArgs), GameOptionsManager.Instance.CurrentGameOptions.RoleOptions.GetChancePerGame(RoleTypes.GuardianAngel));
                break;
            case "guardianangelchance":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.CurrentGameOptions.RoleOptions.SetRoleRate(RoleTypes.GuardianAngel, GameOptionsManager.Instance.CurrentGameOptions.RoleOptions.GetNumPerGame(RoleTypes.GuardianAngel), int.Parse(subArgs));
                break;
            case "protectcooldown":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.CurrentGameOptions.SetFloat(FloatOptionNames.GuardianAngelCooldown, float.Parse(subArgs));
                break;
            case "protectduration":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.CurrentGameOptions.SetFloat(FloatOptionNames.ProtectionDurationSeconds, float.Parse(subArgs));
                break;
            case "protectvisibletoimpostors":
                subArgs = args.Length < 3 ? "" : args[2];

                switch (subArgs)
                {
                    case "on":
                        GameOptionsManager.Instance.CurrentGameOptions.SetBool(BoolOptionNames.ImpostorsCanSeeProtect, true);
                        break;
                    case "off":
                        GameOptionsManager.Instance.CurrentGameOptions.SetBool(BoolOptionNames.ImpostorsCanSeeProtect, false);
                        break;
                }

                break;
            case "shapeshiftercount":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.CurrentGameOptions.RoleOptions.SetRoleRate(RoleTypes.Shapeshifter, int.Parse(subArgs), GameOptionsManager.Instance.CurrentGameOptions.RoleOptions.GetChancePerGame(RoleTypes.Shapeshifter));
                break;
            case "shapeshifterchance":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.CurrentGameOptions.RoleOptions.SetRoleRate(RoleTypes.Shapeshifter, GameOptionsManager.Instance.CurrentGameOptions.RoleOptions.GetNumPerGame(RoleTypes.Shapeshifter), int.Parse(subArgs));
                break;
            case "shapeshiftduration":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.CurrentGameOptions.SetFloat(FloatOptionNames.ShapeshifterDuration, float.Parse(subArgs));
                break;
            case "shapeshiftcooldown":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.CurrentGameOptions.SetFloat(FloatOptionNames.ShapeshifterCooldown, float.Parse(subArgs));
                break;
            case "leaveshapeshiftevidence":
                subArgs = args.Length < 3 ? "" : args[2];

                switch (subArgs)
                {
                    case "on":
                        GameOptionsManager.Instance.CurrentGameOptions.SetBool(BoolOptionNames.ShapeshifterLeaveSkin, true);
                        break;
                    case "off":
                        GameOptionsManager.Instance.CurrentGameOptions.SetBool(BoolOptionNames.ShapeshifterLeaveSkin, false);
                        break;
                }

                break;
            case "phantomcount":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.CurrentGameOptions.RoleOptions.SetRoleRate(RoleTypes.Phantom, int.Parse(subArgs), GameOptionsManager.Instance.CurrentGameOptions.RoleOptions.GetChancePerGame(RoleTypes.Phantom));
                break;
            case "phantomchance":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.CurrentGameOptions.RoleOptions.SetRoleRate(RoleTypes.Phantom, GameOptionsManager.Instance.CurrentGameOptions.RoleOptions.GetNumPerGame(RoleTypes.Phantom), int.Parse(subArgs));
                break;
            case "invisduration":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.CurrentGameOptions.SetFloat(FloatOptionNames.PhantomDuration, float.Parse(subArgs));
                break;
            case "inviscooldown":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.CurrentGameOptions.SetFloat(FloatOptionNames.PhantomCooldown, float.Parse(subArgs));
                break;
            case "noisemakercount":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.CurrentGameOptions.RoleOptions.SetRoleRate(RoleTypes.Noisemaker, int.Parse(subArgs), GameOptionsManager.Instance.CurrentGameOptions.RoleOptions.GetChancePerGame(RoleTypes.Noisemaker));
                break;
            case "noisemakerchance":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.CurrentGameOptions.RoleOptions.SetRoleRate(RoleTypes.Noisemaker, GameOptionsManager.Instance.CurrentGameOptions.RoleOptions.GetNumPerGame(RoleTypes.Noisemaker), int.Parse(subArgs));
                break;
            case "noisemakerimpostoralert":
                subArgs = args.Length < 3 ? "" : args[2];

                switch (subArgs)
                {
                    case "on":
                        GameOptionsManager.Instance.CurrentGameOptions.SetBool(BoolOptionNames.NoisemakerImpostorAlert, true);
                        break;
                    case "off":
                        GameOptionsManager.Instance.CurrentGameOptions.SetBool(BoolOptionNames.NoisemakerImpostorAlert, false);
                        break;
                }

                break;
            case "noisemakeralertduration":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.CurrentGameOptions.SetFloat(FloatOptionNames.NoisemakerAlertDuration, int.Parse(subArgs));
                break;
            case "trackercount":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.CurrentGameOptions.RoleOptions.SetRoleRate(RoleTypes.Tracker, int.Parse(subArgs), GameOptionsManager.Instance.CurrentGameOptions.RoleOptions.GetChancePerGame(RoleTypes.Tracker));
                break;
            case "trackerchance":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.CurrentGameOptions.RoleOptions.SetRoleRate(RoleTypes.Tracker, GameOptionsManager.Instance.CurrentGameOptions.RoleOptions.GetNumPerGame(RoleTypes.Tracker), int.Parse(subArgs));
                break;
            case "trackduration":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.CurrentGameOptions.SetFloat(FloatOptionNames.TrackerDuration, float.Parse(subArgs));
                break;
            case "trackcooldown":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.CurrentGameOptions.SetFloat(FloatOptionNames.TrackerCooldown, float.Parse(subArgs));
                break;
            case "trackdelay":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.CurrentGameOptions.SetFloat(FloatOptionNames.TrackerDelay, float.Parse(subArgs));
                break;
            default:
                Utils.SendMessage(GetString("Commands.ChangeSettingHelp"), player.PlayerId);
                break;
        }

        GameOptionsManager.Instance.GameHostOptions = GameOptionsManager.Instance.CurrentGameOptions;
        GameManager.Instance.LogicOptions.SyncOptions();
    }

    private static void VersionCommand(PlayerControl player, string text, string[] args)
    {
        string versionText = Main.PlayerVersion.OrderBy(pair => pair.Key).Aggregate(string.Empty, (current, kvp) => current + $"{kvp.Key}: ({Main.AllPlayerNames[kvp.Key]}) {kvp.Value.forkId}/{kvp.Value.version}({kvp.Value.tag})\n");
        if (versionText != string.Empty) FastDestroyableSingleton<HudManager>.Instance.Chat.AddChat(player, (player.FriendCode.GetDevUser().HasTag() ? "\n" : string.Empty) + versionText);
    }

    private static void LTCommand(PlayerControl player, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(nameof(LTCommand), text);
            return;
        }

        if (!GameStates.IsLobby) return;

        float timer = GameStartManagerPatch.Timer;
        int minutes = (int)timer / 60;
        int seconds = (int)timer % 60;
        string lt = string.Format(GetString("LobbyCloseTimer"), $"{minutes:00}:{seconds:00}");
        if (timer <= 60) lt = Utils.ColorString(Color.red, lt);

        Utils.SendMessage(lt, player.PlayerId);
    }

    // -------------------------------------------------------------------------------------------------------------------------

    private static bool CheckMute(byte id)
    {
        if (!MutedPlayers.TryGetValue(id, out (long MuteTimeStamp, int Duration) mute)) return false;

        long timeLeft = mute.Duration - (Utils.TimeStamp - mute.MuteTimeStamp);

        if (timeLeft <= 0)
        {
            MutedPlayers.Remove(id);
            return false;
        }

        Utils.SendMessage("\n", id, string.Format(GetString("MuteMessage"), timeLeft));
        return true;
    }

    private static string FixRoleNameInput(string text)
    {
        text = text.Replace("着", "者").Trim().ToLower();

        return text switch
        {
            "管理員" or "管理" or "gm" => GetString("GM"),
            "賞金獵人" or "赏金" or "bh" or "bounty" => GetString("BountyHunter"),
            "自爆兵" or "自爆" => GetString("Bomber"),
            "邪惡的追踪者" or "邪恶追踪者" or "追踪" or "et" => GetString("EvilTracker"),
            "煙花商人" or "烟花" or "fw" => GetString("FireWorks"),
            "夢魘" or "夜魇" => GetString("Mare"),
            "詭雷" => GetString("BoobyTrap"),
            "黑手黨" or "黑手" => GetString("Mafia"),
            "嗜血殺手" or "嗜血" or "sk" => GetString("SerialKiller"),
            "千面鬼" or "千面" => GetString("ShapeMaster"),
            "狂妄殺手" or "狂妄" or "arr" => GetString("Sans"),
            "殺戮機器" or "杀戮" or "机器" or "杀戮兵器" or "km" => GetString("Minimalism"),
            "蝕時者" or "蚀时" or "偷时" or "tt" => GetString("TimeThief"),
            "狙擊手" or "狙击" => GetString("Sniper"),
            "傀儡師" or "傀儡" => GetString("Puppeteer"),
            "殭屍" or "丧尸" => GetString("Zombie"),
            "吸血鬼" or "吸血" or "vamp" => GetString("Vampire"),
            "術士" => GetString("Warlock"),
            "駭客" or "黑客" => GetString("Hacker"),
            "刺客" or "忍者" => GetString("Assassin"),
            "礦工" => GetString("Miner"),
            "逃逸者" or "逃逸" => GetString("Escapee"),
            "女巫" => GetString("Witch"),
            "監視者" or "监管" or "aa" => GetString("AntiAdminer"),
            "清道夫" or "清道" or "scav" => GetString("Scavenger"),
            "窺視者" or "窥视" => GetString("Watcher"),
            "誘餌" or "大奖" or "头奖" => GetString("Bait"),
            "擺爛人" or "摆烂" => GetString("Needy"),
            "獨裁者" or "独裁" or "dict" => GetString("Dictator"),
            "法醫" or "doc" => GetString("Doctor"),
            "偵探" or "det" => GetString("Detective"),
            "幸運兒" or "幸运" => GetString("Luckey"),
            "大明星" or "明星" or "ss" => GetString("SuperStar"),
            "網紅" or "cel" or "celeb" => GetString("CyberStar"),
            "demo" => GetString("Demolitionist"),
            "俠客" => GetString("SwordsMan"),
            "正義賭怪" or "正义的赌怪" or "好赌" or "正义赌" or "ng" => GetString("NiceGuesser"),
            "邪惡賭怪" or "邪恶的赌怪" or "坏赌" or "恶赌" or "邪恶赌" or "赌怪" or "eg" => GetString("EvilGuesser"),
            "市長" or "逝长" => GetString("Mayor"),
            "被害妄想症" or "被害妄想" or "被迫害妄想症" or "被害" or "妄想" or "妄想症" => GetString("Paranoia"),
            "愚者" or "愚" => GetString("Psychic"),
            "修理大师" or "修理" or "维修" or "sm" => GetString("SabotageMaster"),
            "警長" => GetString("Sheriff"),
            "告密者" or "告密" => GetString("Snitch"),
            "增速者" or "增速" => GetString("SpeedBooster"),
            "時間操控者" or "时间操控人" or "时间操控" or "tm" => GetString("TimeManager"),
            "陷阱師" or "陷阱" or "小奖" => GetString("Trapper"),
            "傳送師" or "传送" or "trans" => GetString("Transporter"),
            "縱火犯" or "纵火" or "arso" => GetString("Arsonist"),
            "處刑人" or "处刑" or "exe" => GetString("Executioner"),
            "小丑" or "丑皇" or "jest" => GetString("Jester"),
            "投機者" or "投机" or "oppo" => GetString("Opportunist"),
            "馬里奧" or "马力欧" => GetString("Mario"),
            "恐怖分子" or "恐怖" or "terro" => GetString("Terrorist"),
            "豺狼" or "蓝狼" or "狼" => GetString("Jackal"),
            "神" or "上帝" => GetString("God"),
            "情人" or "愛人" or "链子" or "老婆" or "老公" or "lover" => GetString("Lovers"),
            "絕境者" or "绝境" or "last" or "lastimp" or "last imp" or "Last" => GetString("LastImpostor"),
            "閃電俠" or "闪电" => GetString("Flashman"),
            "靈媒" => GetString("Seer"),
            "破平者" or "破平" => GetString("Brakar"),
            "執燈人" or "执灯" or "灯人" => GetString("Torch"),
            "膽小" or "胆小" or "obli" => GetString("Oblivious"),
            "迷惑者" or "迷幻" or "bew" => GetString("Bewilder"),
            "sun" => GetString("Sunglasses"),
            "蠢蛋" or "笨蛋" or "蠢狗" or "傻逼" => GetString("Fool"),
            "冤罪師" or "冤罪" or "inno" => GetString("Innocent"),
            "資本家" or "资本主义" or "资本" or "cap" or "capi" => GetString("Capitalism"),
            "老兵" or "vet" => GetString("Veteran"),
            "加班狂" or "加班" => GetString("Workhorse"),
            "復仇者" or "复仇" => GetString("Avanger"),
            "鵜鶘" or "pel" or "peli" => GetString("Pelican"),
            "保鏢" or "bg" => GetString("Bodyguard"),
            "up" or "up主" or "yt" => GetString("Youtuber"),
            "利己主義者" or "利己主义" or "利己" or "ego" => GetString("Egoist"),
            "贗品商" or "赝品" => GetString("Counterfeiter"),
            "擲雷兵" or "掷雷" or "闪光弹" or "gren" or "grena" => GetString("Grenadier"),
            "竊票者" or "偷票" or "偷票者" or "窃票师" or "窃票" => GetString("TicketsStealer"),
            "教父" => GetString("Gangster"),
            "革命家" or "革命" or "revo" => GetString("Revolutionist"),
            "fff團" or "fff" or "fff团" => GetString("FFF"),
            "清理工" or "清潔工" or "清洁工" or "清理" or "清洁" or "janitor" => GetString("Cleaner"),
            "醫生" => GetString("Medicaler"),
            "占卜師" or "占卜" or "ft" => GetString("Divinator"),
            "雙重人格" or "双重" or "双人格" or "人格" or "schizo" or "scizo" or "shizo" => GetString("DualPersonality"),
            "玩家" => GetString("Gamer"),
            "情報販子" or "情报" or "贩子" => GetString("Messenger"),
            "球狀閃電" or "球闪" or "球状" => GetString("BallLightning"),
            "潛藏者" or "潜藏" => GetString("DarkHide"),
            "貪婪者" or "贪婪" => GetString("Greedier"),
            "工作狂" or "工作" or "worka" => GetString("Workaholic"),
            "呪狼" or "咒狼" or "cw" => GetString("CursedWolf"),
            "寶箱怪" or "宝箱" => GetString("Mimic"),
            "集票者" or "集票" or "寄票" or "机票" => GetString("Collector"),
            "活死人" or "活死" => GetString("Glitch"),
            "奪魂者" or "多混" or "夺魂" or "sc" => GetString("ImperiusCurse"),
            "自爆卡車" or "自爆" or "卡车" or "provo" => GetString("Provocateur"),
            "快槍手" or "快枪" or "qs" => GetString("QuickShooter"),
            "隱蔽者" or "隐蔽" or "小黑人" => GetString("Concealer"),
            "抹除者" or "抹除" => GetString("Eraser"),
            "肢解者" or "肢解" => GetString("OverKiller"),
            "劊子手" or "侩子手" or "柜子手" => GetString("Hangman"),
            "陽光開朗大男孩" or "阳光" or "开朗" or "大男孩" or "阳光开朗" or "开朗大男孩" or "阳光大男孩" or "sunny" => GetString("Sunnyboy"),
            "法官" or "审判" => GetString("Judge"),
            "入殮師" or "入检师" or "入殓" or "mor" => GetString("Mortician"),
            "通靈師" or "通灵" => GetString("Mediumshiper"),
            "吟游詩人" or "诗人" => GetString("Bard"),
            "隱匿者" or "隐匿" or "隐身" or "隐身人" or "印尼" => GetString("Swooper"),
            "船鬼" or "cp" => GetString("Crewpostor"),
            "嗜血騎士" or "血骑" or "骑士" or "bk" => GetString("BloodKnight"),
            "賭徒" => GetString("Totocalcio"),
            "分散机" => GetString("Disperser"),
            "和平之鸽" or "和平之鴿" or "和平的鸽子" or "和平" or "dop" or "dove of peace" => GetString("DovesOfNeace"),
            "持槍" or "持械" or "手长" => GetString("Reach"),
            "monarch" => GetString("Monarch"),
            "sch" => GetString("SchrodingersCat"),
            "glitch" => GetString("Glitch"),
            "безумный" or "mad" => GetString("Madmate"),
            "анти админер" or "anti adminer" => GetString("AntiAdminer"),
            _ => text
        };
    }

    public static bool GetRoleByName(string name, out CustomRoles role)
    {
        role = new();
        if (name == "") return false;

        if ((TranslationController.InstanceExists ? TranslationController.Instance.currentLanguage.languageID : SupportedLangs.SChinese) == SupportedLangs.SChinese)
        {
            Regex r = new("[\u4e00-\u9fa5]+$");
            MatchCollection mc = r.Matches(name);
            var result = string.Empty;

            for (var i = 0; i < mc.Count; i++)
            {
                if (mc[i].ToString() == "是") continue;

                result += mc[i]; //匹配结果是完整的数字，此处可以不做拼接的
            }

            name = FixRoleNameInput(result.Replace("是", string.Empty).Trim());
        }
        else
            name = name.Trim().ToLower();

        foreach (CustomRoles rl in Enum.GetValues<CustomRoles>())
        {
            if (rl.IsVanilla()) continue;

            string roleName = GetString(rl.ToString()).ToLower().Trim().Replace(" ", string.Empty);
            string nameWithoutId = Regex.Replace(name.Replace(" ", string.Empty), @"^\d+", string.Empty);

            if (nameWithoutId == roleName)
            {
                role = rl;
                return true;
            }
        }

        return false;
    }

    private static void SendRolesInfo(string role, byte playerId, bool isDev = false, bool isUp = false)
    {
        if (!CustomGameMode.Standard.IsActiveOrIntegrated())
        {
            string text = GetString($"ModeDescribe.{Options.CurrentGameMode}");
            bool allInOne = Options.CurrentGameMode == CustomGameMode.AllInOne;
            Utils.SendMessage(allInOne ? "\n" : text, playerId, allInOne ? text : "", sendOption: SendOption.None);
            if (!CustomGameMode.HideAndSeek.IsActiveOrIntegrated()) return;
        }

        role = role.Trim().ToLower();
        if (role.StartsWith("/r")) _ = role.Replace("/r", string.Empty);
        if (role.StartsWith("/up")) _ = role.Replace("/up", string.Empty);
        if (role.EndsWith("\r\n")) _ = role.Replace("\r\n", string.Empty);
        if (role.EndsWith("\n")) _ = role.Replace("\n", string.Empty);

        if (role == "")
        {
            Utils.ShowActiveRoles(playerId);
            return;
        }

        role = FixRoleNameInput(role).ToLower().Trim().Replace(" ", string.Empty);

        foreach (CustomRoles rl in Enum.GetValues<CustomRoles>())
        {
            if (rl.IsVanilla()) continue;

            string roleName = GetString(rl.ToString());

            if (role == roleName.ToLower().Trim().TrimStart('*').Replace(" ", string.Empty))
            {
                if ((isDev || isUp) && GameStates.IsLobby)
                {
                    var devMark = "▲";
                    if (rl.IsAdditionRole() || rl is CustomRoles.GM) devMark = string.Empty;

                    if (rl.GetCount() < 1 || rl.GetMode() == 0) devMark = string.Empty;

                    if (isUp) Utils.SendMessage(devMark == "▲" ? string.Format(GetString("Message.YTPlanSelected"), roleName) : string.Format(GetString("Message.YTPlanSelectFailed"), roleName), playerId, sendOption: SendOption.None);

                    if (isUp) return;
                }

                StringBuilder sb = new();
                var title = $"<{Main.RoleColors[rl]}>{roleName}</color> {Utils.GetRoleMode(rl)}";
                StringBuilder settings = new();
                sb.Append(GetString($"{rl}InfoLong").TrimStart());
                if (Options.CustomRoleSpawnChances.TryGetValue(rl, out StringOptionItem chance)) AddSettings(chance);

                if (rl is CustomRoles.LovingCrewmate or CustomRoles.LovingImpostor && Options.CustomRoleSpawnChances.TryGetValue(CustomRoles.Lovers, out chance)) AddSettings(chance);

                string txt = $"<size=90%>{sb}</size>".Replace(roleName, rl.ToColoredString()).Replace(roleName.ToLower(), rl.ToColoredString());
                sb.Clear().Append(txt);

                if (rl.PetActivatedAbility()) sb.Append($"<size=50%>{GetString("SupportsPetMessage")}</size>");

                if (settings.Length > 0) Utils.SendMessage("\n", playerId, settings.ToString());

                Utils.SendMessage(sb.ToString(), playerId, title);
                return;

                void AddSettings(StringOptionItem stringOptionItem)
                {
                    settings.AppendLine($"<size=70%><u>{GetString("SettingsForRoleText")} <{Main.RoleColors[rl]}>{roleName}</color>:</u>");
                    Utils.ShowChildrenSettings(stringOptionItem, ref settings, disableColor: false);
                    settings.Append("</size>");
                }
            }
        }

        foreach (CustomGameMode gameMode in Enum.GetValues<CustomGameMode>())
        {
            string gmString = GetString(gameMode.ToString());
            string match = gmString.ToLower().Trim().TrimStart('*').Replace(" ", string.Empty);

            if (role.Equals(match, StringComparison.OrdinalIgnoreCase))
            {
                string text = GetString($"ModeDescribe.{gameMode}");
                bool allInOne = gameMode == CustomGameMode.AllInOne;
                Utils.SendMessage(allInOne ? "\n" : text, playerId, allInOne ? text : gmString, sendOption: SendOption.None);
                return;
            }
        }

        Utils.SendMessage(isUp ? GetString("Message.YTPlanCanNotFindRoleThePlayerEnter") : GetString("Message.CanNotFindRoleThePlayerEnter"), playerId, sendOption: SendOption.None);
    }

    // -------------------------------------------------------------------------------------------------------------------------

    public static void OnReceiveChat(PlayerControl player, string text, out bool canceled)
    {
        canceled = false;
        if (!AmongUsClient.Instance.AmHost || player.IsHost()) return;

        long now = Utils.TimeStamp;

        if (LastSentCommand.TryGetValue(player.PlayerId, out long ts) && ts + 2 >= now)
        {
            Logger.Warn("Chat message ignored, it was sent too soon after their last message", "ReceiveChat");
            return;
        }

        if (GameStates.InGame && (Silencer.ForSilencer.Contains(player.PlayerId) || (Main.PlayerStates[player.PlayerId].Role is Dad { IsEnable: true } dad && dad.UsingAbilities.Contains(Dad.Ability.GoForMilk))) && player.IsAlive())
        {
            ChatManager.SendPreviousMessagesToAll();
            canceled = true;
            LastSentCommand[player.PlayerId] = now;
            return;
        }

        if (text.StartsWith("\n")) text = text[1..];

        CheckAnagramGuess(player.PlayerId, text.ToLower());

        string[] args = text.Split(' ');

        if (!Starspawn.IsDayBreak)
        {
            if (GuessManager.GuesserMsg(player, text) ||
                Judge.TrialMsg(player, text) ||
                NiceSwapper.SwapMsg(player, text) ||
                ParityCop.ParityCheckMsg(player, text) ||
                Councillor.MurderMsg(player, text))
            {
                canceled = true;
                LastSentCommand[player.PlayerId] = now;
                return;
            }

            if (Mediumshiper.MsMsg(player, text) || Mafia.MafiaMsgCheck(player, text))
            {
                LastSentCommand[player.PlayerId] = now;
                return;
            }
        }

        var commandEntered = false;

        if (text.StartsWith('/') && (!GameStates.IsMeeting || MeetingHud.Instance.state is not MeetingHud.VoteStates.Results and not MeetingHud.VoteStates.Proceeding))
        {
            foreach (Command command in AllCommands)
            {
                if (!command.IsThisCommand(text)) continue;

                Logger.Info($" Recognized command: {text}", "ReceiveChat");
                commandEntered = true;

                if (!command.CanUseCommand(player, sendErrorMessage: true))
                {
                    canceled = true;
                    break;
                }

                command.Action(player, text, args);
                if (command.IsCanceled) canceled = command.AlwaysHidden || !Options.HostSeesCommandsEnteredByOthers.GetBool();
                break;
            }
        }

        if (CheckMute(player.PlayerId))
        {
            canceled = true;
            ChatManager.SendPreviousMessagesToAll();
            return;
        }

        if (GameStates.IsInGame && !ChatUpdatePatch.LoversMessage && (player.IsAlive() || ExileController.Instance) && Lovers.PrivateChat.GetBool() && (ExileController.Instance || !GameStates.IsMeeting) && Options.CurrentGameMode == CustomGameMode.Standard)
        {
            ChatManager.SendPreviousMessagesToAll(true);
            canceled = true;

            if (player.Is(CustomRoles.Lovers) || player.GetCustomRole() is CustomRoles.LovingCrewmate or CustomRoles.LovingImpostor)
            {
                PlayerControl otherLover = Main.LoversPlayers.FirstOrDefault(x => x.PlayerId != player.PlayerId);

                if (otherLover != null)
                {
                    LateTask.New(() =>
                    {
                        string title = player.GetRealName();
                        ChatUpdatePatch.LoversMessage = true;
                        var sender = CustomRpcSender.Create("LoversChat", SendOption.Reliable);
                        sender = Utils.SendMessage(text, otherLover.PlayerId, title, writer: sender, multiple: true);
                        sender = Utils.SendMessage(text, player.PlayerId, title, writer: sender, multiple: true);
                        sender.Notify(otherLover, $"<size=80%><{Main.RoleColors[CustomRoles.Lovers]}>[\u2665]</color> {text}</size>", 8f);
                        sender.SendMessage();
                        LateTask.New(() => ChatUpdatePatch.LoversMessage = false, Math.Max(AmongUsClient.Instance.Ping / 1000f * 2f, Main.MessageWait.Value + 0.5f), log: false);
                    }, 0.2f, log: false);
                }
            }
            else
                LateTask.New(() => Utils.SendMessage(GetString("LoversChatCannotTalkMsg"), player.PlayerId, GetString("LoversChatCannotTalkTitle")), 0.5f, log: false);
        }

        if (!canceled) ChatManager.SendMessage(player, text);

        if (commandEntered) LastSentCommand[player.PlayerId] = now;

        SpamManager.CheckSpam(player, text);
    }
}

[HarmonyPatch(typeof(ChatController), nameof(ChatController.Update))]
internal static class ChatUpdatePatch
{
    public static bool LoversMessage;
    public static readonly List<(string Text, byte SendTo, string Title, long SendTimeStamp)> LastMessages = [];

    public static void Postfix(ChatController __instance)
    {
        var chatBubble = __instance.chatBubblePool.Prefab.CastFast<ChatBubble>();
        chatBubble.TextArea.overrideColorTags = false;

        if (Main.DarkTheme.Value)
        {
            chatBubble.TextArea.color = Color.white;
            chatBubble.Background.color = new(0.1f, 0.1f, 0.1f, 1f);
        }

        LastMessages.RemoveAll(x => Utils.TimeStamp - x.SendTimeStamp > 10);
    }

    internal static bool SendLastMessages(ref CustomRpcSender sender)
    {
        PlayerControl player = GameStates.IsLobby ? Main.AllPlayerControls.Without(PlayerControl.LocalPlayer).RandomElement() : Main.AllAlivePlayerControls.MinBy(x => x.PlayerId) ?? Main.AllPlayerControls.MinBy(x => x.PlayerId) ?? PlayerControl.LocalPlayer;
        if (player == null) return false;

        bool wasCleared = false;

        foreach ((string msg, byte sendTo, string title, _) in LastMessages)
            wasCleared = SendMessage(player, msg, sendTo, title, ref sender);

        return LastMessages.Count > 0 && !wasCleared;
    }

    private static bool SendMessage(PlayerControl player, string msg, byte sendTo, string title, ref CustomRpcSender sender)
    {
        int clientId = sendTo == byte.MaxValue ? -1 : Utils.GetPlayerById(sendTo).GetClientId();

        string name = player.Data.PlayerName;

        if (clientId == -1)
        {
            player.SetName(title);
            FastDestroyableSingleton<HudManager>.Instance.Chat.AddChat(player, msg);
            player.SetName(name);
        }

        sender.AutoStartRpc(player.NetId, (byte)RpcCalls.SetName, clientId)
            .Write(player.Data.NetId)
            .Write(title)
            .EndRpc();

        sender.AutoStartRpc(player.NetId, (byte)RpcCalls.SendChat, clientId)
            .Write(msg)
            .EndRpc();

        sender.AutoStartRpc(player.NetId, (byte)RpcCalls.SetName, clientId)
            .Write(player.Data.NetId)
            .Write(player.Data.PlayerName)
            .EndRpc();

        if (sender.stream.Length > 800)
        {
            sender.SendMessage();
            sender = CustomRpcSender.Create(sender.name, sender.sendOption);
            return true;
        }

        return false;
    }
}

[HarmonyPatch(typeof(FreeChatInputField), nameof(FreeChatInputField.Awake))]
internal static class FreeChatFieldAwakePatch
{
    public static void Postfix(FreeChatInputField __instance)
    {
        UpdateCharCountPatch.Postfix(__instance);
    }
}

[HarmonyPatch(typeof(FreeChatInputField), nameof(FreeChatInputField.UpdateCharCount))]
internal static class UpdateCharCountPatch
{
    public static void Postfix(FreeChatInputField __instance)
    {
        int length = __instance.textArea.text.Length;
        __instance.charCountText.SetText(length <= 0 ? GetString("ThankYouForUsingEHR") : $"{length}/{__instance.textArea.characterLimit}");
        __instance.charCountText.enableWordWrapping = false;

        __instance.charCountText.color = length switch
        {
            < 1000 => Color.black,
            < 1200 => new(1f, 1f, 0f, 1f),
            _ => Color.red
        };
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
internal static class RpcSendChatPatch
{
    public static bool Prefix(PlayerControl __instance, string chatText, ref bool __result)
    {
        if (string.IsNullOrWhiteSpace(chatText))
        {
            __result = false;
            return false;
        }

        int return_count = PlayerControl.LocalPlayer.name.Count(x => x == '\n');
        chatText = new StringBuilder(chatText).Insert(0, "\n", return_count).ToString();
        if (AmongUsClient.Instance.AmClient && FastDestroyableSingleton<HudManager>.Instance) FastDestroyableSingleton<HudManager>.Instance.Chat.AddChat(__instance, chatText);

        if (chatText.Contains("who", StringComparison.OrdinalIgnoreCase)) FastDestroyableSingleton<UnityTelemetry>.Instance.SendWho();

        MessageWriter messageWriter = AmongUsClient.Instance.StartRpc(__instance.NetId, (byte)RpcCalls.SendChat, SendOption.None);
        messageWriter.Write(chatText);
        messageWriter.EndMessage();
        __result = true;
        return false;
    }
}