using Assets.CoreScripts;
using HarmonyLib;
using Hazel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TOHE.Modules;
using TOHE.Roles.Crewmate;
using TOHE.Roles.Impostor;
using UnityEngine;
using static TOHE.Translator;


namespace TOHE;

[HarmonyPatch(typeof(ChatController), nameof(ChatController.SendChat))]
internal class ChatCommands
{
    // Function to check if a player is a moderator
    private static bool IsPlayerModerator(string friendCode)
    {
        var friendCodesFilePath = @"./TOHE_DATA/Moderators.txt";
        var friendCodes = File.ReadAllLines(friendCodesFilePath);
        return friendCodes.Contains(friendCode) || friendCode.Contains(friendCode + "\n") || friendCode.Contains("\n" + friendCode);
    }

    public static List<string> ChatHistory = new();

    public static bool Prefix(ChatController __instance)
    {
        if (__instance.freeChatField.textArea.text == string.Empty) return false;
        __instance.timeSinceLastMessage = 3f;
        var text = __instance.freeChatField.textArea.text;
        if (!ChatHistory.Any() || ChatHistory[^1] != text) ChatHistory.Add(text);
        ChatControllerUpdatePatch.CurrentHistorySelection = ChatHistory.Count;
        string[] args = text.Split(' ');
        string subArgs = string.Empty;
        var canceled = false;
        var cancelVal = string.Empty;
        Main.isChatCommand = true;
        Logger.Info(text, "SendChat");
        ChatManager.SendMessage(PlayerControl.LocalPlayer, text);
        if (text.Length >= 3) if (text[..2] == "/r" && text[..3] != "/rn") args[0] = "/r";
        if (text.Length >= 4) if (text[..3] == "/up") args[0] = "/up";
        if (GuessManager.GuesserMsg(PlayerControl.LocalPlayer, text)) goto Canceled;
        if (Judge.TrialMsg(PlayerControl.LocalPlayer, text)) goto Canceled;
        if (NiceSwapper.SwapMsg(PlayerControl.LocalPlayer, text)) goto Canceled;
        if (ParityCop.ParityCheckMsg(PlayerControl.LocalPlayer, text)) goto Canceled;
        //if (Pirate.DuelCheckMsg(PlayerControl.LocalPlayer, text)) goto Canceled;
        if (Councillor.MurderMsg(PlayerControl.LocalPlayer, text)) goto Canceled;
        if (Mediumshiper.MsMsg(PlayerControl.LocalPlayer, text)) goto Canceled;
        if (MafiaRevengeManager.MafiaMsgCheck(PlayerControl.LocalPlayer, text)) goto Canceled;
        if (RetributionistRevengeManager.RetributionistMsgCheck(PlayerControl.LocalPlayer, text)) goto Canceled;
        switch (args[0])
        {
            case "/dump":
                canceled = true;
                Utils.DumpLog();
                break;
            case "/v":
            case "/version":
                canceled = true;
                string version_text = string.Empty;
                foreach (var kvp in Main.playerVersion.OrderBy(pair => pair.Key))
                {
                    version_text += $"{kvp.Key}:{Main.AllPlayerNames[kvp.Key]}:{kvp.Value.forkId}/{kvp.Value.version}({kvp.Value.tag})\n";
                }
                if (version_text != string.Empty) HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, (PlayerControl.LocalPlayer.FriendCode.GetDevUser().HasTag() ? "\n" : string.Empty) + version_text);
                break;
            default:
                Main.isChatCommand = false;
                break;
        }
        if (AmongUsClient.Instance.AmHost)
        {
            var localPlayerId = PlayerControl.LocalPlayer.PlayerId;
            Main.isChatCommand = true;
            switch (args[0])
            {
                case "/w":
                case "/win":
                case "/winner":
                    canceled = true;
                    if (!Main.winnerNameList.Any()) Utils.SendMessage(GetString("NoInfoExists"));
                    else Utils.SendMessage("<b><u>Winners:</b></u>\n" + string.Join(", ", Main.winnerNameList));
                    break;

                case "/l":
                case "/lastresult":
                    canceled = true;
                    Utils.ShowKillLog();
                    Utils.ShowLastRoles();
                    Utils.ShowLastResult();
                    break;

                case "/rn":
                case "/rename":
                    canceled = true;
                    if (args.Length < 1) break;
                    if (args[1].Length is > 10 or < 1)
                        Utils.SendMessage(GetString("Message.AllowNameLength"), localPlayerId);
                    else Main.nickName = args[1];
                    break;

                case "/hn":
                case "/hidename":
                    canceled = true;
                    Main.HideName.Value = args.Length > 1 ? args.Skip(1).Join(delimiter: " ") : Main.HideName.DefaultValue.ToString();
                    GameStartManagerPatch.GameStartManagerStartPatch.HideName.text =
                        ColorUtility.TryParseHtmlString(Main.HideColor.Value, out _)
                            ? $"<color={Main.HideColor.Value}>{Main.HideName.Value}</color>"
                            : $"<color={Main.ModColor}>{Main.HideName.Value}</color>";
                    break;

                case "/level":
                    canceled = true;
                    subArgs = args.Length < 2 ? string.Empty : args[1];
                    Utils.SendMessage(string.Format(GetString("Message.SetLevel"), subArgs), localPlayerId);
                    _ = int.TryParse(subArgs, out int input);
                    if (input is < 1 or > 999)
                    {
                        Utils.SendMessage(GetString("Message.AllowLevelRange"), localPlayerId);
                        break;
                    }
                    var number = Convert.ToUInt32(input);
                    PlayerControl.LocalPlayer.RpcSetLevel(number - 1);
                    break;

                case "/n":
                case "/now":
                    canceled = true;
                    subArgs = args.Length < 2 ? string.Empty : args[1];
                    switch (subArgs)
                    {
                        case "r":
                        case "roles":
                            Utils.ShowActiveRoles();
                            break;
                        case "a":
                        case "all":
                            Utils.ShowAllActiveSettings();
                            break;
                        default:
                            Utils.ShowActiveSettings();
                            break;
                    }
                    break;

                case "/dis":
                case "/disconnect":
                    canceled = true;
                    subArgs = args.Length < 2 ? string.Empty : args[1];
                    switch (subArgs)
                    {
                        case "crew":
                            GameManager.Instance.enabled = false;
                            GameManager.Instance.RpcEndGame(GameOverReason.HumansDisconnect, false);
                            break;

                        case "imp":
                            GameManager.Instance.enabled = false;
                            GameManager.Instance.RpcEndGame(GameOverReason.ImpostorDisconnect, false);
                            break;

                        default:
                            __instance.AddChat(PlayerControl.LocalPlayer, "crew | imp");
                            cancelVal = "/dis";
                            break;
                    }
                    ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Admin, 0);
                    break;

                case "/r":
                    canceled = true;
                    subArgs = text.Remove(0, 2);
                    SendRolesInfo(subArgs, 255, PlayerControl.LocalPlayer.FriendCode.GetDevUser().DeBug);
                    break;

                case "/up":
                    canceled = true;
                    subArgs = text.Remove(0, 3);
                    if (!PlayerControl.LocalPlayer.FriendCode.GetDevUser().IsUp) break;
                    if (!Options.EnableUpMode.GetBool())
                    {
                        Utils.SendMessage(string.Format(GetString("Message.YTPlanDisabled"), GetString("EnableYTPlan")), localPlayerId);
                        break;
                    }
                    if (!GameStates.IsLobby)
                    {
                        Utils.SendMessage(GetString("Message.OnlyCanUseInLobby"), localPlayerId);
                        break;
                    }
                    SendRolesInfo(subArgs, localPlayerId, isUp: true);
                    break;

                case "/h":
                case "/help":
                    canceled = true;
                    Utils.ShowHelp(localPlayerId);
                    break;
                case "/kcount":
                    canceled = true;
                    if (GameStates.IsLobby || !Options.EnableKillerLeftCommand.GetBool()) break;
                    int impnum = 0;
                    int neutralnum = 0;
                    foreach (PlayerControl pc in Main.AllAlivePlayerControls)
                    {
                        if (Options.ShowImpRemainOnEject.GetBool())
                        {
                            if (pc.GetCustomRole().IsImpostor())
                                impnum++;
                        }
                        if (Options.ShowNKRemainOnEject.GetBool())
                        {
                            if (pc.GetCustomRole().IsNK())
                                neutralnum++;
                        }
                    }
                    Utils.SendMessage($"There {(impnum == 1 ? "is" : "are")}\n<b>{impnum}</b> <color=#ff1919>{(impnum == 1 ? "Impostor" : "Impostors")}</color> and <b>{neutralnum}</b> <color=#7f8c8d>{(neutralnum == 1 ? "Neutral Killer" : "Neutral Killers")}</color> left.", localPlayerId);
                    break;
                case "/m":
                case "/myrole":
                    canceled = true;
                    var role = PlayerControl.LocalPlayer.GetCustomRole();
                    if (GameStates.IsInGame)
                    {
                        var lp = PlayerControl.LocalPlayer;
                        var sb = new StringBuilder();
                        _ = sb.Append(GetString(role.ToString()) + Utils.GetRoleMode(role) + lp.GetRoleInfo(true));
                        if (Options.CustomRoleSpawnChances.TryGetValue(role, out var opt))
                            Utils.ShowChildrenSettings(Options.CustomRoleSpawnChances[role], ref sb, command: true);
                        var txt = sb.ToString();
                        _ = sb.Clear().Append(txt.RemoveHtmlTags());
                        for (int i = 0; i < Main.PlayerStates[lp.PlayerId].SubRoles.Count; i++)
                        {
                            CustomRoles subRole = Main.PlayerStates[lp.PlayerId].SubRoles[i];
                            _ = sb.Append($"\n\n" + GetString($"{subRole}") + Utils.GetRoleMode(subRole) + GetString($"{subRole}InfoLong"));
                        }
                        Utils.SendMessage(sb.ToString(), lp.PlayerId);
                    }
                    else
                        Utils.SendMessage((PlayerControl.LocalPlayer.FriendCode.GetDevUser().HasTag() ? "\n" : string.Empty) + GetString("Message.CanNotUseInLobby"), localPlayerId);
                    break;

                case "/t":
                case "/template":
                    canceled = true;
                    if (args.Length > 1) TemplateManager.SendTemplate(args[1]);
                    else HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, (PlayerControl.LocalPlayer.FriendCode.GetDevUser().HasTag() ? "\n" : string.Empty) + $"{GetString("ForExample")}:\n{args[0]} test");
                    break;

                case "/mw":
                case "/messagewait":
                    canceled = true;
                    if (args.Length > 1 && int.TryParse(args[1], out int sec))
                    {
                        Main.MessageWait.Value = sec;
                        Utils.SendMessage(string.Format(GetString("Message.SetToSeconds"), sec), 0);
                    }
                    else Utils.SendMessage($"{GetString("Message.MessageWaitHelp")}\n{GetString("ForExample")}:\n{args[0]} 3", 0);
                    break;

                case "/say":
                case "/s":
                    canceled = true;
                    if (args.Length > 1)
                        Utils.SendMessage(args.Skip(1).Join(delimiter: " "), title: $"<color=#ff0000>{GetString("MessageFromTheHost")}</color>");
                    break;

                case "/kick":
                    canceled = true;
                    // Check if the kick command is enabled in the settings
                    if (Options.ApplyModeratorList.GetValue() == 0)
                    {
                        Utils.SendMessage(GetString("KickCommandDisabled"), localPlayerId);
                        break;
                    }

                    // Check if the player has the necessary privileges to use the command
                    if (!IsPlayerModerator(PlayerControl.LocalPlayer.FriendCode))
                    {
                        Utils.SendMessage(GetString("KickCommandNoAccess"), localPlayerId);
                        break;
                    }

                    subArgs = args.Length < 2 ? string.Empty : args[1];
                    if (string.IsNullOrEmpty(subArgs) || !byte.TryParse(subArgs, out byte kickPlayerId))
                    {
                        Utils.SendMessage(GetString("KickCommandInvalidID"), localPlayerId);
                        break;
                    }

                    if (kickPlayerId == 0)
                    {
                        Utils.SendMessage(GetString("KickCommandKickHost"), localPlayerId);
                        break;
                    }

                    var kickedPlayer = Utils.GetPlayerById(kickPlayerId);
                    if (kickedPlayer == null)
                    {
                        Utils.SendMessage(GetString("KickCommandInvalidID"), localPlayerId);
                        break;
                    }

                    // Prevent moderators from kicking other moderators
                    if (IsPlayerModerator(kickedPlayer.FriendCode))
                    {
                        Utils.SendMessage(GetString("KickCommandKickMod"), localPlayerId);
                        break;
                    }

                    // Kick the specified player
                    AmongUsClient.Instance.KickPlayer(kickedPlayer.GetClientId(), true);
                    string kickedPlayerName = kickedPlayer.GetRealName();
                    string textToSend = $"{kickedPlayerName} {GetString("KickCommandKicked")}";
                    if (GameStates.IsInGame)
                    {
                        textToSend += $"{GetString("KickCommandKickedRole")} {GetString(kickedPlayer.GetCustomRole().ToString())}";
                    }
                    Utils.SendMessage(textToSend);
                    break;
                case "/exe":
                    canceled = true;
                    if (GameStates.IsLobby)
                    {
                        Utils.SendMessage(GetString("Message.CanNotUseInLobby"), localPlayerId);
                        break;
                    }
                    if (args.Length < 2 || !int.TryParse(args[1], out int id)) break;
                    var player = Utils.GetPlayerById(id);
                    if (player != null)
                    {
                        player.Data.IsDead = true;
                        Main.PlayerStates[player.PlayerId].deathReason = PlayerState.DeathReason.etc;
                        player.RpcExileV2();
                        Main.PlayerStates[player.PlayerId].SetDead();
                        if (player.AmOwner) Utils.SendMessage(GetString("HostKillSelfByCommand"), title: $"<color=#ff0000>{GetString("DefaultSystemMessageTitle")}</color>");
                        else Utils.SendMessage(string.Format(GetString("Message.Executed"), player.Data.PlayerName));
                    }
                    break;

                case "/kill":
                    canceled = true;
                    if (GameStates.IsLobby)
                    {
                        Utils.SendMessage(GetString("Message.CanNotUseInLobby"), localPlayerId);
                        break;
                    }
                    if (args.Length < 2 || !int.TryParse(args[1], out int id2)) break;
                    var target = Utils.GetPlayerById(id2);
                    if (target != null)
                    {
                        target.Kill(target);
                        if (target.AmOwner) Utils.SendMessage(GetString("HostKillSelfByCommand"), title: $"<color=#ff0000>{GetString("DefaultSystemMessageTitle")}</color>");
                        else Utils.SendMessage(string.Format(GetString("Message.Executed"), target.Data.PlayerName));
                    }
                    break;

                case "/colour":
                case "/color":
                    canceled = true;
                    if (GameStates.IsInGame)
                    {
                        Utils.SendMessage(GetString("Message.OnlyCanUseInLobby"), localPlayerId);
                        break;
                    }
                    subArgs = args.Length < 2 ? string.Empty : args[1];
                    var color = Utils.MsgToColor(subArgs, true);
                    if (color == byte.MaxValue)
                    {
                        Utils.SendMessage(GetString("IllegalColor"), localPlayerId);
                        break;
                    }
                    PlayerControl.LocalPlayer.RpcSetColor(color);
                    Utils.SendMessage(string.Format(GetString("Message.SetColor"), subArgs), localPlayerId);
                    break;

                case "/quit":
                case "/qt":
                    canceled = true;
                    Utils.SendMessage(GetString("Message.CanNotUseByHost"), localPlayerId);
                    break;

                case "/xf":
                    canceled = true;
                    if (!GameStates.IsInGame)
                    {
                        Utils.SendMessage(GetString("Message.CanNotUseInLobby"), localPlayerId);
                        break;
                    }
                    foreach (PlayerControl pc in Main.AllAlivePlayerControls)
                    {
                        pc.RpcSetNameEx(pc.GetRealName(isMeeting: true));
                    }
                    ChatUpdatePatch.DoBlockChat = false;
                    Utils.NotifyRoles(isForMeeting: GameStates.IsMeeting, NoCache: true);
                    Utils.SendMessage(GetString("Message.TryFixName"), localPlayerId);
                    break;

                case "/id":
                    canceled = true;
                    string msgText = GetString("PlayerIdList");
                    foreach (PlayerControl pc in Main.AllPlayerControls)
                    {
                        msgText += "\n" + pc.PlayerId.ToString() + " → " + Main.AllPlayerNames[pc.PlayerId];
                    }

                    Utils.SendMessage(msgText, localPlayerId);
                    break;

                /*
                case "/qq":
                    canceled = true;
                    if (Main.newLobby) Cloud.ShareLobby(true);
                    else Utils.SendMessage("很抱歉，每个房间车队姬只会发一次", localPlayerId);
                    break;
                */

                case "/changerole":
                    //if (!DebugModeManager.AmDebugger) break;
                    canceled = true;
                    subArgs = text.Remove(0, 8);
                    var setRole = FixRoleNameInput(subArgs.Trim());
                    foreach (CustomRoles rl in Enum.GetValues(typeof(CustomRoles)))
                    {
                        if (rl.IsVanilla()) continue;
                        var roleName = GetString(rl.ToString()).ToLower().Trim();
                        if (setRole.Contains(roleName))
                        {
                            PlayerControl.LocalPlayer.RpcSetRole(rl.GetRoleTypes());
                            PlayerControl.LocalPlayer.RpcSetCustomRole(rl);
                            Utils.NotifyRoles(SpecifySeer: PlayerControl.LocalPlayer);
                            PlayerControl.LocalPlayer.MarkDirtySettings();
                        }
                    }
                    break;

                case "/end":
                    canceled = true;
                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Draw);
                    GameManager.Instance.LogicFlow.CheckEndCriteria();
                    break;
                case "/cosid":
                    canceled = true;
                    var of = PlayerControl.LocalPlayer.Data.DefaultOutfit;
                    Logger.Warn($"ColorId: {of.ColorId}", "Get Cos Id");
                    Logger.Warn($"PetId: {of.PetId}", "Get Cos Id");
                    Logger.Warn($"HatId: {of.HatId}", "Get Cos Id");
                    Logger.Warn($"SkinId: {of.SkinId}", "Get Cos Id");
                    Logger.Warn($"VisorId: {of.VisorId}", "Get Cos Id");
                    Logger.Warn($"NamePlateId: {of.NamePlateId}", "Get Cos Id");
                    break;

                case "/mt":
                case "/hy":
                    canceled = true;
                    if (GameStates.IsMeeting) MeetingHud.Instance.RpcClose();
                    else PlayerControl.LocalPlayer.NoCheckStartMeeting(null, true);
                    break;

                case "/cs":
                    canceled = true;
                    subArgs = text.Remove(0, 3);
                    PlayerControl.LocalPlayer.RPCPlayCustomSound(subArgs.Trim());
                    break;

                case "/sd":
                    canceled = true;
                    subArgs = text.Remove(0, 3);
                    if (args.Length < 1 || !int.TryParse(args[1], out int sound1)) break;
                    RPC.PlaySoundRPC(localPlayerId, (Sounds)sound1);
                    break;

                case "/gno":
                    canceled = true;
                    if (!GameStates.IsLobby && PlayerControl.LocalPlayer.IsAlive())
                    {
                        Utils.SendMessage(GetString("GNoCommandInfo"), localPlayerId);
                        break;
                    }
                    subArgs = args.Length != 2 ? "" : args[1];
                    if (subArgs == "" || !int.TryParse(subArgs, out int guessedNo))
                    {
                        Utils.SendMessage(GetString("GNoCommandInfo"), localPlayerId);
                        break;
                    }
                    else if (guessedNo < 0 || guessedNo > 99)
                    {
                        Utils.SendMessage(GetString("GNoCommandInfo"), localPlayerId);
                        break;
                    }
                    else
                    {
                        int targetNumber = Main.GuessNumber[localPlayerId][0];
                        if (Main.GuessNumber[localPlayerId][0] == -1)
                        {
                            var rand = IRandom.Instance;
                            Main.GuessNumber[localPlayerId][0] = rand.Next(0, 100);
                            targetNumber = Main.GuessNumber[localPlayerId][0];
                        }
                        Main.GuessNumber[localPlayerId][1]--;
                        if (Main.GuessNumber[localPlayerId][1] == 0 && guessedNo != targetNumber)
                        {
                            Main.GuessNumber[localPlayerId][0] = -1;
                            Main.GuessNumber[localPlayerId][1] = 7;
                            //targetNumber = Main.GuessNumber[localPlayerId][0];
                            Utils.SendMessage(string.Format(GetString("GNoLost"), targetNumber), localPlayerId);
                            break;
                        }
                        else if (guessedNo < targetNumber)
                        {
                            Utils.SendMessage(string.Format(GetString("GNoLow"), Main.GuessNumber[localPlayerId][1]), localPlayerId);
                            break;
                        }
                        else if (guessedNo > targetNumber)
                        {
                            Utils.SendMessage(string.Format(GetString("GNoHigh"), Main.GuessNumber[localPlayerId][1]), localPlayerId);
                            break;
                        }
                        else
                        {
                            Utils.SendMessage(string.Format(GetString("GNoWon"), Main.GuessNumber[localPlayerId][1]), localPlayerId);
                            Main.GuessNumber[localPlayerId][0] = -1;
                            Main.GuessNumber[localPlayerId][1] = 7;
                            break;
                        }

                    }

                default:
                    Main.isChatCommand = false;
                    break;
            }
        }
        goto Skip;
    Canceled:
        Main.isChatCommand = false;
        canceled = true;
    Skip:
        if (canceled)
        {

            Logger.Info("Command Canceled", "ChatCommand");
            __instance.freeChatField.textArea.Clear();
            __instance.freeChatField.textArea.SetText(cancelVal);
        }
        return !canceled;
    }

    public static string FixRoleNameInput(string text)
    {
        text = text.Replace("着", "者").Trim().ToLower();
        return text switch
        {
            "管理員" or "管理" or "gm" => GetString("GM"),
            "賞金獵人" or "赏金" => GetString("BountyHunter"),
            "自爆兵" or "自爆" => GetString("Bomber"),
            "邪惡的追踪者" or "邪恶追踪者" or "追踪" => GetString("EvilTracker"),
            "煙花商人" or "烟花" => GetString("FireWorks"),
            "夢魘" or "夜魇" => GetString("Mare"),
            "詭雷" => GetString("BoobyTrap"),
            "黑手黨" or "黑手" => GetString("Mafia"),
            "嗜血殺手" or "嗜血" => GetString("SerialKiller"),
            "千面鬼" or "千面" => GetString("ShapeMaster"),
            "狂妄殺手" or "狂妄" => GetString("Sans"),
            "殺戮機器" or "杀戮" or "机器" or "杀戮兵器" => GetString("Minimalism"),
            "蝕時者" or "蚀时" or "偷时" => GetString("TimeThief"),
            "狙擊手" or "狙击" => GetString("Sniper"),
            "傀儡師" or "傀儡" => GetString("Puppeteer"),
            "殭屍" or "丧尸" => GetString("Zombie"),
            "吸血鬼" or "吸血" => GetString("Vampire"),
            "術士" => GetString("Warlock"),
            "駭客" or "黑客" => GetString("Hacker"),
            "刺客" or "忍者" => GetString("Assassin"),
            "礦工" => GetString("Miner"),
            "逃逸者" or "逃逸" => GetString("Escapee"),
            "女巫" => GetString("Witch"),
            "監視者" or "监管" => GetString("AntiAdminer"),
            "清道夫" or "清道" => GetString("Scavenger"),
            "窺視者" or "窥视" => GetString("Watcher"),
            "誘餌" or "大奖" or "头奖" => GetString("Bait"),
            "擺爛人" or "摆烂" => GetString("Needy"),
            "獨裁者" or "独裁" => GetString("Dictator"),
            "法醫" => GetString("Doctor"),
            "偵探" => GetString("Detective"),
            "幸運兒" or "幸运" => GetString("Luckey"),
            "大明星" or "明星" => GetString("SuperStar"),
            "網紅" => GetString("CyberStar"),
            "demo" => GetString("Demolitionist"),
            "俠客" => GetString("SwordsMan"),
            "正義賭怪" or "正义的赌怪" or "好赌" or "正义赌" => GetString("NiceGuesser"),
            "邪惡賭怪" or "邪恶的赌怪" or "坏赌" or "恶赌" or "邪恶赌" or "赌怪" => GetString("EvilGuesser"),
            "市長" or "逝长" => GetString("Mayor"),
            "被害妄想症" or "被害妄想" or "被迫害妄想症" or "被害" or "妄想" or "妄想症" => GetString("Paranoia"),
            "愚者" or "愚" => GetString("Psychic"),
            "修理大师" or "修理" or "维修" => GetString("SabotageMaster"),
            "警長" => GetString("Sheriff"),
            "告密者" or "告密" => GetString("Snitch"),
            "增速者" or "增速" => GetString("SpeedBooster"),
            "時間操控者" or "时间操控人" or "时间操控" => GetString("TimeManager"),
            "陷阱師" or "陷阱" or "小奖" => GetString("Trapper"),
            "傳送師" or "传送" => GetString("Transporter"),
            "縱火犯" or "纵火" => GetString("Arsonist"),
            "處刑人" or "处刑" => GetString("Executioner"),
            "小丑" or "丑皇" => GetString("Jester"),
            "投機者" or "投机" => GetString("Opportunist"),
            "馬里奧" or "马力欧" => GetString("Mario"),
            "恐怖分子" or "恐怖" => GetString("Terrorist"),
            "豺狼" or "蓝狼" or "狼" => GetString("Jackal"),
            "神" or "上帝" => GetString("God"),
            "情人" or "愛人" or "链子" or "老婆" or "老公" => GetString("Lovers"),
            "絕境者" or "绝境" => GetString("LastImpostor"),
            "閃電俠" or "闪电" => GetString("Flashman"),
            "靈媒" => GetString("Seer"),
            "破平者" or "破平" => GetString("Brakar"),
            "執燈人" or "执灯" or "灯人" => GetString("Torch"),
            "膽小" or "胆小" => GetString("Oblivious"),
            "迷惑者" or "迷幻" => GetString("Bewilder"),
            "sun" => GetString("Sunglasses"),
            "蠢蛋" or "笨蛋" or "蠢狗" or "傻逼" => GetString("Fool"),
            "冤罪師" or "冤罪" => GetString("Innocent"),
            "資本家" or "资本主义" or "资本" => GetString("Capitalism"),
            "老兵" => GetString("Veteran"),
            "加班狂" or "加班" => GetString("Workhorse"),
            "復仇者" or "复仇" => GetString("Avanger"),
            "鵜鶘" => GetString("Pelican"),
            "保鏢" => GetString("Bodyguard"),
            "up" or "up主" => GetString("Youtuber"),
            "利己主義者" or "利己主义" or "利己" => GetString("Egoist"),
            "贗品商" or "赝品" => GetString("Counterfeiter"),
            "擲雷兵" or "掷雷" or "闪光弹" => GetString("Grenadier"),
            "竊票者" or "偷票" or "偷票者" or "窃票师" or "窃票" => GetString("TicketsStealer"),
            "教父" => GetString("Gangster"),
            "革命家" or "革命" => GetString("Revolutionist"),
            "fff團" or "fff" or "fff团" => GetString("FFF"),
            "清理工" or "清潔工" or "清洁工" or "清理" or "清洁" => GetString("Cleaner"),
            "醫生" => GetString("Medicaler"),
            "占卜師" or "占卜" => GetString("Divinator"),
            "雙重人格" or "双重" or "双人格" or "人格" => GetString("DualPersonality"),
            "玩家" => GetString("Gamer"),
            "情報販子" or "情报" or "贩子" => GetString("Messenger"),
            "球狀閃電" or "球闪" or "球状" => GetString("BallLightning"),
            "潛藏者" or "潜藏" => GetString("DarkHide"),
            "貪婪者" or "贪婪" => GetString("Greedier"),
            "工作狂" or "工作" => GetString("Workaholic"),
            "呪狼" or "咒狼" => GetString("CursedWolf"),
            "寶箱怪" or "宝箱" => GetString("Mimic"),
            "集票者" or "集票" or "寄票" or "机票" => GetString("Collector"),
            "活死人" or "活死" => GetString("Glitch"),
            "奪魂者" or "多混" or "夺魂" => GetString("ImperiusCurse"),
            "自爆卡車" or "自爆" or "卡车" => GetString("Provocateur"),
            "快槍手" or "快枪" => GetString("QuickShooter"),
            "隱蔽者" or "隐蔽" or "小黑人" => GetString("Concealer"),
            "抹除者" or "抹除" => GetString("Eraser"),
            "肢解者" or "肢解" => GetString("OverKiller"),
            "劊子手" or "侩子手" or "柜子手" => GetString("Hangman"),
            "陽光開朗大男孩" or "阳光" or "开朗" or "大男孩" or "阳光开朗" or "开朗大男孩" or "阳光大男孩" => GetString("Sunnyboy"),
            "法官" or "审判" => GetString("Judge"),
            "入殮師" or "入检师" or "入殓" => GetString("Mortician"),
            "通靈師" or "通灵" => GetString("Mediumshiper"),
            "吟游詩人" or "诗人" => GetString("Bard"),
            "隱匿者" or "隐匿" or "隐身" or "隐身人" or "印尼" => GetString("Swooper"),
            "船鬼" => GetString("Crewpostor"),
            "嗜血騎士" or "血骑" or "骑士" or "bk" => GetString("BloodKnight"),
            "賭徒" => GetString("Totocalcio"),
            "分散机" => GetString("Disperser"),
            "和平之鸽" or "和平之鴿" or "和平的鸽子" or "和平" => GetString("DovesOfNeace"),
            "持槍" or "持械" or "手长" => GetString("Reach"),
            "monarch" => GetString("Monarch"),
            _ => text,
        };
    }

    public static bool GetRoleByName(string name, out CustomRoles role)
    {
        role = new();
        if (name == "" || name == string.Empty) return false;

        if ((TranslationController.InstanceExists ? TranslationController.Instance.currentLanguage.languageID : SupportedLangs.SChinese) == SupportedLangs.SChinese)
        {
            Regex r = new("[\u4e00-\u9fa5]+$");
            MatchCollection mc = r.Matches(name);
            string result = string.Empty;
            for (int i = 0; i < mc.Count; i++)
            {
                if (mc[i].ToString() == "是") continue;
                result += mc[i]; //匹配结果是完整的数字，此处可以不做拼接的
            }
            name = FixRoleNameInput(result.Replace("是", string.Empty).Trim());
        }
        else name = name.Trim().ToLower();

        System.Collections.IList list = Enum.GetValues(typeof(CustomRoles));
        for (int i = 0; i < list.Count; i++)
        {
            CustomRoles rl = (CustomRoles)list[i];
            if (rl.IsVanilla()) continue;
            var roleName = GetString(rl.ToString()).ToLower().Trim().Replace(" ", string.Empty);
            string nameWithoutId = Regex.Replace(name.Replace(" ", string.Empty), @"^\d+", string.Empty);
            if (nameWithoutId == roleName)
            {
                role = rl;
                return true;
            }
        }
        return false;
    }
    public static void SendRolesInfo(string role, byte playerId, bool isDev = false, bool isUp = false)
    {
        if (Options.CurrentGameMode == CustomGameMode.SoloKombat)
        {
            Utils.SendMessage(GetString("ModeDescribe.SoloKombat"), playerId);
            return;
        }
        if (Options.CurrentGameMode == CustomGameMode.FFA)
        {
            Utils.SendMessage(GetString("ModeDescribe.FFA"), playerId);
            return;
        }

        role = role.Trim().ToLower();
        if (role.StartsWith("/r")) _ = role.Replace("/r", string.Empty);
        if (role.StartsWith("/up")) _ = role.Replace("/up", string.Empty);
        if (role.EndsWith("\r\n")) _ = role.Replace("\r\n", string.Empty);
        if (role.EndsWith("\n")) _ = role.Replace("\n", string.Empty);

        if (role == "" || role == string.Empty)
        {
            Utils.ShowActiveRoles(playerId);
            return;
        }

        role = FixRoleNameInput(role).ToLower().Trim().Replace(" ", string.Empty);

        System.Collections.IList list = Enum.GetValues(typeof(CustomRoles));
        for (int i = 0; i < list.Count; i++)
        {
            CustomRoles rl = (CustomRoles)list[i];
            if (rl.IsVanilla()) continue;
            var roleName = GetString(rl.ToString());
            if (role == roleName.ToLower().Trim().TrimStart('*').Replace(" ", string.Empty))
            {
                string devMark = string.Empty;
                if ((isDev || isUp) && GameStates.IsLobby)
                {
                    devMark = "▲";
                    if (CustomRolesHelper.IsAdditionRole(rl) || rl is CustomRoles.GM) devMark = string.Empty;
                    if (rl.GetCount() < 1 || rl.GetMode() == 0) devMark = string.Empty;
                    if (isUp)
                    {
                        if (devMark == "▲") Utils.SendMessage(string.Format(GetString("Message.YTPlanSelected"), roleName), playerId);
                        else Utils.SendMessage(string.Format(GetString("Message.YTPlanSelectFailed"), roleName), playerId);
                    }
                    if (devMark == "▲")
                    {
                        byte pid = playerId == 255 ? (byte)0 : playerId; // rl contains the ID whose role we want to set, move that to pid
                        _ = Main.DevRole.Remove(pid);
                        Main.DevRole.Add(pid, rl);
                    }
                    if (isUp) return;
                }
                var sb = new StringBuilder();
                _ = sb.Append(devMark + "<b>" + roleName + "</b>" + Utils.GetRoleMode(rl) + GetString($"{rl}InfoLong"));
                if (Options.CustomRoleSpawnChances.ContainsKey(rl))
                {
                    Utils.ShowChildrenSettings(Options.CustomRoleSpawnChances[rl], ref sb, command: true);
                    var txt = sb.ToString();
                    _ = sb.Clear().Append(txt.RemoveHtmlTags());
                }
                Utils.SendMessage(sb.ToString(), playerId);
                return;
            }
        }
        if (isUp) Utils.SendMessage(GetString("Message.YTPlanCanNotFindRoleThePlayerEnter"), playerId);
        else Utils.SendMessage(GetString("Message.CanNotFindRoleThePlayerEnter"), playerId);
        return;
    }
    public static void OnReceiveChat(PlayerControl player, string text, out bool canceled)
    {
        canceled = false;
        if (!AmongUsClient.Instance.AmHost) return;
        if (player.PlayerId != 0) ChatManager.SendMessage(player, text);
        if (text.StartsWith("\n")) text = text[1..];
        //if (!text.StartsWith("/")) return;
        string[] args = text.Split(' ');
        string subArgs = string.Empty;
        //if (text.Length >= 3) if (text[..2] == "/r" && text[..3] != "/rn") args[0] = "/r";
        //   if (SpamManager.CheckSpam(player, text)) return;
        if (GuessManager.GuesserMsg(player, text)) { canceled = true; return; }
        if (Judge.TrialMsg(player, text)) { canceled = true; return; }
        if (NiceSwapper.SwapMsg(player, text)) { canceled = true; return; }
        if (ParityCop.ParityCheckMsg(player, text)) { canceled = true; return; }
        //if (Pirate.DuelCheckMsg(player, text)) { canceled = true; return; }
        if (Councillor.MurderMsg(player, text)) { canceled = true; return; }
        if (Mediumshiper.MsMsg(player, text)) return;
        if (MafiaRevengeManager.MafiaMsgCheck(player, text)) return;
        if (RetributionistRevengeManager.RetributionistMsgCheck(player, text)) return;
        switch (args[0])
        {
            case "/l":
            case "/lastresult":
                Utils.ShowKillLog(player.PlayerId);
                Utils.ShowLastRoles(player.PlayerId);
                Utils.ShowLastResult(player.PlayerId);
                break;

            case "/n":
            case "/now":
                subArgs = args.Length < 2 ? string.Empty : args[1];
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
                break;

            case "/r":
                subArgs = text.Remove(0, 2);
                SendRolesInfo(subArgs, player.PlayerId, player.FriendCode.GetDevUser().DeBug);
                break;

            case "/h":
            case "/help":
                Utils.ShowHelpToClient(player.PlayerId);
                break;

            case "/m":
            case "/myrole":
                var role = player.GetCustomRole();
                if (GameStates.IsInGame)
                {
                    var sb = new StringBuilder();
                    _ = sb.Append(GetString(role.ToString()) + Utils.GetRoleMode(role) + player.GetRoleInfo(true));
                    if (Options.CustomRoleSpawnChances.TryGetValue(role, out _))
                        Utils.ShowChildrenSettings(Options.CustomRoleSpawnChances[role], ref sb, command: true);
                    var txt = sb.ToString();
                    _ = sb.Clear().Append(txt.RemoveHtmlTags());
                    for (int i = 0; i < Main.PlayerStates[player.PlayerId].SubRoles.Count; i++)
                    {
                        CustomRoles subRole = Main.PlayerStates[player.PlayerId].SubRoles[i];
                        _ = sb.Append($"\n\n" + GetString($"{subRole}") + Utils.GetRoleMode(subRole) + GetString($"{subRole}InfoLong"));
                    }
                    Utils.SendMessage(sb.ToString(), player.PlayerId);
                }
                else
                    Utils.SendMessage(GetString("Message.CanNotUseInLobby"), player.PlayerId);
                break;

            case "/t":
            case "/template":
                if (args.Length > 1) TemplateManager.SendTemplate(args[1], player.PlayerId);
                else Utils.SendMessage($"{GetString("ForExample")}:\n{args[0]} test", player.PlayerId);
                break;

            case "/colour":
            case "/color":
                if (Options.PlayerCanSetColor.GetBool() || player.FriendCode.GetDevUser().IsDev || player.FriendCode.GetDevUser().ColorCmd)
                {
                    if (GameStates.IsInGame)
                    {
                        Utils.SendMessage(GetString("Message.OnlyCanUseInLobby"), player.PlayerId);
                        break;
                    }
                    subArgs = args.Length < 2 ? string.Empty : args[1];
                    var color = Utils.MsgToColor(subArgs);
                    if (color == byte.MaxValue)
                    {
                        Utils.SendMessage(GetString("IllegalColor"), player.PlayerId);
                        break;
                    }
                    player.RpcSetColor(color);
                    Utils.SendMessage(string.Format(GetString("Message.SetColor"), subArgs), player.PlayerId);
                }
                else
                {
                    Utils.SendMessage(GetString("DisableUseCommand"), player.PlayerId);
                }
                break;

            case "/quit":
            case "/qt":
                subArgs = args.Length < 2 ? string.Empty : args[1];
                var cid = player.PlayerId.ToString();
                cid = cid.Length != 1 ? cid.Substring(1, 1) : cid;
                if (subArgs.Equals(cid))
                {
                    string name = player.GetRealName();
                    Utils.SendMessage(string.Format(GetString("Message.PlayerQuitForever"), name));
                    AmongUsClient.Instance.KickPlayer(player.GetClientId(), true);
                }
                else
                {
                    Utils.SendMessage(string.Format(GetString("SureUse.quit"), cid), player.PlayerId);
                }
                break;
            case "/id":
                if (Options.ApplyModeratorList.GetValue() == 0 || !IsPlayerModerator(player.FriendCode)) break;

                string msgText = GetString("PlayerIdList");
                foreach (PlayerControl pc in Main.AllPlayerControls)
                {
                    msgText += "\n" + pc.PlayerId.ToString() + " → " + Main.AllPlayerNames[pc.PlayerId];
                }

                Utils.SendMessage(msgText, player.PlayerId);
                break;
            case "/kick":
                // Check if the kick command is enabled in the settings
                if (Options.ApplyModeratorList.GetValue() == 0)
                {
                    Utils.SendMessage(GetString("KickCommandDisabled"), player.PlayerId);
                    break;
                }

                // Check if the player has the necessary privileges to use the command
                if (!IsPlayerModerator(player.FriendCode))
                {
                    Utils.SendMessage(GetString("KickCommandNoAccess"), player.PlayerId);
                    break;
                }

                subArgs = args.Length < 2 ? string.Empty : args[1];
                if (string.IsNullOrEmpty(subArgs) || !byte.TryParse(subArgs, out byte kickPlayerId))
                {
                    Utils.SendMessage(GetString("KickCommandInvalidID"), player.PlayerId);
                    break;
                }

                if (kickPlayerId == 0)
                {
                    Utils.SendMessage(GetString("KickCommandKickHost"), player.PlayerId);
                    break;
                }

                var kickedPlayer = Utils.GetPlayerById(kickPlayerId);
                if (kickedPlayer == null)
                {
                    Utils.SendMessage(GetString("KickCommandInvalidID"), player.PlayerId);
                    break;
                }

                // Prevent moderators from kicking other moderators
                if (IsPlayerModerator(kickedPlayer.FriendCode))
                {
                    Utils.SendMessage(GetString("KickCommandKickMod"), player.PlayerId);
                    break;
                }

                // Kick the specified player
                AmongUsClient.Instance.KickPlayer(kickedPlayer.GetClientId(), true);
                string kickedPlayerName = kickedPlayer.GetRealName();
                string textToSend = $"{kickedPlayerName} {GetString("KickCommandKicked")}";
                if (GameStates.IsInGame)
                {
                    textToSend += $"{GetString("KickCommandKickedRole")} {GetString(kickedPlayer.GetCustomRole().ToString())}";
                }
                Utils.SendMessage(textToSend);
                break;

            case "/xf":
                if (!GameStates.IsInGame)
                {
                    Utils.SendMessage(GetString("Message.CanNotUseInLobby"), player.PlayerId);
                    break;
                }
                ChatUpdatePatch.DoBlockChat = false;
                Utils.NotifyRoles(isForMeeting: GameStates.IsMeeting, NoCache: true);
                Utils.SendMessage(GetString("Message.TryFixName"), player.PlayerId);
                break;

            case "/say":
            case "/s":
                if (player.FriendCode.GetDevUser().IsDev)
                {
                    if (args.Length > 1)
                        Utils.SendMessage(args.Skip(1).Join(delimiter: " "), title: $"<color={Main.ModColor}>{GetString("MessageFromDev")}</color>");
                }
                else if (player.FriendCode.IsDevUser())
                {
                    if (args.Length > 1)
                        Utils.SendMessage(args.Skip(1).Join(delimiter: " "), title: $"<color=#4bc9b0>{GetString("MessageFromSponsor")}</color>");
                }
                break;

            case "/kcount":
                if (GameStates.IsLobby || !Options.EnableKillerLeftCommand.GetBool()) break;
                int impnum = 0;
                int neutralnum = 0;
                foreach (PlayerControl players in Main.AllAlivePlayerControls)
                {
                    if (Options.ShowImpRemainOnEject.GetBool())
                    {
                        if (players.GetCustomRole().IsImpostor())
                            impnum++;
                    }
                    if (Options.ShowNKRemainOnEject.GetBool())
                    {
                        if (players.GetCustomRole().IsNK())
                            neutralnum++;
                    }
                }
                Utils.SendMessage($"There {(impnum == 1 ? "is" : "are")}\n<b>{impnum}</b> <color=#ff1919>{(impnum == 1 ? "Impostor" : "Impostors")}</color> and <b>{neutralnum}</b> <color=#7f8c8d>{(neutralnum == 1 ? "Neutral Killer" : "Neutral Killers")}</color> left.", player.PlayerId);
                break;

            case "/gno":
                canceled = true;
                if (!GameStates.IsLobby && player.IsAlive())
                {
                    Utils.SendMessage(GetString("GNoCommandInfo"), player.PlayerId);
                    break;
                }
                subArgs = args.Length != 2 ? "" : args[1];
                if (subArgs == "" || !int.TryParse(subArgs, out int guessedNo))
                {
                    Utils.SendMessage(GetString("GNoCommandInfo"), player.PlayerId);
                    break;
                }
                else if (guessedNo < 0 || guessedNo > 99)
                {
                    Utils.SendMessage(GetString("GNoCommandInfo"), player.PlayerId);
                    break;
                }
                else
                {
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
                        //targetNumber = Main.GuessNumber[player.PlayerId][0];
                        Utils.SendMessage(string.Format(GetString("GNoLost"), targetNumber), player.PlayerId);
                        break;
                    }
                    else if (guessedNo < targetNumber)
                    {
                        Utils.SendMessage(string.Format(GetString("GNoLow"), Main.GuessNumber[player.PlayerId][1]), player.PlayerId);
                        break;
                    }
                    else if (guessedNo > targetNumber)
                    {
                        Utils.SendMessage(string.Format(GetString("GNoHigh"), Main.GuessNumber[player.PlayerId][1]), player.PlayerId);
                        break;
                    }
                    else
                    {
                        Utils.SendMessage(string.Format(GetString("GNoWon"), 7 - Main.GuessNumber[player.PlayerId][1]), player.PlayerId);
                        Main.GuessNumber[player.PlayerId][0] = -1;
                        Main.GuessNumber[player.PlayerId][1] = 7;
                        break;
                    }
                }

            default:
                break;
        }
        if (SpamManager.CheckSpam(player, text)) return;
    }
}
[HarmonyPatch(typeof(ChatController), nameof(ChatController.Update))]
internal class ChatUpdatePatch
{
    public static bool DoBlockChat;
    public static void Postfix(ChatController __instance)
    {
        if (!AmongUsClient.Instance.AmHost || !Main.MessagesToSend.Any() || (Main.MessagesToSend[0].Item2 == byte.MaxValue && Main.MessageWait.Value > __instance.timeSinceLastMessage)) return;
        if (DoBlockChat) return;
        var player = Main.AllAlivePlayerControls.OrderBy(x => x.PlayerId).FirstOrDefault() ?? Main.AllPlayerControls.OrderBy(x => x.PlayerId).FirstOrDefault();
        if (player == null) return;
        (string msg, byte sendTo, string title) = Main.MessagesToSend[0];
        Main.MessagesToSend.RemoveAt(0);
        int clientId = sendTo == byte.MaxValue ? -1 : Utils.GetPlayerById(sendTo).GetClientId();
        var name = player.Data.PlayerName;
        if (clientId == -1)
        {
            player.SetName(title);
            DestroyableSingleton<HudManager>.Instance.Chat.AddChat(player, msg);
            player.SetName(name);
        }
        var writer = CustomRpcSender.Create("MessagesToSend", SendOption.None);
        _ = writer.StartMessage(clientId);
        _ = writer.StartRpc(player.NetId, (byte)RpcCalls.SetName)
            .Write(title)
            .EndRpc();
        _ = writer.StartRpc(player.NetId, (byte)RpcCalls.SendChat)
            .Write(msg)
            .EndRpc();
        _ = writer.StartRpc(player.NetId, (byte)RpcCalls.SetName)
            .Write(player.Data.PlayerName)
            .EndRpc();
        _ = writer.EndMessage();
        writer.SendMessage();
        __instance.timeSinceLastMessage = 0f;
    }
}

[HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChat))]
internal class AddChatPatch
{
    public static void Postfix(string chatText)
    {
        switch (chatText)
        {
            default:
                break;
        }
        if (!AmongUsClient.Instance.AmHost) return;
    }
}
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
internal class RpcSendChatPatch
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
        if (AmongUsClient.Instance.AmClient && DestroyableSingleton<HudManager>.Instance)
            DestroyableSingleton<HudManager>.Instance.Chat.AddChat(__instance, chatText);
        if (chatText.Contains("who", StringComparison.OrdinalIgnoreCase))
            DestroyableSingleton<UnityTelemetry>.Instance.SendWho();
        MessageWriter messageWriter = AmongUsClient.Instance.StartRpc(__instance.NetId, (byte)RpcCalls.SendChat, SendOption.None);
        messageWriter.Write(chatText);
        messageWriter.EndMessage();
        __result = true;
        return false;
    }
}