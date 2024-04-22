using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Assets.CoreScripts;
using EHR.Modules;
using EHR.Roles.Crewmate;
using EHR.Roles.Impostor;
using EHR.Roles.Neutral;
using HarmonyLib;
using Hazel;
using UnityEngine;
using static EHR.Translator;


namespace EHR;

[HarmonyPatch(typeof(ChatController), nameof(ChatController.SendChat))]
internal class ChatCommands
{
    // Function to check if a player is a moderator
    public static bool IsPlayerModerator(string friendCode)
    {
        if (friendCode == "" || friendCode == string.Empty || !Options.ApplyModeratorList.GetBool()) return false;
        const string friendCodesFilePath = "./EHR_DATA/Moderators.txt";
        var friendCodes = File.ReadAllLines(friendCodesFilePath);
        return friendCodes.Any(code => code.Contains(friendCode, StringComparison.OrdinalIgnoreCase));
    }

    public static List<string> ChatHistory = [];
    public static Dictionary<byte, long> LastSentCommand = [];

    public static bool Prefix(ChatController __instance)
    {
        if (__instance.quickChatField.visible) return true;
        if (__instance.freeChatField.textArea.text == string.Empty) return false;
        if (!GameStates.IsModHost && !AmongUsClient.Instance.AmHost) return true;
        __instance.timeSinceLastMessage = 3f;

        var text = __instance.freeChatField.textArea.text;

        if (ChatHistory.Count == 0 || ChatHistory[^1] != text) ChatHistory.Add(text);
        ChatControllerUpdatePatch.CurrentHistorySelection = ChatHistory.Count;

        string[] args = text.Split(' ');
        var canceled = false;
        var cancelVal = string.Empty;
        Main.IsChatCommand = true;

        Logger.Info(text, "SendChat");

        ChatManager.SendMessage(PlayerControl.LocalPlayer, text);

        if (text.Length >= 3)
            if (text[..2] == "/r" && text[..3] != "/rn")
                args[0] = "/r";
        if (text.Length >= 4)
            if (text[..3] == "/up")
                args[0] = "/up";

        if (GuessManager.GuesserMsg(PlayerControl.LocalPlayer, text)) goto Canceled;
        if (Judge.TrialMsg(PlayerControl.LocalPlayer, text)) goto Canceled;
        if (NiceSwapper.SwapMsg(PlayerControl.LocalPlayer, text)) goto Canceled;
        if (ParityCop.ParityCheckMsg(PlayerControl.LocalPlayer, text)) goto Canceled;
        if (Councillor.MurderMsg(PlayerControl.LocalPlayer, text)) goto Canceled;
        if (Mediumshiper.MsMsg(PlayerControl.LocalPlayer, text)) goto Canceled;
        if (Mafia.MafiaMsgCheck(PlayerControl.LocalPlayer, text)) goto Canceled;

        switch (args[0])
        {
            case "/dump":
                //canceled = true;
                Utils.DumpLog();
                break;
            case "/v":
            case "/version":
                canceled = true;
                string version_text = Main.PlayerVersion.OrderBy(pair => pair.Key).Aggregate(string.Empty, (current, kvp) => current + $"{kvp.Key}:{Main.AllPlayerNames[kvp.Key]}:{kvp.Value.forkId}/{kvp.Value.version}({kvp.Value.tag})\n");

                if (version_text != string.Empty) HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, (PlayerControl.LocalPlayer.FriendCode.GetDevUser().HasTag() ? "\n" : string.Empty) + version_text);
                break;
            default:
                Main.IsChatCommand = false;
                break;
        }

        if (AmongUsClient.Instance.AmHost)
        {
            var localPlayerId = PlayerControl.LocalPlayer.PlayerId;
            Main.IsChatCommand = true;
            string subArgs;
            switch (args[0])
            {
                case "/w":
                case "/win":
                case "/winner":
                    canceled = true;
                    if (Main.WinnerNameList.Count == 0) Utils.SendMessage(GetString("NoInfoExists"));
                    else Utils.SendMessage("<b><u>Winners:</b></u>\n" + string.Join(", ", Main.WinnerNameList));
                    break;

                case "/l":
                case "/lastresult":
                    canceled = true;
                    Utils.ShowKillLog();
                    Utils.ShowLastAddOns();
                    Utils.ShowLastRoles();
                    Utils.ShowLastResult();
                    break;

                case "/rn":
                case "/rename":
                    canceled = true;
                    if (args.Length < 1) break;
                    if (args[1].Length is > 50 or < 1)
                        Utils.SendMessage(GetString("Message.AllowNameLength"), localPlayerId);
                    else Main.NickName = args[1];
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
                    if (!PlayerControl.LocalPlayer.FriendCode.GetDevUser().IsUp) break;
                    Utils.SendMessage($"{GetString("UpReplacedMessage")}", localPlayerId);
                    //if (!Options.EnableUpMode.GetBool())
                    //{
                    //    Utils.SendMessage(string.Format(GetString("Message.YTPlanDisabled"), GetString("EnableYTPlan")), localPlayerId);
                    //    break;
                    //}
                    //if (!GameStates.IsLobby)
                    //{
                    //    Utils.SendMessage(GetString("Message.OnlyCanUseInLobby"), localPlayerId);
                    //    break;
                    //}
                    //SendRolesInfo(subArgs, localPlayerId, isUp: true);
                    break;

                case "/setrole":
                    canceled = true;
                    subArgs = text.Remove(0, 8);
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

                    if (!GuessManager.MsgToPlayerAndRole(subArgs, out byte resultId, out CustomRoles roleToSet, out _))
                    {
                        Utils.SendMessage($"{GetString("InvalidArguments")}", localPlayerId);
                        break;
                    }

                    var targetPc = Utils.GetPlayerById(resultId);
                    if (targetPc == null) break;

                    if (roleToSet.IsAdditionRole())
                    {
                        if (!Main.SetAddOns.ContainsKey(resultId)) Main.SetAddOns[resultId] = [];

                        if (Main.SetAddOns[resultId].Contains(roleToSet)) Main.SetAddOns[resultId].Remove(roleToSet);
                        else Main.SetAddOns[resultId].Add(roleToSet);
                    }
                    else Main.SetRoles[targetPc.PlayerId] = roleToSet;

                    var playername = $"<b>{Utils.ColorString(Main.PlayerColors.TryGetValue(resultId, out var textColor) ? textColor : Color.white, targetPc.GetRealName())}</b>";
                    var rolename = $"<color={Main.RoleColors[roleToSet]}> {GetString(roleToSet.ToString())} </color>";
                    Utils.SendMessage("\n", localPlayerId, string.Format(GetString("RoleSelected"), playername, rolename));

                    break;

                case "/h":
                case "/help":
                    canceled = true;
                    Utils.ShowHelp(localPlayerId);
                    break;

                case "/kcount":
                    canceled = true;
                    if (GameStates.IsLobby || !Options.EnableKillerLeftCommand.GetBool()) break;
                    Utils.SendMessage(Utils.GetRemainingKillers(), localPlayerId);
                    break;

                case "/combo": // Format: /combo [add/ban/remove/allow] [main role] [addon]
                    canceled = true;
                    if (args.Length < 4)
                    {
                        if (Main.AlwaysSpawnTogetherCombos.Count == 0 && Main.NeverSpawnTogetherCombos.Count == 0) break;
                        var sb = new StringBuilder();
                        sb.Append("<size=70%>");
                        if (Main.AlwaysSpawnTogetherCombos.Count > 0)
                        {
                            sb.AppendLine(GetString("AlwaysComboListTitle"));
                            sb.AppendLine(Main.AlwaysSpawnTogetherCombos.Join(x => $"{x.Key.ToColoredString()} \u00a7 {x.Value.ToColoredString()}", "\n"));
                            sb.AppendLine();
                        }

                        if (Main.NeverSpawnTogetherCombos.Count > 0)
                        {
                            sb.AppendLine(GetString("NeverComboListTitle"));
                            sb.AppendLine(Main.NeverSpawnTogetherCombos.Join(x => $"{x.Key.ToColoredString()} \u2194 {x.Value.ToColoredString()}", "\n"));
                            sb.AppendLine();
                        }

                        sb.Append(GetString("ComboUsage"));

                        Utils.SendMessage("\n", localPlayerId, sb.ToString());
                        break;
                    }

                    switch (args[1])
                    {
                        case "add":
                        case "ban":
                            if (GetRoleByName(args[2], out CustomRoles mainRole) && GetRoleByName(args[3], out CustomRoles addOn))
                            {
                                if (mainRole.IsAdditionRole() || !addOn.IsAdditionRole() || addOn == CustomRoles.Lovers) break;
                                if (args[1] == "add") Main.AlwaysSpawnTogetherCombos[mainRole] = addOn;
                                else Main.NeverSpawnTogetherCombos[mainRole] = addOn;
                                Utils.SendMessage(string.Format(args[1] == "add" ? GetString("ComboAdd") : GetString("ComboBan"), GetString(mainRole.ToString()), GetString(addOn.ToString())), localPlayerId);
                            }

                            break;
                        case "remove":
                        case "allow":
                            if (GetRoleByName(args[2], out CustomRoles mainRole2) && GetRoleByName(args[3], out CustomRoles addOn2))
                            {
                                if (mainRole2.IsAdditionRole() || !addOn2.IsAdditionRole()) break;
                                if (args[1] == "remove" && Main.AlwaysSpawnTogetherCombos.TryGetValue(mainRole2, out var comboAddon) && comboAddon == addOn2)
                                {
                                    Main.AlwaysSpawnTogetherCombos.Remove(mainRole2);
                                    Utils.SendMessage(string.Format(GetString("ComboRemove"), GetString(mainRole2.ToString()), GetString(addOn2.ToString())), localPlayerId);
                                }
                                else if (Main.NeverSpawnTogetherCombos.TryGetValue(mainRole2, out var comboAddon2) && comboAddon2 == addOn2)
                                {
                                    Main.NeverSpawnTogetherCombos.Remove(mainRole2);
                                    Utils.SendMessage(string.Format(GetString("ComboAllow"), GetString(mainRole2.ToString()), GetString(addOn2.ToString())), localPlayerId);
                                }
                            }

                            break;
                    }

                    break;

                case "/eff":
                case "/effect":
                    canceled = true;
                    if (args.Length < 2 || !GameStates.IsInTask || !Randomizer.Exists) break;
                    if (Enum.TryParse(args[1], ignoreCase: true, out Randomizer.Effect enumerable))
                    {
                        enumerable.Apply(PlayerControl.LocalPlayer);
                    }

                    break;

                case "/m":
                case "/myrole":
                    canceled = true;
                    var lp = PlayerControl.LocalPlayer;
                    var role = lp.GetCustomRole();
                    if (GameStates.IsInGame)
                    {
                        var sb = new StringBuilder();
                        var settings = new StringBuilder();
                        settings.Append("<size=70%>");
                        _ = sb.Append(GetString(role.ToString()) + Utils.GetRoleMode(role) + lp.GetRoleInfo(true));
                        if (Options.CustomRoleSpawnChances.TryGetValue(role, out var opt))
                            Utils.ShowChildrenSettings(opt, ref settings, disableColor: false);
                        settings.Append("</size>");
                        var txt = sb.ToString();
                        _ = sb.Clear().Append(txt.RemoveHtmlTags());
                        if (role.PetActivatedAbility() && sb.Length < 1000) sb.Append("<size=50%>" + GetString("SupportsPetMessage").RemoveHtmlTags() + "</size>");
                        sb.Append("<size=70%>");
                        foreach (CustomRoles subRole in Main.PlayerStates[localPlayerId].SubRoles.ToArray())
                        {
                            _ = sb.Append("\n\n" + GetString($"{subRole}") + Utils.GetRoleMode(subRole) + GetString($"{subRole}InfoLong"));
                        }

                        Utils.SendMessage("\n", localPlayerId, settings.ToString());
                        Utils.SendMessage(sb.ToString(), localPlayerId, string.Empty);
                    }
                    else
                        Utils.SendMessage((lp.FriendCode.GetDevUser().HasTag() ? "\n" : string.Empty) + GetString("Message.CanNotUseInLobby"), localPlayerId);

                    break;

                case "/tpout":
                    canceled = true;
                    if (!GameStates.IsLobby || !Options.PlayerCanTPInAndOut.GetBool()) break;
                    PlayerControl.LocalPlayer.TP(new Vector2(0.1f, 3.8f));
                    break;

                case "/tpin":
                    canceled = true;
                    if (!GameStates.IsLobby || !Options.PlayerCanTPInAndOut.GetBool()) break;
                    PlayerControl.LocalPlayer.TP(new Vector2(-0.2f, 1.3f));
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

                case "/death":
                    if (!GameStates.IsInGame) break;
                    var killer = PlayerControl.LocalPlayer.GetRealKiller();
                    Utils.SendMessage("\n", localPlayerId, string.Format(GetString("DeathCommand"), killer.GetRealName(), GetString(killer.GetCustomRole().ToString())));
                    break;

                case "/say":
                case "/s":
                    canceled = true;
                    if (args.Length > 1)
                        Utils.SendMessage(args.Skip(1).Join(delimiter: " "), title: $"<color=#ff0000>{GetString("MessageFromTheHost")}</color>");
                    break;

                case "/vote":
                    canceled = true;
                    if (text.Length < 6 || !GameStates.IsMeeting) break;
                    string toVote = text[6..].Replace(" ", string.Empty);
                    if (!byte.TryParse(toVote, out var voteId)) break;
                    MeetingHud.Instance?.CastVote(PlayerControl.LocalPlayer.PlayerId, voteId);
                    break;

                case "/ask":
                    canceled = true;
                    if (args.Length < 3) break;
                    Mathematician.Ask(PlayerControl.LocalPlayer, args[1], args[2]);
                    break;

                case "/ans":
                case "/answer":
                    canceled = true;
                    if (args.Length < 2) break;
                    Mathematician.Reply(PlayerControl.LocalPlayer, args[1]);
                    break;

                case "/qa":
                    if (args.Length < 2 || !QuizMaster.On || !PlayerControl.LocalPlayer.IsAlive()) break;
                    var qm = (QuizMaster)Main.PlayerStates.Values.First(x => x.Role is QuizMaster).Role;
                    if (qm.Target != localPlayerId) break;
                    qm.Answer(args[1].ToUpper());
                    break;

                case "/qs":
                    if (args.Length < 2 || !QuizMaster.On || !PlayerControl.LocalPlayer.IsAlive()) break;
                    var qm2 = (QuizMaster)Main.PlayerStates.Values.First(x => x.Role is QuizMaster).Role;
                    if (qm2.Target != localPlayerId || !QuizMaster.MessagesToSend.TryGetValue(localPlayerId, out var msg)) break;
                    Utils.SendMessage(msg, localPlayerId, GetString("QuizMaster.QuestionSample.Title"));
                    break;

                case "/ban":
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
                    AmongUsClient.Instance.KickPlayer(kickedPlayer.GetClientId(), args[0] == "/ban");
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
                    msgText = Main.AllPlayerControls.Aggregate(msgText, (current, pc) => current + "\n" + pc.PlayerId + " → " + Main.AllPlayerNames[pc.PlayerId]);

                    Utils.SendMessage(msgText, localPlayerId);
                    break;

                case "/changerole":
                    if (GameStates.IsLobby || !PlayerControl.LocalPlayer.FriendCode.GetDevUser().IsUp) break;
                    canceled = true;
                    subArgs = text.Remove(0, 8);
                    var setRole = FixRoleNameInput(subArgs.Trim());
                    foreach (CustomRoles rl in Enum.GetValues(typeof(CustomRoles)))
                    {
                        if (rl.IsVanilla()) continue;
                        var roleName = GetString(rl.ToString()).ToLower().Trim();
                        if (setRole.Contains(roleName))
                        {
                            PlayerControl pc = PlayerControl.LocalPlayer;
                            if (!rl.IsAdditionRole()) pc.RpcSetRole(rl.GetRoleTypes());
                            pc.RpcSetCustomRole(rl);

                            if (rl.IsGhostRole()) GhostRolesManager.SpecificAssignGhostRole(pc.PlayerId, rl, true);

                            Main.PlayerStates[pc.PlayerId].RemoveSubRole(CustomRoles.NotAssigned);
                            Main.ChangedRole = true;
                            break;
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

                    if (guessedNo is < 0 or > 99)
                    {
                        Utils.SendMessage(GetString("GNoCommandInfo"), localPlayerId);
                        break;
                    }

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
                        Utils.SendMessage(string.Format(GetString("GNoLost"), targetNumber), localPlayerId);
                        break;
                    }

                    if (guessedNo < targetNumber)
                    {
                        Utils.SendMessage(string.Format(GetString("GNoLow"), Main.GuessNumber[localPlayerId][1]), localPlayerId);
                        break;
                    }

                    if (guessedNo > targetNumber)
                    {
                        Utils.SendMessage(string.Format(GetString("GNoHigh"), Main.GuessNumber[localPlayerId][1]), localPlayerId);
                        break;
                    }

                    Utils.SendMessage(string.Format(GetString("GNoWon"), Main.GuessNumber[localPlayerId][1]), localPlayerId);
                    Main.GuessNumber[localPlayerId][0] = -1;
                    Main.GuessNumber[localPlayerId][1] = 7;
                    break;

                default:
                    Main.IsChatCommand = false;
                    break;
            }
        }

        if (Silencer.ForSilencer.Contains(PlayerControl.LocalPlayer.PlayerId) && PlayerControl.LocalPlayer.IsAlive())
        {
            ChatManager.SendPreviousMessagesToAll();
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

        return !canceled;
    }

    public static string FixRoleNameInput(string text)
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
            _ => text,
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
            string result = string.Empty;
            for (int i = 0; i < mc.Count; i++)
            {
                if (mc[i].ToString() == "是") continue;
                result += mc[i]; //匹配结果是完整的数字，此处可以不做拼接的
            }

            name = FixRoleNameInput(result.Replace("是", string.Empty).Trim());
        }
        else name = name.Trim().ToLower();

        foreach (var rl in EnumHelper.GetAllValues<CustomRoles>())
        {
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
        if (Options.CurrentGameMode != CustomGameMode.Standard)
        {
            Utils.SendMessage(GetString($"ModeDescribe.{Options.CurrentGameMode}"), playerId);
            return;
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

        foreach (var rl in EnumHelper.GetAllValues<CustomRoles>())
        {
            if (rl.IsVanilla()) continue;
            var roleName = GetString(rl.ToString());
            if (role == roleName.ToLower().Trim().TrimStart('*').Replace(" ", string.Empty))
            {
                if ((isDev || isUp) && GameStates.IsLobby)
                {
                    string devMark = "▲";
                    if (rl.IsAdditionRole() || rl is CustomRoles.GM) devMark = string.Empty;
                    if (rl.GetCount() < 1 || rl.GetMode() == 0) devMark = string.Empty;
                    if (isUp)
                    {
                        Utils.SendMessage(devMark == "▲" ? string.Format(GetString("Message.YTPlanSelected"), roleName) : string.Format(GetString("Message.YTPlanSelectFailed"), roleName), playerId);
                    }

                    //if (devMark == "▲")
                    //{
                    //    byte pid = playerId == 255 ? (byte)0 : playerId;
                    //    _ = Main.DevRole.Remove(pid);
                    //    Main.DevRole.Add(pid, rl);
                    //}

                    if (isUp) return;
                }

                var sb = new StringBuilder();
                var title = $"<{Main.RoleColors[rl]}>{roleName}</color> {Utils.GetRoleMode(rl)}";
                _ = sb.Append($"{GetString($"{rl}InfoLong")}");
                var settings = new StringBuilder();
                if (Options.CustomRoleSpawnChances.TryGetValue(rl, out StringOptionItem chance))
                {
                    settings.AppendLine($"<size=70%><u>{GetString("SettingsForRoleText")} <{Main.RoleColors[rl]}>{roleName}</color>:</u>");
                    Utils.ShowChildrenSettings(chance, ref settings, disableColor: false);
                    settings.Append("</size>");
                    var txt = $"<size=90%>{sb}</size>";
                    _ = sb.Clear().Append(txt);
                }

                if (rl.PetActivatedAbility() && sb.Length < 1000) sb.Append($"<size=50%>{GetString("SupportsPetMessage")}</size>");
                Utils.SendMessage(text: "\n", sendTo: playerId, title: settings.ToString());
                Utils.SendMessage(text: sb.ToString(), sendTo: playerId, title: title);
                return;
            }
        }

        Utils.SendMessage(isUp ? GetString("Message.YTPlanCanNotFindRoleThePlayerEnter") : GetString("Message.CanNotFindRoleThePlayerEnter"), playerId);
    }

    public static void OnReceiveChat(PlayerControl player, string text, out bool canceled)
    {
        canceled = false;
        if (!AmongUsClient.Instance.AmHost) return;
        long now = Utils.TimeStamp;
        if (LastSentCommand.TryGetValue(player.PlayerId, out var ts) && ts + 2 >= now)
        {
            Logger.Warn("Command Ignored, it was sent too soon after their last command", "ReceiveChat");
            return;
        }

        if (player.PlayerId != 0) ChatManager.SendMessage(player, text);
        if (text.StartsWith("\n")) text = text[1..];

        string[] args = text.Split(' ');
        string subArgs;

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

        bool isCommand = true;
        switch (args[0])
        {
            case "/l":
            case "/lastresult":
                Utils.ShowKillLog(player.PlayerId);
                Utils.ShowLastAddOns(player.PlayerId);
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
                    var settings = new StringBuilder();
                    settings.Append("<size=70%>");
                    _ = sb.Append(GetString(role.ToString()) + Utils.GetRoleMode(role) + player.GetRoleInfo(true));
                    if (Options.CustomRoleSpawnChances.TryGetValue(role, out var opt))
                        Utils.ShowChildrenSettings(opt, ref settings, disableColor: false);
                    settings.Append("</size>");
                    var txt = sb.ToString();
                    _ = sb.Clear().Append(txt.RemoveHtmlTags());
                    if (role.PetActivatedAbility() && sb.Length < 1000) sb.Append("<size=50%>" + GetString("SupportsPetMessage").RemoveHtmlTags() + "</size>");
                    sb.Append("<size=70%>");
                    foreach (CustomRoles subRole in Main.PlayerStates[player.PlayerId].SubRoles.ToArray())
                    {
                        _ = sb.Append("\n\n" + GetString($"{subRole}") + Utils.GetRoleMode(subRole) + GetString($"{subRole}InfoLong"));
                    }

                    Utils.SendMessage("\n", player.PlayerId, settings.ToString());
                    Utils.SendMessage(sb.ToString(), player.PlayerId, string.Empty);
                    _ = new LateTask(() => { }, 0.5f, log: false);
                }
                else
                    Utils.SendMessage(GetString("Message.CanNotUseInLobby"), player.PlayerId);

                break;

            case "/t":
            case "/template":
                if (args.Length > 1) TemplateManager.SendTemplate(args[1], player.PlayerId);
                else Utils.SendMessage($"{GetString("ForExample")}:\n{args[0]} test", player.PlayerId);
                break;

            case "/say":
            case "/s":
                if (!IsPlayerModerator(player.FriendCode) && !DebugModeManager.AmDebugger && !player.FriendCode.GetDevUser().IsDev) break;
                if (args.Length > 1) Utils.SendMessage(args.Skip(1).Join(delimiter: " "), title: $"<color=#ff0000>{GetString("SayTitle")}</color>");
                break;

            case "/death":
                if (!GameStates.IsInGame || player.IsAlive()) break;
                var killer = player.GetRealKiller();
                if (killer == null) break;
                Utils.SendMessage("\n", player.PlayerId, string.Format(GetString("DeathCommand"), Utils.ColorString(Main.PlayerColors.TryGetValue(killer.PlayerId, out var pcColor) ? pcColor : Color.white, killer.GetRealName()), $"<{Main.RoleColors[killer.GetCustomRole()]}>{GetString(killer.GetCustomRole().ToString())}</color>"));
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

            case "/tpout":
                canceled = true;
                if (!GameStates.IsLobby || !Options.PlayerCanTPInAndOut.GetBool()) break;
                player.TP(new Vector2(0.1f, 3.8f));
                break;

            case "/tpin":
                canceled = true;
                if (!GameStates.IsLobby || !Options.PlayerCanTPInAndOut.GetBool()) break;
                player.TP(new Vector2(-0.2f, 1.3f));
                break;

            case "/rename":
            case "/rn":
                if (!Options.PlayerCanSetName.GetBool() || args.Length < 2) break;
                if (GameStates.IsInGame)
                {
                    Utils.SendMessage(GetString("Message.OnlyCanUseInLobby"), player.PlayerId);
                    break;
                }

                var name = args.Skip(1).Join(delimiter: " ");
                if (name.Length is > 10 or < 1)
                {
                    Utils.SendMessage(GetString("Message.AllowNameLength"), player.PlayerId);
                    break;
                }

                Main.AllPlayerNames[player.PlayerId] = name;
                player.RpcSetName(name);
                break;

            case "/id":
                string msgText = GetString("PlayerIdList");
                msgText = Main.AllPlayerControls.Aggregate(msgText, (current, pc) => $"{current}\n{pc.PlayerId} \u2192 {Main.AllPlayerNames[pc.PlayerId]}");

                Utils.SendMessage(msgText, player.PlayerId);
                break;
            case "/vote":
                canceled = true;
                if (text.Length < 6 || !GameStates.IsMeeting) break;
                string toVote = text[6..].Replace(" ", string.Empty);
                if (!byte.TryParse(toVote, out var voteId)) break;
                ChatManager.SendPreviousMessagesToAll();
                MeetingHud.Instance?.CastVote(player.PlayerId, voteId);
                break;
            case "/ask":
                if (args.Length < 3) break;
                Mathematician.Ask(player, args[1], args[2]);
                break;
            case "/ans":
            case "/answer":
                if (args.Length < 2) break;
                Mathematician.Reply(player, args[1]);
                break;
            case "/qa":
                if (args.Length < 2 || !QuizMaster.On || !player.IsAlive()) break;
                var qm = (QuizMaster)Main.PlayerStates.Values.First(x => x.Role is QuizMaster).Role;
                if (qm.Target != player.PlayerId) break;
                qm.Answer(args[1].ToUpper());
                break;
            case "/qs":
                if (args.Length < 2 || !QuizMaster.On || !player.IsAlive()) break;
                var qm2 = (QuizMaster)Main.PlayerStates.Values.First(x => x.Role is QuizMaster).Role;
                if (qm2.Target != player.PlayerId || !QuizMaster.MessagesToSend.TryGetValue(player.PlayerId, out var msg)) break;
                Utils.SendMessage(msg, player.PlayerId, GetString("QuizMaster.QuestionSample.Title"));
                break;
            case "/ban":
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
                AmongUsClient.Instance.KickPlayer(kickedPlayer.GetClientId(), args[0] == "/ban");
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

            case "/kcount":
                if (GameStates.IsLobby || !Options.EnableKillerLeftCommand.GetBool()) break;
                Utils.SendMessage(Utils.GetRemainingKillers(), player.PlayerId);
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

                if (guessedNo < 0 || guessedNo > 99)
                {
                    Utils.SendMessage(GetString("GNoCommandInfo"), player.PlayerId);
                    break;
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
                    //targetNumber = Main.GuessNumber[player.PlayerId][0];
                    Utils.SendMessage(string.Format(GetString("GNoLost"), targetNumber), player.PlayerId);
                    break;
                }

                if (guessedNo < targetNumber)
                {
                    Utils.SendMessage(string.Format(GetString("GNoLow"), Main.GuessNumber[player.PlayerId][1]), player.PlayerId);
                    break;
                }

                if (guessedNo > targetNumber)
                {
                    Utils.SendMessage(string.Format(GetString("GNoHigh"), Main.GuessNumber[player.PlayerId][1]), player.PlayerId);
                    break;
                }

                Utils.SendMessage(string.Format(GetString("GNoWon"), 7 - Main.GuessNumber[player.PlayerId][1]), player.PlayerId);
                Main.GuessNumber[player.PlayerId][0] = -1;
                Main.GuessNumber[player.PlayerId][1] = 7;
                break;

            default:
                isCommand = false;
                break;
        }

        if (Silencer.ForSilencer.Contains(player.PlayerId) && player.IsAlive() && player.PlayerId != 0)
        {
            ChatManager.SendPreviousMessagesToAll();
            canceled = true;
            LastSentCommand[player.PlayerId] = now;
            return;
        }

        if (isCommand) LastSentCommand[player.PlayerId] = now;
        SpamManager.CheckSpam(player, text);
    }
}

[HarmonyPatch(typeof(ChatController), nameof(ChatController.Update))]
internal class ChatUpdatePatch
{
    public static bool DoBlockChat;

    public static void Postfix(ChatController __instance)
    {
        var chatBubble = __instance.chatBubblePool.Prefab.Cast<ChatBubble>();
        chatBubble.TextArea.overrideColorTags = false;
        if (Main.DarkTheme.Value)
        {
            chatBubble.TextArea.color = Color.white;
            chatBubble.Background.color = Color.black;
        }

        if (!AmongUsClient.Instance.AmHost || Main.MessagesToSend.Count == 0 || (Main.MessagesToSend[0].RECEIVER_ID == byte.MaxValue && Main.MessageWait.Value > __instance.timeSinceLastMessage) || DoBlockChat) return;

        var player = Main.AllAlivePlayerControls.MinBy(x => x.PlayerId) ?? Main.AllPlayerControls.MinBy(x => x.PlayerId) ?? PlayerControl.LocalPlayer;
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

        var writer = CustomRpcSender.Create("MessagesToSend");
        writer.StartMessage(clientId);
        writer.StartRpc(player.NetId, (byte)RpcCalls.SetName)
            .Write(title)
            .EndRpc();
        writer.StartRpc(player.NetId, (byte)RpcCalls.SendChat)
            .Write(msg)
            .EndRpc();
        writer.StartRpc(player.NetId, (byte)RpcCalls.SetName)
            .Write(player.Data.PlayerName)
            .EndRpc();
        writer.EndMessage();
        writer.SendMessage();

        __instance.timeSinceLastMessage = 0f;
    }
}

[HarmonyPatch(typeof(FreeChatInputField), nameof(FreeChatInputField.UpdateCharCount))]
internal class UpdateCharCountPatch
{
    public static void Postfix(FreeChatInputField __instance)
    {
        int length = __instance.textArea.text.Length;
        __instance.charCountText.SetText(length <= 0 ? "Thank you for using EHR!" : $"{length}/{__instance.textArea.characterLimit}");
        __instance.charCountText.enableWordWrapping = false;
        if (length < (AmongUsClient.Instance.AmHost ? 1700 : 250))
            __instance.charCountText.color = Color.black;
        else if (length < (AmongUsClient.Instance.AmHost ? 2000 : 300))
            __instance.charCountText.color = new(1f, 1f, 0f, 1f);
        else
            __instance.charCountText.color = Color.red;
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