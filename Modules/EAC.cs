using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using HarmonyLib;
using Hazel;
using InnerNet;
using UnityEngine;
using static EHR.Translator;

namespace EHR;

internal static class EAC
{
    public static int DeNum;

    public static void WarnHost(int denum = 1)
    {
        DeNum += denum;
        if (ErrorText.Instance)
        {
            ErrorText.Instance.CheatDetected = DeNum > 3;
            ErrorText.Instance.SBDetected = DeNum > 10;
            if (ErrorText.Instance.CheatDetected) ErrorText.Instance.AddError(ErrorText.Instance.SBDetected ? ErrorCode.SBDetected : ErrorCode.CheatDetected);
            else ErrorText.Instance.Clear();
        }
    }

    public static bool ReceiveRpc(PlayerControl pc, byte callId, MessageReader reader)
    {
        if (!AmongUsClient.Instance.AmHost) return false;
        // if (RoleBasisChanger.IsChangeInProgress) return false;
        if (pc == null || reader == null) return false;
        try
        {
            MessageReader sr = MessageReader.Get(reader);
            var rpc = (RpcCalls)callId;
            switch (rpc)
            {
                case RpcCalls.CheckName:
                    if (!GameStates.IsLobby)
                    {
                        WarnHost();
                        Report(pc, "CheckName out of Lobby");
                        HandleCheat(pc, "CheckName out of Lobby");
                        Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] CheckName out of lobby, rejected", "EAC");
                        return true;
                    }

                    break;
                case RpcCalls.SendChat when !pc.IsHost():
                    var text = sr.ReadString();
                    if ((
                            text.Contains('░') ||
                            text.Contains('▄') ||
                            text.Contains('█') ||
                            text.Contains('▌') ||
                            text.Contains('▒') ||
                            text.Contains("习近平")
                        ))
                    {
                        Report(pc, "Illegal messages");
                        Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] sent an illegal message, which has been rejected", "EAC");
                        return true;
                    }

                    break;
                case RpcCalls.ReportDeadBody:
                    sr.ReadByte();

                    if (GameStates.IsMeeting && MeetingHud.Instance.state != MeetingHud.VoteStates.Animating && !pc.IsHost())
                    {
                        WarnHost();
                        Report(pc, "Report dead body in meeting");
                        HandleCheat(pc, "Report dead body in meeting");
                        Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] attempted to report a body in a meeting, rejected", "EAC");
                        return true;
                    }

                    if (!GameStates.IsInGame)
                    {
                        WarnHost();
                        Report(pc, "Try to Report body out of game B");
                        HandleCheat(pc, "Try to Report body out of game B");
                        Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] attempted to report a body that may have been illegally killed, but was rejected", "EAC");
                        return true;
                    }

                    break;
                case RpcCalls.CheckColor when !pc.IsHost():
                    if (!GameStates.IsLobby)
                    {
                        WarnHost();
                        Report(pc, "CheckColor out of Lobby");
                        HandleCheat(pc, "CheckColor out of Lobby");
                        Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] check color out of lobby, rejected", "EAC");
                        return true;
                    }

                    break;
                case RpcCalls.SetColor when !pc.IsHost():
                    Report(pc, "Directly SetColor");
                    HandleCheat(pc, "Directly SetColor");
                    Logger.Fatal($"Directly SetColor【{pc.GetClientId()}:{pc.GetRealName()}】has been rejected", "EAC");
                    return true;
                case RpcCalls.CheckMurder:
                    if (GameStates.IsLobby)
                    {
                        WarnHost();
                        Report(pc, "CheckMurder in Lobby");
                        HandleCheat(pc, "CheckMurder in Lobby");
                        Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] Illegal check kill, rejected", "EAC");
                        return true;
                    }

                    break;
                case RpcCalls.MurderPlayer:
                    sr.ReadNetObject<PlayerControl>();
                    if (GameStates.IsLobby)
                    {
                        Report(pc, "Directly Murder Player In Lobby");
                        HandleCheat(pc, "Directly Murder Player In Lobby");
                        Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] was killed directly in the lobby, rejected", "EAC");
                        return true;
                    }

                    Report(pc, "Directly Murder Player");
                    HandleCheat(pc, "Directly Murder Player");
                    Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] directly killed, rejected", "EAC");
                    return true;
                case RpcCalls.CheckShapeshift:
                    if (GameStates.IsLobby)
                    {
                        Report(pc, "Lobby Check Shapeshift");
                        HandleCheat(pc, "Lobby Check Shapeshift");
                        Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] directly transformed in lobby, rejected", "EAC");
                        return true;
                    }

                    break;
                case RpcCalls.Shapeshift when !pc.IsHost():
                {
                    Report(pc, "Directly Shapeshift");
                    MessageWriter swriter = AmongUsClient.Instance.StartRpcImmediately(pc.NetId, (byte)RpcCalls.Shapeshift, SendOption.Reliable);
                    swriter.WriteNetObject(pc);
                    swriter.Write(false);
                    AmongUsClient.Instance.FinishRpcImmediately(swriter);
                    HandleCheat(pc, "Directly Shapeshift");
                    Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] directly transformed, rejected", "EAC");
                    return true;
                }
                case RpcCalls.StartVanish:
                case RpcCalls.StartAppear:
                {
                    var sreason = "Direct Phantom RPCs " + rpc;
                    Report(pc, sreason);
                    var swriter = AmongUsClient.Instance.StartRpcImmediately(pc.NetId, (byte)RpcCalls.StartAppear, SendOption.Reliable);
                    swriter.Write(false);
                    AmongUsClient.Instance.FinishRpcImmediately(swriter);
                    HandleCheat(pc, sreason);
                    Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()} {sreason}, rejected", "EAC");
                    return true;
                }
                case RpcCalls.SendChatNote:
                    Report(pc, "Directly Send ChatNote");
                    HandleCheat(pc, "Directly Send ChatNote");
                    Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] directly sent ChatNote, which has been rejected", "EAC");
                    return true;
                case RpcCalls.CompleteTask when GameStates.IsMeeting && MeetingHud.Instance.state != MeetingHud.VoteStates.Animating && !pc.IsHost():
                    Report(pc, "Complete Task in Meeting");
                    HandleCheat(pc, "Complete Task in Meeting");
                    Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] completed a task in a meeting, which has been rejected", "EAC");
                    return true;
            }

            switch (callId)
            {
                case 101: // Aum Chat
                    try
                    {
                        var firstString = sr.ReadString();
                        var secondString = sr.ReadString();
                        sr.ReadInt32();

                        var flag = string.IsNullOrEmpty(firstString) && string.IsNullOrEmpty(secondString);

                        if (!flag)
                        {
                            Report(pc, "Aum Chat RPC");
                            HandleCheat(pc, "Aum Chat RPC");
                            Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] sent AUM chat, rejected", "EAC");
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
                        var aumid = sr.ReadByte();

                        if (aumid == pc.PlayerId)
                        {
                            Report(pc, "AUM RPC");
                            HandleCheat(pc, "AUM RPC");
                            Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] sent AUM RPC, rejected", "EAC");
                            return true;
                        }
                    }
                    catch
                    {
                    }

                    break;
                case unchecked((byte)420): // 164 Sicko
                    if (sr.BytesRemaining == 0)
                    {
                        Report(pc, "Sicko RPC");
                        HandleCheat(pc, "Sicko RPC");
                        Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] sent Sicko RPC, rejected", "EAC");
                        return true;
                    }

                    break;
                case 7 when !pc.IsHost():
                case 8 when !pc.IsHost():
                    if (!GameStates.IsLobby)
                    {
                        WarnHost();
                        Report(pc, "Illegal setting of color");
                        Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] illegally set the color, rejected", "EAC");
                        return true;
                    }

                    break;
                case 5 when !pc.IsHost():
                    sr.ReadString();
                    if (GameStates.IsInGame)
                    {
                        WarnHost();
                        Report(pc, "Illegal setting of game name");
                        Logger.Fatal($"Illegal modification of the game name of the player [{pc.GetClientId()}:{pc.GetRealName()}] has been rejected", "EAC");
                        return true;
                    }

                    break;
                case 47:
                    if (GameStates.IsLobby)
                    {
                        WarnHost();
                        Report(pc, "Illegal Killing");
                        Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] illegally killed, rejected", "EAC");
                        return true;
                    }

                    break;
                case 38 when !pc.IsHost():
                    if (GameStates.IsInGame)
                    {
                        WarnHost();
                        Report(pc, "Set level in game");
                        Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] changed the level in the game, which has been rejected", "EAC");
                        return true;
                    }

                    break;
                case 39 when !pc.IsHost():
                case 40 when !pc.IsHost():
                case 41 when !pc.IsHost():
                case 42 when !pc.IsHost():
                case 43 when !pc.IsHost():
                    if (GameStates.IsInGame)
                    {
                        WarnHost();
                        Report(pc, "Change skin in game");
                        Logger.Fatal($"Player [{pc.GetClientId()}:{pc.GetRealName()}] changed skin in the game, rejected", "EAC");
                        return true;
                    }

                    break;
            }
        }
        catch (Exception e)
        {
            Logger.Exception(e, "EAC");
        }

        WarnHost(-1);
        return false;
    }

    public static bool PlayerPhysicsRpcCheck(PlayerPhysics __instance, byte callId, MessageReader reader) // Credit: NikoCat233
    {
        if (!AmongUsClient.Instance.AmHost) return false;

        var rpcType = (RpcCalls)callId;
        MessageReader subReader = MessageReader.Get(reader);

        var player = __instance.myPlayer;

        if (!player)
        {
            Logger.Warn("Received Physics RPC without a player", "EAC_PlayerPhysics");
            return true;
        }

        if (GameStates.IsLobby && rpcType is not RpcCalls.Pet and not RpcCalls.CancelPet)
        {
            WarnHost();
            Report(player, $"Physics {rpcType} in lobby (can be spoofed by others)");
            HandleCheat(player, $"Physics {rpcType} in lobby (can be spoofed by others)");
            Logger.Fatal($"【{player.GetClientId()}:{player.GetRealName()}】 attempted to {rpcType} in lobby.", "EAC_physics");
            return true;
        }

        switch (rpcType)
        {
            case RpcCalls.EnterVent:
            case RpcCalls.ExitVent:
                int ventid = subReader.ReadPackedInt32();
                if (!HasVent(ventid))
                {
                    if (AmongUsClient.Instance.AmHost)
                    {
                        WarnHost();
                        Report(player, "Vent null vent (can be spoofed by others)");
                        HandleCheat(player, "Vent null vent (can be spoofed by others)");
                        Logger.Fatal($"【{player.GetClientId()}:{player.GetRealName()}】 attempted to enter an unexisting vent. {ventid}", "EAC_physics");
                    }
                    else
                    {
                        // Not sure whether host will send null vent to a player huh
                        Logger.Warn($"【{player.GetClientId()}:{player.GetRealName()}】 attempted to enter an unexisting vent. {ventid}", "EAC_physics");
                        if (rpcType is RpcCalls.ExitVent)
                        {
                            player.Visible = true;
                            player.inVent = false;
                            player.moveable = true;
                            player.NetTransform.SetPaused(false);
                        }
                    }

                    return true;
                }

                break;

            case RpcCalls.BootFromVent:
                // BootFromVent can only be sent by host
                WarnHost();
                Report(player, "Got boot from vent from clients, can be spoofed");
                HandleCheat(player, "Got boot from vent from clients, can be spoofed");
                Logger.Fatal($"【{player.GetClientId()}:{player.GetRealName()}】 sent boot from vent, can be spoofed.", "EAC_physics");
                break;

            case RpcCalls.ClimbLadder:
                int ladderId = subReader.ReadPackedInt32();
                if (!HasLadder(ladderId))
                {
                    if (AmongUsClient.Instance.AmHost)
                    {
                        WarnHost();
                        Report(player, "climb null ladder (can be spoofed by others)");
                        HandleCheat(player, "climb null ladder (can be spoofed by others)");
                        Logger.Fatal($"【{player.GetClientId()}:{player.GetRealName()}】 attempted to climb an unexisting ladder.", "EAC_physics");
                    }

                    return true;
                }

                if (player.AmOwner)
                {
                    Logger.Fatal("Got climb ladder for myself, this is impossible", "EAC_physics");
                    return true;
                }

                break;

            case RpcCalls.Pet:
                if (player.AmOwner)
                {
                    Logger.Fatal("Got pet pet for myself, this is impossible", "EAC_physics");
                    return true;
                }

                break;
        }

        return false;

        bool HasLadder(int ladderId) => ShipStatus.Instance.Ladders.Any(l => l.Id == ladderId);
        bool HasVent(int ventId) => ShipStatus.Instance.AllVents.Any(v => v.Id == ventId);
    }

    internal static void Report(PlayerControl pc, string reason)
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
                string msg0 = string.Format(GetString("Message.BannedByEAC"), pc?.Data?.PlayerName, text);
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
                var hashedPuid = pc.GetClient().GetHashedPuid();
                if (!BanManager.TempBanWhiteList.Contains(hashedPuid))
                    BanManager.TempBanWhiteList.Add(hashedPuid);
                AmongUsClient.Instance.KickPlayer(pc.GetClientId(), true);
                string msg2 = string.Format(GetString("Message.TempBannedByEAC"), pc?.Data?.PlayerName, text);
                Logger.Warn(msg2, "EAC");
                Logger.SendInGame(msg2);
                break;
        }
    }

    internal static bool CheckInvalidSabotage(SystemTypes systemType, PlayerControl player, byte amount)
    {
        if (player.IsHost()) return false;
        if ((GameStates.IsMeeting && MeetingHud.Instance.state != MeetingHud.VoteStates.Animating) || ExileController.Instance)
        {
            WarnHost();
            Report(player, "Bad Sabotage D : In Meeting");
            HandleCheat(player, "Bad Sabotage D : In Meeting");
            Logger.Fatal($"Player [{player.GetClientId()}:{player.GetRealName()}] Bad Sabotage D, rejected", "EAC");
            return true;
        }

        var Mapid = Main.NormalOptions.MapId;
        switch (systemType)
        {
            case SystemTypes.LifeSupp:
                if (Mapid != 0 && Mapid != 1 && Mapid != 3) goto Cheat;
                if (amount != 64 && amount != 65) goto Cheat;
                break;
            case SystemTypes.Comms:
                switch (amount)
                {
                    case 0:
                        if (Mapid is 1 or 5) goto Cheat;
                        break;
                    case 64:
                    case 65:
                    case 32:
                    case 33:
                    case 16:
                    case 17:
                        if (Mapid is not (1 or 5)) goto Cheat;
                        break;
                    default:
                        goto Cheat;
                }

                break;
            case SystemTypes.Electrical:
                if (Mapid == 5) goto Cheat;
                if (amount >= 5) goto Cheat;
                break;
            case SystemTypes.Laboratory:
                if (Mapid != 2) goto Cheat;
                if (amount is not (64 or 65 or 32 or 33)) goto Cheat;
                break;
            case SystemTypes.Reactor:
                if (Mapid is 2 or 4) goto Cheat;
                if (amount is not (64 or 65 or 32 or 33)) goto Cheat;
                break;
            case SystemTypes.HeliSabotage:
                if (Mapid != 4) goto Cheat;
                if (amount is not (64 or 65 or 16 or 17 or 32 or 33)) goto Cheat;
                break;
            case SystemTypes.MushroomMixupSabotage:
                goto Cheat;
        }

        return false;

        Cheat:
        {
            WarnHost();
            Report(player, "Bad Sabotage C : Hack send RPC");
            HandleCheat(player, "Bad Sabotage C");
            Logger.Fatal($"Player [{player.GetClientId()}:{player.GetRealName()}] Bad Sabotage C, rejected", "EAC");
            return true;
        }
    }
}

// https://github.com/0xDrMoe/TownofHost-Enhanced/blob/main/Patches/InnerNetClientPatch.cs
enum GameDataTag : byte
{
    DataFlag = 1,
    RpcFlag = 2,
    SpawnFlag = 4,
    DespawnFlag = 5,
    SceneChangeFlag = 6,
    ReadyFlag = 7,
    ChangeSettingsFlag = 8,
    ConsoleDeclareClientPlatformFlag = 205,
    PS4RoomRequest = 206,
    XboxDeclareXuid = 207,
}

[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.HandleGameDataInner))]
internal class GameDataHandlerPatch
{
    public static bool Prefix(InnerNetClient __instance, MessageReader reader, int msgNum)
    {
        var tag = (GameDataTag)reader.Tag;

        switch (tag)
        {
            case GameDataTag.DataFlag:
            {
                var netId = reader.ReadPackedUInt32();
                if (__instance.allObjectsFast.TryGetValue(netId, out var obj))
                {
                    if (obj.AmOwner)
                    {
                        Logger.Warn($"Received DataFlag for object {netId.ToString()} {obj.name} that we own.", "GameDataHandlerPatch");
                        EAC.WarnHost();
                        return false;
                    }

                    if (AmongUsClient.Instance.AmHost)
                    {
                        if (obj == MeetingHud.Instance)
                        {
                            Logger.Warn($"Received DataFlag for MeetingHud {netId.ToString()} that we own.", "GameDataHandlerPatch");
                            EAC.WarnHost();
                            return false;
                        }

                        if (obj == VoteBanSystem.Instance)
                        {
                            Logger.Warn($"Received DataFlag for VoteBanSystem {netId.ToString()} that we own.", "GameDataHandlerPatch");
                            EAC.WarnHost();
                            return false;
                        }

                        if (obj is NetworkedPlayerInfo)
                        {
                            Logger.Warn($"Received DataFlag for NetworkedPlayerInfo {netId.ToString()} that we own.", "GameDataHandlerPatch");
                            EAC.WarnHost();
                            return false;
                        }
                    }
                }

                break;
            }

            case GameDataTag.RpcFlag:
                break;

            case GameDataTag.SpawnFlag:
                break;

            case GameDataTag.DespawnFlag:
                break;

            case GameDataTag.SceneChangeFlag:
            {
                // Sender is only allowed to change his own scene.
                var clientId = reader.ReadPackedInt32();
                var scene = reader.ReadString();

                var client = Utils.GetClientById(clientId);

                if (client == null)
                {
                    Logger.Warn($"Received SceneChangeFlag for unknown client {clientId}.", "GameDataHandlerPatch");
                    return false;
                }

                if (string.IsNullOrEmpty(scene))
                {
                    Logger.Warn($"Client {client.PlayerName} ({client.Id}) tried to send SceneChangeFlag with null scene.", "GameDataHandlerPatch");
                    EAC.WarnHost();
                    return false;
                }

                if (scene.ToLower() == "tutorial")
                {
                    Logger.Warn($"Client {client.PlayerName} ({client.Id}) tried to send SceneChangeFlag to Tutorial.", "GameDataHandlerPatch");
                    EAC.WarnHost(100);

                    if (GameStates.IsOnlineGame && AmongUsClient.Instance.AmHost)
                    {
                        Utils.ErrorEnd("SceneChange Tutorial Hack");
                    }

                    return false;
                }

                if (GameStates.IsInGame)
                {
                    Logger.Warn($"Client {client.PlayerName} ({client.Id}) tried to send SceneChangeFlag during mid of game.", "GameDataHandlerPatch");
                    return false;
                }

                break;
            }

            case GameDataTag.ReadyFlag:
            {
                var clientId = reader.ReadPackedInt32();
                var client = Utils.GetClientById(clientId);

                if (client == null)
                {
                    Logger.Warn($"Received ReadyFlag for unknown client {clientId}.", "GameDataHandlerPatch");
                    EAC.WarnHost();
                    return false;
                }

                if (AmongUsClient.Instance.AmHost)
                {
                    if (!StartGameHostPatchEAC.IsStartingAsHost)
                    {
                        Logger.Warn($"Received ReadyFlag while game is started from {clientId}.", "GameDataHandlerPatch");
                        EAC.WarnHost();
                        return false;
                    }
                }

                break;
            }

            case GameDataTag.ConsoleDeclareClientPlatformFlag:
                break;
        }

        return true;
    }
}

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.CoStartGameHost))]
internal static class StartGameHostPatchEAC
{
    public static bool IsStartingAsHost;

    public static void Prefix()
    {
        if (LobbyBehaviour.Instance != null)
            IsStartingAsHost = true;
    }

    public static void Postfix()
    {
        if (ShipStatus.Instance != null)
            IsStartingAsHost = false;
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
static class CheckInvalidMovementPatch
{
    private static readonly Dictionary<byte, long> LastCheck = [];
    public static readonly Dictionary<byte, Vector2> LastPosition = [];
    public static readonly HashSet<byte> ExemptedPlayers = [];

    public static void Postfix(PlayerControl __instance)
    {
        if (!GameStates.IsInTask || ExileController.Instance || !Options.EnableMovementChecking.GetBool() || Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod) >= 1.9f || AmongUsClient.Instance.Ping >= 300 || Options.CurrentGameMode == CustomGameMode.NaturalDisasters || Utils.GetRegionName() is not ("EU" or "NA" or "AS") || __instance == null || __instance.PlayerId == 255 || !__instance.IsAlive() || __instance.inVent) return;

        var pos = __instance.Pos();
        var now = Utils.TimeStamp;

        if (!LastPosition.TryGetValue(__instance.PlayerId, out var lastPosition))
        {
            SetCurrentData();
            return;
        }

        if (LastCheck.TryGetValue(__instance.PlayerId, out var lastCheck) && lastCheck == now) return;

        SetCurrentData();

        if (Vector2.Distance(lastPosition, pos) > 10f && PhysicsHelpers.AnythingBetween(__instance.Collider, lastPosition, pos, Constants.ShipOnlyMask, false))
        {
            if (ExemptedPlayers.Remove(__instance.PlayerId)) return;

            EAC.WarnHost();
            EAC.Report(__instance, "This player is moving too fast, possibly using a speed hack.");
        }

        return;

        void SetCurrentData()
        {
            LastPosition[__instance.PlayerId] = pos;
            LastCheck[__instance.PlayerId] = now;
        }
    }
}