using System;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using InnerNet;
using static EHR.Translator;

namespace EHR;

internal static class EAC
{
    public static int DeNum;

    private static void WarnHost(int denum = 1)
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
        if (RoleBasisChanger.IsChangeInProgress) return false;
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
                    // Non-Host Clients will never send StartMeeting RPC
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

                    if (pc.Data.DefaultOutfit.ColorId != -1 && (Main.AllPlayerControls.Count(x => x.Data.DefaultOutfit.ColorId == color) >= 5 || color > 18))
                    {
                        WarnHost();
                        Report(pc, "Illegal color setting");
                        AmongUsClient.Instance.KickPlayer(pc.GetClientId(), false);
                        Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] illegally set the color and has been rejected", "EAC");
                        return true;
                    }

                    if (pc.AmOwner)
                    {
                        WarnHost();
                        Report(pc, "Illegal setting of host color");
                        Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] illegally set the host's color, which has been rejected", "EAC");
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
                    // Calls will only be sent by server / host
                    Report(pc, "Directly Murder Player");
                    HandleCheat(pc, "Directly Murder Player");
                    Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] killed directly, rejected", "EAC");
                    return true;
                case RpcCalls.CheckShapeshift:
                    if (GameStates.IsLobby)
                    {
                        Report(pc, "Lobby CheckShapeshift");
                        HandleCheat(pc, "Lobby CheckShapeshift");
                        Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] directly shapeshifted and has been rejected", "EAC");
                        return true;
                    }

                    break;
                case RpcCalls.Shapeshift:
                    Report(pc, "Directly Shapeshift");
                    var swriter = AmongUsClient.Instance.StartRpcImmediately(pc.NetId, (byte)RpcCalls.Shapeshift, SendOption.Reliable);
                    swriter.WriteNetObject(pc);
                    swriter.Write(false);
                    AmongUsClient.Instance.FinishRpcImmediately(swriter);
                    HandleCheat(pc, "Directly Shapeshift");
                    Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] directly shapeshifted and has been rejected", "EAC");
                    return true;
            }

            switch (callId)
            {
                case 101: // Aum Chat
                    try
                    {
                        var firstString = reader.ReadString();
                        var secondString = reader.ReadString();
                        reader.ReadInt32();

                        var flag = string.IsNullOrEmpty(firstString) && string.IsNullOrEmpty(secondString);

                        if (!flag)
                        {
                            Report(pc, "Aum Chat RPC");
                            HandleCheat(pc, "Aum Chat RPC");
                            Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] sent an AUM chat, which was rejected", "EAC");
                            return true;
                        }
                    }
                    catch
                    {
                    }

                    break;
                case unchecked((byte)42069): // 85 AUM
                    try
                    {
                        var aumid = reader.ReadByte();

                        if (aumid == pc.PlayerId)
                        {
                            Report(pc, "Aum RPC");
                            HandleCheat(pc, "Aum RPC");
                            Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] sent AUM RPC, which was rejected", "EAC");
                            return true;
                        }
                    }
                    catch
                    {
                    }

                    break;
                case unchecked((byte)420): // 164 Sicko
                    if (reader.BytesRemaining == 0)
                    {
                        Report(pc, "Sicko RPC");
                        HandleCheat(pc, "Sicko RPC");
                        Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] sent Sicko RPC, which was rejected", "EAC");
                        return true;
                    }

                    break;
                case 7:
                case 8:
                    if (!GameStates.IsLobby)
                    {
                        WarnHost();
                        Report(pc, "Illegal color setting");
                        Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] illegally set the color and has been rejected", "EAC");
                        return true;
                    }

                    break;
                case 5:
                    sr.ReadString();
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
                case 38:
                    if (GameStates.IsInGame)
                    {
                        WarnHost();
                        Report(pc, "Set level in game");
                        Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] changed the level in the game and it has been rejected", "EAC");
                        return true;
                    }

                    if (sr.ReadPackedUInt32() > 0)
                    {
                        uint ClientDataLevel = pc.GetClient() == null ? pc.GetClient().PlayerLevel : 0;
                        uint PlayerControlLevel = sr.ReadPackedUInt32();
                        if (ClientDataLevel != 0 && Math.Abs(PlayerControlLevel - ClientDataLevel) > 4)
                        {
                            WarnHost();
                            Report(pc, "Sus Level Change");
                            AmongUsClient.Instance.KickPlayer(pc.GetClientId(), false);
                            Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] changed the level in the game and it has been rejected", "EAC");
                            return true;
                        }
                    }

                    break;
                case 39:
                case 40:
                case 41:
                case 42:
                case 43:
                    if (GameStates.IsInGame)
                    {
                        WarnHost();
                        Report(pc, "Change skin in game");
                        Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] changed the skin in the game and it has been rejected", "EAC");
                        return true;
                    }

                    if (pc.AmOwner)
                    {
                        WarnHost();
                        Report(pc, "Change Host skin");
                        Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] changed the owner's skin and it has been rejected", "EAC");
                        return true;
                    }

                    break;
            }
        }
        catch
        {
        }

        WarnHost(-1);
        return false;
    }

    private static void Report(PlayerControl pc, string reason)
    {
        string msg = $"{pc.GetClientId()}|{pc.FriendCode}|{pc.Data.PlayerName}|{pc.GetClient().GetHashedPuid()}|{reason}";
        //Cloud.SendData(msg);
        Logger.Fatal($"EAC report: {msg}", "EAC Cloud");
        if (Options.CheatResponses.GetInt() != 5) Logger.SendInGame(string.Format(GetString("Message.NoticeByEAC"), $"{pc.Data?.PlayerName} | {pc.GetClient().GetHashedPuid()}", reason));
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

    private static void HandleCheat(PlayerControl pc, string text)
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
                foreach (var player in Main.AllPlayerControls)
                {
                    if (player.PlayerId != pc?.Data?.PlayerId)
                    {
                        var message = string.Format(GetString("Message.NoticeByEAC"), pc?.Data?.PlayerName, text);
                        var title = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Impostor), GetString("MessageFromEAC"));
                        Utils.SendMessage(message, player.PlayerId, title);
                    }
                }

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