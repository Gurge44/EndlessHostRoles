using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AmongUs.GameOptions;
using Assets.CoreScripts;
using EHR.AddOns.Common;
using EHR.Crewmate;
using EHR.Impostor;
using EHR.Modules;
using EHR.Neutral;
using EHR.Patches;
using HarmonyLib;
using Hazel;
using InnerNet;
using UnityEngine;
using UnityEngine.Networking;
using static EHR.Translator;

// ReSharper disable InconsistentNaming

namespace EHR;

internal class Command(string[] commandForms, string arguments, string description, Command.UsageLevels usageLevel, Command.UsageTimes usageTime, Action<PlayerControl, string, string, string[]> action, bool isCanceled, bool alwaysHidden, string[] argsDescriptions = null)
{
    public enum UsageLevels
    {
        Everyone,
        Modded,
        Host,
        HostOrModerator,
        HostOrAdmin
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

    public static Dictionary<string, Command> AllCommands = [];

    public string[] CommandForms => commandForms;
    public string Arguments => arguments;
    public string Description => description;
    public string[] ArgsDescriptions => argsDescriptions ?? [];
    public UsageLevels UsageLevel => usageLevel;
    public UsageTimes UsageTime => usageTime;
    public Action<PlayerControl, string, string, string[]> Action => action;
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
            case UsageLevels.HostOrAdmin when !pc.IsHost() && AmongUsClient.Instance.AmHost && !ChatCommands.IsPlayerAdmin(pc.FriendCode):
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

    private static readonly Dictionary<char, int> PollVotes = [];
    private static readonly Dictionary<char, string> PollAnswers = [];
    private static readonly List<byte> PollVoted = [];
    private static float PollTimer = 45f;
    private static List<CustomGameMode> GMPollGameModes = [];
    private static List<MapNames> MPollMaps = [];

    public static readonly Dictionary<byte, (long MuteTimeStamp, int Duration)> MutedPlayers = [];

    public static Dictionary<byte, List<CustomRoles>> DraftRoles = [];
    public static Dictionary<byte, CustomRoles> DraftResult = [];

    public static readonly HashSet<byte> Spectators = [];
    public static readonly HashSet<byte> LastSpectators = [];
    public static readonly HashSet<byte> ForcedSpectators = [];

    private static HashSet<byte> ReadyPlayers = [];
    public static HashSet<byte> VotedToStart = [];

    private static string CurrentAnagram = string.Empty;

    public static bool HasMessageDuringEjectionScreen;

    public static void LoadCommands()
    {
        Command.AllCommands = new Dictionary<string, Command>
        {
            ["Command.LT"] = new(["lt", "лт", "大厅关闭时间"], "", GetString("CommandDescription.LT"), Command.UsageLevels.Everyone, Command.UsageTimes.InLobby, LTCommand, false, false),
            ["Command.Dump"] = new(["dump", "дамп", "лог", "导出日志"], "", GetString("CommandDescription.Dump"), Command.UsageLevels.Modded, Command.UsageTimes.Always, DumpCommand, false, false),
            ["Command.Version"] = new(["v", "version", "в", "версия", "检查版本", "versão"], "", GetString("CommandDescription.Version"), Command.UsageLevels.Modded, Command.UsageTimes.Always, VersionCommand, false, false),
            ["Command.ChangeSetting"] = new(["cs", "changesetting", "измнастр", "修改设置", "mudarconfig", "mudarconfiguração"], "{name} {?} [?]", GetString("CommandDescription.ChangeSetting"), Command.UsageLevels.Host, Command.UsageTimes.InLobby, ChangeSettingCommand, true, false, [GetString("CommandArgs.ChangeSetting.Name"), GetString("CommandArgs.ChangeSetting.UnknownValue"), GetString("CommandArgs.ChangeSetting.UnknownValue")]),
            ["Command.Winner"] = new(["win", "winner", "победители", "获胜者", "vencedor"], "", GetString("CommandDescription.Winner"), Command.UsageLevels.Everyone, Command.UsageTimes.InLobby, WinnerCommand, true, false),
            ["Command.LastResult"] = new(["l", "lastresult", "л", "对局职业信息", "resultados", "ultimoresultado"], "", GetString("CommandDescription.LastResult"), Command.UsageLevels.Everyone, Command.UsageTimes.InLobby, LastResultCommand, true, false),
            ["Command.Rename"] = new(["rn", "rename", "name", "рн", "ренейм", "переименовать", "修改名称", "renomear"], "{name}", GetString("CommandDescription.Rename"), Command.UsageLevels.Everyone, Command.UsageTimes.InLobby, RenameCommand, true, false, [GetString("CommandArgs.Rename.Name")]),
            ["Command.HideName"] = new(["hn", "hidename", "хн", "спрник", "隐藏姓名", "semnome", "escondernome"], "", GetString("CommandDescription.HideName"), Command.UsageLevels.Host, Command.UsageTimes.InLobby, HideNameCommand, true, false),
            ["Command.Level"] = new(["level", "лвл", "уровень", "修改等级", "nível"], "{level}", GetString("CommandDescription.Level"), Command.UsageLevels.Host, Command.UsageTimes.InLobby, LevelCommand, true, false, [GetString("CommandArgs.Level.Level")]),
            ["Command.Now"] = new(["n", "now", "н", "当前设置", "atual"], "", GetString("CommandDescription.Now"), Command.UsageLevels.Everyone, Command.UsageTimes.Always, NowCommand, true, false),
            ["Command.Disconnect"] = new(["dis", "disconnect", "дис", "断连"], "{team}", GetString("CommandDescription.Disconnect"), Command.UsageLevels.Host, Command.UsageTimes.InGame, DisconnectCommand, true, false, [GetString("CommandArgs.Disconnect.Team")]),
            ["Command.R"] = new(["r", "р", "função"], "[role]", GetString("CommandDescription.R"), Command.UsageLevels.Everyone, Command.UsageTimes.Always, RCommand, true, false, [GetString("CommandArgs.R.Role")]),
            ["Command.Up"] = new(["up", "指定"], "{role}", GetString("CommandDescription.Up"), Command.UsageLevels.Host, Command.UsageTimes.InLobby, UpCommand, true, false, [GetString("CommandArgs.Up.Role")]),
            ["Command.SetRole"] = new(["setrole", "setaddon", "сетроль", "预设职业", "definir-função"], "{id} {role}", GetString("CommandDescription.SetRole"), Command.UsageLevels.Host, Command.UsageTimes.InLobby, SetRoleCommand, true, false, [GetString("CommandArgs.SetRole.Id"), GetString("CommandArgs.SetRole.Role")]),
            ["Command.Help"] = new(["h", "help", "хэлп", "хелп", "помощь", "帮助", "ajuda"], "", GetString("CommandDescription.Help"), Command.UsageLevels.Everyone, Command.UsageTimes.Always, HelpCommand, true, false),
            ["Command.KCount"] = new(["gamestate", "gstate", "gs", "kcount", "kc", "кубийц", "гс", "статигры", "对局状态", "estadojogo", "status"], "", GetString("CommandDescription.KCount"), Command.UsageLevels.Everyone, Command.UsageTimes.InGame, KCountCommand, true, false),
            ["Command.AddMod"] = new(["addmod", "добмодера", "指定协管", "moderador-add"], "{id}", GetString("CommandDescription.AddMod"), Command.UsageLevels.Host, Command.UsageTimes.Always, AddModCommand, true, false, [GetString("CommandArgs.AddMod.Id")]),
            ["Command.DeleteMod"] = new(["deletemod", "убрмодера", "удмодера", "убратьмодера", "удалитьмодера", "移除协管", "moderador-remover"], "{id}", GetString("CommandDescription.DeleteMod"), Command.UsageLevels.Host, Command.UsageTimes.Always, DeleteModCommand, true, false, [GetString("CommandArgs.DeleteMod.Id")]),
            ["Command.Combo"] = new(["combo", "комбо", "设置不会同时出现的职业", "combinação", "combinar"], "{mode} {role} {addon} [all]", GetString("CommandDescription.Combo"), Command.UsageLevels.Everyone, Command.UsageTimes.Always, ComboCommand, true, false, [GetString("CommandArgs.Combo.Mode"), GetString("CommandArgs.Combo.Role"), GetString("CommandArgs.Combo.Addon"), GetString("CommandArgs.Combo.All")]),
            ["Command.Effect"] = new(["eff", "effect", "эффект", "效果", "efeito"], "{effect}", GetString("CommandDescription.Effect"), Command.UsageLevels.Host, Command.UsageTimes.InGame, EffectCommand, true, false, [GetString("CommandArgs.Effect.Effect")]),
            ["Command.AFKExempt"] = new(["afkexempt", "освафк", "афкосв", "挂机检测器不会检测", "afk-isentar"], "{id}", GetString("CommandDescription.AFKExempt"), Command.UsageLevels.HostOrAdmin, Command.UsageTimes.Always, AFKExemptCommand, true, false, [GetString("CommandArgs.AFKExempt.Id")]),
            ["Command.MyRole"] = new(["m", "myrole", "м", "мояроль", "我的职业", "minhafunção"], "", GetString("CommandDescription.MyRole"), Command.UsageLevels.Everyone, Command.UsageTimes.InGame, MyRoleCommand, true, false),
            ["Command.TPOut"] = new(["tpout", "тпаут", "传送出"], "", GetString("CommandDescription.TPOut"), Command.UsageLevels.Everyone, Command.UsageTimes.InLobby, TPOutCommand, true, false),
            ["Command.TPIn"] = new(["tpin", "тпин", "传送进"], "", GetString("CommandDescription.TPIn"), Command.UsageLevels.Everyone, Command.UsageTimes.InLobby, TPInCommand, true, false),
            ["Command.Template"] = new(["t", "template", "т", "темплейт", "模板"], "{tag}", GetString("CommandDescription.Template"), Command.UsageLevels.Everyone, Command.UsageTimes.Always, TemplateCommand, true, false, [GetString("CommandArgs.Template.Tag")]),
            ["Command.MessageWait"] = new(["mw", "messagewait", "мв", "медленныйрежим", "消息冷却", "espera-mensagens"], "{duration}", GetString("CommandDescription.MessageWait"), Command.UsageLevels.Host, Command.UsageTimes.Always, MessageWaitCommand, true, false, [GetString("CommandArgs.MessageWait.Duration")]),
            ["Command.Death"] = new(["death", "d", "д", "смерть", "死亡原因", "abate"], "[id]", GetString("CommandDescription.Death"), Command.UsageLevels.Everyone, Command.UsageTimes.AfterDeath, DeathCommand, true, false, [GetString("CommandArgs.Death.Id")]),
            ["Command.Say"] = new(["say", "s", "сказать", "с", "说", "falar", "dizer"], "{message}", GetString("CommandDescription.Say"), Command.UsageLevels.HostOrModerator, Command.UsageTimes.Always, SayCommand, true, false, [GetString("CommandArgs.Say.Message")]),
            ["Command.Vote"] = new(["vote", "голос", "投票给", "votar"], "{id}", GetString("CommandDescription.Vote"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, VoteCommand, true, true, [GetString("CommandArgs.Vote.Id")]),
            ["Command.Ask"] = new(["ask", "спр", "спросить", "数学家提问", "perguntar"], "{number1} {number2}", GetString("CommandDescription.Ask"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, AskCommand, true, true, [GetString("CommandArgs.Ask.Number1"), GetString("CommandArgs.Ask.Number2")]),
            ["Command.Answer"] = new(["ans", "answer", "отв", "ответить", "回答数学家问题", "responder"], "{number}", GetString("CommandDescription.Answer"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, AnswerCommand, true, false, [GetString("CommandArgs.Answer.Number")]),
            ["Command.QA"] = new(["qa", "вопротв", "回答测验大师问题", "questão-responder"], "{letter}", GetString("CommandDescription.QA"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, QACommand, true, false, [GetString("CommandArgs.QA.Letter")]),
            ["Command.QS"] = new(["qs", "вопрпоказать", "检查测验大师问题", "questão-ver"], "", GetString("CommandDescription.QS"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, QSCommand, true, false),
            ["Command.Target"] = new(["target", "цель", "腹语者标记", "alvo"], "{id}", GetString("CommandDescription.Target"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, TargetCommand, true, true, [GetString("CommandArgs.Target.Id")]),
            ["Command.Chat"] = new(["chat", "сообщение", "腹语者发送消息"], "{message}", GetString("CommandDescription.Chat"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, ChatCommand, true, true, [GetString("CommandArgs.Chat.Message")]),
            ["Command.Check"] = new(["check", "проверить", "检查", "veificar"], "{id} {role}", GetString("CommandDescription.Check"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, CheckCommand, true, true, [GetString("CommandArgs.Check.Id"), GetString("CommandArgs.Check.Role")]),
            ["Command.Ban"] = new(["ban", "kick", "бан", "кик", "забанить", "кикнуть", "封禁", "踢出", "banir", "expulsar"], "{id} [reason]", GetString("CommandDescription.Ban"), Command.UsageLevels.HostOrModerator, Command.UsageTimes.Always, BanKickCommand, true, false, [GetString("CommandArgs.Ban.Id"), GetString("CommandArgs.Ban.Reason")]),
            ["Command.Exe"] = new(["exe", "выкинуть", "驱逐", "executar"], "{id}", GetString("CommandDescription.Exe"), Command.UsageLevels.HostOrAdmin, Command.UsageTimes.Always, ExeCommand, true, false, [GetString("CommandArgs.Exe.Id")]),
            ["Command.Kill"] = new(["kill", "убить", "击杀", "matar"], "{id}", GetString("CommandDescription.Kill"), Command.UsageLevels.Host, Command.UsageTimes.Always, KillCommand, true, false, [GetString("CommandArgs.Kill.Id")]),
            ["Command.Colour"] = new(["colour", "color", "цвет", "更改颜色", "cor"], "{color}", GetString("CommandDescription.Colour"), Command.UsageLevels.Everyone, Command.UsageTimes.InLobby, ColorCommand, true, false, [GetString("CommandArgs.Colour.Color")]),
            ["Command.ID"] = new(["id", "guesslist", "айди", "ID列表"], "", GetString("CommandDescription.ID"), Command.UsageLevels.Everyone, Command.UsageTimes.Always, IDCommand, true, false),
            ["Command.ChangeRole"] = new(["changerole", "изменитьроль", "измроль", "修改职业", "mudar-função"], "{role}", GetString("CommandDescription.ChangeRole"), Command.UsageLevels.Host, Command.UsageTimes.InGame, ChangeRoleCommand, true, false, [GetString("CommandArgs.ChangeRole.Role")]),
            ["Command.End"] = new(["end", "закончить", "завершить", "结束游戏", "encerrar", "finalizar", "fim"], "", GetString("CommandDescription.End"), Command.UsageLevels.HostOrAdmin, Command.UsageTimes.InGame, EndCommand, true, false),
            ["Command.CosID"] = new(["cosid", "костюм", "одежда", "服装ID"], "", GetString("CommandDescription.CosID"), Command.UsageLevels.Modded, Command.UsageTimes.Always, CosIDCommand, true, false),
            ["Command.MTHY"] = new(["mt", "hy", "собрание", "开会/结束会议"], "", GetString("CommandDescription.MTHY"), Command.UsageLevels.Host, Command.UsageTimes.InGame, MTHYCommand, true, false),
            ["Command.CSD"] = new(["csd", "кзвук", "自定义播放声音"], "{sound}", GetString("CommandDescription.CSD"), Command.UsageLevels.Modded, Command.UsageTimes.Always, CSDCommand, true, false, [GetString("CommandArgs.CSD.Sound")]),
            ["Command.SD"] = new(["sd", "взвук", "游戏中播放声音"], "{sound}", GetString("CommandDescription.SD"), Command.UsageLevels.Modded, Command.UsageTimes.Always, SDCommand, true, false, [GetString("CommandArgs.SD.Sound")]),
            ["Command.GNO"] = new(["gno", "гно", "猜数字"], "{number}", GetString("CommandDescription.GNO"), Command.UsageLevels.Everyone, Command.UsageTimes.AfterDeathOrLobby, GNOCommand, true, false, [GetString("CommandArgs.GNO.Number")]),
            ["Command.Poll"] = new(["poll", "опрос", "发起调查", "enquete"], "{question} {answerA} {answerB} [answerC] [answerD]", GetString("CommandDescription.Poll"), Command.UsageLevels.HostOrModerator, Command.UsageTimes.Always, PollCommand, true, false, [GetString("CommandArgs.Poll.Question"), GetString("CommandArgs.Poll.AnswerA"), GetString("CommandArgs.Poll.AnswerB"), GetString("CommandArgs.Poll.AnswerC"), GetString("CommandArgs.Poll.AnswerD")]),
            ["Command.PV"] = new(["pv", "проголосовать", "选择调查选项"], "{vote}", GetString("CommandDescription.PV"), Command.UsageLevels.Everyone, Command.UsageTimes.Always, PVCommand, false, false, [GetString("CommandArgs.PV.Vote")]),
            ["Command.HM"] = new(["hm", "мс", "мессенджер", "送信"], "{id}", GetString("CommandDescription.HM"), Command.UsageLevels.Everyone, Command.UsageTimes.AfterDeath, HMCommand, true, false, [GetString("CommandArgs.HM.Id")]),
            ["Command.Decree"] = new(["decree", "указ", "总统命令", "decretar"], "{number}", GetString("CommandDescription.Decree"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, DecreeCommand, true, true, [GetString("CommandArgs.Decree.Number")]),
            ["Command.AddVIP"] = new(["addvip", "добавитьвип", "добвип", "指定会员", "vip-add"], "{id}", GetString("CommandDescription.AddVIP"), Command.UsageLevels.Host, Command.UsageTimes.Always, AddVIPCommand, true, false, [GetString("CommandArgs.AddVIP.Id")]),
            ["Command.DeleteVIP"] = new(["deletevip", "удвип", "убрвип", "удалитьвип", "убратьвип", "删除会员", "vip-remover"], "{id}", GetString("CommandDescription.DeleteVIP"), Command.UsageLevels.Host, Command.UsageTimes.Always, DeleteVIPCommand, true, false, [GetString("CommandArgs.DeleteVIP.Id")]),
            ["Command.Assume"] = new(["assume", "предположить", "传销头目预测投票", "assumir"], "{id} {number}", GetString("CommandDescription.Assume"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, AssumeCommand, true, true, [GetString("CommandArgs.Assume.Id"), GetString("CommandArgs.Assume.Number")]),
            ["Command.Note"] = new(["note", "заметка", "记者管理笔记", "nota", "anotar"], "{action} [?]", GetString("CommandDescription.Note"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, NoteCommand, true, true, [GetString("CommandArgs.Note.Action"), GetString("CommandArgs.Note.UnknownValue")]),
            ["Command.OS"] = new(["os", "optionset", "шансроли", "设置职业生成概率"], "{chance} {role}", GetString("CommandDescription.OS"), Command.UsageLevels.HostOrAdmin, Command.UsageTimes.InLobby, OSCommand, true, false, [GetString("CommandArgs.OS.Chance"), GetString("CommandArgs.OS.Role")]),
            ["Command.Negotiation"] = new(["negotiation", "neg", "наказание", "谈判方式", "negociar", "negociação"], "{number}", GetString("CommandDescription.Negotiation"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, NegotiationCommand, true, false, [GetString("CommandArgs.Negotiation.Number")]),
            ["Command.Mute"] = new(["mute", "замутить", "мут", "禁言", "mutar", "silenciar"], "{id} [duration]", GetString("CommandDescription.Mute"), Command.UsageLevels.HostOrModerator, Command.UsageTimes.AfterDeathOrLobby, MuteCommand, true, false, [GetString("CommandArgs.Mute.Id"), GetString("CommandArgs.Mute.Duration")]),
            ["Command.Unmute"] = new(["unmute", "размутить", "размут", "解禁", "desmutar", "desilenciar"], "{id}", GetString("CommandDescription.Unmute"), Command.UsageLevels.HostOrAdmin, Command.UsageTimes.Always, UnmuteCommand, true, false, [GetString("CommandArgs.Unmute.Id")]),
            ["Command.DraftStart"] = new(["draftstart", "ds", "драфтстарт", "启用草稿", "todosescolhem-iniciar"], "", GetString("CommandDescription.DraftStart"), Command.UsageLevels.HostOrModerator, Command.UsageTimes.InLobby, DraftStartCommand, true, false),
            ["Command.DraftDescription"] = new(["dd", "draftdesc", "draftdescription", "драфтописание", "драфтопис", "草稿描述", "todosescolhem-descricao"], "{index}", GetString("CommandDescription.DraftDescription"), Command.UsageLevels.Everyone, Command.UsageTimes.InLobby, DraftDescriptionCommand, false, false, [GetString("CommandArgs.DraftDescription.Index")]),
            ["Command.Draft"] = new(["draft", "драфт", "选择草稿", "todosescolhem-escolher"], "{number}", GetString("CommandDescription.Draft"), Command.UsageLevels.Everyone, Command.UsageTimes.InLobby, DraftCommand, false, false, [GetString("CommandArgs.Draft.Number")]),
            ["Command.ReadyCheck"] = new(["rc", "readycheck", "проверитьготовность", "准备检测", "verificação-de-prontidão"], "", GetString("CommandDescription.ReadyCheck"), Command.UsageLevels.HostOrModerator, Command.UsageTimes.InLobby, ReadyCheckCommand, true, false),
            ["Command.Ready"] = new(["ready", "готов", "准备", "pronto"], "", GetString("CommandDescription.Ready"), Command.UsageLevels.Everyone, Command.UsageTimes.InLobby, ReadyCommand, true, false),
            ["Command.EnableAllRoles"] = new(["enableallroles", "вклвсероли", "启用所有职业", "habilitar-todas-as-funções"], "", GetString("CommandDescription.EnableAllRoles"), Command.UsageLevels.Host, Command.UsageTimes.InLobby, EnableAllRolesCommand, true, false),
            ["Command.Achievements"] = new(["achievements", "достижения", "成就", "conquistas"], "", GetString("CommandDescription.Achievements"), Command.UsageLevels.Modded, Command.UsageTimes.Always, AchievementsCommand, true, false),
            ["Command.DeathNote"] = new(["dn", "deathnote", "угадатьимя", "отгадатьимя", "死亡笔记"], "{name}", GetString("CommandDescription.DeathNote"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, DeathNoteCommand, true, true, [GetString("CommandArgs.DeathNote.Name")]),
            ["Command.Whisper"] = new(["w", "whisper", "шёпот", "ш", "私聊", "sussurrar"], "{id} {message}", GetString("CommandDescription.Whisper"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, WhisperCommand, true, true, [GetString("CommandArgs.Whisper.Id"), GetString("CommandArgs.Whisper.Message")]),
            ["Command.HWhisper"] = new(["hw", "hwhisper", "хш", "хшёпот"], "{id} {message}", GetString("CommandDescription.HWhisper"), Command.UsageLevels.Host, Command.UsageTimes.Always, HWhisperCommand, true, false, [GetString("CommandArgs.HWhisper.Id"), GetString("CommandArgs.HWhisper.Message")]),
            ["Command.Spectate"] = new(["spectate", "наблюдатель", "спектатор", "观战", "espectar"], "[id]", GetString("CommandDescription.Spectate"), Command.UsageLevels.Everyone, Command.UsageTimes.InLobby, SpectateCommand, false, false, [GetString("CommandArgs.Spectate.Id")]),
            ["Command.Anagram"] = new(["anagram", "анаграмма"], "", GetString("CommandDescription.Anagram"), Command.UsageLevels.Everyone, Command.UsageTimes.AfterDeathOrLobby, AnagramCommand, true, false),
            ["Command.RoleList"] = new(["rl", "rolelist", "роли"], "", GetString("CommandDescription.RoleList"), Command.UsageLevels.Everyone, Command.UsageTimes.Always, RoleListCommand, true, false),
            ["Command.JailTalk"] = new(["jt", "jailtalk", "监狱谈话"], "{message}", GetString("CommandDescription.JailTalk"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, JailTalkCommand, true, true, [GetString("CommandArgs.JailTalk.Message")]),
            ["Command.GameModeList"] = new(["gm", "gml", "gamemodes", "gamemodelist", "режимы", "模式列表"], "", GetString("CommandDescription.GameModeList"), Command.UsageLevels.Everyone, Command.UsageTimes.Always, GameModeListCommand, true, false),
            ["Command.GameModePoll"] = new(["gmp", "gmpoll", "pollgm", "gamemodepoll", "гмполл", "模式投票"], "", GetString("CommandDescription.GameModePoll"), Command.UsageLevels.HostOrModerator, Command.UsageTimes.InLobby, GameModePollCommand, true, false),
            ["Command.MapPoll"] = new(["mp", "mpoll", "pollm", "mappoll", "мполл"], "", GetString("CommandDescription.MapPoll"), Command.UsageLevels.HostOrModerator, Command.UsageTimes.InLobby, MapPollCommand, true, false),
            ["Command.EightBall"] = new(["8ball", "шар", "八球"], "[question]", GetString("CommandDescription.EightBall"), Command.UsageLevels.Everyone, Command.UsageTimes.Always, EightBallCommand, false, false, [GetString("CommandArgs.EightBall.Question")]),
            ["Command.AddTag"] = new(["addtag", "createtag", "добавитьтег", "создатьтег", "добтег", "添加标签", "adicionartag"], "{id} {color} {tag}", GetString("CommandDescription.AddTag"), Command.UsageLevels.Host, Command.UsageTimes.Always, AddTagCommand, true, false, [GetString("CommandArgs.AddTag.Id"), GetString("CommandArgs.AddTag.Color"), GetString("CommandArgs.AddTag.Tag")]),
            ["Command.DeleteTag"] = new(["deletetag", "удалитьтег", "убратьтег", "удтег", "убртег", "删除标签"], "{id}", GetString("CommandDescription.DeleteTag"), Command.UsageLevels.Host, Command.UsageTimes.InLobby, DeleteTagCommand, true, false, [GetString("CommandArgs.DeleteTag.Id")]),
            ["Command.DayBreak"] = new(["daybreak", "db", "дейбрейк", "破晓"], "", GetString("CommandDescription.DayBreak"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, DayBreakCommand, true, true),
            ["Command.Fix"] = new(["fix", "blackscreenfix", "fixblackscreen", "фикс", "исправить", "修复"], "{id}", GetString("CommandDescription.Fix"), Command.UsageLevels.HostOrModerator, Command.UsageTimes.InGame, FixCommand, true, false, [GetString("CommandArgs.Fix.Id")]),
            ["Command.XOR"] = new(["xor", "异或命令"], "{role} {role}", GetString("CommandDescription.XOR"), Command.UsageLevels.Everyone, Command.UsageTimes.Always, XORCommand, true, false, [GetString("CommandArgs.XOR.Role"), GetString("CommandArgs.XOR.Role")]),
            ["Command.ChemistInfo"] = new(["ci", "chemistinfo", "химик", "化学家"], "", GetString("CommandDescription.ChemistInfo"), Command.UsageLevels.Everyone, Command.UsageTimes.Always, ChemistInfoCommand, true, false),
            ["Command.Forge"] = new(["forge"], "{id} {role}", GetString("CommandDescription.Forge"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, ForgeCommand, true, true, [GetString("CommandArgs.Forge.Id"), GetString("CommandArgs.Forge.Role")]),
            ["Command.Choose"] = new(["choose", "pick", "выбрать", "选择", "escolher"], "{role}", GetString("CommandDescription.Choose"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, ChooseCommand, true, true, [GetString("CommandArgs.Choose.Role")]),
            ["Command.CopyPreset"] = new(["copypreset", "presetcopy", "скопироватьсохранение", "скопироватьсохр", "复制预设", "copiar"], "{sourcepreset} {targetpreset}", GetString("CommandDescription.CopyPreset"), Command.UsageLevels.Host, Command.UsageTimes.InLobby, CopyPresetCommand, true, false, [GetString("CommandArgs.CopyPreset.SourcePreset"), GetString("CommandArgs.CopyPreset.TargetPreset")]),
            ["Command.AddAdmin"] = new(["addadmin", "добавитьадмин", "добадмин", "指定管理员", "admin-add"], "{id}", GetString("CommandDescription.AddAdmin"), Command.UsageLevels.Host, Command.UsageTimes.Always, AddAdminCommand, true, false, [GetString("CommandArgs.AddAdmin.Id")]),
            ["Command.DeleteAdmin"] = new(["deleteadmin", "удалитьадмин", "убратьадмин", "удалитьадминку", "убратьадминку", "删除管理员", "admin-remover"], "{id}", GetString("CommandDescription.DeleteAdmin"), Command.UsageLevels.Host, Command.UsageTimes.Always, DeleteAdminCommand, true, false, [GetString("CommandArgs.DeleteAdmin.Id")]),
            ["Command.VoteStart"] = new(["vs", "votestart", "голосованиестарт", "投票开始"], "", GetString("CommandDescription.VoteStart"), Command.UsageLevels.Everyone, Command.UsageTimes.InLobby, VoteStartCommand, true, false),
            ["Command.Imitate"] = new(["imitate", "имитировать", "模仿"], "{id}", GetString("CommandDescription.Imitate"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, ImitateCommand, true, true, [GetString("CommandArgs.Imitate.Id")]),
            ["Command.Retribute"] = new(["ret", "retribute", "воздать", "报复"], "{id}", GetString("CommandDescription.Retribute"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, RetributeCommand, true, true, [GetString("CommandArgs.Retribute.Id")]),
            ["Command.Revive"] = new(["revive", "воскрешение", "воскрешать", "复活", "reviver"], "{id}", GetString("CommandDescription.Revive"), Command.UsageLevels.Host, Command.UsageTimes.InGame, ReviveCommand, true, false, [GetString("CommandArgs.Revive.Id")]),
            ["Command.Select"] = new(["select", "выбратьигрока", "选择玩家", "selecionar"], "{id} {role}", GetString("CommandDescription.Select"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, SelectCommand, true, true, [GetString("CommandArgs.Select.Id"), GetString("CommandArgs.Select.Role")]),
            ["Command.UIScale"] = new(["uiscale", "масштаб"], "{scale}", GetString("CommandDescription.UIScale"), Command.UsageLevels.Modded, Command.UsageTimes.Always, UIScaleCommand, true, false, [GetString("CommandArgs.UIScale.Scale")]),
            ["Command.Fabricate"] = new(["fabricate", "фабриковать", "伪造", "fabricar"], "{deathreason}", GetString("CommandDescription.Fabricate"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, FabricateCommand, true, true, [GetString("CommandArgs.Fabricate.DeathReason")]),
            ["Command.Start"] = new(["start", "старт", "开始"], "", GetString("CommandDescription.Start"), Command.UsageLevels.HostOrModerator, Command.UsageTimes.InLobby, StartCommand, false, false),
            ["Command.ConfirmAuth"] = new(["confirmauth"], "{uuid}", GetString("CommandDescription.ConfirmAuth"), Command.UsageLevels.Everyone, Command.UsageTimes.Always, ConfirmAuthCommand, true, false, [GetString("CommandArgs.ConfirmAuth.UUID")]),
                        
            // Commands with action handled elsewhere
            ["Command.Guess"] = new(["shoot", "guess", "bet", "bt", "st", "угадать", "бт", "猜测", "赌", "adivinhar"], "{id} {role}", GetString("CommandDescription.Guess"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, (_, _, _, _) => { }, true, false, [GetString("CommandArgs.Guess.Id"), GetString("CommandArgs.Guess.Role")]),
            ["Command.Trial"] = new(["tl", "sp", "jj", "trial", "суд", "засудить", "审判", "判", "julgar"], "{id}", GetString("CommandDescription.Trial"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, (_, _, _, _) => { }, true, false, [GetString("CommandArgs.Trial.Id")]),
            ["Command.Swap"] = new(["sw", "swap", "st", "свап", "свапнуть", "换票", "trocar"], "{id}", GetString("CommandDescription.Swap"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, (_, _, _, _) => { }, true, false, [GetString("CommandArgs.Swap.Id")]),
            ["Command.Compare"] = new(["compare", "cp", "cmp", "сравнить", "ср", "检查", "comparar"], "{id1} {id2}", GetString("CommandDescription.Compare"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, (_, _, _, _) => { }, true, false, [GetString("CommandArgs.Compare.Id1"), GetString("CommandArgs.Compare.Id2")]),
            ["Command.Medium"] = new(["ms", "mediumship", "medium", "медиум", "回答"], "{answer}", GetString("CommandDescription.Medium"), Command.UsageLevels.Everyone, Command.UsageTimes.InMeeting, (_, _, _, _) => { }, true, false, [GetString("CommandArgs.Medium.Answer")]),
            ["Command.Revenge"] = new(["rv", "месть", "отомстить", "复仇"], "{id}", GetString("CommandDescription.Revenge"), Command.UsageLevels.Everyone, Command.UsageTimes.AfterDeath, (_, _, _, _) => { }, true, false, [GetString("CommandArgs.Revenge.Id")]),

        };
    }

    private static string[] ModsFileCache = [];
    private static string[] VIPsFileCache = [];
    private static string[] AdminsFileCache = [];
    private static long LastModFileUpdate;
    private static long LastVIPFileUpdate;
    private static long LastAdminFileUpdate;

    // Function to check if a Player is Moderator
    public static bool IsPlayerModerator(string friendCode)
    {
        if (IsPlayerAdmin(friendCode)) return true;
        
        friendCode = friendCode.Replace(':', '#');

        if (friendCode == "" || friendCode == string.Empty || !Options.ApplyModeratorList.GetBool()) return false;

        if (Main.UserData.TryGetValue(friendCode, out Options.UserData userData) && userData.Moderator)
            return true;

        long now = Utils.TimeStamp;
        string[] friendCodes;

        if (LastModFileUpdate + 5 > now)
            friendCodes = ModsFileCache;
        else
        {
            var friendCodesFilePath = $"{Main.DataPath}/EHR_DATA/Moderators.txt";

            if (!File.Exists(friendCodesFilePath))
            {
                File.WriteAllText(friendCodesFilePath, string.Empty);
                return false;
            }

            friendCodes = ModsFileCache = File.ReadAllLines(friendCodesFilePath);
            LastModFileUpdate = now;
        }

        return friendCodes.Any(code => code.Contains(friendCode, StringComparison.OrdinalIgnoreCase));
    }

    // Function to check if a player is a VIP
    public static bool IsPlayerVIP(string friendCode)
    {
        friendCode = friendCode.Replace(':', '#');

        if (friendCode == "" || friendCode == string.Empty || !Options.ApplyVIPList.GetBool()) return false;

        if (Main.UserData.TryGetValue(friendCode, out Options.UserData userData) && userData.Vip)
            return true;

        long now = Utils.TimeStamp;
        string[] friendCodes;

        if (LastVIPFileUpdate + 5 > now)
            friendCodes = VIPsFileCache;
        else
        {
            var friendCodesFilePath = $"{Main.DataPath}/EHR_DATA/VIPs.txt";

            if (!File.Exists(friendCodesFilePath))
            {
                File.WriteAllText(friendCodesFilePath, string.Empty);
                return false;
            }

            friendCodes = VIPsFileCache = File.ReadAllLines(friendCodesFilePath);
            LastVIPFileUpdate = now;
        }

        return friendCodes.Any(code => code.Contains(friendCode, StringComparison.OrdinalIgnoreCase));
    }

    // Function to check if a player is an Admin
    public static bool IsPlayerAdmin(string friendCode)
    {
        friendCode = friendCode.Replace(':', '#');

        if (friendCode == "" || friendCode == string.Empty || !Options.ApplyAdminList.GetBool()) return false;

        if (Main.UserData.TryGetValue(friendCode, out Options.UserData userData) && userData.Admin)
            return true;

        long now = Utils.TimeStamp;
        string[] friendCodes;

        if (LastAdminFileUpdate + 5 > now)
            friendCodes = AdminsFileCache;
        else
        {
            var friendCodesFilePath = $"{Main.DataPath}/EHR_DATA/Admins.txt";

            if (!File.Exists(friendCodesFilePath))
            {
                File.WriteAllText(friendCodesFilePath, string.Empty);
                return false;
            }

            friendCodes = AdminsFileCache = File.ReadAllLines(friendCodesFilePath);
            LastAdminFileUpdate = now;
        }

        return friendCodes.Any(code => code.Contains(friendCode, StringComparison.OrdinalIgnoreCase));
    }

    public static bool Prefix(ChatController __instance)
    {
        if (__instance.quickChatField.visible) return true;

        __instance.freeChatField.textArea.text = __instance.freeChatField.textArea.text.Replace("\b", string.Empty).Replace("\r", string.Empty);
        
        __instance.timeSinceLastMessage = 3f;

        string text = __instance.freeChatField.textArea.text.Trim();
        var cancelVal = string.Empty;

        switch (Options.CurrentGameMode)
        {
            case CustomGameMode.TheMindGame when AmongUsClient.Instance.AmHost:
                TheMindGame.OnChat(PlayerControl.LocalPlayer, text.ToLower());
                break;
            case CustomGameMode.TheMindGame:
                MessageWriter w = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.TMGSync, SendOption.Reliable, AmongUsClient.Instance.HostId);
                w.WriteNetObject(PlayerControl.LocalPlayer);
                w.Write(text);
                AmongUsClient.Instance.FinishRpcImmediately(w);
                break;
            case CustomGameMode.BedWars when AmongUsClient.Instance.AmHost:
                BedWars.OnChat(PlayerControl.LocalPlayer, text);
                break;
        }

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
            if (Swapper.SwapMsg(PlayerControl.LocalPlayer, text)) goto Canceled;
            if (Inspector.InspectorCheckMsg(PlayerControl.LocalPlayer, text)) goto Canceled;
            if (Councillor.MurderMsg(PlayerControl.LocalPlayer, text)) goto Canceled;
            if (Medium.MsMsg(PlayerControl.LocalPlayer, text)) goto Canceled;
            if (Nemesis.NemesisMsgCheck(PlayerControl.LocalPlayer, text)) goto Canceled;
        }

        Main.IsChatCommand = false;

        if (text.StartsWith('/'))
        {
            foreach ((string key, Command command) in Command.AllCommands)
            {
                if (!command.IsThisCommand(text)) continue;

                Logger.Info($" Recognized command: {text}", "ChatCommand");
                Main.IsChatCommand = true;

                if (!command.CanUseCommand(PlayerControl.LocalPlayer, sendErrorMessage: true))
                    goto Canceled;

                command.Action(PlayerControl.LocalPlayer, key, text, args);
                if (command.IsCanceled) goto Canceled;

                break;
            }

            Statistics.HasUsedAnyCommand = true;
        }

        if (!Main.IsChatCommand && Astral.On && !PlayerControl.LocalPlayer.Is(CustomRoles.Astral))
            LateTask.New(() => Main.PlayerStates.Values.DoIf(x => !x.IsDead && x.Role is Astral { BackTS: > 0 } && x.Player != null, x => ChatManager.ClearChat(x.Player)), 0.2f, log: false);

        if (CheckMute(PlayerControl.LocalPlayer.PlayerId))
            goto Canceled;

        if (string.IsNullOrWhiteSpace(text))
            goto Canceled;

        goto Skip;
        Canceled:
        Main.IsChatCommand = false;
        canceled = true;
        Skip:

        if (ExileController.Instance)
            canceled = true;

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

                LateTask.New(() => Utils.DirtyName.Add(PlayerControl.LocalPlayer.PlayerId), 0.2f, log: false);
            }

            ChatManager.SendMessage(PlayerControl.LocalPlayer, text);
        }

        if (text.Contains("666") && PlayerControl.LocalPlayer.Is(CustomRoles.Demon))
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

    private static void RequestCommandProcessingFromHost(string text, string commandKey)
    {
        PlayerControl pc = PlayerControl.LocalPlayer;
        MessageWriter w = AmongUsClient.Instance.StartRpcImmediately(pc.NetId, (byte)CustomRPC.RequestCommandProcessing, SendOption.Reliable, AmongUsClient.Instance.HostId);
        w.Write(commandKey);
        w.Write(pc.PlayerId);
        w.Write(text);
        AmongUsClient.Instance.FinishRpcImmediately(w);
    }

    // ---------------------------------------------------------------------------------------------------------------------------------------------
    
    private static void StartCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        VotedToStart.UnionWith(Main.AllPlayerControls.Select(x => x.PlayerId));
    }
    
    private static void FabricateCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (Starspawn.IsDayBreak) return;

        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        if (!Main.PlayerStates.TryGetValue(player.PlayerId, out PlayerState state) || state.IsDead || state.Role is not Fabricator fab) return;
        
        if (args.Length < 2 || !Enum.GetValues<PlayerState.DeathReason>().FindFirst(x => GetString($"DeathReason.{x}").Replace(" ", string.Empty).Equals(args[1].Replace(" ", string.Empty), StringComparison.OrdinalIgnoreCase), out PlayerState.DeathReason newDeathReason))
        {
            Utils.SendMessage("\n", player.PlayerId, string.Format(GetString("Fabricator.InvalidDeathReason"), args.Length >= 2 ? args[1] : ""));
            return;
        }

        fab.NextDeathReason = newDeathReason;
        
        Utils.SendMessage("\n", player.PlayerId, string.Format(GetString("Fabricator.SetDeathReason"), GetString($"DeathReason.{newDeathReason}")));
        
        MeetingManager.SendCommandUsedMessage(args[0]);
    }

    private static void UIScaleCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (args.Length < 2 || !float.TryParse(args[1], out float scale) || scale == 0f) return;
        HudManagerStartPatch.TryResizeUI(scale);
    }
    
    private static void SelectCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (Starspawn.IsDayBreak) return;

        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        if (!Main.PlayerStates.TryGetValue(player.PlayerId, out PlayerState state) || state.IsDead || state.Role is not Loner loner || loner.Done) return;
        if (args.Length < 3 || !GuessManager.MsgToPlayerAndRole(text[7..], out byte targetId, out CustomRoles pickedRole, out _) || targetId == player.PlayerId) return;
        if (!pickedRole.IsImpostor() || pickedRole.IsVanilla() || CustomRoleSelector.RoleResult.ContainsValue(pickedRole) || pickedRole.GetMode() == 0) return;
        if (!Main.PlayerStates.TryGetValue(targetId, out PlayerState ts) || ts.IsDead) return;

        loner.PickedPlayer = targetId;
        loner.PickedRole = pickedRole;

        Utils.SendMessage("\n", player.PlayerId, string.Format(GetString("Loner.Picked"), targetId.ColoredPlayerName(), pickedRole.ToColoredString()));

        MeetingManager.SendCommandUsedMessage(args[0]);
    }
    
    private static void ReviveCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if ((!Options.NoGameEnd.GetBool() && !player.FriendCode.GetDevUser().up) || args.Length < 2 || !byte.TryParse(args[1], out byte targetId)) return;
        
        PlayerControl target = Utils.GetPlayerById(targetId);
        if (target == null) return;
        
        target.RpcRevive();
    }
    
    private static void ConfirmAuthCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (GameStates.CurrentServerType != GameStates.ServerType.Vanilla)
        {
            Utils.SendMessage("\n", player.PlayerId, GetString("ConfirmAuth.ErrorNotVanilla"));
            return;
        }

        if (!Options.PostLobbyCodeToEHRWebsite.GetBool())
        {
            Utils.SendMessage("\n", player.PlayerId, string.Format(GetString("ConfirmAuth.ErrorLobbyUnlisted"), GetString("PostLobbyCodeToEHRDiscordServer")));
            return;
        }

        if (args.Length >= 2)
        {
            string uuid = args[1].Trim();
            if (string.IsNullOrWhiteSpace(uuid)) return;

            Main.Instance.StartCoroutine(SendVerificationCoroutine(uuid));
        }

        return;

        IEnumerator SendVerificationCoroutine(string uuid)
        {
            string friendCode = player.FriendCode;
            string puid = player.GetClient().ProductUserId;
            var gameId = AmongUsClient.Instance.GameId.ToString();

            if (string.IsNullOrWhiteSpace(friendCode) || string.IsNullOrWhiteSpace(puid))
            {
                Logger.Error($" Missing friendcode/puid for player {player.PlayerId}", "ConfirmAuth");
                yield break;
            }

            if (string.IsNullOrWhiteSpace(gameId) || gameId == "32")
            {
                Logger.Error(" Invalid GameId", "ConfirmAuth");
                yield break;
            }

            var json = $"{{\"uuid\":\"{uuid}\",\"friend_code\":\"{friendCode}\",\"puid\":\"{puid}\",\"game_id\":\"{gameId}\"}}";
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

            var uwr = new UnityWebRequest("https://gurge44.pythonanywhere.com/api/verify_ingame", "POST")
            {
                uploadHandler = new UploadHandlerRaw(bodyRaw),
                downloadHandler = new DownloadHandlerBuffer()
            };
            uwr.SetRequestHeader("Content-Type", "application/json");
            uwr.SetRequestHeader("User-Agent", $"{Main.ModName} v{Main.PluginVersion}");
            uwr.timeout = 10; // seconds

            yield return uwr.SendWebRequest();

            if (uwr.result != UnityWebRequest.Result.Success)
            {
                Logger.Error($" HTTP error sending verification: {uwr.error} (code {(int)uwr.responseCode})", "ConfirmAuth");
                Utils.SendMessage("\n", player.PlayerId, string.Format(GetString("ConfirmAuth.Error"), uwr.error));
            }
            else
            {
                Logger.Msg($" Sent ok. Resp code {(int)uwr.responseCode}. Body: {uwr.downloadHandler?.text}", "ConfirmAuth");
                Utils.SendMessage("\n", player.PlayerId, GetString("ConfirmAuth.Success"));
            }
        }
    }

    public static void RetributeCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (Starspawn.IsDayBreak) return;

        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        if (!player.IsAlive() || !Main.PlayerStates.TryGetValue(player.PlayerId, out PlayerState state) || state.Role is not Retributionist { Notified: true } rb || rb.Camping == byte.MaxValue) return;

        PlayerControl campTarget = Utils.GetPlayerById(rb.Camping);
        if (campTarget == null || campTarget.IsAlive() || !Main.PlayerStates.TryGetValue(campTarget.PlayerId, out PlayerState campState)) return;

        if (args.Length < 2 || !byte.TryParse(args[1], out byte targetId)) return;

        byte realKiller = campState.GetRealKiller();

        if (realKiller != targetId)
        {
            rb.Notified = false;
            RPC.PlaySoundRPC(player.PlayerId, Sounds.SabotageSound);
            Utils.SendMessage("\n", player.PlayerId, GetString("Retributionist.Fail"));
        }
        else
        {
            PlayerControl killer = Utils.GetPlayerById(realKiller);

            if (killer == null || !killer.IsAlive())
            {
                rb.Notified = false;
                Utils.SendMessage("\n", player.PlayerId, GetString("Retributionist.KillerDead"));
            }
            else if (!killer.Is(CustomRoles.Pestilence))
            {
                killer.SetRealKiller(player);
                Main.PlayerStates[killer.PlayerId].deathReason = PlayerState.DeathReason.Retribution;
                Medic.IsDead(killer);
                killer.RpcGuesserMurderPlayer();
                Utils.AfterPlayerDeathTasks(killer, true);
                Utils.SendMessage("\n", title: Utils.ColorString(Utils.GetRoleColor(CustomRoles.Retributionist), string.Format(GetString("Retributionist.SuccessOthers"), targetId.ColoredPlayerName(), CustomRoles.Retributionist.ToColoredString())));
                Utils.SendMessage("\n", player.PlayerId, GetString("Retributionist.Success"));
            }
        }

        MeetingManager.SendCommandUsedMessage(args[0]);
    }

    public static void ImitateCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (Starspawn.IsDayBreak) return;

        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        if (!Imitator.PlayerIdList.Contains(player.PlayerId) || !player.IsAlive() || args.Length < 2 || !byte.TryParse(args[1], out byte targetId) || !Main.PlayerStates.TryGetValue(targetId, out PlayerState targetState)) return;

        if (!targetState.IsDead)
        {
            RPC.PlaySoundRPC(player.PlayerId, Sounds.SabotageSound);
            Utils.SendMessage("\n", player.PlayerId, GetString("Imitator.TargetMustBeDead"));
            return;
        }

        if (!targetState.MainRole.Is(Team.Crewmate) || targetState.MainRole == CustomRoles.GM)
        {
            RPC.PlaySoundRPC(player.PlayerId, Sounds.SabotageSound);
            Utils.SendMessage("\n", player.PlayerId, GetString("Imitator.TargetMustBeCrew"));
            return;
        }

        Imitator.ImitatingRole[player.PlayerId] = targetState.MainRole;
        RPC.PlaySoundRPC(player.PlayerId, Sounds.TaskComplete);
        Logger.Info($"{player.GetRealName()} will be imitating as {targetState.MainRole}", "Imitator");
        Utils.SendMessage("\n", player.PlayerId, string.Format(GetString("Imitator.Success"), targetId.ColoredPlayerName()));

        MeetingManager.SendCommandUsedMessage(args[0]);
    }
    
    private static void VoteStartCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        if (Options.DisableVoteStartCommand.GetBool())
        {
            Utils.SendMessage("\n", player.PlayerId, GetString("VoteStartDisabled"));
            return;
        }

        if (VotedToStart.Add(player.PlayerId))
        {
            int voteCount = VotedToStart.Count;
            int playerCount = PlayerControl.AllPlayerControls.Count;
            var percentage = (int)Math.Round(voteCount / (float)playerCount * 100f);
            var required = (int)Math.Ceiling(playerCount / 2f);
            Utils.SendMessage(string.Format(GetString("VotedToStart"), voteCount, playerCount, percentage, required), title: string.Format(GetString("VotedToStart.Title"), player.PlayerId.ColoredPlayerName()));
        }
    }
    
    private static void DeleteAdminCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        if (args.Length < 2 || !byte.TryParse(args[1], out byte remAdminId)) return;

        PlayerControl remAdminPc = Utils.GetPlayerById(remAdminId);
        if (remAdminPc == null) return;

        string remFc = remAdminPc.FriendCode.Replace(':', '#');

        if (!IsPlayerAdmin(remFc))
        {
            Utils.SendMessage(GetString("PlayerNotAdmin"), player.PlayerId);
            return;
        }

        File.WriteAllLines($"{Main.DataPath}/EHR_DATA/Admins.txt", File.ReadAllLines($"{Main.DataPath}/EHR_DATA/Admins.txt").Where(x => !x.Contains(remFc)));
        Utils.SendMessage(GetString("PlayerRemovedFromAdminList"), player.PlayerId);
    }

    private static void AddAdminCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        if (args.Length < 2 || !byte.TryParse(args[1], out byte newAdminId)) return;

        PlayerControl newAdminPc = Utils.GetPlayerById(newAdminId);
        if (newAdminPc == null) return;

        string fc = newAdminPc.FriendCode.Replace(':', '#');
        if (IsPlayerModerator(fc)) Utils.SendMessage(GetString("PlayerAlreadyAdmin"), player.PlayerId);

        File.AppendAllText($"{Main.DataPath}/EHR_DATA/Admins.txt", $"\n{fc}");
        Utils.SendMessage(GetString("PlayerAddedToAdminList"), player.PlayerId);
    }
    
    private static void CopyPresetCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (args.Length < 3 || !int.TryParse(args[1], out int sourcePresetId) || sourcePresetId is < 1 or > 10 || (!int.TryParse(args[2], out int targetPreset) && targetPreset is < 1 or > 10)) return;

        Prompt.Show(string.Format(GetString("Promt.CopyPreset"), sourcePresetId, targetPreset), Copy, () => { });
        return;

        void Copy()
        {
            sourcePresetId--;
            targetPreset--;

            foreach (OptionItem optionItem in OptionItem.AllOptions)
            {
                if (optionItem.IsSingleValue) continue;
                optionItem.AllValues[targetPreset] = optionItem.AllValues[sourcePresetId];
            }

            OptionItem.SyncAllOptions();
            OptionSaver.Save();
        }
    }
    
    private static void ChooseCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        if (!player.IsAlive() || !player.Is(CustomRoles.Pawn) || !Main.PlayerStates.TryGetValue(player.PlayerId, out var state)) return;
        if (args.Length < 2 || !GetRoleByName(string.Join(' ', args[1..]), out var role) || role.GetMode() == 0) return;

        ((Pawn)state.Role).ChosenRole = role;

        MeetingManager.SendCommandUsedMessage(args[0]);
    }

    private static void ForgeCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (Starspawn.IsDayBreak) return;

        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        if (!player.IsAlive() || !player.Is(CustomRoles.Forger) || player.GetAbilityUseLimit() < 1) return;
        if (args.Length < 3 || !GuessManager.MsgToPlayerAndRole(text[6..], out byte targetId, out CustomRoles forgeRole, out _)) return;

        player.RpcRemoveAbilityUse();

        Forger.Forges[targetId] = forgeRole;
        Utils.SendMessage("\n", player.PlayerId, string.Format(GetString("ForgeSuccess"), (int)Math.Round(player.GetAbilityUseLimit(), 1), targetId.ColoredPlayerName(), forgeRole.ToColoredString()));

        MeetingManager.SendCommandUsedMessage(args[0]);
    }

    private static void ChemistInfoCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        Utils.SendMessage(Chemist.GetProcessesInfo(), player.PlayerId, CustomRoles.Chemist.ToColoredString());
    }

    private static void XORCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }
        
        if (!player.IsHost() || args.Length < 3 || !GetRoleByName(args[1], out CustomRoles role1) || !GetRoleByName(args[2], out CustomRoles role2))
        {
            Utils.SendMessage(string.Join('\n', Main.XORRoles.ConvertAll(x => $"{x.Item1.ToColoredString()} ⊕ {x.Item2.ToColoredString()}")), player.PlayerId, GetString("XORListTitle"));
            return;
        }

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

    private static void FixCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        if (args.Length < 2 || !byte.TryParse(args[1], out byte id)) return;

        var pc = id.GetPlayer();
        if (pc == null) return;

        pc.FixBlackScreen();

        if (Main.AllPlayerControls.All(x => x.IsAlive()))
            Logger.SendInGame(GetString("FixBlackScreenWaitForDead"), Color.yellow);
    }

    public static void DayBreakCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        if (!player.IsAlive() || Main.PlayerStates[player.PlayerId].Role is not Starspawn sp || sp.HasUsedDayBreak) return;

        Starspawn.IsDayBreak = true;
        sp.HasUsedDayBreak = true;

        player.RPCPlayCustomSound("Line");
        Utils.SendMessage("\n", title: string.Format(GetString("StarspawnUsedDayBreak"), CustomRoles.Starspawn.ToColoredString()));
    }

    private static void AddTagCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (args.Length < 4 || !byte.TryParse(args[1], out byte id)) return;

        PlayerControl pc = id.GetPlayer();
        if (pc == null) return;

        Color color = ColorUtility.TryParseHtmlString($"#{args[2].ToLower()}", out Color c) ? c : Color.red;
        string tag = Utils.ColorString(color, string.Join(' ', args[3..]) + " ");
        PrivateTagManager.AddTag(pc.FriendCode, tag);

        Utils.SendMessage("\n", player.PlayerId, string.Format(GetString("AddTagSuccess"), tag, id.ColoredPlayerName(), id));
    }

    private static void DeleteTagCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (args.Length < 2 || !byte.TryParse(args[1], out byte id)) return;

        PlayerControl pc = id.GetPlayer();
        if (pc == null) return;

        PrivateTagManager.DeleteTag(pc.FriendCode);
        Utils.SendMessage("\n", player.PlayerId, string.Format(GetString("DeleteTagSuccess"), id.ColoredPlayerName()));
        Utils.DirtyName.Add(pc.PlayerId);
    }

    private static void EightBallCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (Options.Disable8ballCommand.GetBool())
        {
            Utils.SendMessage("\n", player.PlayerId, GetString("EightBallDisabled"), sendOption: SendOption.None);
            return;
        }

        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        Utils.SendMessage(GetString($"8BallResponse.{IRandom.Instance.Next(20)}"), player.IsAlive() ? byte.MaxValue : player.PlayerId, GetString("8BallResponseTitle"));
    }

    public static void GameModePollCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        GMPollGameModes = Enum.GetValues<CustomGameMode>()[..^1].Where(x => Options.GMPollGameModesSettings[x].GetBool()).ToList();
        string gmNames = string.Join(' ', GMPollGameModes.Select(x => GetString(x.ToString()).Replace(' ', '_')));
        var msg = $"/poll {GetString("GameModePoll.Question").TrimEnd('?')}? {gmNames}";
        PollCommand(player, "Command.Poll", msg, msg.Split(' '));
    }
    
    public static void MapPollCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        MPollMaps = Enum.GetValues<MapNames>().Where(x => Options.MPollMapsSettings[x].GetBool()).ToList();
        string mNames = string.Join(' ', MPollMaps.Select(x => GetString(x.ToString()).Replace(' ', '_')));
        var msg = $"/poll {GetString("MapPoll.Question").TrimEnd('?')}? {mNames}";
        PollCommand(player, "Command.Poll", msg, msg.Split(' '));
    }

    private static void GameModeListCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        string info = string.Join("\n\n", Enum.GetValues<CustomGameMode>()[1..^1]
            .Select(x => (GameMode: x, Color: Main.RoleColors.GetValueOrDefault(CustomRoleSelector.GameModeRoles.TryGetValue(x, out CustomRoles role) ? role : x == CustomGameMode.HideAndSeek ? CustomRoles.Hider : CustomRoles.Witness, "#000000")))
            .Select(x => $"<{x.Color}><u><b>{GetString($"{x.GameMode}")}</b></u></color><size=75%>\n{GetString($"ModeDescribe.{x.GameMode}").Split("\n\n")[0]}</size>"));

        Utils.SendMessage(info, player.PlayerId, GetString("GameModeListTitle"));
    }

    private static void JailTalkCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (args.Length < 2) return;

        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        Jailor jailor = Main.PlayerStates[player.PlayerId].Role as Jailor ?? Main.PlayerStates.Select(x => x.Value.Role as Jailor).FirstOrDefault(x => x != null);
        if (jailor == null) return;

        bool amJailor = Jailor.PlayerIdList.Contains(player.PlayerId);
        bool amJailed = player.PlayerId == jailor.JailorTarget;
        if (!amJailor && !amJailed) return;

        string title = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jailor), GetString("JailTalkTitle"));

        string message = string.Join(' ', args[1..]);

        if (amJailor) Utils.SendMessage(message, jailor.JailorTarget, title);
        else Jailor.PlayerIdList.ForEach(x => Utils.SendMessage(message, x, title, sendOption: SendOption.None));

        MeetingManager.SendCommandUsedMessage(args[0]);
    }

    private static void RoleListCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
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
                factionMin = Math.Max(0, Main.NormalOptions.MaxPlayers - Options.FactionMinMaxSettings[Team.Neutral].MaxSetting.GetInt() - Options.FactionMinMaxSettings[Team.Impostor].MaxSetting.GetInt() - Options.FactionMinMaxSettings[Team.Coven].MaxSetting.GetInt());
                factionMax = Math.Max(0, Main.NormalOptions.MaxPlayers - Options.FactionMinMaxSettings[Team.Neutral].MinSetting.GetInt() - Options.FactionMinMaxSettings[Team.Impostor].MinSetting.GetInt() - Options.FactionMinMaxSettings[Team.Coven].MinSetting.GetInt());
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

    private static void AnagramCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
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

    private static void SpectateCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        if (player.IsHost() && args.Length > 1 && byte.TryParse(args[1], out byte targetId))
        {
            PlayerControl pc = targetId.GetPlayer();
            if (pc == null) return;

            if (ForcedSpectators.Remove(targetId))
                Utils.SendMessage("\n", player.PlayerId, string.Format(GetString("SpectateCommand.RemovedForcedSpectator"), targetId.ColoredPlayerName()));

            if (ForcedSpectators.Add(targetId))
                Utils.SendMessage("\n", player.PlayerId, string.Format(GetString("SpectateCommand.ForcedSpectator"), targetId.ColoredPlayerName()));
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

    private static void WhisperCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
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

        PlayerState state = Main.PlayerStates[targetId];
        if (state.IsDead || state.SubRoles.Contains(CustomRoles.Shy)) return;

        string fromName = player.PlayerId.ColoredPlayerName();
        string toName = targetId.ColoredPlayerName();
        
        string msg = args[2..].Join(delimiter: " ");
        string title = string.Format(GetString("WhisperTitle"), fromName, player.PlayerId);

        Utils.SendMessage(msg, targetId, title);
        ChatUpdatePatch.LastMessages.Add((msg, targetId, title, Utils.TimeStamp));

        MeetingManager.SendCommandUsedMessage(args[0]);

        string coloredRole = CustomRoles.Listener.ToColoredString();

        foreach (PlayerControl listener in Main.AllAlivePlayerControls)
        {
            if (!listener.Is(CustomRoles.Listener) || IRandom.Instance.Next(100) >= Listener.WhisperHearChance.GetInt()) continue;
            string message = IRandom.Instance.Next(100) < Listener.FullMessageHearChance.GetInt() ? string.Format(GetString("Listener.FullMessage"), coloredRole, fromName, toName, msg) : string.Format(GetString("Listener.FromTo"), coloredRole, fromName, toName);
            Utils.SendMessage("\n", listener.PlayerId, message);
            
            if (listener.AmOwner && ++Listener.LocalPlayerHeardMessagesThisMeeting >= 3)
                Achievements.Type.Eavesdropper.Complete();
        }
    }

    private static void HWhisperCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (args.Length < 3 || !byte.TryParse(args[1], out byte targetId)) return;

        string msg = args[2..].Join(delimiter: " ");
        string title = string.Format(GetString("HWhisperTitle"), player.PlayerId.ColoredPlayerName());

        Utils.SendMessage(msg, targetId, title);
        ChatUpdatePatch.LastMessages.Add((msg, targetId, title, Utils.TimeStamp));
    }

    private static void DeathNoteCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (Starspawn.IsDayBreak) return;

        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        if (!player.Is(CustomRoles.NoteKiller) || args.Length < 2) return;

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
            RPC.PlaySoundRPC(player.PlayerId, Sounds.SabotageSound);
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
        
        pc.RpcGuesserMurderPlayer();
        Utils.AfterPlayerDeathTasks(pc, true);

        string coloredName = deadPlayer.ColoredPlayerName();
        Utils.SendMessage("\n", player.PlayerId, string.Format(GetString("DeathNoteCommand.Success"), coloredName), sendOption: SendOption.None);
        Utils.SendMessage(string.Format(GetString("DeathNoteCommand.SuccessForOthers"), coloredName));

        NoteKiller.Kills++;
        
        if (player.AmOwner && NoteKiller.Kills >= 3)
            Achievements.Type.IKnowYourNames.CompleteAfterGameEnd();

        MeetingManager.SendCommandUsedMessage(args[0]);
    }

    private static void AchievementsCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        Func<Achievements.Type, string> ToAchievementString = x => $"<b>{GetString($"Achievement.{x}")}</b> - {GetString($"Achievement.{x}.Description")}";

        Achievements.Type[] allAchievements = Enum.GetValues<Achievements.Type>();
        Achievements.Type[] union = Achievements.CompletedAchievements.Union(Achievements.WaitingAchievements).ToArray();
        var completedAchievements = $"<size=70%>{union.Join(ToAchievementString, "\n")}</size>";
        var incompleteAchievements = $"<size=70%>{allAchievements.Except(union).Join(ToAchievementString, "\n")}</size>";

        Utils.SendMessage(incompleteAchievements, player.PlayerId, GetString("IncompleteAchievementsTitle"));
        Utils.SendMessage(completedAchievements, player.PlayerId, GetString("CompletedAchievementsTitle") + $" <#00a5ff>(<#00ffa5>{union.Length}</color>/{allAchievements.Length})</color>");
    }

    private static void EnableAllRolesCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        Prompt.Show(
            GetString("Promt.EnableAllRoles"),
            () => Options.CustomRoleSpawnChances.Values.DoIf(x => x.GetValue() == 0, x => x.SetValue(1)),
            () => Utils.EnterQuickSetupRoles(false));
    }

    public static void ReadyCheckCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
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

    private static void ReadyCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        ReadyPlayers.Add(player.PlayerId);
    }

    public static void DraftStartCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (Options.CurrentGameMode != CustomGameMode.Standard) return;

        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        DraftResult = [];

        byte[] allPlayerIds = Main.AllPlayerControls.Select(x => x.PlayerId).ToArray();
        List<CustomRoles> allRoles = Enum.GetValues<CustomRoles>().Where(x => x < CustomRoles.NotAssigned && x.IsEnable() && !x.IsForOtherGameMode() && !CustomHnS.AllHnSRoles.Contains(x) && !x.IsVanilla() && x is not CustomRoles.GM).ToList();

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

        int maxRolesPerPlayer = Options.DraftMaxRolesPerPlayer.GetInt();

        DraftRoles = allRoles
            .Take(allPlayerIds.Length * maxRolesPerPlayer)
            .CombineWith(impRoles, nkRoles, nnkRoles, covenRoles)
            .Shuffle()
            .Partition(allPlayerIds.Length)
            .Zip(allPlayerIds)
            .ToDictionary(x => x.Second, x => x.First.Take(maxRolesPerPlayer).ToList());

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
                if (DraftResult.Count >= DraftRoles.Count || !GameStates.IsLobby || GameStates.InGame) yield break;
            }
        }
    }

    private static void DraftDescriptionCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        if (DraftRoles.Count == 0 || !DraftRoles.TryGetValue(player.PlayerId, out List<CustomRoles> roles) || args.Length < 2 || !int.TryParse(args[1], out int chosenIndex) || roles.Count < chosenIndex) return;

        CustomRoles role = roles[chosenIndex - 1];
        string coloredString = role.ToColoredString();
        string roleName = GetString(role.ToString());
        StringBuilder sb = new();
        StringBuilder settings = new();
        var title = $"{coloredString} {Utils.GetRoleMode(role)}";
        sb.Append(GetString($"{role}InfoLong").FixRoleName(role).TrimStart());
        if (Options.CustomRoleSpawnChances.TryGetValue(role, out StringOptionItem chance)) AddSettings(chance);
        if (role is CustomRoles.LovingCrewmate or CustomRoles.LovingImpostor && Options.CustomRoleSpawnChances.TryGetValue(CustomRoles.Lovers, out chance)) AddSettings(chance);
        string txt = $"<size=90%>{sb}</size>".Replace(roleName, coloredString).Replace(roleName.ToLower(), coloredString);
        sb.Clear().Append(txt);
        if (settings.Length > 0) Utils.SendMessage("\n", player.PlayerId, settings.ToString());
        Utils.SendMessage(sb.ToString(), player.PlayerId, title);
        return;

        void AddSettings(StringOptionItem stringOptionItem)
        {
            settings.AppendLine($"<size=70%><u>{GetString("SettingsForRoleText")} <{Main.RoleColors[role]}>{roleName}</color>:</u>");
            Utils.ShowChildrenSettings(stringOptionItem, ref settings, disableColor: false);
            settings.Append("</size>");
        }
    }

    private static void DraftCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        if (DraftRoles.Count == 0 || !DraftRoles.TryGetValue(player.PlayerId, out List<CustomRoles> roles) || args.Length < 2 || !int.TryParse(args[1], out int chosenIndex)) return;

        if (roles.Count < chosenIndex || chosenIndex < 1)
        {
            DraftResult.Remove(player.PlayerId);
            Utils.SendMessage(string.Format(GetString("DraftChosen"), GetString("pet_RANDOM_FOR_EVERYONE")), player.PlayerId, GetString("DraftTitle"));
            return;
        }

        CustomRoles role = roles[chosenIndex - 1];
        DraftResult[player.PlayerId] = role;
        Utils.SendMessage(string.Format(GetString("DraftChosen"), role.ToColoredString()), player.PlayerId, GetString("DraftTitle"));

        if (DraftResult.Count >= DraftRoles.Count) Utils.SendMessage("\n", PlayerControl.LocalPlayer.PlayerId, GetString("EveryoneDrafted"));
    }

    private static void MuteCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
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

    private static void UnmuteCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        if (args.Length < 2 || !byte.TryParse(args[1], out byte id)) return;

        MutedPlayers.Remove(id);
        Utils.SendMessage("\n", player.PlayerId, string.Format(GetString("PlayerUnmuted"), id.ColoredPlayerName()));
        Utils.SendMessage("\n", id, string.Format(GetString("YouUnmuted"), player.PlayerId.ColoredPlayerName()));
        if (!player.IsHost()) Utils.SendMessage("\n", 0, string.Format(GetString("AdminUnmuted"), player.PlayerId.ColoredPlayerName(), id.ColoredPlayerName()));
    }

    private static void NegotiationCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        if (!Negotiator.On || !player.IsAlive() || args.Length < 2 || !int.TryParse(args[1], out int index)) return;

        Negotiator.ReceiveCommand(player, index);

        MeetingManager.SendCommandUsedMessage(args[0]);
    }

    private static void OSCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }
        
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

    private static void NoteCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (Starspawn.IsDayBreak) return;

        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        if (!player.Is(CustomRoles.Journalist) || !player.IsAlive()) return;

        Journalist.OnReceiveCommand(player, args);

        MeetingManager.SendCommandUsedMessage(args[0]);
    }

    private static void AssumeCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (Starspawn.IsDayBreak) return;

        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        if (args.Length < 3 || !byte.TryParse(args[1], out byte id) || !int.TryParse(args[2], out int num) || !player.Is(CustomRoles.Assumer) || !player.IsAlive()) return;

        Assumer.Assume(player.PlayerId, id, num);

        MeetingManager.SendCommandUsedMessage(args[0]);
    }

    private static void DeleteVIPCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (args.Length < 2 || !byte.TryParse(args[1], out byte VIPId)) return;

        PlayerControl VIPPc = Utils.GetPlayerById(VIPId);
        if (VIPPc == null) return;

        string fc = VIPPc.FriendCode.Replace(':', '#');
        if (!IsPlayerVIP(fc)) Utils.SendMessage(GetString("PlayerNotVIP"), player.PlayerId);

        string[] lines = File.ReadAllLines($"{Main.DataPath}/EHR_DATA/VIPs.txt").Where(line => !line.Contains(fc)).ToArray();
        File.WriteAllLines($"{Main.DataPath}/EHR_DATA/VIPs.txt", lines);
        Utils.SendMessage(GetString("PlayerRemovedFromVIPList"), player.PlayerId);
    }

    private static void AddVIPCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (args.Length < 2 || !byte.TryParse(args[1], out byte newVIPId)) return;

        PlayerControl newVIPPc = Utils.GetPlayerById(newVIPId);
        if (newVIPPc == null) return;

        string fc = newVIPPc.FriendCode.Replace(':', '#');
        if (IsPlayerVIP(fc)) Utils.SendMessage(GetString("PlayerAlreadyVIP"), player.PlayerId);

        File.AppendAllText($"{Main.DataPath}/EHR_DATA/VIPs.txt", $"\n{fc}");
        Utils.SendMessage(GetString("PlayerAddedToVIPList"), player.PlayerId);
    }

    private static void DecreeCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (Starspawn.IsDayBreak) return;

        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        if (!player.Is(CustomRoles.President)) return;

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

    private static void HMCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        if (!player.Is(CustomRoles.Messenger) || Messenger.Sent.Contains(player.PlayerId) || args.Length < 2 || !int.TryParse(args[1], out int id) || id is > 3 or < 1) return;

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
    private static void PollCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
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

        int splitIndex = Array.IndexOf(args, args.First(x => x.Contains('?'))) + 1;
        string[] answers = args[splitIndex..];

        string msg = string.Join(" ", args[1..splitIndex]) + "\n";
        bool gmPoll = msg.Contains(GetString("GameModePoll.Question"));
        bool mPoll = msg.Contains(GetString("MapPoll.Question"));
        
        if (gmPoll && GMPollGameModes.Count > 6) msg += "<size=70%>";

        PollTimer = gmPoll ? 60f : 45f;
        Color[] gmPollColors = gmPoll ? Main.GameModeColors.Where(x => GMPollGameModes.Contains(x.Key)).Select(x => x.Value).ToArray() : [];
        

        for (var i = 0; i < Math.Max(answers.Length, 2); i++)
        {
            var choiceLetter = (char)(i + 65);
            msg += Utils.ColorString(gmPoll ? gmPollColors[i] : RandomColor(), $"{char.ToUpper(choiceLetter)}) {answers[i]}\n");
            PollVotes[choiceLetter] = 0;
            PollAnswers[choiceLetter] = $"<size=70%>〖 {answers[i]} 〗</size>";
        }

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

            while ((notEveryoneVoted || gmPoll || mPoll) && PollTimer > 0f)
            {
                if (!GameStates.IsLobby) yield break;

                notEveryoneVoted = Main.AllPlayerControls.Length - 1 > PollVotes.Values.Sum();
                PollTimer -= Time.deltaTime;
                resendTimer += Time.deltaTime;

                if (resendTimer > 23f)
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

            if (winners.Length is > 0 and < 4 && GameStates.IsLobby)
            {
                int winnerIndex = (winners.Length == 1 ? winners[0].Key : winners.RandomElement().Key) - 65;
                if (gmPoll) Options.GameMode.SetValue((int)GMPollGameModes[winnerIndex] - 1, doSave: true, doSync: true);
                if (mPoll) Main.NormalOptions.MapId = (byte)winnerIndex;
            }
        }

        static Color32 RandomColor()
        {
            byte[] colors = IRandom.Sequence(3, 0, 160).Select(x => (byte)x).ToArray();
            return new(colors[0], colors[1], colors[2], 255);
        }
    }

    private static void PVCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
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

    private static void HelpCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        Utils.ShowHelp(player.PlayerId);
    }

    private static void DumpCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        Utils.DumpLog();
    }

    private static void GNOCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        if (!GameStates.IsLobby && player.IsAlive())
        {
            Utils.SendMessage(GetString("GNoCommandInfo"), player.PlayerId, sendOption: SendOption.None);
            return;
        }

        string subArgs = args.Length != 2 ? "" : args[1];

        if (subArgs == "" || !int.TryParse(subArgs, out int guessedNo) || guessedNo is < 0 or > 99)
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

    private static void SDCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        if (args.Length < 1 || !int.TryParse(args[1], out int sound1)) return;

        RPC.PlaySoundRPC(player.PlayerId, (Sounds)sound1);
    }

    private static void CSDCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        string subArgs = text.Remove(0, 3);
        player.RPCPlayCustomSound(subArgs.Trim());
    }

    private static void MTHYCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        if (GameStates.IsMeeting)
        {
            MeetingHudRpcClosePatch.AllowClose = true;
            MeetingHud.Instance.RpcClose();
        }
        else
            player.NoCheckStartMeeting(null, true);
    }

    private static void CosIDCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
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

    private static void EndCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        if (!player.IsHost() && !IsPlayerAdmin(player.FriendCode)) return;

        CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Draw);
        GameManager.Instance.LogicFlow.CheckEndCriteria();
    }

    private static void ChangeRoleCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
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
                player.RpcChangeRoleBasis(rl);

                if (rl.IsGhostRole()) GhostRolesManager.SpecificAssignGhostRole(player.PlayerId, rl, true);

                Main.PlayerStates[player.PlayerId].RemoveSubRole(CustomRoles.NotAssigned);
                break;
            }
        }
    }

    private static void IDCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        string msgText = GetString("PlayerIdList");
        msgText = Main.AllPlayerControls.Aggregate(msgText, (current, pc) => $"{current}\n{pc.PlayerId} \u2192 {pc.GetRealName()}");

        Utils.SendMessage(msgText, player.PlayerId);
    }

    private static void ColorCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
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

    private static void KillCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
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

    private static void ExeCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
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

    private static void BanKickCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
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

        if (string.IsNullOrWhiteSpace(subArgs) || !byte.TryParse(subArgs, out byte kickPlayerId))
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
        if (IsPlayerModerator(kickedPlayer.FriendCode) && !player.IsHost())
        {
            Utils.SendMessage(GetString("KickCommandKickMod"), player.PlayerId, sendOption: SendOption.None);
            return;
        }

        try
        {
            string kickedPlayerName = kickedPlayer.GetRealName();
            var textToSend = $"{kickedPlayerName} {GetString("KickCommandKicked")}";
            if (GameStates.IsInGame) textToSend += string.Format(GetString("KickCommandKickedRole"), kickedPlayer.GetCustomRole().ToColoredString());
            if (args.Length >= 3) textToSend += $"\n{GetString("KickCommandKickedReason")} {string.Join(' ', args[2..])}";

            Utils.SendMessage(textToSend, sendOption: GameStates.IsInGame ? SendOption.Reliable : SendOption.None);
        
            string modLogFilePath = $"{Main.DataPath}/EHR_DATA/ModLogs/{DateTime.Now:yyyy-MM-dd}.txt";
        
            if (!File.Exists(modLogFilePath))
            {
                string directoryName = Path.GetDirectoryName(modLogFilePath);
                if (!string.IsNullOrWhiteSpace(directoryName)) Directory.CreateDirectory(directoryName);
                File.WriteAllText(modLogFilePath, "=== Moderation Log ===\n");
            }
        
            string logEntry = $"[{DateTime.Now:HH:mm:ss}] {player.GetRealName()} {(args[0] == "/ban" ? "banned" : "kicked")} {kickedPlayerName} [{kickedPlayer.FriendCode}|{kickedPlayer.GetClient().GetHashedPuid()}] for {(args.Length >= 3 ? string.Join(' ', args[2..]) : "[no reason provided]")}\n";
            File.AppendAllText(modLogFilePath, logEntry);
        }
        catch (Exception e) { Utils.ThrowException(e); }
        
        AmongUsClient.Instance.KickPlayer(kickedPlayer.OwnerId, args[0] == "/ban");
    }

    private static void CheckCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (Starspawn.IsDayBreak) return;

        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        if (!player.IsAlive() || !player.Is(CustomRoles.Inquirer) || player.GetAbilityUseLimit() < 1) return;

        if (args.Length < 3 || !GuessManager.MsgToPlayerAndRole(text[6..], out byte checkId, out CustomRoles checkRole, out _)) return;

        bool hasRole = Utils.GetPlayerById(checkId).Is(checkRole);
        if (IRandom.Instance.Next(100) < Inquirer.FailChance.GetInt()) hasRole = !hasRole;

        LateTask.New(() => Utils.SendMessage(GetString(hasRole ? "Inquirer.MessageTrue" : "Inquirer.MessageFalse"), player.PlayerId), 0.2f, log: false);
        player.RpcRemoveAbilityUse();

        MeetingManager.SendCommandUsedMessage(args[0]);
    }

    private static void ChatCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (Starspawn.IsDayBreak) return;

        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        if (!Ventriloquist.On || !player.IsAlive() || !player.Is(CustomRoles.Ventriloquist) || player.PlayerId.GetAbilityUseLimit() < 1) return;

        var vl2 = (Ventriloquist)Main.PlayerStates[player.PlayerId].Role;
        if (vl2.Target == byte.MaxValue) return;

        PlayerControl tg = Utils.GetPlayerById(vl2.Target);
        string msg = text[6..];
        LateTask.New(() => tg?.RpcSendChat(msg), 0.2f, log: false);
        ChatManager.AddChatHistory(tg, msg);

        player.RpcRemoveAbilityUse();

        MeetingManager.SendCommandUsedMessage(args[0]);
    }

    public static void TargetCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (Starspawn.IsDayBreak) return;

        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        if (!Ventriloquist.On || !player.IsAlive() || !player.Is(CustomRoles.Ventriloquist) || player.PlayerId.GetAbilityUseLimit() < 1) return;

        var vl = (Ventriloquist)Main.PlayerStates[player.PlayerId].Role;
        vl.Target = args.Length < 2 ? byte.MaxValue : byte.TryParse(args[1], out byte targetId) ? targetId : byte.MaxValue;

        player.RPCPlayCustomSound("Line");

        MeetingManager.SendCommandUsedMessage(args[0]);
    }

    private static void QSCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        if (!QuizMaster.On || !player.IsAlive()) return;

        var qm2 = (QuizMaster)Main.PlayerStates.Values.First(x => x.Role is QuizMaster).Role;
        if (qm2.Target != player.PlayerId || !QuizMaster.MessagesToSend.TryGetValue(player.PlayerId, out string msg)) return;

        Utils.SendMessage(msg, player.PlayerId, GetString("QuizMaster.QuestionSample.Title"));

        MeetingManager.SendCommandUsedMessage(args[0]);
    }

    private static void QACommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        if (args.Length < 2 || !QuizMaster.On || !player.IsAlive()) return;

        var qm = (QuizMaster)Main.PlayerStates.Values.First(x => x.Role is QuizMaster).Role;
        if (qm.Target != player.PlayerId) return;

        qm.Answer(args[1].ToUpper());

        MeetingManager.SendCommandUsedMessage(args[0]);
    }

    private static void AnswerCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        if (args.Length < 2) return;

        Mathematician.Reply(player, args[1]);
    }

    private static void AskCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (Starspawn.IsDayBreak) return;

        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        if (args.Length < 3 || !player.Is(CustomRoles.Mathematician)) return;

        Mathematician.Ask(player, args[1], args[2]);
    }

    private static void VoteCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        if (text.Length < 6 || !GameStates.IsMeeting) return;

        string toVote = text[6..].Replace(" ", string.Empty);
        if (!byte.TryParse(toVote, out byte voteId) || MeetingHud.Instance.playerStates?.FirstOrDefault(x => x.TargetPlayerId == player.PlayerId)?.DidVote is true or null) return;

        if (voteId > Main.AllPlayerControls.Length) return;

        PlayerControl votedPlayer = voteId.GetPlayer();
        if (!player.UsesMeetingShapeshift() && Main.PlayerStates.TryGetValue(player.PlayerId, out PlayerState state) && votedPlayer != null && state.Role.OnVote(player, votedPlayer)) return;

        MeetingHud.Instance.CastVote(player.PlayerId, voteId);
    }

    private static void SayCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        if (!player.IsHost() && !IsPlayerModerator(player.FriendCode)) return;

        if (args.Length > 1) Utils.SendMessage(args[1..].Join(delimiter: " "), title: $"<color=#ff0000>{GetString(player.IsHost() ? "MessageFromTheHost" : "SayTitle")}</color>");
    }

    private static void DeathCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        if (!GameStates.IsInGame) return;
        if (Main.DiedThisRound.Contains(player.PlayerId) && Utils.IsRevivingRoleAlive()) return;

        PlayerControl target = args.Length < 2 || !byte.TryParse(args[1], out byte targetId) ? player : targetId.GetPlayer();
        if (target == null) return;

        PlayerControl killer = target.GetRealKiller();

        if (killer == null)
        {
            Utils.SendMessage("\n", player.PlayerId, string.Format(GetString("DeathCommandFail"), GetString($"DeathReason.{Main.PlayerStates[target.PlayerId].deathReason}")), sendOption: SendOption.None);
            return;
        }

        Utils.SendMessage("\n", player.PlayerId, string.Format(GetString("DeathCommand"), killer.PlayerId.ColoredPlayerName(), (killer.Is(CustomRoles.Bloodlust) ? $"{CustomRoles.Bloodlust.ToColoredString()} " : string.Empty) + killer.GetCustomRole().ToColoredString()));
    }

    private static void MessageWaitCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (args.Length > 1 && int.TryParse(args[1], out int sec))
        {
            Main.MessageWait.Value = sec;
            Utils.SendMessage(string.Format(GetString("Message.SetToSeconds"), sec), 0);
        }
        else
            Utils.SendMessage($"{GetString("Message.MessageWaitHelp")}\n{GetString("ForExample")}:\n{args[0]} 3", 0);
    }

    private static void TemplateCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        if (player.AmOwner)
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

    private static void TPInCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        if (!GameStates.IsLobby) return;

        if (!Options.PlayerCanTPInAndOut.GetBool() && !IsPlayerVIP(player.FriendCode) && !player.FriendCode.GetDevUser().up)
        {
            Utils.SendMessage(GetString("Message.OnlyVIPCanUse"), player.PlayerId, sendOption: SendOption.None);
            return;
        }

        player.TP(new Vector2(-0.2f, 1.3f));
    }

    private static void TPOutCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        if (!GameStates.IsLobby) return;

        if (!Options.PlayerCanTPInAndOut.GetBool() && !IsPlayerVIP(player.FriendCode) && !player.FriendCode.GetDevUser().up)
        {
            Utils.SendMessage(GetString("Message.OnlyVIPCanUse"), player.PlayerId, sendOption: SendOption.None);
            return;
        }

        player.TP(new Vector2(0.1f, 3.8f));
    }

    private static void MyRoleCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
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
                sb.Append($"\n\n{subRole.ToColoredString()} {Utils.GetRoleMode(subRole)} {GetString($"{subRole}InfoLong").FixRoleName(subRole)}");
                string searchSubStr = GetString(subRole.ToString());
                sb.Replace(searchSubStr, subRole.ToColoredString());
                sb.Replace(searchSubStr.ToLower(), subRole.ToColoredString());
            }

            if (settings.Length > 0) Utils.SendMessage("\n", player.PlayerId, settings.ToString());

            Utils.SendMessage(sb.Append("</size>").ToString(), player.PlayerId, titleSb.ToString());
            if (role.UsesPetInsteadOfKill()) Utils.SendMessage("\n", player.PlayerId, GetString("UsesPetInsteadOfKillNotice"));
            if (player.UsesMeetingShapeshift()) Utils.SendMessage("\n", player.PlayerId, GetString("UsesMeetingShapeshiftNotice"));
        }
        else
            Utils.SendMessage((player.FriendCode.GetDevUser().HasTag() ? "\n" : string.Empty) + GetString("Message.CanNotUseInLobby"), player.PlayerId);
    }

    private static void AFKExemptCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        if (args.Length < 2 || !byte.TryParse(args[1], out byte afkId)) return;

        AFKDetector.ExemptedPlayers.Add(afkId);
        Utils.SendMessage("\n", player.PlayerId, string.Format(GetString("PlayerExemptedFromAFK"), afkId.ColoredPlayerName()));
    }

    private static void EffectCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        if (args.Length < 2 || !GameStates.IsInTask || !Randomizer.Exists) return;

        if (Enum.TryParse(args[1], true, out Randomizer.Effect effect)) effect.Apply(player);
    }

    private static void ComboCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        if (!player.IsHost() || args.Length < 4)
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

    private static void DeleteModCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        if (args.Length < 2 || !byte.TryParse(args[1], out byte remModId)) return;

        PlayerControl remModPc = Utils.GetPlayerById(remModId);
        if (remModPc == null) return;

        string remFc = remModPc.FriendCode.Replace(':', '#');

        if (!IsPlayerModerator(remFc))
        {
            Utils.SendMessage(GetString("PlayerNotMod"), player.PlayerId);
            return;
        }

        File.WriteAllLines($"{Main.DataPath}/EHR_DATA/Moderators.txt", File.ReadAllLines($"{Main.DataPath}/EHR_DATA/Moderators.txt").Where(x => !x.Contains(remFc)));
        Utils.SendMessage(GetString("PlayerRemovedFromModList"), player.PlayerId);
    }

    private static void AddModCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        if (args.Length < 2 || !byte.TryParse(args[1], out byte newModId)) return;

        PlayerControl newModPc = Utils.GetPlayerById(newModId);
        if (newModPc == null) return;

        string fc = newModPc.FriendCode.Replace(':', '#');

        if (IsPlayerModerator(fc))
        {
            Utils.SendMessage(GetString("PlayerAlreadyMod"), player.PlayerId);
            return;
        }

        File.AppendAllText($"{Main.DataPath}/EHR_DATA/Moderators.txt", $"\n{fc}");
        Utils.SendMessage(GetString("PlayerAddedToModList"), player.PlayerId);
    }

    private static void KCountCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        if (GameStates.IsLobby || !Options.EnableKillerLeftCommand.GetBool() || Main.AllAlivePlayerControls.Length < Options.MinPlayersForGameStateCommand.GetInt())
        {
            Utils.SendMessage(GetString("Message.CommandUnavailable"), player.PlayerId, sendOption: SendOption.None);
            return;
        }

        Utils.SendMessage("\n", player.PlayerId, Utils.GetGameStateData());
    }

    private static void SetRoleCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        string subArgs = string.Join(' ', args[1..]);

        if (!GuessManager.MsgToPlayerAndRole(subArgs, out byte resultId, out CustomRoles roleToSet, out _))
        {
            Utils.SendMessage(GetString("InvalidArguments"), player.PlayerId);
            return;
        }

        if (resultId != 0 && !player.FriendCode.GetDevUser().up && !GameStates.IsLocalGame)
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

    private static void UpCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        Utils.SendMessage($"{GetString("UpReplacedMessage")}", player.PlayerId);
    }

    private static void RCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        string subArgs = text.Remove(0, 2);
        byte to = player.AmOwner && Input.GetKeyDown(KeyCode.LeftShift) ? byte.MaxValue : player.PlayerId;
        SendRolesInfo(subArgs, to);
    }

    private static void DisconnectCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        string subArgs = args.Length < 2 ? string.Empty : args[1];

        switch (subArgs)
        {
            case "crew":
                GameManager.Instance.enabled = false;
                GameManager.Instance.ShouldCheckForGameEnd = false;
                MessageWriter msg = AmongUsClient.Instance.StartEndGame();
                msg.Write((byte)6);
                msg.Write(false);
                AmongUsClient.Instance.FinishEndGame(msg);
                break;

            case "imp":
                GameManager.Instance.enabled = false;
                GameManager.Instance.ShouldCheckForGameEnd = false;
                MessageWriter msg2 = AmongUsClient.Instance.StartEndGame();
                msg2.Write((byte)5);
                msg2.Write(false);
                AmongUsClient.Instance.FinishEndGame(msg2);
                break;

            default:
                if (!HudManager.InstanceExists) break;
                HudManager.Instance.Chat.AddChat(player, "crew | imp");
                break;
        }

        ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Admin, 0);
    }

    private static void NowCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
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

    private static void LevelCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
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

    private static void HideNameCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        Main.HideName.Value = args.Length > 1 ? string.Join(' ', args[1..]) : Main.HideName.DefaultValue.ToString();

        GameStartManagerPatch.GameStartManagerStartPatch.HideName.text =
            ColorUtility.TryParseHtmlString(Main.HideColor.Value, out _)
                ? $"<color={Main.HideColor.Value}>{Main.HideName.Value}</color>"
                : $"<color={Main.ModColor}>{Main.HideName.Value}</color>";
    }

    private static void RenameCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        if (args.Length < 2) return;

        string name = Regex.Replace(string.Join(' ', args[1..]), "<size=[^>]*>", string.Empty).Trim();

        if (name.RemoveHtmlTags().Length is > 15 or < 1)
            Utils.SendMessage(GetString("Message.AllowNameLength"), player.PlayerId, sendOption: SendOption.None);
        else
        {
            if (player.AmOwner)
                Main.NickName = name;
            else
            {
                if (!Options.PlayerCanSetName.GetBool() && !IsPlayerVIP(player.FriendCode) && !player.FriendCode.GetDevUser().up)
                {
                    Utils.SendMessage(GetString("Message.OnlyVIPCanUse"), player.PlayerId, sendOption: SendOption.None);
                    return;
                }

                if (GameStates.IsInGame)
                {
                    Utils.SendMessage(GetString("Message.OnlyCanUseInLobby"), player.PlayerId, sendOption: SendOption.None);
                    return;
                }

                Main.AllPlayerNames[player.PlayerId] = name;
                player.RpcSetName(name);
            }
        }
    }

    private static void LastResultCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        Utils.ShowKillLog(player.PlayerId);
        Utils.ShowLastAddOns(player.PlayerId);
        Utils.ShowLastRoles(player.PlayerId);
        Utils.ShowLastResult(player.PlayerId);
    }

    private static void WinnerCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        if (Main.WinnerNameList.Count == 0)
            Utils.SendMessage(GetString("NoInfoExists"), sendOption: SendOption.None);
        else
            Utils.SendMessage("<b><u>Winners:</b></u>\n" + string.Join(", ", Main.WinnerNameList));
    }

    private static void ChangeSettingCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
            return;
        }

        string subArgs = args.Length < 2 ? "" : args[1];

        switch (subArgs)
        {
            case "map":
                subArgs = args.Length < 3 ? "" : args[2];

                switch (subArgs)
                {
                    case "skeld":
                    case "theskeld":
                        GameOptionsManager.Instance.CurrentGameOptions.SetByte(ByteOptionNames.MapId, 0);
                        break;
                    case "mira":
                    case "mirahq":
                        GameOptionsManager.Instance.CurrentGameOptions.SetByte(ByteOptionNames.MapId, 1);
                        break;
                    case "polus":
                        GameOptionsManager.Instance.CurrentGameOptions.SetByte(ByteOptionNames.MapId, 2);
                        break;
                    case "dleks":
                    case "dlekseht":
                        GameOptionsManager.Instance.CurrentGameOptions.SetByte(ByteOptionNames.MapId, 3);
                        break;
                    case "airship":
                        GameOptionsManager.Instance.CurrentGameOptions.SetByte(ByteOptionNames.MapId, 4);
                        break;
                    case "fungle":
                    case "thefungle":
                        GameOptionsManager.Instance.CurrentGameOptions.SetByte(ByteOptionNames.MapId, 5);
                        break;
                    case "submerged" when SubmergedCompatibility.Loaded:
                        GameOptionsManager.Instance.CurrentGameOptions.SetByte(ByteOptionNames.MapId, 6);
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
            case "vipercount":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.CurrentGameOptions.RoleOptions.SetRoleRate(RoleTypes.Viper, int.Parse(subArgs), GameOptionsManager.Instance.CurrentGameOptions.RoleOptions.GetChancePerGame(RoleTypes.Viper));
                break;
            case "viperchance":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.CurrentGameOptions.RoleOptions.SetRoleRate(RoleTypes.Viper, GameOptionsManager.Instance.CurrentGameOptions.RoleOptions.GetNumPerGame(RoleTypes.Viper), int.Parse(subArgs));
                break;
            case "viperdissolvetime":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.CurrentGameOptions.SetFloat(FloatOptionNames.ViperDissolveTime, float.Parse(subArgs));
                break;
            case "detectivecount":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.CurrentGameOptions.RoleOptions.SetRoleRate(RoleTypes.Detective, int.Parse(subArgs), GameOptionsManager.Instance.CurrentGameOptions.RoleOptions.GetChancePerGame(RoleTypes.Detective));
                break;
            case "detectivechance":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.CurrentGameOptions.RoleOptions.SetRoleRate(RoleTypes.Detective, GameOptionsManager.Instance.CurrentGameOptions.RoleOptions.GetNumPerGame(RoleTypes.Detective), int.Parse(subArgs));
                break;
            case "detectivesuspectlimit":
                subArgs = args.Length < 3 ? "" : args[2];
                GameOptionsManager.Instance.CurrentGameOptions.SetFloat(FloatOptionNames.DetectiveSuspectLimit, float.Parse(subArgs));
                break;
            default:
                Utils.SendMessage(GetString("Commands.ChangeSettingHelp"), player.PlayerId);
                break;
        }

        GameOptionsManager.Instance.GameHostOptions = GameOptionsManager.Instance.CurrentGameOptions;
        GameManager.Instance.LogicOptions.SyncOptions();
    }

    private static void VersionCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        string versionText = Main.PlayerVersion.OrderBy(pair => pair.Key).Aggregate(string.Empty, (current, kvp) => current + $"{kvp.Key}: ({Main.AllPlayerNames[kvp.Key]}) {kvp.Value.forkId}/{kvp.Value.version}({kvp.Value.tag})\n");
        if (versionText != string.Empty && HudManager.InstanceExists) HudManager.Instance.Chat.AddChat(player, (player.FriendCode.GetDevUser().HasTag() ? "\n" : string.Empty) + versionText);
    }

    private static void LTCommand(PlayerControl player, string commandKey, string text, string[] args)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            RequestCommandProcessingFromHost(text, commandKey);
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

        return text switch { _ => text };
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
        
        string nameWithoutId = Regex.Replace(name.Replace(" ", string.Empty), @"^\d+", string.Empty);

        foreach (CustomRoles rl in Enum.GetValues<CustomRoles>())
        {
            if (rl.IsVanilla()) continue;
            
            string roleName = Regex.Replace(GetString(rl.ToString()).RemoveHtmlTags().ToLower(), @"[^\p{L}-]+", string.Empty);

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
        if (Options.CurrentGameMode != CustomGameMode.Standard)
        {
            string text = GetString($"ModeDescribe.{Options.CurrentGameMode}");
            Utils.SendMessage(text, playerId, sendOption: SendOption.None);
            if (Options.CurrentGameMode != CustomGameMode.HideAndSeek) return;
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

            string roleName = Regex.Replace(GetString(rl.ToString()).RemoveHtmlTags().ToLower().Trim().TrimStart('*'), @"[^\p{L}-]+", string.Empty);

            if (role == roleName)
            {
                if ((isDev || isUp) && GameStates.IsLobby)
                {
                    var devMark = "▲";
                    if (rl.IsAdditionRole() || rl is CustomRoles.GM) devMark = string.Empty;

                    if (rl.GetCount() < 1 || rl.GetMode() == 0) devMark = string.Empty;

                    if (isUp) Utils.SendMessage(devMark == "▲" ? string.Format(GetString("Message.YTPlanSelected"), roleName) : string.Format(GetString("Message.YTPlanSelectFailed"), roleName), playerId, sendOption: SendOption.None);

                    if (isUp) return;
                }

                string coloredString = rl.ToColoredString();
                StringBuilder sb = new();
                StringBuilder settings = new();
                var title = $"{coloredString} {Utils.GetRoleMode(rl)}";
                sb.Append(GetString($"{rl}InfoLong").FixRoleName(rl).TrimStart());
                if (Options.CustomRoleSpawnChances.TryGetValue(rl, out StringOptionItem chance)) AddSettings(chance);
                if (rl is CustomRoles.LovingCrewmate or CustomRoles.LovingImpostor && Options.CustomRoleSpawnChances.TryGetValue(CustomRoles.Lovers, out chance)) AddSettings(chance);

                string txt = $"<size=90%>{sb}</size>".Replace(roleName, coloredString, StringComparison.OrdinalIgnoreCase);
                sb.Clear().Append(txt);

                if (rl.PetActivatedAbility()) sb.Append($"<size=50%>{GetString("SupportsPetMessage")}</size>");

                if (settings.Length > 0) Utils.SendMessage("\n", playerId, settings.ToString());
                if (rl.UsesPetInsteadOfKill()) Utils.SendMessage("\n", playerId, GetString("UsesPetInsteadOfKillNotice"));
                if (rl.UsesMeetingShapeshift()) Utils.SendMessage("\n", playerId, GetString("UsesMeetingShapeshiftNotice"));

                Utils.SendMessage(sb.ToString(), playerId, title);
                return;

                void AddSettings(StringOptionItem stringOptionItem)
                {
                    settings.AppendLine($"<size=70%><u>{GetString("SettingsForRoleText")} {rl.ToColoredString()}:</u>");
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
                Utils.SendMessage(text, playerId, gmString, sendOption: SendOption.None);
                return;
            }
        }

        Utils.SendMessage(isUp ? GetString("Message.YTPlanCanNotFindRoleThePlayerEnter") : GetString("Message.CanNotFindRoleThePlayerEnter"), playerId, sendOption: SendOption.None);
    }

    // -------------------------------------------------------------------------------------------------------------------------

    public static void OnReceiveChat(PlayerControl player, string text, out bool canceled)
    {
        canceled = false;
        if (!AmongUsClient.Instance.AmHost || player.AmOwner) return;

        long now = Utils.TimeStamp;

        if (LastSentCommand.TryGetValue(player.PlayerId, out long ts) && ts + 2 >= now && !player.IsModdedClient())
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

        switch (Options.CurrentGameMode)
        {
            case CustomGameMode.TheMindGame when !player.IsModdedClient():
                TheMindGame.OnChat(player, text.ToLower());
                break;
            case CustomGameMode.BedWars:
                BedWars.OnChat(player, text);
                break;
        }

        CheckAnagramGuess(player.PlayerId, text.ToLower());

        foreach (PlayerState state in Main.PlayerStates.Values)
        {
            if (state.Role is Astral astral && astral.BackTS != 0 && state.Player != null && state.Player.PlayerId != player.PlayerId)
            {
                if (state.Player.AmOwner) canceled = true;
                else ChatManager.ClearChat(state.Player);
            }
        }

        string[] args = text.Split(' ');

        if (!Starspawn.IsDayBreak)
        {
            if (GuessManager.GuesserMsg(player, text) ||
                Judge.TrialMsg(player, text) ||
                Swapper.SwapMsg(player, text) ||
                Inspector.InspectorCheckMsg(player, text) ||
                Councillor.MurderMsg(player, text))
            {
                canceled = true;
                LastSentCommand[player.PlayerId] = now;
                return;
            }

            if (Medium.MsMsg(player, text) || Nemesis.NemesisMsgCheck(player, text))
            {
                LastSentCommand[player.PlayerId] = now;
                return;
            }
        }

        var commandEntered = false;

        if (text.StartsWith('/') && !player.IsModdedClient() && (!GameStates.IsMeeting || MeetingHud.Instance.state is not MeetingHud.VoteStates.Results and not MeetingHud.VoteStates.Proceeding))
        {
            foreach ((string key, Command command) in Command.AllCommands)
            {
                if (!command.IsThisCommand(text)) continue;

                Logger.Info($" Recognized command: {text}", "ReceiveChat");
                commandEntered = true;

                if (!command.CanUseCommand(player, sendErrorMessage: true))
                {
                    canceled = true;
                    break;
                }

                if (command.AlwaysHidden) ChatManager.SendPreviousMessagesToAll();
                command.Action(player, key, text, args);
                if (command.IsCanceled) canceled = command.AlwaysHidden || !Options.HostSeesCommandsEnteredByOthers.GetBool();
                break;
            }
        }

        if (!commandEntered && Astral.On && !player.Is(CustomRoles.Astral))
            Main.PlayerStates.Values.DoIf(x => !x.IsDead && x.Role is Astral { BackTS: > 0 } && x.Player != null, x => ChatManager.ClearChat(x.Player));

        if (CheckMute(player.PlayerId))
        {
            canceled = true;
            ChatManager.SendPreviousMessagesToAll();
            return;
        }

        if (ExileController.Instance)
        {
            canceled = true;
            HasMessageDuringEjectionScreen = true;
        }

        if (!canceled) ChatManager.SendMessage(player, text);

        switch (commandEntered)
        {
            case true:
                LastSentCommand[player.PlayerId] = now;
                break;
            case false:
                SpamManager.CheckSpam(player, text);
                break;
        }
    }
}

[HarmonyPatch(typeof(ChatController), nameof(ChatController.Update))]
internal static class ChatUpdatePatch
{
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
        int clientId = sendTo == byte.MaxValue ? -1 : Utils.GetPlayerById(sendTo).OwnerId;

        string name = player.Data.PlayerName;

        if (clientId == -1 && HudManager.InstanceExists)
        {
            player.SetName(title);
            HudManager.Instance.Chat.AddChat(player, msg);
            player.SetName(name);
        }

        sender.AutoStartRpc(player.NetId, RpcCalls.SetName, clientId)
            .Write(player.Data.NetId)
            .Write(title)
            .EndRpc();

        sender.AutoStartRpc(player.NetId, RpcCalls.SendChat, clientId)
            .Write(msg)
            .EndRpc();

        sender.AutoStartRpc(player.NetId, RpcCalls.SetName, clientId)
            .Write(player.Data.NetId)
            .Write(player.Data.PlayerName)
            .EndRpc();

        if (sender.stream.Length > 500)
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
        if (AmongUsClient.Instance.AmClient && HudManager.InstanceExists) HudManager.Instance.Chat.AddChat(__instance, chatText);

        if (chatText.Contains("who", StringComparison.OrdinalIgnoreCase)) UnityTelemetry.Instance.SendWho();

        MessageWriter messageWriter = AmongUsClient.Instance.StartRpcImmediately(__instance.NetId, (byte)RpcCalls.SendChat, SendOption.Reliable);
        messageWriter.Write(chatText);
        AmongUsClient.Instance.FinishRpcImmediately(messageWriter);
        __result = true;
        return false;
    }
}
