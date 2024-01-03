using AmongUs.GameOptions;
using Hazel;
using InnerNet;
using System;
using System.Collections.Generic;
using System.Linq;
using static TOHE.Translator;

namespace TOHE;

internal class EAC
{
    public static int DeNum = 0;
    public static void WarnHost(int denum = 1)
    {
        DeNum += denum;
        if (ErrorText.Instance != null)
        {
            ErrorText.Instance.CheatDetected = DeNum > 3;
            ErrorText.Instance.SBDetected = DeNum > 10;
            if (ErrorText.Instance.CheatDetected)
                ErrorText.Instance.AddError(ErrorText.Instance.SBDetected ? ErrorCode.SBDetected : ErrorCode.CheatDetected);
            else
                ErrorText.Instance.Clear();
        }
    }
    public static bool ReceiveRpc(PlayerControl pc, byte callId, MessageReader reader)
    {
        if (!AmongUsClient.Instance.AmHost) return false;
        if (pc == null || reader == null) return false;
        try
        {
            MessageReader sr = MessageReader.Get(reader);
            var rpc = (RpcCalls)callId;
            switch (rpc)
            {
                //case RpcCalls.SetName:
                // string name = sr.ReadString();
                // if (sr.BytesRemaining > 0 && sr.ReadBoolean()) return false;
                // if (
                // ((name.Contains("<size") || name.Contains("size>")) && name.Contains('?') && !name.Contains("color")) ||
                // name.Length > 160 ||
                // name.Count(f => f.Equals("\"\\n\"")) > 3 ||
                // name.Count(f => f.Equals("\n")) > 3 ||
                // name.Count(f => f.Equals("\r")) > 3 ||
                // name.Contains('░') ||
                // name.Contains('▄') ||
                // name.Contains('█') ||
                // name.Contains('▌') ||
                // name.Contains('▒') ||
                // name.Contains("Xi Jinping")
                // )
                // {
                // WarnHost();
                // Report(pc, "Illegal setting of game name");
                // Logger.Fatal($"Illegal modification of the game name of player [{pc.GetClientId()}:{pc.GetRealName()}] has been rejected", "EAC");
                // return true;
                // }
                // break;
                case RpcCalls.SetRole:
                    var role = (RoleTypes)sr.ReadUInt16();
                    if (GameStates.IsLobby && (role is RoleTypes.CrewmateGhost or RoleTypes.ImpostorGhost))
                    {
                        WarnHost();
                        Report(pc, "Illegal setting status to ghost");
                        Logger.Fatal($"Illegal setting of the status of player [{pc.GetClientId()}:{pc.GetRealName()}] to ghost, has been rejected", "EAC");
                        return true;
                    }
                    break;
                case RpcCalls.SendChat:
                    var text = sr.ReadString();
                    if (text.StartsWith("/")) return false;
                    if (
                        text.Contains('░') ||
                        text.Contains('▄') ||
                        text.Contains('█') ||
                        text.Contains('▌') ||
                        text.Contains('▒') ||
                        text.Contains("Xi Jinping")
                        )
                    {
                        Report(pc, "Illegal message");
                        Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] sent an illegal message, which has been rejected", "EAC");
                        return true;
                    }
                    break;
                case RpcCalls.StartMeeting:
                    //Client will never send StartMeeting rpc
                    WarnHost();
                    Report(pc, "Bad StartMeeting");
                    HandleCheat(pc, "Bad StartMeeting");
                    Logger.Fatal($"Illegal setting of the game name of the player [{pc.GetClientId()}:{pc.GetRealName()}] has been rejected", "EAC");
                    return true;
                case RpcCalls.ReportDeadBody:
                    if (!GameStates.IsInGame)
                    {
                        WarnHost();
                        Report(pc, "Report body out of game A");
                        HandleCheat(pc, "Report body out of game A");
                        Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] has a non-in-game meeting and has been rejected", "EAC");
                        return true;
                    }
                    if (ReportTimes.TryGetValue(pc.PlayerId, out int rtimes))
                    {
                        if (rtimes > 14)
                        {
                            WarnHost();
                            Report(pc, "Spam report bodies A");
                            HandleCheat(pc, "Spam report bodies A");
                            Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] reported corpses 14 times and has been rejected", "EAC");
                            return true;
                        }
                    }
                    break;
                case RpcCalls.SetColor:
                case RpcCalls.CheckColor:
                    var color = sr.ReadByte();
                    if (!GameStates.IsLobby)
                    {
                        WarnHost();
                        Report(pc, "Set color in game");
                        HandleCheat(pc, "Set color in game");
                        Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] sets the color in the game and has been rejected", "EAC");
                        return true;
                    }
                    if (pc.Data.DefaultOutfit.ColorId != -1 &&
                        (Main.AllPlayerControls.Count(x => x.Data.DefaultOutfit.ColorId == color) >= 5
                        || color < 0 || color > 18))
                    {
                        WarnHost();
                        Report(pc, "Illegal color setting");
                        AmongUsClient.Instance.KickPlayer(pc.GetClientId(), false);
                        Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] illegally set the color and has been rejected", "EAC");
                        return true;
                    }
                    break;
                case RpcCalls.CheckMurder:
                    if (GameStates.IsLobby)
                    {
                        WarnHost();
                        Report(pc, "CheckMurder in Lobby");
                        HandleCheat(pc, "CheckMurder in Lobby");
                        Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] illegally checked for kill and has been rejected", "EAC");
                        return true;
                    }
                    break;
                case RpcCalls.MurderPlayer:
                    //If using version protocol, client should never directly send Murder player rpc
                    Report(pc, "Directly Murder Player");
                    HandleCheat(pc, "Directly Murder Player");
                    Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] killed directly, rejected", "EAC");
                    return true;
                case RpcCalls.Shapeshift:
                    if (GameStates.IsLobby)
                    {
                        Report(pc, "ShapeShift in lobby");
                        HandleCheat(pc, "ShapeShift in lobby");
                        Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] lobby is deformed and has been rejected", "EAC");
                        return true;
                    }
                    var target = sr.ReadNetObject<PlayerControl>();
                    if (target == null)
                    {
                        Report(pc, "ShapeShift to null player!");
                        //HandleCheat(pc, "ShapeShift to null player!");
                        Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] illegally transformed into an empty player and has been rejected", "EAC");
                        return true;
                    }
                    break;
            }
            switch (callId)
            {
                case 101:
                    var AUMChat = sr.ReadString();
                    WarnHost();
                    Report(pc, "AUM");
                    HandleCheat(pc, GetString("EAC.CheatDetected.EAC"));
                    return true;
                case 7:
                    if (!GameStates.IsLobby)
                    {
                        WarnHost();
                        Report(pc, "Illegal color setting");
                        Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] illegally set the color and has been rejected", "EAC");
                        return true;
                    }
                    break;
                case 5:
                    string name = sr.ReadString();
                    if (GameStates.IsInGame)
                    {
                        WarnHost();
                        Report(pc, "Illegal setting of game name");
                        Logger.Fatal($"Illegal modification of the game name of player [{pc.GetClientId()}:{pc.GetRealName()}] has been rejected", "EAC");
                        return true;
                    }
                    break;
                case 47:
                    if (GameStates.IsLobby)
                    {
                        WarnHost();
                        Report(pc, "illegal kill");
                        Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] illegally killed and has been rejected", "EAC");
                        return true;
                    }
                    break;
                case 41:
                    if (GameStates.IsInGame)
                    {
                        WarnHost();
                        Report(pc, "Illegal pet setting");
                        Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] illegally set a pet and has been rejected", "EAC");
                        return true;
                    }
                    break;
                case 40:
                    if (GameStates.IsInGame)
                    {
                        WarnHost();
                        Report(pc, "Illegal skin setting");
                        Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] illegally set the skin and has been rejected", "EAC");
                        return true;
                    }
                    break;
                case 42:
                    if (GameStates.IsInGame)
                    {
                        WarnHost();
                        Report(pc, "Illegal facial decoration");
                        Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] illegally set facial decoration and has been rejected", "EAC");
                        return true;
                    }
                    break;
                case 39:
                    if (GameStates.IsInGame)
                    {
                        WarnHost();
                        Report(pc, "Illegal hat setting");
                        Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] illegally set a hat and has been rejected", "EAC");
                        return true;
                    }
                    break;
                case 43:
                    if (sr.BytesRemaining > 0 && sr.ReadBoolean()) return false;
                    if (GameStates.IsInGame)
                    {
                        WarnHost();
                        Report(pc, "Illegal setting of game name");
                        Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] illegally set the name and has been rejected", "EAC");
                        return true;
                    }
                    break;
            }
        }
        catch (Exception e)
        {
            Logger.Exception(e, "EAC");
            //throw e;
        }
        WarnHost(-1);
        return false;
    }
    public static Dictionary<byte, CustomRoles> OriginalRoles = [];
    public static void LogAllRoles()
    {
        foreach (var pc in Main.AllPlayerControls.ToArray())
        {
            try
            {
                OriginalRoles.Add(pc.PlayerId, pc.GetCustomRole());
            }
            catch (Exception error)
            {
                Logger.Fatal(error.ToString(), "EAC.LogAllRoles");
            }
        }
    }
    public static bool RpcUpdateSystemCheck(PlayerControl player, SystemTypes systemType, byte amount)
    {
        //Update system rpc can not get received by playercontrol.handlerpc
        var Mapid = Main.NormalOptions.MapId;
        Logger.Info("Check sabotage RPC" + ", PlayerName: " + player.GetNameWithRole() + ", SabotageType: " + systemType.ToString() + ", amount: " + amount.ToString(), "EAC");
        if (player.AmOwner || !AmongUsClient.Instance.AmHost) return false;
        switch (systemType) //Normal sabotage using buttons
        {
            case SystemTypes.Sabotage:
                if (!player.HasKillButton() && !player.CanUseSabotage())
                {
                    WarnHost();
                    Report(player, "Bad Sabotage A : Non Imp");
                    HandleCheat(player, "Bad Sabotage A : Non Imp");
                    Logger.Fatal($"Player【{player.GetClientId()}:{player.GetRealName()}】Bad Sabotage A, rejected", "EAC");
                    return true;
                }
                break;
            case SystemTypes.LifeSupp:
                if (Mapid is not 0 and not 1 and not 3) goto YesCheat;
                else if (amount is not 64 and not 65) goto YesCheat;
                break;
            case SystemTypes.Comms:
                if (amount == 0)
                {
                    if (Mapid is 1 or 5) goto YesCheat;
                }
                else if (amount is 64 or 65 or 32 or 33 or 16 or 17)
                {
                    if (!(Mapid is 1 or 5)) goto YesCheat;
                }
                else goto YesCheat;
                break;
            case SystemTypes.Electrical:
                if (Mapid == 5) goto YesCheat;
                if (amount >= 5) //0 - 4 normal lights. other sabotage, Should never be sent by client
                {
                    goto YesCheat;
                }
                break;
            case SystemTypes.Laboratory:
                if (Mapid != 2) goto YesCheat;
                else if (!(amount is 64 or 65 or 32 or 33)) goto YesCheat;
                break;
            case SystemTypes.Reactor:
                if (Mapid is 2 or 4) goto YesCheat;
                else if (!(amount is 64 or 65 or 32 or 33)) goto YesCheat;
                //Airship use heli sabotage /Other use 64,65 | 32,33
                break;
            case SystemTypes.HeliSabotage:
                if (Mapid != 4) goto YesCheat;
                else if (!(amount is 64 or 65 or 16 or 17 or 32 or 33)) goto YesCheat;
                break;
            case SystemTypes.MushroomMixupSabotage:
                goto YesCheat;
        }

        if ((GameStates.IsMeeting && MeetingHud.Instance.state != MeetingHud.VoteStates.Animating) || ExileController.Instance != null)
        {
            WarnHost();
            Report(player, "Bad Sabotage D : In Meeting");
            Logger.Fatal($"Player【{player.GetClientId()}:{player.GetRealName()}】Bad Sabotage D, rejected", "EAC");
            return true;
        }
        //There may be cases where a player is fixing reactor and a meeting start, triggering EAC check in meeting

        return false;

    YesCheat:
        {
            WarnHost();
            Report(player, "Bad Sabotage C : Hack send RPC");
            HandleCheat(player, "Bad Sabotage C");
            Logger.Fatal($"Player【{player.GetClientId()}:{player.GetRealName()}】Bad Sabotage C, rejected", "EAC");
            return true;
        }
    }

    public static Dictionary<byte, int> ReportTimes = [];
    public static bool RpcReportDeadBodyCheck(PlayerControl player, GameData.PlayerInfo target)
    {
        if (!ReportTimes.ContainsKey(player.PlayerId))
        {
            ReportTimes.Add(player.PlayerId, 0);
        }
        //target == null , button event
        if (target == null || !Main.OverDeadPlayerList.Contains(target.PlayerId))
        {
            ReportTimes[player.PlayerId]++;
        }

        if (!GameStates.IsInGame)
        {
            WarnHost();
            Report(player, "Report body out of game");
            HandleCheat(player, "Report body out of game");
            Logger.Fatal($"Player [{player.GetClientId()}:{player.GetRealName()}] is not meeting in the game and has been rejected", "EAC");
            return true;
        }

        if (ReportTimes[player.PlayerId] >= 14)
        {
            //I believe nobody can report 14 different bodies in a single round if host players normally
            //This check is still not enough to stop spam meeting hacks or crazy hosts that spam kill command
            WarnHost();
            Report(player, "Spam report bodies");
            HandleCheat(player, "Spam report bodies");
            Logger.Fatal($"Player [{player.GetClientId()}:{player.GetRealName()}] reported corpses 14 times and has been rejected", "EAC");
            return true;
        }
        if (GameStates.IsMeeting)
        {
            //Cancel rpc report body if a meeting is already held
            Logger.Info($"Player [{player.GetClientId()}:{player.GetRealName()}] held a meeting during the meeting and it has been rejected", "EAC");
            return true;
        }

        return false;
        // Niko intended to do report living player check,
        // but concerning roles like bait, hacker somehow never use report dead body,
        // Niko gave up
    }
    public static void Report(PlayerControl pc, string reason)
    {
        string msg = $"{pc.GetClientId()}|{pc.FriendCode}|{pc.Data.PlayerName}|{pc.GetClient().GetHashedPuid()}|{reason}";
        //Cloud.SendData(msg);
        Logger.Fatal($"EAC report: {msg}", "EAC Cloud");
        Logger.SendInGame(string.Format(GetString("Message.NoticeByEAC"), $"{pc?.Data?.PlayerName} | {pc.GetClient().GetHashedPuid()}", reason));
    }
    public static bool ReceiveInvalidRpc(PlayerControl pc, byte callId)
    {
        switch (callId)
        {
            case unchecked((byte)42069):
                Report(pc, "AUM");
                HandleCheat(pc, GetString("EAC.CheatDetected.EAC"));
                return true;
        }
        return true;
    }
    public static void HandleCheat(PlayerControl pc, string text)
    {
        switch (Options.CheatResponses.GetInt())
        {
            case 0:
                AmongUsClient.Instance.KickPlayer(pc.GetClientId(), true);
                string msg0 = string.Format(GetString("Message.BanedByEAC"), pc?.Data?.PlayerName, text);
                Logger.Warn(msg0, "EAC");
                Logger.SendInGame(msg0);
                break;
            case 1:
                AmongUsClient.Instance.KickPlayer(pc.GetClientId(), false);
                string msg1 = string.Format(GetString("Message.KickedByEAC"), pc?.Data?.PlayerName, text);
                Logger.Warn(msg1, "EAC");
                Logger.SendInGame(msg1);
                break;
            case 2:
                Utils.SendMessage(string.Format(GetString("Message.NoticeByEAC"), pc?.Data?.PlayerName, text), PlayerControl.LocalPlayer.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Impostor), GetString("MessageFromEAC")));
                break;
            case 3:
                foreach (var apc in Main.AllPlayerControls.Where(x => x.PlayerId != pc?.Data?.PlayerId).ToArray())
                    Utils.SendMessage(string.Format(GetString("Message.NoticeByEAC"), pc?.Data?.PlayerName, text), pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Impostor), GetString("MessageFromEAC")));
                break;
            case 4:
                if (!BanManager.TempBanWhiteList.Contains(pc.GetClient().GetHashedPuid()))
                    BanManager.TempBanWhiteList.Add(pc.GetClient().GetHashedPuid());
                AmongUsClient.Instance.KickPlayer(pc.GetClientId(), true);
                string msg2 = string.Format(GetString("Message.TempBanedByEAC"), pc?.Data?.PlayerName, text);
                Logger.Warn(msg2, "EAC");
                Logger.SendInGame(msg2);
                break;
        }
    }
}