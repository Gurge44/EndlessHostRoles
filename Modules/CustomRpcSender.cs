using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Neutral;
using Hazel;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using InnerNet;
using UnityEngine;

namespace EHR;

public class CustomRpcSender
{
    public enum State
    {
        BeforeInit = 0, // Cannot do anything before initialization
        Ready, // Ready to send - StartMessage and SendMessage can be executed
        InRootMessage, // State between StartMessage and EndMessage - StartRpc and EndMessage can be executed
        InRpc, // State between StartRpc and EndRpc - Write and EndRpc can be executed
        Finished // Nothing can be done after sending
    }

    private readonly List<MessageWriter> doneStreams = [];
    private readonly bool isUnsafe;

    private readonly bool log;
    public readonly string name;

    private readonly OnSendDelegateType onSendDelegate;
    public readonly SendOption sendOption;

    // 0~: targetClientId (GameDataTo)
    // -1: All players (GameData)
    // -2: Not set
    private int currentRpcTarget;

    private State currentState = State.BeforeInit;
    public MessageWriter stream;

    private CustomRpcSender() { }

    private CustomRpcSender(string name, SendOption sendOption, bool isUnsafe, bool log)
    {
        stream = MessageWriter.Get(sendOption);

        this.name = name;
        this.isUnsafe = isUnsafe;
        this.sendOption = sendOption;
        this.log = log;
        currentRpcTarget = -2;
        onSendDelegate = () => { };

        currentState = State.Ready;
        if (log) Logger.Info($"\"{name}\" is ready", "CustomRpcSender");
    }

    public State CurrentState
    {
        get => currentState;
        set
        {
            if (isUnsafe)
                currentState = value;
            else
                Logger.Warn("CurrentState can only be overwritten when isUnsafe is true", "CustomRpcSender");
        }
    }

    public static CustomRpcSender Create(string name = "No Name Sender", SendOption sendOption = SendOption.None, bool isUnsafe = false, bool log = true)
    {
        return new(name, sendOption, isUnsafe, log);
    }

    public CustomRpcSender AutoStartRpc(
        uint targetNetId,
        RpcCalls rpcCall,
        int targetClientId = -1,
        [CallerFilePath] string callerPath = "",
        [CallerLineNumber] int callerLine = 0)
    {
        // ReSharper disable ExplicitCallerInfoArgument
        return AutoStartRpc(targetNetId, (byte)rpcCall, targetClientId, callerPath, callerLine);
        // ReSharper restore ExplicitCallerInfoArgument
    }

    public CustomRpcSender AutoStartRpc(
        uint targetNetId,
        byte callId,
        int targetClientId = -1,
        [CallerFilePath] string callerPath = "",
        [CallerLineNumber] int callerLine = 0)
    {
        if (targetClientId == -2) targetClientId = -1;

        if (currentState is not State.Ready and not State.InRootMessage)
        {
            var errorMsg = $"Tried to start RPC automatically, but State is not Ready or InRootMessage (in: \"{name}\", state: {currentState}) (called from {callerPath}:{callerLine})";

            if (isUnsafe)
                Logger.Warn(errorMsg, "CustomRpcSender.Warn");
            else
                throw new InvalidOperationException(errorMsg);
        }

        if (currentRpcTarget != targetClientId)
        {
            // StartMessage processing
            if (currentState == State.InRootMessage)
                EndMessage(startNew: true);

            StartMessage(targetClientId);
        }

        StartRpc(targetNetId, callId);

        return this;
    }

    public void SendMessage(bool dispose = false)
    {
        if (currentState == State.InRootMessage) EndMessage();

        if (currentState != State.Ready && !dispose)
        {
            var errorMsg = $"Tried to send RPC but State is not Ready (in: \"{name}\", state: {currentState})";

            if (isUnsafe)
                Logger.Warn(errorMsg, "CustomRpcSender.Warn");
            else
                throw new InvalidOperationException(errorMsg);
        }

        if (stream.Length >= 1500 && sendOption == SendOption.Reliable && !dispose) Logger.Warn($"Large reliable packet \"{name}\" is sending ({stream.Length} bytes)", "CustomRpcSender");
        else if (log || stream.Length > 3) Logger.Info($"\"{name}\" is finished (Length: {stream.Length}, dispose: {dispose}, sendOption: {sendOption})", "CustomRpcSender");

        if (!dispose)
        {
            if (doneStreams.Count > 0)
            {
                var sb = new StringBuilder(" + Lengths: ");

                doneStreams.ForEach(x =>
                {
                    if (x.Length >= 1500 && sendOption == SendOption.Reliable) Logger.Warn($"Large reliable packet \"{name}\" is sending ({x.Length} bytes)", "CustomRpcSender");
                    else if (log || x.Length > 3) sb.Append($" | {x.Length}");

                    AmongUsClient.Instance.SendOrDisconnect(x);
                    x.Recycle();
                });

                Logger.Info(sb.ToString(), "CustomRpcSender");

                doneStreams.Clear();
            }

            AmongUsClient.Instance.SendOrDisconnect(stream);
            onSendDelegate();
        }

        currentState = State.Finished;
        stream.Recycle();
    }

    private CustomRpcSender Write(Action<MessageWriter> action)
    {
        if (currentState != State.InRpc)
        {
            var errorMsg = $"Tried to write RPC, but State is not Write (in: \"{name}\")";

            if (isUnsafe)
                Logger.Warn(errorMsg, "CustomRpcSender.Warn");
            else
                throw new InvalidOperationException(errorMsg);
        }

        action(stream);

        return this;
    }

    private delegate void OnSendDelegateType();

    #region Start/End Message

    public CustomRpcSender StartMessage(int targetClientId = -1)
    {
        if (currentState != State.Ready)
        {
            var errorMsg = $"Tried to start Message but State is not Ready (in: \"{name}\")";

            if (isUnsafe)
                Logger.Warn(errorMsg, "CustomRpcSender.Warn");
            else
                throw new InvalidOperationException(errorMsg);
        }

        if (stream.Length > 500)
        {
            doneStreams.Add(stream);
            stream = MessageWriter.Get(sendOption);
        }

        if (targetClientId < 0)
        {
            // RPC for everyone
            stream.StartMessage(5);
            stream.Write(AmongUsClient.Instance.GameId);
        }
        else
        {
            // RPC (Desync) to a specific client
            stream.StartMessage(6);
            stream.Write(AmongUsClient.Instance.GameId);
            stream.WritePacked(targetClientId);
        }

        currentRpcTarget = targetClientId;
        currentState = State.InRootMessage;
        return this;
    }

    public CustomRpcSender EndMessage(bool startNew = false)
    {
        if (currentState != State.InRootMessage)
        {
            var errorMsg = $"Tried to exit Message but State is not InRootMessage (in: \"{name}\")";

            if (isUnsafe)
                Logger.Warn(errorMsg, "CustomRpcSender.Warn");
            else
                throw new InvalidOperationException(errorMsg);
        }

        stream.EndMessage();

        if (startNew)
        {
            doneStreams.Add(stream);
            stream = MessageWriter.Get(sendOption);
        }

        currentRpcTarget = -2;
        currentState = State.Ready;
        return this;
    }

    #endregion

    #region Start/End Rpc

    public CustomRpcSender StartRpc(uint targetNetId, RpcCalls rpcCall)
    {
        return StartRpc(targetNetId, (byte)rpcCall);
    }

    public CustomRpcSender StartRpc(
        uint targetNetId,
        byte callId)
    {
        if (currentState != State.InRootMessage)
        {
            var errorMsg = $"Tried to start RPC but State is not InRootMessage (in: \"{name}\")";

            if (isUnsafe)
                Logger.Warn(errorMsg, "CustomRpcSender.Warn");
            else
                throw new InvalidOperationException(errorMsg);
        }

        stream.StartMessage(2);
        stream.WritePacked(targetNetId);
        stream.Write(callId);

        currentState = State.InRpc;
        return this;
    }

    public CustomRpcSender EndRpc()
    {
        if (currentState != State.InRpc)
        {
            var errorMsg = $"Tried to terminate RPC but State is not InRpc (in: \"{name}\")";

            if (isUnsafe)
                Logger.Warn(errorMsg, "CustomRpcSender.Warn");
            else
                throw new InvalidOperationException(errorMsg);
        }

        stream.EndMessage();
        currentState = State.InRootMessage;
        return this;
    }

    #endregion

    // Write

    #region PublicWriteMethods

    public CustomRpcSender Write(float val)
    {
        return Write(w => w.Write(val));
    }

    public CustomRpcSender Write(string val)
    {
        return Write(w => w.Write(val));
    }

    public CustomRpcSender Write(ulong val)
    {
        return Write(w => w.Write(val));
    }

    public CustomRpcSender Write(int val)
    {
        return Write(w => w.Write(val));
    }

    public CustomRpcSender Write(uint val)
    {
        return Write(w => w.Write(val));
    }

    public CustomRpcSender Write(ushort val)
    {
        return Write(w => w.Write(val));
    }

    public CustomRpcSender Write(byte val)
    {
        return Write(w => w.Write(val));
    }

    public CustomRpcSender Write(sbyte val)
    {
        return Write(w => w.Write(val));
    }

    public CustomRpcSender Write(bool val)
    {
        return Write(w => w.Write(val));
    }

    public CustomRpcSender Write(Il2CppStructArray<byte> bytes)
    {
        return Write(w => w.Write(bytes));
    }

    public CustomRpcSender Write(Il2CppStructArray<byte> bytes, int offset, int length)
    {
        return Write(w => w.Write(bytes, offset, length));
    }

    public CustomRpcSender WriteBytesAndSize(Il2CppStructArray<byte> bytes)
    {
        return Write(w => w.WriteBytesAndSize(bytes));
    }

    public CustomRpcSender WritePacked(int val)
    {
        return Write(w => w.WritePacked(val));
    }

    public CustomRpcSender WritePacked(uint val)
    {
        return Write(w => w.WritePacked(val));
    }

    public CustomRpcSender WriteNetObject(InnerNetObject obj)
    {
        return Write(w => w.WriteNetObject(obj));
    }

    public CustomRpcSender WriteVector2(Vector2 vector2)
    {
        return Write(w => NetHelpers.WriteVector2(vector2, w));
    }

    #endregion
}

public static class CustomRpcSenderExtensions
{
    public static bool RpcSetRole(this CustomRpcSender sender, PlayerControl player, RoleTypes role, int targetClientId = -1, bool noRpcForSelf = true, bool changeRoleMap = false)
    {
        if (AmongUsClient.Instance.ClientId == targetClientId && noRpcForSelf)
        {
            player.SetRole(role);
            return false;
        }

        sender.AutoStartRpc(player.NetId, RpcCalls.SetRole, targetClientId)
            .Write((ushort)role)
            .Write(true)
            .EndRpc();

        if (changeRoleMap)
        {
            try
            {
                if (targetClientId != -1) ChangeRoleMapForClient(Utils.GetClientById(targetClientId).Character.PlayerId);
                else Main.PlayerStates.Keys.Do(ChangeRoleMapForClient);
            }
            catch (Exception e) { Utils.ThrowException(e); }

            void ChangeRoleMapForClient(byte id)
            {
                (byte, byte) key = (id, player.PlayerId);

                if (StartGameHostPatch.RpcSetRoleReplacer.RoleMap.TryGetValue(key, out (RoleTypes RoleType, CustomRoles CustomRole) pair))
                {
                    pair.RoleType = role;
                    StartGameHostPatch.RpcSetRoleReplacer.RoleMap[key] = pair;
                }
            }
        }

        return true;
    }

    // From TOH: https://github.com/tukasa0001/TownOfHost
    public static void RpcSetName(this CustomRpcSender sender, PlayerControl player, string name, PlayerControl seer = null)
    {
        bool seerIsNull = seer == null;
        int targetClientId = seerIsNull ? -1 : seer.OwnerId;

        name = name.Replace("color=", string.Empty);

        switch (seerIsNull)
        {
            case true when Main.LastNotifyNames.Where(x => x.Key.Item1 == player.PlayerId).All(x => x.Value == name):
            case false when Main.LastNotifyNames[(player.PlayerId, seer.PlayerId)] == name:
                return;
            case true:
                Main.AllPlayerControls.Do(x => Main.LastNotifyNames[(player.PlayerId, x.PlayerId)] = name);
                break;
            default:
                Main.LastNotifyNames[(player.PlayerId, seer.PlayerId)] = name;
                break;
        }

        sender.AutoStartRpc(player.NetId, RpcCalls.SetName, targetClientId)
            .Write(player.Data.NetId)
            .Write(name)
            .Write(false)
            .EndRpc();
    }

    public static bool RpcGuardAndKill(this CustomRpcSender sender, PlayerControl killer, PlayerControl target = null, bool forObserver = false, bool fromSetKCD = false)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            StackFrame caller = new(1, false);
            MethodBase callerMethod = caller.GetMethod();
            string callerMethodName = callerMethod?.Name;
            string callerClassName = callerMethod?.DeclaringType?.FullName;
            Logger.Warn($"Modded non-host client activated RpcGuardAndKill from {callerClassName}.{callerMethodName}", "RpcGuardAndKill");
            return false;
        }

        if (target == null) target = killer;

        var returnValue = false;

        // Check Observer
        if (!forObserver && !MeetingStates.FirstMeeting)
        {
            foreach (PlayerControl x in Main.AllPlayerControls)
            {
                if (x.Is(CustomRoles.Observer) && killer.PlayerId != x.PlayerId && sender.RpcGuardAndKill(x, target, true))
                    returnValue = true;
            }
        }

        // Host
        if (killer.AmOwner) killer.MurderPlayer(target, MurderResultFlags.FailedProtected);

        // Other Clients
        if (!killer.IsHost())
        {
            sender.AutoStartRpc(killer.NetId, RpcCalls.MurderPlayer, killer.OwnerId);
            sender.WriteNetObject(target);
            sender.Write((int)MurderResultFlags.FailedProtected);
            sender.EndRpc();

            if (!MeetingStates.FirstMeeting && !AntiBlackout.SkipTasks && !ExileController.Instance && GameStates.IsInTask && killer.IsBeginner() && Main.GotShieldAnimationInfoThisGame.Add(killer.PlayerId))
                sender.Notify(killer, Translator.GetString("PleaseStopBeingDumb"), 10f);

            returnValue = true;
        }

        if (!fromSetKCD) killer.AddKillTimerToDict(true);

        return returnValue;
    }

    public static bool SetKillCooldown(this CustomRpcSender sender, PlayerControl player, float time = -1f, PlayerControl target = null, bool forceAnime = false)
    {
        if (player == null) return false;

        Logger.Info($"{player.GetNameWithRole()}'s KCD set to {(Math.Abs(time - -1f) < 0.5f ? Main.AllPlayerKillCooldown[player.PlayerId] : time)}s", "SetKCD");

        if (player.GetCustomRole().UsesPetInsteadOfKill())
        {
            if (Math.Abs(time - -1f) < 0.5f)
                player.AddKCDAsAbilityCD();
            else
                player.AddAbilityCD((int)Math.Round(time));

            if (player.GetCustomRole() is not CustomRoles.Necromancer and not CustomRoles.Deathknight and not CustomRoles.Renegade and not CustomRoles.Sidekick) return false;
        }

        if (!player.CanUseKillButton() && !AntiBlackout.SkipTasks && !IntroCutsceneDestroyPatch.PreventKill) return false;

        player.AddKillTimerToDict(cd: time);
        if (target == null) target = player;

        if (time >= 0f)
            Main.AllPlayerKillCooldown[player.PlayerId] = time * 2;
        else
            Main.AllPlayerKillCooldown[player.PlayerId] *= 2;

        var returnValue = false;

        if (player.Is(CustomRoles.Glitch) && Main.PlayerStates[player.PlayerId].Role is Glitch gc)
        {
            gc.LastKill = Utils.TimeStamp + ((int)(time / 2) - Glitch.KillCooldown.GetInt());
            gc.KCDTimer = (int)(time / 2);
        }
        else if (forceAnime || !player.IsModdedClient() || !Options.DisableShieldAnimations.GetBool())
        {
            returnValue |= sender.SyncSettings(player);
            returnValue |= sender.RpcGuardAndKill(player, target, fromSetKCD: true);
        }
        else
        {
            time = Main.AllPlayerKillCooldown[player.PlayerId] / 2;

            if (player.AmOwner)
                PlayerControl.LocalPlayer.SetKillTimer(time);
            else
            {
                sender.AutoStartRpc(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetKillTimer, player.OwnerId);
                sender.Write(time);
                sender.EndRpc();

                returnValue = true;
            }

            foreach (PlayerControl x in Main.AllPlayerControls)
            {
                if (x.Is(CustomRoles.Observer) && target.PlayerId != x.PlayerId && sender.RpcGuardAndKill(x, target, true, true))
                    returnValue = true;
            }
        }

        if (player.GetCustomRole() is not CustomRoles.Inhibitor and not CustomRoles.Saboteur)
        {
            player.ResetKillCooldown(sync: false);
            LateTask.New(player.SyncSettings, 1f, log: false);
        }

        return returnValue;
    }

    public static bool RpcResetAbilityCooldown(this CustomRpcSender sender, PlayerControl target)
    {
        if (!AmongUsClient.Instance.AmHost) return false;

        Logger.Info($"Reset Ability Cooldown for {target.name} (ID: {target.PlayerId})", "RpcResetAbilityCooldown");

        if (target.Is(CustomRoles.Glitch) && Main.PlayerStates[target.PlayerId].Role is Glitch gc)
        {
            gc.LastHack = Utils.TimeStamp;
            gc.HackCDTimer = 10;

            return false;
        }

        if (target.AmOwner)
        {
            // If target is host
            target.Data.Role.SetCooldown();
            return false;
        }

        if (target.IsModdedClient())
        {
            sender.AutoStartRpc(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.ResetAbilityCooldown, target.OwnerId);
            sender.EndRpc();
            return true;
        }

        sender.AutoStartRpc(target.NetId, RpcCalls.ProtectPlayer, target.OwnerId);
        sender.WriteNetObject(target);
        sender.Write(0);
        sender.EndRpc();

        return true;
    }

    public static void RpcDesyncRepairSystem(this CustomRpcSender sender, PlayerControl target, SystemTypes systemType, int amount)
    {
        sender.AutoStartRpc(ShipStatus.Instance.NetId, RpcCalls.UpdateSystem, target.OwnerId);
        sender.Write((byte)systemType);
        sender.WriteNetObject(target);
        sender.Write((byte)amount);
        sender.EndRpc();
    }

    public static bool Notify(this CustomRpcSender sender, PlayerControl pc, string text, float time = 6f, bool overrideAll = false, bool log = true, bool setName = true)
    {
        if (!AmongUsClient.Instance.AmHost || pc == null) return false;
        if (!GameStates.IsInTask) return false;
        if (!text.Contains("<color=") && !text.Contains("</color>")) text = Utils.ColorString(Color.white, text);
        if (!text.Contains("<size=")) text = $"<size=1.9>{text}</size>";

        long expireTS = Utils.TimeStamp + (long)time;

        if (overrideAll || !NameNotifyManager.Notifies.TryGetValue(pc.PlayerId, out Dictionary<string, long> notifies))
            NameNotifyManager.Notifies[pc.PlayerId] = new() { { text, expireTS } };
        else
            notifies[text] = expireTS;

        bool returnValue = pc.IsNonHostModdedClient();

        if (returnValue) NameNotifyManager.SendRPC(sender, pc.PlayerId, text, expireTS, overrideAll);
        if (setName) returnValue |= Utils.WriteSetNameRpcsToSender(ref sender, false, false, false, false, false, false, pc, [pc], [], out bool senderWasCleared) && !senderWasCleared;
        if (log) Logger.Info($"New name notify for {pc.GetNameWithRole().RemoveHtmlTags()}: {text} ({time}s)", "Name Notify");

        return returnValue;
    }

    public static bool TP(this CustomRpcSender sender, PlayerControl pc, Vector2 location, bool noCheckState = false, bool log = true)
    {
        if (!AmongUsClient.Instance.AmHost) return false;
        
        CustomNetworkTransform nt = pc.NetTransform;

        if (!noCheckState)
        {
            if (pc.Is(CustomRoles.AntiTP)) return false;

            if (pc.inVent || pc.inMovingPlat || pc.onLadder || !pc.IsAlive() || pc.MyPhysics.Animations.IsPlayingAnyLadderAnimation() || pc.MyPhysics.Animations.IsPlayingEnterVentAnimation())
            {
                if (log) Logger.Warn($"Target ({pc.GetNameWithRole().RemoveHtmlTags()}) is in an un-teleportable state - Teleporting canceled", "TP");
                return false;
            }

            if (Vector2.Distance(pc.Pos(), location) < 0.5f)
            {
                if (log) Logger.Warn($"Target ({pc.GetNameWithRole().RemoveHtmlTags()}) is too close to the destination - Teleporting canceled", "TP");
                return false;
            }
        }

        
        nt.SnapTo(location, (ushort)(nt.lastSequenceId + 328));
        nt.SetDirtyBit(uint.MaxValue);

        var newSid = (ushort)(nt.lastSequenceId + 8);

        sender.AutoStartRpc(nt.NetId, RpcCalls.SnapTo);
        sender.WriteVector2(location);
        sender.Write(newSid);
        sender.EndRpc();

        if (log) Logger.Info($"{pc.GetNameWithRole().RemoveHtmlTags()} => {location}", "TP");

        CheckInvalidMovementPatch.LastPosition[pc.PlayerId] = location;
        CheckInvalidMovementPatch.ExemptedPlayers.Add(pc.PlayerId);

        if (sender.sendOption == SendOption.Reliable) Utils.NumSnapToCallsThisRound++;
        return true;
    }

    public static bool NotifyRolesSpecific(this CustomRpcSender sender, PlayerControl seer, PlayerControl target, out CustomRpcSender newSender, out bool senderWasCleared)
    {
        senderWasCleared = false;
        newSender = sender;
        if (!AmongUsClient.Instance.AmHost || seer == null || seer.Data.Disconnected || (seer.IsModdedClient() && (seer.IsHost() || Options.CurrentGameMode == CustomGameMode.Standard)) || (!SetUpRoleTextPatch.IsInIntro && GameStates.IsLobby)) return false;
        var hasValue = Utils.WriteSetNameRpcsToSender(ref sender, false, false, false, false, false, false, seer, [seer], [target], out senderWasCleared) && !senderWasCleared;
        newSender = sender;
        return hasValue;
    }

    public static bool SyncSettings(this CustomRpcSender sender, PlayerControl player)
    {
        if (GameStates.CurrentServerType == GameStates.ServerType.Vanilla)
        {
            player.SyncSettings();
            return false;
        }

        var optionsender = GameOptionsSender.AllSenders.OfType<PlayerGameOptionsSender>().FirstOrDefault(x => x.player.PlayerId == player.PlayerId);
        if (optionsender == null) return false;

        var options = optionsender.BuildGameOptions();

        if (player.AmOwner)
        {
            foreach (GameLogicComponent com in GameManager.Instance.LogicComponents)
            {
                if (com.TryCast(out LogicOptions lo))
                    lo.SetGameOptions(options);
            }

            GameOptionsManager.Instance.CurrentGameOptions = options;
            return false;
        }

        var logicOptions = GameManager.Instance.LogicOptions;
        var id = GameManager.Instance.LogicComponents.IndexOf(logicOptions);

        if (sender.CurrentState == CustomRpcSender.State.InRootMessage) sender.EndMessage();

        var writer = sender.stream;

        writer.StartMessage(6);
        {
            writer.Write(AmongUsClient.Instance.GameId);
            writer.WritePacked(player.OwnerId);
            writer.StartMessage(1);
            {
                writer.WritePacked(GameManager.Instance.NetId);
                writer.StartMessage((byte)id);
                {
                    writer.WriteBytesAndSize(logicOptions.gameOptionsFactory.ToBytes(options, AprilFoolsMode.IsAprilFoolsModeToggledOn));
                }
                writer.EndMessage();
            }
            writer.EndMessage();
        }
        writer.EndMessage();

        return true;
    }
}