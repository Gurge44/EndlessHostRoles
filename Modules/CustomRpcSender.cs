using System;
using System.Diagnostics.CodeAnalysis;
using AmongUs.GameOptions;
using Hazel;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using InnerNet;

namespace EHR;

public class CustomRpcSender
{
    public enum State
    {
        BeforeInit = 0, // Cannot do anything before initialization
        Ready, // Ready to send StartMessage and SendMessage can be executed
        InRootMessage, // State between StartMessage and EndMessage StartRpc and EndMessage can be executed
        InRpc, // State between StartRpc and EndRpc Write and EndRpc can be executed
        Finished // Nothing can be done after sending
    }

    private readonly bool isUnsafe;
    private readonly string name;

    private readonly OnSendDelegateType onSendDelegate;
    public readonly MessageWriter stream;

    // 0~: targetClientId (GameDataTo)
    // -1: All players (GameData)
    // -2: Not set
    private int currentRpcTarget;

    private State currentState = State.BeforeInit;

    private CustomRpcSender()
    {
    }

    public CustomRpcSender(string name, SendOption sendOption, bool isUnsafe)
    {
        stream = MessageWriter.Get(sendOption);

        this.name = name;
        this.isUnsafe = isUnsafe;
        currentRpcTarget = -2;
        onSendDelegate = () => Logger.Info($"{this.name}'s onSendDelegate =>", "CustomRpcSender");

        currentState = State.Ready;
        Logger.Info($"\"{name}\" is ready", "CustomRpcSender");
    }

    public State CurrentState
    {
        get { return currentState; }
        set
        {
            if (isUnsafe) currentState = value;
            else Logger.Warn("CurrentState can only be overwritten when isUnsafe is true", "CustomRpcSender");
        }
    }

    public static CustomRpcSender Create(string name = "No Name Sender", SendOption sendOption = SendOption.None, bool isUnsafe = false)
    {
        return new(name, sendOption, isUnsafe);
    }

    public CustomRpcSender AutoStartRpc(
        uint targetNetId,
        byte callId,
        int targetClientId = -1)
    {
        if (targetClientId == -2) targetClientId = -1;
        if (currentState is not State.Ready and not State.InRootMessage)
        {
            string errorMsg = $"Tried to start RPC automatically, but State is not Ready or InRootMessage (in: \"{name}\")";
            if (isUnsafe)
            {
                Logger.Warn(errorMsg, "CustomRpcSender.Warn");
            }
            else
            {
                throw new InvalidOperationException(errorMsg);
            }
        }

        if (currentRpcTarget != targetClientId)
        {
            // StartMessage processing
            if (currentState == State.InRootMessage) EndMessage();
            StartMessage(targetClientId);
        }

        StartRpc(targetNetId, callId);

        return this;
    }

    public void SendMessage()
    {
        if (currentState == State.InRootMessage) EndMessage();
        if (currentState != State.Ready)
        {
            string errorMsg = $"Tried to send RPC but State is not Ready (in: \"{name}\")";
            if (isUnsafe)
            {
                Logger.Warn(errorMsg, "CustomRpcSender.Warn");
            }
            else
            {
                throw new InvalidOperationException(errorMsg);
            }
        }

        AmongUsClient.Instance.SendOrDisconnect(stream);
        onSendDelegate();
        currentState = State.Finished;
        Logger.Info($"\"{name}\" is finished", "CustomRpcSender");
        stream.Recycle();
    }

    private CustomRpcSender Write(Action<MessageWriter> action)
    {
        if (currentState != State.InRpc)
        {
            string errorMsg = $"Tried to write RPC, but State is not Write (in: \"{name}\")";
            if (isUnsafe)
            {
                Logger.Warn(errorMsg, "CustomRpcSender.Warn");
            }
            else
            {
                throw new InvalidOperationException(errorMsg);
            }
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
            string errorMsg = $"Tried to start Message but State is not Ready (in: \"{name}\")";
            if (isUnsafe)
            {
                Logger.Warn(errorMsg, "CustomRpcSender.Warn");
            }
            else
            {
                throw new InvalidOperationException(errorMsg);
            }
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

    [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "<Pending>")]
    public CustomRpcSender EndMessage(int targetClientId = -1)
    {
        if (currentState != State.InRootMessage)
        {
            string errorMsg = $"Tried to exit Message but State is not InRootMessage (in: \"{name}\")";
            if (isUnsafe)
            {
                Logger.Warn(errorMsg, "CustomRpcSender.Warn");
            }
            else
            {
                throw new InvalidOperationException(errorMsg);
            }
        }

        stream.EndMessage();

        currentRpcTarget = -2;
        currentState = State.Ready;
        return this;
    }

    #endregion

    #region Start/End Rpc

    public CustomRpcSender StartRpc(uint targetNetId, RpcCalls rpcCall)
        => StartRpc(targetNetId, (byte)rpcCall);

    public CustomRpcSender StartRpc(
        uint targetNetId,
        byte callId)
    {
        if (currentState != State.InRootMessage)
        {
            string errorMsg = $"Tried to start RPC but State is not InRootMessage (in: \"{name}\")";
            if (isUnsafe)
            {
                Logger.Warn(errorMsg, "CustomRpcSender.Warn");
            }
            else
            {
                throw new InvalidOperationException(errorMsg);
            }
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
            string errorMsg = $"Tried to terminate RPC but State is not InRpc (in: \"{name}\")";
            if (isUnsafe)
            {
                Logger.Warn(errorMsg, "CustomRpcSender.Warn");
            }
            else
            {
                throw new InvalidOperationException(errorMsg);
            }
        }

        stream.EndMessage();
        currentState = State.InRootMessage;
        return this;
    }

    #endregion

    // Write

    #region PublicWriteMethods

    public CustomRpcSender Write(float val) => Write(w => w.Write(val));
    public CustomRpcSender Write(string val) => Write(w => w.Write(val));
    public CustomRpcSender Write(ulong val) => Write(w => w.Write(val));
    public CustomRpcSender Write(int val) => Write(w => w.Write(val));
    public CustomRpcSender Write(uint val) => Write(w => w.Write(val));
    public CustomRpcSender Write(ushort val) => Write(w => w.Write(val));
    public CustomRpcSender Write(byte val) => Write(w => w.Write(val));
    public CustomRpcSender Write(sbyte val) => Write(w => w.Write(val));
    public CustomRpcSender Write(bool val) => Write(w => w.Write(val));
    public CustomRpcSender Write(Il2CppStructArray<byte> bytes) => Write(w => w.Write(bytes));
    public CustomRpcSender Write(Il2CppStructArray<byte> bytes, int offset, int length) => Write(w => w.Write(bytes, offset, length));
    public CustomRpcSender WriteBytesAndSize(Il2CppStructArray<byte> bytes) => Write(w => w.WriteBytesAndSize(bytes));
    public CustomRpcSender WritePacked(int val) => Write(w => w.WritePacked(val));
    public CustomRpcSender WritePacked(uint val) => Write(w => w.WritePacked(val));
    public CustomRpcSender WriteNetObject(InnerNetObject obj) => Write(w => w.WriteNetObject(obj));

    #endregion
}

public static class CustomRpcSenderExtensions
{
    public static void RpcSetRole(this CustomRpcSender sender, PlayerControl player, RoleTypes role, int targetClientId = -1)
    {
        sender.AutoStartRpc(player.NetId, (byte)RpcCalls.SetRole, targetClientId)
            .Write((ushort)role)
            .EndRpc();
    }

    public static void RpcMurderPlayerV3(this CustomRpcSender sender, PlayerControl player, PlayerControl target, int targetClientId = -1)
    {
        sender.AutoStartRpc(player.NetId, (byte)RpcCalls.MurderPlayer, targetClientId)
            .WriteNetObject(target)
            .Write((byte)ExtendedPlayerControl.ResultFlags)
            .EndRpc();
    }
}