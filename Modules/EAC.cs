using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AmongUs.GameOptions;
using AmongUs.QuickChat;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using Hazel;
using InnerNet;
using UnityEngine;
using static EHR.Translator;

namespace EHR;

internal static class EAC
{
    public static int DeNum;
    public static readonly HashSet<string> InvalidReports = [];
    public static readonly Dictionary<byte, Stopwatch> TimeSinceLastTaskCompletion = [];

    public static void WarnHost(int denum = 1)
    {
        DeNum += denum;

        if (ErrorText.Instance)
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

        MessageReader sr = MessageReader.Get(reader);
        bool gameStarted = AmongUsClient.Instance.GameState is InnerNetClient.GameStates.Started or InnerNetClient.GameStates.Ended;

        try
        {
            var rpc = (RpcCalls)callId;

            switch (rpc)
            {
                case RpcCalls.CheckName:
                {
                    if (!GameStates.IsLobby)
                    {
                        WarnHost();
                        Report(pc, "CheckName out of Lobby");
                        HandleCheat(pc, "CheckName out of Lobby");
                        Logger.Fatal($"Player [{pc.OwnerId}:{pc.GetRealName()}] CheckName out of lobby, rejected", "EAC");
                        sr.Recycle();
                        return true;
                    }

                    break;
                }
                case RpcCalls.SendChat when !pc.IsNonHostModdedClient():
                {
                    string text = sr.ReadString();

                    if (text.Contains('░') ||
                        text.Contains('▄') ||
                        text.Contains('█') ||
                        text.Contains('▌') ||
                        text.Contains('▒') ||
                        text.Contains("习近平") ||
                        (!pc.IsModdedClient() && text.Length > 100))
                    {
                        Report(pc, "Illegal messages");
                        Logger.Fatal($"Player [{pc.OwnerId}:{pc.GetRealName()}] sent an illegal message, which has been rejected", "EAC");
                        sr.Recycle();
                        return true;
                    }

                    break;
                }
                case RpcCalls.ReportDeadBody:
                {
                    byte targetId = sr.ReadByte();

                    if (GameStates.IsMeeting && MeetingHud.Instance.state != MeetingHud.VoteStates.Animating && !pc.IsHost())
                    {
                        WarnHost();
                        Report(pc, "Report dead body in meeting");
                        HandleCheat(pc, "Report dead body in meeting");
                        Logger.Fatal($"Player [{pc.OwnerId}:{pc.GetRealName()}] attempted to report a body in a meeting, rejected", "EAC");
                        sr.Recycle();
                        return true;
                    }

                    if (!GameStates.IsInGame)
                    {
                        WarnHost();
                        Report(pc, "Try to Report body out of game B");
                        sr.Recycle();
                        return true;
                    }

                    if (GameManager.Instance.TryCast<HideAndSeekManager>())
                    {
                        WarnHost();
                        Report(pc, "Try to Report body in Hide and Seek");
                        HandleCheat(pc, "Try to Report body in Hide and Seek");
                        Logger.Fatal($"Player [{pc.OwnerId}:{pc.GetRealName()}] attempted to report a body in Hide and Seek, rejected", "EAC");
                        sr.Recycle();
                        return true;
                    }

                    if (targetId != byte.MaxValue)
                    {
                        bool bodyExists = Object.FindObjectsOfType<DeadBody>().Any(deadBody => deadBody.ParentId == targetId);

                        if (!bodyExists && targetId != pc.PlayerId && (!MeetingHud.Instance || MeetingHud.Instance.state != MeetingHud.VoteStates.Animating))
                        {
                            Logger.Warn($"Player [{pc.OwnerId}:{pc.GetRealName()}] attempted to report a body that does't exist D", "EAC");
                            sr.Recycle();
                            return true;
                        }
                    }

                    break;
                }
                case RpcCalls.SendQuickChat:
                {
                    var quickChatPhraseType = (QuickChatPhraseType)sr.ReadByte();

                    switch (quickChatPhraseType)
                    {
                        case QuickChatPhraseType.Empty:
                        {
                            HandleCheat(pc, "Empty message in quick chat");
                            sr.Recycle();
                            return true;
                        }
                        case QuickChatPhraseType.PlayerId:
                        {
                            byte playerID = sr.ReadByte();

                            if (playerID == 255)
                            {
                                HandleCheat(pc, "Sending invalid player in quick chat");
                                sr.Recycle();
                                return true;
                            }

                            if (GameStates.InGame && GameData.Instance.GetPlayerById(playerID) == null)
                            {
                                HandleCheat(pc, "Sending non existing player in quick chat");
                                sr.Recycle();
                                return true;
                            }

                            break;
                        }
                    }

                    if (quickChatPhraseType != QuickChatPhraseType.ComplexPhrase) break;
                    sr.ReadUInt16();
                    int num = sr.ReadByte();

                    switch (num)
                    {
                        case 0:
                        {
                            HandleCheat(pc, "Complex phrase without arguments");
                            sr.Recycle();
                            return true;
                        }
                        case > 3:
                        {
                            HandleCheat(pc, "Trying to crash or lag other players");
                            sr.Recycle();
                            return true;
                        }
                    }

                    break;
                }
                case RpcCalls.CheckColor when !pc.IsNonHostModdedClient():
                {
                    if (!GameStates.IsLobby)
                    {
                        WarnHost();
                        Report(pc, "CheckColor out of Lobby");
                        HandleCheat(pc, "CheckColor out of Lobby");
                        Logger.Fatal($"Player [{pc.OwnerId}:{pc.GetRealName()}] check color out of lobby, rejected", "EAC");
                        sr.Recycle();
                        return true;
                    }

                    byte colorId = sr.ReadByte();

                    if (colorId > 17)
                    {
                        WarnHost();
                        Report(pc, "Invalid color");
                        HandleCheat(pc, "Invalid color");
                        Logger.Fatal($"Player [{pc.OwnerId}:{pc.GetRealName()}] sent invalid color {colorId}, rejected", "EAC");
                        sr.Recycle();
                        return true;
                    }

                    break;
                }
                case RpcCalls.SetColor when !pc.IsModdedClient() && (!Options.PlayerCanSetColor.GetBool() || !GameStates.IsLobby):
                {
                    WarnHost();
                    Report(pc, "Directly SetColor");
                    HandleCheat(pc, "Directly SetColor");
                    Logger.Fatal($"Directly SetColor【{pc.OwnerId}:{pc.GetRealName()}】has been rejected", "EAC");
                    sr.Recycle();
                    return true;
                }
                case RpcCalls.CheckMurder:
                {
                    if (GameStates.IsLobby)
                    {
                        WarnHost();
                        Report(pc, "CheckMurder in Lobby");
                        HandleCheat(pc, "CheckMurder in Lobby");
                        Logger.Fatal($"Player [{pc.OwnerId}:{pc.GetRealName()}] Illegal check kill, rejected", "EAC");
                        sr.Recycle();
                        return true;
                    }

                    break;
                }
                case RpcCalls.MurderPlayer:
                {
                    var target = sr.ReadNetObject<PlayerControl>();
                    var resultFlags = (MurderResultFlags)sr.ReadInt32();

                    if (GameStates.IsInTask && !resultFlags.HasFlag(MurderResultFlags.FailedError) && !resultFlags.HasFlag(MurderResultFlags.FailedProtected) && target != null && !target.Data.IsDead)
                        LateTask.New(() => target.RpcRevive(), 0.1f, log: false);

                    Report(pc, "Directly Murder Player");
                    HandleCheat(pc, "Directly Murder Player");
                    Logger.Fatal($"Player [{pc.OwnerId}:{pc.GetRealName()}] directly killed, rejected", "EAC");
                    sr.Recycle();
                    return true;
                }
                case RpcCalls.CheckShapeshift:
                {
                    var target = sr.ReadNetObject<PlayerControl>();
                    bool animate = sr.ReadBoolean();

                    if (!gameStarted)
                    {
                        WarnHost();
                        Report(pc, "Using shift button in lobby");
                        HandleCheat(pc, "Using shift button in lobby");
                        Logger.Fatal($"Player [{pc.OwnerId}:{pc.GetRealName()}] sent CheckShapeshift in lobby, rejected", "EAC");
                        sr.Recycle();
                        return true;
                    }

                    if (target != null && target != pc && !animate)
                    {
                        WarnHost();
                        Report(pc, "No shapeshift animation");
                        HandleCheat(pc, "No shapeshift animation");
                        Logger.Fatal($"Player [{pc.OwnerId}:{pc.GetRealName()}] sent CheckShapeshift with no animation, rejected", "EAC");
                        sr.Recycle();
                        return true;
                    }

                    if (pc.shapeshiftTargetPlayerId != -1 && target != null && target != pc)
                    {
                        WarnHost();
                        Report(pc, "Shapeshifting while shapeshifted");
                        HandleCheat(pc, "Shapeshifting while shapeshifted");
                        Logger.Fatal($"Player [{pc.OwnerId}:{pc.GetRealName()}] sent CheckShapeshift while shapeshifted, rejected", "EAC");
                        sr.Recycle();
                        return true;
                    }

                    if (((MeetingHud.Instance && MeetingHud.Instance.state != MeetingHud.VoteStates.Animating) || ExileController.Instance) && target != pc && !(Options.UseMeetingShapeshift.GetBool() && (GuessManager.Data.ContainsKey(pc.PlayerId) || pc.UsesMeetingShapeshift())))
                    {
                        WarnHost();
                        Report(pc, "Trying to shift during meeting");
                        sr.Recycle();
                        return true;
                    }

                    break;
                }
                case RpcCalls.Shapeshift when !pc.IsNonHostModdedClient():
                {
                    Report(pc, "Directly Shapeshift");
                    MessageWriter swriter = AmongUsClient.Instance.StartRpcImmediately(pc.NetId, (byte)RpcCalls.Shapeshift, SendOption.Reliable);
                    swriter.WriteNetObject(pc);
                    swriter.Write(false);
                    AmongUsClient.Instance.FinishRpcImmediately(swriter);
                    HandleCheat(pc, "Directly Shapeshift");
                    Logger.Fatal($"Player [{pc.OwnerId}:{pc.GetRealName()}] directly transformed, rejected", "EAC");
                    sr.Recycle();
                    return true;
                }
                case RpcCalls.StartVanish:
                case RpcCalls.StartAppear:
                {
                    string sreason = "Direct Specter RPCs " + rpc;
                    Report(pc, sreason);
                    MessageWriter swriter = AmongUsClient.Instance.StartRpcImmediately(pc.NetId, (byte)RpcCalls.StartAppear, SendOption.Reliable);
                    swriter.Write(false);
                    AmongUsClient.Instance.FinishRpcImmediately(swriter);
                    HandleCheat(pc, sreason);
                    Logger.Fatal($"Player [{pc.OwnerId}:{pc.GetRealName()} {sreason}, rejected", "EAC");
                    sr.Recycle();
                    return true;
                }
                case RpcCalls.CompleteTask:
                {
                    if (!gameStarted)
                    {
                        WarnHost();
                        Report(pc, "CompleteTask Rpc in lobby");
                        HandleCheat(pc, "CompleteTask Rpc in lobby");
                        Logger.Fatal($"Player [{pc.OwnerId}:{pc.GetRealName()}] sent CompleteTask RPC in lobby, rejected", "EAC");
                        sr.Recycle();
                        return true;
                    }

                    if (TimeSinceLastTaskCompletion.TryGetValue(pc.PlayerId, out Stopwatch timer) && timer.ElapsedMilliseconds < 100)
                    {
                        WarnHost();
                        Report(pc, "Auto complete tasks");
                        HandleCheat(pc, "Auto complete tasks");
                        Logger.Fatal($"Player [{pc.OwnerId}:{pc.GetRealName()}] sent CompleteTask RPC too fast, rejected", "EAC");
                        sr.Recycle();
                        return true;
                    }

                    if ((MeetingHud.Instance && MeetingHud.Instance.state != MeetingHud.VoteStates.Animating && !ReportDeadBodyPatch.MeetingStarted) || ExileController.Instance)
                    {
                        WarnHost();
                        Report(pc, "Doing task during meeting");
                        sr.Recycle();
                        return true;
                    }

                    TimeSinceLastTaskCompletion[pc.PlayerId] = Stopwatch.StartNew();
                    break;
                }
                case RpcCalls.SetStartCounter:
                {
                    if (GameStates.InGame)
                    {
                        WarnHost();
                        Report(pc, "SetStartCounter mid game");
                        HandleCheat(pc, "SetStartCounter Rpc mid game");
                        Logger.Fatal($"Player [{pc.OwnerId}:{pc.GetRealName()}] sent SetStartCounter RPC in game, rejected", "EAC");
                        sr.Recycle();
                        return true;
                    }

                    sr.ReadPackedInt32();
                    sbyte startCounter = sr.ReadSByte();

                    if (startCounter != -1)
                    {
                        WarnHost();
                        Report(pc, "Invalid SetStartCounter");
                        HandleCheat(pc, "Invalid SetStartCounter Rpc");
                        Logger.Fatal($"Player [{pc.OwnerId}:{pc.GetRealName()}] sent invalid SetStartCounter RPC, rejected", "EAC");
                        sr.Recycle();
                        return true;
                    }

                    break;
                }
                case RpcCalls.CheckVanish:
                {
                    if (!gameStarted)
                    {
                        WarnHost();
                        Report(pc, "Using vanish button in lobby");
                        HandleCheat(pc, "Using vanish button in lobby");
                        Logger.Fatal($"Player [{pc.OwnerId}:{pc.GetRealName()}] sent CheckVanish in lobby, rejected", "EAC");
                        sr.Recycle();
                        return true;
                    }

                    if ((MeetingHud.Instance && MeetingHud.Instance.state != MeetingHud.VoteStates.Animating) || ExileController.Instance)
                    {
                        WarnHost();
                        Report(pc, "Trying to vanish during meeting");
                        sr.Recycle();
                        return true;
                    }

                    break;
                }
                case RpcCalls.CheckAppear:
                {
                    if (!gameStarted)
                    {
                        WarnHost();
                        Report(pc, "Using appear button in lobby");
                        HandleCheat(pc, "Using appear button in lobby");
                        Logger.Fatal($"Player [{pc.OwnerId}:{pc.GetRealName()}] tried to appear in lobby, rejected", "EAC");
                        sr.Recycle();
                        return true;
                    }

                    if ((MeetingHud.Instance && MeetingHud.Instance.state != MeetingHud.VoteStates.Animating) || ExileController.Instance)
                    {
                        WarnHost();
                        Report(pc, "Trying to appear during meeting");
                        sr.Recycle();
                        return true;
                    }

                    break;
                }
                case RpcCalls.PlayAnimation:
                {
                    if (!gameStarted)
                    {
                        WarnHost();
                        Report(pc, "PlayAnimation Rpc in lobby");
                        HandleCheat(pc, "PlayAnimation Rpc in lobby");
                        Logger.Fatal($"Player [{pc.OwnerId}:{pc.GetRealName()}] sent PlayAnimation RPC in lobby, rejected", "EAC");
                        sr.Recycle();
                        return true;
                    }

                    if (!GameManager.Instance.LogicOptions.GetVisualTasks())
                    {
                        WarnHost();
                        Report(pc, "PlayAnimation Rpc with visuals off");
                        HandleCheat(pc, "PlayAnimation Rpc with visuals off");
                        Logger.Fatal($"Player [{pc.OwnerId}:{pc.GetRealName()}] sent PlayAnimation RPC with visuals off, rejected", "EAC");
                        sr.Recycle();
                        return true;
                    }

                    if ((MeetingHud.Instance && MeetingHud.Instance.state != MeetingHud.VoteStates.Animating) || ExileController.Instance)
                    {
                        WarnHost();
                        Report(pc, "PlayAnimation Rpc during meeting");
                        sr.Recycle();
                        return true;
                    }

                    break;
                }
                case RpcCalls.Exiled:
                case RpcCalls.SetName:
                case RpcCalls.StartMeeting:
                case RpcCalls.SendChatNote:
                case RpcCalls.SetRole:
                case RpcCalls.ProtectPlayer:
                case RpcCalls.UseZipline:
                case RpcCalls.TriggerSpores:
                case RpcCalls.RejectShapeshift:
                {
                    if (!pc.IsModdedClient())
                    {
                        WarnHost();
                        Report(pc, "Invalid Rpc");
                        HandleCheat(pc, "Invalid Rpc");
                        Logger.Fatal($"Player [{pc.OwnerId}:{pc.GetRealName()}] sent invalid RPC {rpc}, rejected", "EAC");
                    }

                    sr.Recycle();
                    return true;
                }
                case RpcCalls.SetScanner:
                {
                    if (!GameManager.Instance.LogicOptions.GetVisualTasks())
                    {
                        WarnHost();
                        Report(pc, "SetScanner Rpc with visuals off");
                        HandleCheat(pc, "SetScanner Rpc with visuals off");
                        Logger.Fatal($"Player [{pc.OwnerId}:{pc.GetRealName()}] sent SetScanner RPC with visuals off, rejected", "EAC");
                        sr.Recycle();
                        return true;
                    }

                    if (!sr.ReadBoolean()) break;

                    if (!gameStarted)
                    {
                        WarnHost();
                        Report(pc, "SetScanner Rpc in lobby");
                        HandleCheat(pc, "SetScanner Rpc in lobby");
                        Logger.Fatal($"Player [{pc.OwnerId}:{pc.GetRealName()}] sent SetScanner RPC in lobby, rejected", "EAC");
                        sr.Recycle();
                        return true;
                    }

                    if ((MeetingHud.Instance && MeetingHud.Instance.state != MeetingHud.VoteStates.Animating) || ExileController.Instance)
                    {
                        WarnHost();
                        Report(pc, "SetScanner Rpc during meeting");
                        sr.Recycle();
                        return true;
                    }

                    if (pc.IsImpostor())
                    {
                        WarnHost();
                        Report(pc, "SetScanner Rpc as impostor");
                        sr.Recycle();
                        return true;
                    }

                    if (pc.myTasks.ToArray().All(x => x.TaskType != TaskTypes.SubmitScan))
                    {
                        WarnHost();
                        Report(pc, "SetScanner Rpc without Submit Scan task");
                        sr.Recycle();
                        return true;
                    }

                    break;
                }
                case RpcCalls.UsePlatform:
                {
                    if (!gameStarted)
                    {
                        WarnHost();
                        Report(pc, "Using platform in lobby");
                        HandleCheat(pc, "Using platform in lobby");
                        Logger.Fatal($"Player [{pc.OwnerId}:{pc.GetRealName()}] tried to use platform in lobby, rejected", "EAC");
                        sr.Recycle();
                        return true;
                    }

                    if (GameManager.Instance.LogicOptions.MapId != 4 && !SubmergedCompatibility.IsSubmerged())
                    {
                        WarnHost();
                        Report(pc, "Using platform on wrong map");
                        HandleCheat(pc, "Using platform on wrong map");
                        Logger.Fatal($"Player [{pc.OwnerId}:{pc.GetRealName()}] tried to use platform on a map that does not have platforms, rejected", "EAC");
                        sr.Recycle();
                        return true;
                    }

                    if (GameManager.Instance.TryCast<HideAndSeekManager>())
                    {
                        WarnHost();
                        Report(pc, "Using platform in hide n seek");
                        HandleCheat(pc, "Using platform in hide n seek");
                        Logger.Fatal($"Player [{pc.OwnerId}:{pc.GetRealName()}] tried to use platform in hide n seek, rejected", "EAC");
                        sr.Recycle();
                        return true;
                    }

                    if ((MeetingHud.Instance && MeetingHud.Instance.state != MeetingHud.VoteStates.Animating) || ExileController.Instance)
                    {
                        WarnHost();
                        Report(pc, "Using platform during meeting");
                        sr.Recycle();
                        return true;
                    }

                    break;
                }
                case RpcCalls.CheckProtect:
                {
                    var target = sr.ReadNetObject<PlayerControl>();

                    if (!gameStarted)
                    {
                        WarnHost();
                        Report(pc, "Using protect button in lobby");
                        HandleCheat(pc, "Using protect button in lobby");
                        Logger.Fatal($"Player [{pc.OwnerId}:{pc.GetRealName()}] tried to protect in lobby, rejected", "EAC");
                        sr.Recycle();
                        return true;
                    }

                    if (target != null && target == pc)
                    {
                        WarnHost();
                        Report(pc, "Using protect button on self");
                        sr.Recycle();
                        return true;
                    }

                    if (target == null) break;

                    if ((MeetingHud.Instance && MeetingHud.Instance.state != MeetingHud.VoteStates.Animating) || ExileController.Instance)
                    {
                        WarnHost();
                        Report(pc, "Trying to protect during meeting");
                        sr.Recycle();
                        return true;
                    }

                    break;
                }
                case RpcCalls.CheckZipline:
                {
                    if (!gameStarted)
                    {
                        WarnHost();
                        Report(pc, "Using zipline in lobby");
                        HandleCheat(pc, "Using zipline in lobby");
                        Logger.Fatal($"Player [{pc.OwnerId}:{pc.GetRealName()}] tried to use zipline in lobby, rejected", "EAC");
                        sr.Recycle();
                        return true;
                    }

                    if (GameManager.Instance.LogicOptions.MapId < 5)
                    {
                        WarnHost();
                        Report(pc, "Using zipline on wrong map");
                        HandleCheat(pc, "Using zipline on wrong map");
                        Logger.Fatal($"Player [{pc.OwnerId}:{pc.GetRealName()}] tried to use zipline on a map that does not have ziplines, rejected", "EAC");
                        sr.Recycle();
                        return true;
                    }

                    if ((MeetingHud.Instance && MeetingHud.Instance.state != MeetingHud.VoteStates.Animating) || ExileController.Instance)
                    {
                        WarnHost();
                        Report(pc, "Using zipline during meeting");
                        sr.Recycle();
                        return true;
                    }

                    break;
                }
                case RpcCalls.CheckSpore:
                {
                    if (!gameStarted)
                    {
                        WarnHost();
                        Report(pc, "Triggering spore in lobby");
                        HandleCheat(pc, "Triggering spore in lobby");
                        Logger.Fatal($"Player [{pc.OwnerId}:{pc.GetRealName()}] tried to trigger spore in lobby, rejected", "EAC");
                        sr.Recycle();
                        return true;
                    }

                    if (GameManager.Instance.LogicOptions.MapId < 5)
                    {
                        WarnHost();
                        Report(pc, "Triggering spore on wrong map");
                        HandleCheat(pc, "Triggering spore on wrong map");
                        Logger.Fatal($"Player [{pc.OwnerId}:{pc.GetRealName()}] tried to trigger spore on a map that does not have spores, rejected", "EAC");
                        sr.Recycle();
                        return true;
                    }

                    if ((MeetingHud.Instance && MeetingHud.Instance.state != MeetingHud.VoteStates.Animating) || ExileController.Instance)
                    {
                        WarnHost();
                        Report(pc, "Triggering spore during meeting");
                        sr.Recycle();
                        return true;
                    }

                    break;
                }
            }

            switch (callId)
            {
                case 101: // Aum Chat
                {
                    try
                    {
                        string firstString = sr.ReadString();
                        string secondString = sr.ReadString();
                        sr.ReadInt32();

                        bool flag = string.IsNullOrWhiteSpace(firstString) && string.IsNullOrWhiteSpace(secondString);

                        if (!flag)
                        {
                            Report(pc, "Aum Chat RPC");
                            HandleCheat(pc, "Aum Chat RPC");
                            Logger.Fatal($"Player [{pc.OwnerId}:{pc.GetRealName()}] sent AUM chat, rejected", "EAC");
                            sr.Recycle();
                            return true;
                        }
                    }
                    catch { }

                    break;
                }
                case unchecked((byte)42069): // 85 AUM
                {
                    try
                    {
                        byte aumid = sr.ReadByte();

                        if (aumid == pc.PlayerId)
                        {
                            Report(pc, "AUM RPC (Hack)");
                            HandleCheat(pc, "AUM RPC (Hack)");
                            Logger.Fatal($"Player [{pc.OwnerId}:{pc.GetRealName()}] sent AUM RPC, rejected", "EAC");
                            sr.Recycle();
                            return true;
                        }
                    }
                    catch { }

                    break;
                }
                case unchecked((byte)420): // 164 Sicko
                {
                    if (sr.BytesRemaining == 0)
                    {
                        Report(pc, "Sicko RPC (Hack against host-only mods, like EHR)");
                        HandleCheat(pc, "Sicko RPC (Hack against host-only mods, like EHR)");
                        Logger.Fatal($"Player [{pc.OwnerId}:{pc.GetRealName()}] sent Sicko RPC, rejected", "EAC");
                        sr.Recycle();
                        return true;
                    }

                    break;
                }
                case 119:
                {
                    try
                    {
                        string firstString = sr.ReadString();
                        string secondString = sr.ReadString();
                        sr.ReadInt32();
                        bool flag = string.IsNullOrWhiteSpace(firstString) && string.IsNullOrWhiteSpace(secondString);

                        if (!flag)
                        {
                            HandleCheat(pc, "KN Chat");
                            sr.Recycle();
                            return true;
                        }
                    }
                    catch { }

                    break;
                }
                case 250:
                {
                    if (sr.BytesRemaining == 0)
                    {
                        HandleCheat(pc, "KN");
                        sr.Recycle();
                        return true;
                    }

                    break;
                }
                case 7 when !pc.IsNonHostModdedClient():
                case 8 when !pc.IsNonHostModdedClient():
                {
                    if (!GameStates.IsLobby)
                    {
                        WarnHost();
                        Report(pc, "Illegal setting of color");
                        Logger.Fatal($"Player [{pc.OwnerId}:{pc.GetRealName()}] illegally set the color, rejected", "EAC");
                        sr.Recycle();
                        return true;
                    }

                    break;
                }
                case 5 when !pc.IsNonHostModdedClient():
                {
                    sr.ReadString();

                    if (GameStates.IsInGame)
                    {
                        WarnHost();
                        Report(pc, "Illegal setting of game name");
                        Logger.Fatal($"Illegal modification of the game name of the player [{pc.OwnerId}:{pc.GetRealName()}] has been rejected", "EAC");
                        sr.Recycle();
                        return true;
                    }

                    break;
                }
                case 47:
                {
                    if (GameStates.IsLobby)
                    {
                        WarnHost();
                        Report(pc, "Illegal Killing");
                        Logger.Fatal($"Player [{pc.OwnerId}:{pc.GetRealName()}] illegally killed, rejected", "EAC");
                        sr.Recycle();
                        return true;
                    }

                    break;
                }
                case 38 when !pc.IsNonHostModdedClient():
                {
                    if (GameStates.IsInGame)
                    {
                        WarnHost();
                        Report(pc, "Set level in game");
                        Logger.Fatal($"Player [{pc.OwnerId}:{pc.GetRealName()}] changed the level in the game, which has been rejected", "EAC");
                        sr.Recycle();
                        return true;
                    }

                    break;
                }
                case 39 when !pc.IsNonHostModdedClient():
                case 40 when !pc.IsNonHostModdedClient():
                case 41 when !pc.IsNonHostModdedClient():
                case 42 when !pc.IsNonHostModdedClient():
                case 43 when !pc.IsNonHostModdedClient():
                {
                    if (GameStates.IsInGame)
                    {
                        WarnHost();
                        Report(pc, "Change skin in game");
                        Logger.Fatal($"Player [{pc.OwnerId}:{pc.GetRealName()}] changed skin in the game, rejected", "EAC");
                        sr.Recycle();
                        return true;
                    }

                    break;
                }
            }
        }
        catch (Exception e) { Logger.Exception(e, "EAC"); }
        finally { sr.Recycle(); }

        WarnHost(-1);
        return false;
    }

    public static bool PlayerPhysicsRpcCheck(PlayerPhysics __instance, byte callId, MessageReader reader) // Credit: NikoCat233
    {
        if (!AmongUsClient.Instance.AmHost) return false;

        var rpcType = (RpcCalls)callId;
        MessageReader subReader = MessageReader.Get(reader);
        bool gameStarted = AmongUsClient.Instance.GameState is InnerNetClient.GameStates.Started or InnerNetClient.GameStates.Ended;

        try
        {
            PlayerControl player = __instance.myPlayer;

            if (!player)
            {
                Logger.Warn("Received Physics RPC without a player", "EAC_PlayerPhysics");
                subReader.Recycle();
                return true;
            }

            if (GameStates.IsLobby && rpcType is not RpcCalls.Pet and not RpcCalls.CancelPet)
            {
                WarnHost();
                Report(player, $"Physics {rpcType} in lobby (can be spoofed by others)");
                HandleCheat(player, $"Physics {rpcType} in lobby (can be spoofed by others)");
                Logger.Fatal($"【{player.OwnerId}:{player.GetRealName()}】 attempted to {rpcType} in lobby.", "EAC_physics");
                subReader.Recycle();
                return true;
            }

            switch (rpcType)
            {
                case RpcCalls.EnterVent:
                case RpcCalls.ExitVent:
                {
                    if (!gameStarted)
                    {
                        WarnHost();
                        Report(player, "Venting in lobby");
                        HandleCheat(player, "Venting in lobby");
                        Logger.Fatal($"【{player.OwnerId}:{player.GetRealName()}】 attempted to vent in lobby.", "EAC_physics");
                        subReader.Recycle();
                        return true;
                    }

                    if ((MeetingHud.Instance && MeetingHud.Instance.state != MeetingHud.VoteStates.Animating) || ExileController.Instance)
                    {
                        WarnHost();
                        Report(player, "Venting during meeting");
                        HandleCheat(player, "Venting during meeting");
                        Logger.Fatal($"【{player.OwnerId}:{player.GetRealName()}】 attempted to vent during a meeting.", "EAC_physics");
                        subReader.Recycle();
                        return true;
                    }

                    int ventid = subReader.ReadPackedInt32();

                    if (!HasVent(ventid))
                    {
                        if (AmongUsClient.Instance.AmHost)
                        {
                            WarnHost();
                            Report(player, "Vent null vent (can be spoofed by others)");
                            HandleCheat(player, "Vent null vent (can be spoofed by others)");
                            Logger.Fatal($"【{player.OwnerId}:{player.GetRealName()}】 attempted to enter an unexisting vent. {ventid}", "EAC_physics");
                        }
                        else
                        {
                            // Not sure whether host will send null vent to a player huh
                            Logger.Warn($"【{player.OwnerId}:{player.GetRealName()}】 attempted to enter an unexisting vent. {ventid}", "EAC_physics");

                            if (rpcType is RpcCalls.ExitVent)
                            {
                                player.Visible = true;
                                player.inVent = false;
                                player.moveable = true;
                                player.NetTransform.SetPaused(false);
                            }
                        }

                        subReader.Recycle();
                        return true;
                    }

                    break;
                }

                case RpcCalls.BootFromVent:
                {
                    // BootFromVent can only be sent by host
                    WarnHost();
                    Report(player, "Got boot from vent from clients, can be spoofed");
                    HandleCheat(player, "Got boot from vent from clients, can be spoofed");
                    Logger.Fatal($"【{player.OwnerId}:{player.GetRealName()}】 sent boot from vent, can be spoofed.", "EAC_physics");
                    break;
                }

                case RpcCalls.ClimbLadder:
                {
                    if (!gameStarted)
                    {
                        WarnHost();
                        Report(player, "Climbing ladder in lobby");
                        HandleCheat(player, "Climbing ladder in lobby");
                        Logger.Fatal($"【{player.OwnerId}:{player.GetRealName()}】 attempted to climb a ladder in lobby.", "EAC_physics");
                        subReader.Recycle();
                        return true;
                    }

                    if (GameManager.Instance.LogicOptions.MapId < 4)
                    {
                        WarnHost();
                        Report(player, "Climbing ladder on wrong map");
                        HandleCheat(player, "Climbing ladder on wrong map");
                        Logger.Fatal($"【{player.OwnerId}:{player.GetRealName()}】 attempted to climb a ladder on a map that does not have ladders.", "EAC_physics");
                        subReader.Recycle();
                        return true;
                    }

                    if ((MeetingHud.Instance && MeetingHud.Instance.state != MeetingHud.VoteStates.Animating) || ExileController.Instance)
                    {
                        WarnHost();
                        Report(player, "Climbing ladder during meeting");
                        subReader.Recycle();
                        return true;
                    }

                    int ladderId = subReader.ReadPackedInt32();

                    if (!HasLadder(ladderId))
                    {
                        if (AmongUsClient.Instance.AmHost)
                        {
                            WarnHost();
                            Report(player, "climb null ladder (can be spoofed by others)");
                            HandleCheat(player, "climb null ladder (can be spoofed by others)");
                            Logger.Fatal($"【{player.OwnerId}:{player.GetRealName()}】 attempted to climb an unexisting ladder.", "EAC_physics");
                        }

                        subReader.Recycle();
                        return true;
                    }

                    if (player.AmOwner)
                    {
                        Logger.Fatal("Got climb ladder for myself, this is impossible", "EAC_physics");
                        subReader.Recycle();
                        return true;
                    }

                    break;
                }

                case RpcCalls.Pet:
                {
                    if (player.AmOwner)
                    {
                        Logger.Fatal("Got pet pet for myself, this is impossible", "EAC_physics");
                        subReader.Recycle();
                        return true;
                    }

                    goto case RpcCalls.CancelPet;
                }

                case RpcCalls.CancelPet:
                {
                    if (player.inVent)
                    {
                        WarnHost();
                        Report(player, "Petting in vent");
                        subReader.Recycle();
                        return true;
                    }

                    if ((MeetingHud.Instance && MeetingHud.Instance.state != MeetingHud.VoteStates.Animating) || ExileController.Instance)
                    {
                        WarnHost();
                        Report(player, "Petting during meeting");
                        subReader.Recycle();
                        return true;
                    }

                    break;
                }
            }
        }
        finally { subReader.Recycle(); }

        return false;

        bool HasLadder(int ladderId) => ShipStatus.Instance.Ladders.Any(l => l.Id == ladderId);

        bool HasVent(int ventId) => ShipStatus.Instance.AllVents.Any(v => v.Id == ventId);
    }

    internal static void Report(PlayerControl pc, string reason)
    {
        var msg = $"{pc.OwnerId}|{pc.FriendCode}|{pc.Data.PlayerName}|{pc.GetClient().GetHashedPuid()}|{reason}";
        //Cloud.SendData(msg);
        Logger.Fatal($"EAC report: {msg}", "EAC Cloud");
        if (Options.CheatResponses.GetInt() != 4) Logger.SendInGame(string.Format(GetString("Message.NoticeByEAC"), $"{pc.Data?.PlayerName} | {pc.GetClient().GetHashedPuid()}", reason), Color.red);
    }

    public static bool ReceiveInvalidRpc(PlayerControl pc, byte callId)
    {
        switch (callId)
        {
            case unchecked((byte)42069):
            {
                Report(pc, "AUM");
                HandleCheat(pc, GetString("EAC.CheatDetected.EAC"));
                break;
            }
        }

        return true;
    }

    private static void HandleCheat(PlayerControl pc, string text)
    {
        switch (Options.CheatResponses.GetInt())
        {
            case 0:
            {
                if (pc.IsTrusted()) break;
                AmongUsClient.Instance.KickPlayer(pc.OwnerId, true);
                string msg0 = string.Format(GetString("Message.BannedByEAC"), pc.Data?.PlayerName, text);
                Logger.Warn(msg0, "EAC");
                Logger.SendInGame(msg0, Color.yellow);
                break;
            }
            case 1:
            {
                if (pc.IsTrusted()) break;
                AmongUsClient.Instance.KickPlayer(pc.OwnerId, false);
                string msg1 = string.Format(GetString("Message.KickedByEAC"), pc.Data?.PlayerName, text);
                Logger.Warn(msg1, "EAC");
                Logger.SendInGame(msg1, Color.yellow);
                break;
            }
            case 2:
            {
                Utils.SendMessage(string.Format(GetString("Message.NoticeByEAC"), pc?.Data?.PlayerName, text), PlayerControl.LocalPlayer.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Impostor), GetString("MessageFromEAC")));
                break;
            }
            case 3:
            {
                (
                    from player in Main.EnumeratePlayerControls()
                    where player.PlayerId != pc?.Data?.PlayerId
                    let message = string.Format(GetString("Message.NoticeByEAC"), pc?.Data?.PlayerName, text)
                    let title = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Impostor), GetString("MessageFromEAC"))
                    select new Message(message, player.PlayerId, title)
                ).SendMultipleMessages(MessageImportance.Low);
                break;
            }
            case 5:
            {
                if (pc.IsTrusted()) break;

                string hashedPuid = pc.GetClient().GetHashedPuid();
                if (!BanManager.TempBanWhiteList.Contains(hashedPuid)) BanManager.TempBanWhiteList.Add(hashedPuid);

                AmongUsClient.Instance.KickPlayer(pc.OwnerId, true);
                string msg2 = string.Format(GetString("Message.TempBannedByEAC"), pc.Data?.PlayerName, text);
                Logger.Warn(msg2, "EAC");
                Logger.SendInGame(msg2, Color.yellow);
                break;
            }
        }
    }

    internal static bool CheckInvalidSabotage(SystemTypes systemType, PlayerControl player, byte amount)
    {
        if (player.IsModdedClient() || !AmongUsClient.Instance.AmHost) return false;

        if (GameStates.IsMeeting && MeetingHud.Instance.state is MeetingHud.VoteStates.Voted or MeetingHud.VoteStates.NotVoted or MeetingHud.VoteStates.Discussion or MeetingHud.VoteStates.Proceeding)
        {
            WarnHost();
            Report(player, "Bad Sabotage D : In Meeting");
            HandleCheat(player, "Bad Sabotage D : In Meeting");
            Logger.Fatal($"Player [{player.OwnerId}:{player.GetRealName()}] Bad Sabotage D, rejected", "EAC");
            return true;
        }

        byte mapid = Main.NormalOptions.MapId;

        switch (systemType)
        {
            case SystemTypes.LifeSupp:
            {
                if (mapid != 0 && mapid != 1 && mapid != 3 && !SubmergedCompatibility.IsSubmerged()) goto Cheat;
                if (amount != 64 && amount != 65 && !SubmergedCompatibility.IsSubmerged()) goto Cheat;
                break;
            }
            case SystemTypes.Comms:
            {
                switch (amount)
                {
                    case 0:
                    {
                        if (mapid is 1 or 5) goto Cheat;
                        break;
                    }
                    case 64:
                    case 65:
                    case 32:
                    case 33:
                    case 16:
                    case 17:
                    {
                        if (mapid is not (1 or 5)) goto Cheat;
                        break;
                    }
                    default: { goto Cheat; }
                }

                break;
            }
            case SystemTypes.Electrical:
            {
                if (mapid == 5) goto Cheat;
                if (amount >= 5) goto Cheat;
                break;
            }
            case SystemTypes.Laboratory:
            {
                if (mapid != 2) goto Cheat;
                if (amount is not (64 or 65 or 32 or 33)) goto Cheat;
                break;
            }
            case SystemTypes.Reactor:
            {
                if (mapid is 2 or 4) goto Cheat;
                if (amount is not (64 or 65 or 32 or 33)) goto Cheat;
                break;
            }
            case SystemTypes.HeliSabotage:
            {
                if (mapid != 4) goto Cheat;
                if (amount is not (64 or 65 or 16 or 17 or 32 or 33)) goto Cheat;
                break;
            }
            case SystemTypes.MushroomMixupSabotage: { goto Cheat; }
        }

        return false;

        Cheat:

        WarnHost();
        Report(player, "Bad Sabotage C : Hack send RPC");
        HandleCheat(player, "Bad Sabotage C");
        Logger.Fatal($"Player [{player.OwnerId}:{player.GetRealName()}] Bad Sabotage C, rejected", "EAC");
        return true;
    }
}

// https://github.com/0xDrMoe/TownofHost-Enhanced/blob/main/Patches/InnerNetClientPatch.cs
internal enum GameDataTag : byte
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
    XboxDeclareXuid = 207
}

[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.HandleGameDataInner))]
internal static class GameDataHandlerPatch
{
    public static bool Prepare()
    {
        // Disable EAC on Android.
        return !OperatingSystem.IsAndroid();
    }
    
    private static IEnumerator EmptyCoroutine() // fixes errors if we return false
    {
        yield break;
    }

    public static bool Prefix(InnerNetClient __instance, MessageReader reader, int msgNum, ref Il2CppSystem.Collections.IEnumerator __result)
    {
        var tag = (GameDataTag)reader.Tag;

        switch (tag)
        {
            case GameDataTag.DataFlag:
            {
                uint netId = reader.ReadPackedUInt32();

                if (__instance.allObjects.allObjectsFast.TryGetValue(netId, out InnerNetObject obj))
                {
                    if (obj.AmOwner)
                    {
                        Logger.Warn($"Received DataFlag for object {netId.ToString()} {obj.name} that we own.", "GameDataHandlerPatch");
                        EAC.WarnHost();
                        __result = EmptyCoroutine().WrapToIl2Cpp();
                        return false;
                    }

                    if (AmongUsClient.Instance.AmHost)
                    {
                        if (obj == MeetingHud.Instance)
                        {
                            Logger.Warn($"Received DataFlag for MeetingHud {netId.ToString()} that we own.", "GameDataHandlerPatch");
                            EAC.WarnHost();
                            __result = EmptyCoroutine().WrapToIl2Cpp();
                            return false;
                        }

                        if (obj == VoteBanSystem.Instance)
                        {
                            Logger.Warn($"Received DataFlag for VoteBanSystem {netId.ToString()} that we own.", "GameDataHandlerPatch");
                            EAC.WarnHost();
                            __result = EmptyCoroutine().WrapToIl2Cpp();
                            return false;
                        }

                        if (obj is NetworkedPlayerInfo)
                        {
                            Logger.Warn($"Received DataFlag for NetworkedPlayerInfo {netId.ToString()} that we own.", "GameDataHandlerPatch");
                            EAC.WarnHost();
                            __result = EmptyCoroutine().WrapToIl2Cpp();
                            return false;
                        }
                    }
                }

                break;
            }

            case GameDataTag.RpcFlag:
            case GameDataTag.SpawnFlag:
            case GameDataTag.DespawnFlag:
                break;

            case GameDataTag.SceneChangeFlag:
            {
                // Sender is only allowed to change his own scene.
                int clientId = reader.ReadPackedInt32();
                string scene = reader.ReadString();

                ClientData client = Utils.GetClientById(clientId);

                if (client == null)
                {
                    Logger.Warn($"Received SceneChangeFlag for unknown client {clientId}.", "GameDataHandlerPatch");
                    __result = EmptyCoroutine().WrapToIl2Cpp();
                    return false;
                }

                if (string.IsNullOrWhiteSpace(scene))
                {
                    Logger.Warn($"Client {client.PlayerName} ({client.Id}) tried to send SceneChangeFlag with null scene.", "GameDataHandlerPatch");
                    EAC.WarnHost();
                    __result = EmptyCoroutine().WrapToIl2Cpp();
                    return false;
                }

                if (scene.ToLower() == "tutorial")
                {
                    Logger.Warn($"Client {client.PlayerName} ({client.Id}) tried to send SceneChangeFlag to Tutorial.", "GameDataHandlerPatch");
                    EAC.WarnHost(100);

                    if (GameStates.IsOnlineGame && AmongUsClient.Instance.AmHost) Utils.ErrorEnd("SceneChange Tutorial Hack");

                    __result = EmptyCoroutine().WrapToIl2Cpp();
                    return false;
                }

                if (GameStates.IsInGame)
                {
                    Logger.Warn($"Client {client.PlayerName} ({client.Id}) tried to send SceneChangeFlag during mid of game.", "GameDataHandlerPatch");
                    __result = EmptyCoroutine().WrapToIl2Cpp();
                    return false;
                }

                break;
            }

            case GameDataTag.ReadyFlag:
            {
                int clientId = reader.ReadPackedInt32();
                ClientData client = Utils.GetClientById(clientId);

                if (client == null)
                {
                    Logger.Warn($"Received ReadyFlag for unknown client {clientId}.", "GameDataHandlerPatch");
                    EAC.WarnHost();
                    __result = EmptyCoroutine().WrapToIl2Cpp();
                    return false;
                }

                if (AmongUsClient.Instance.AmHost)
                {
                    if (!StartGameHostPatchEAC.IsStartingAsHost)
                    {
                        Logger.Warn($"Received ReadyFlag while game is started from {clientId}.", "GameDataHandlerPatch");
                        EAC.WarnHost();
                        __result = EmptyCoroutine().WrapToIl2Cpp();
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
        if (LobbyBehaviour.Instance != null) IsStartingAsHost = true;
    }

    public static void Postfix()
    {
        if (ShipStatus.Instance != null) IsStartingAsHost = false;
    }
}

//[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
internal static class CheckInvalidMovementPatch
{
    private static readonly Dictionary<byte, long> LastCheck = [];
    public static readonly Dictionary<byte, Vector2> LastPosition = [];
    public static readonly HashSet<byte> ExemptedPlayers = [];

    public static void Postfix(PlayerControl __instance)
    {
        if (!AmongUsClient.Instance.AmHost || !GameStates.IsInTask || ExileController.Instance || !Options.EnableMovementChecking.GetBool() || Main.HasJustStarted || !Main.IntroDestroyed || MeetingStates.FirstMeeting || Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod) >= 1.9f || AmongUsClient.Instance.Ping >= 300 || Options.CurrentGameMode == CustomGameMode.NaturalDisasters || Utils.GetRegionName() is not ("EU" or "NA" or "AS") || !__instance || __instance.PlayerId >= 254 || !__instance.IsAlive() || __instance.inVent) return;

        Vector2 pos = __instance.Pos();
        long now = Utils.TimeStamp;

        if (!LastPosition.TryGetValue(__instance.PlayerId, out Vector2 lastPosition))
        {
            SetCurrentData();
            return;
        }

        if (LastCheck.TryGetValue(__instance.PlayerId, out long lastCheck) && lastCheck == now) return;

        SetCurrentData();

        if (!FastVector2.DistanceWithinRange(lastPosition, pos, 10f) && PhysicsHelpers.AnythingBetween(__instance.Collider, lastPosition, pos, Constants.ShipOnlyMask, false))
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