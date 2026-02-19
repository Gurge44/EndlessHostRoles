using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using AmongUs.GameOptions;
using Hazel;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using InnerNet;
using UnityEngine;

namespace EHR.Modules;

public abstract class GameOptionsSender
{
    protected abstract bool IsDirty { get; set; }

    protected virtual int TargetClientId => -1;

    private Il2CppStructArray<byte> BuildOptionArray()
    {
        IGameOptions opt = BuildGameOptions();

        // option => byte[]
        MessageWriter writer = MessageWriter.Get();
        writer.Write(opt.Version);
        writer.StartMessage(0);
        writer.Write((byte)opt.GameMode);

        if (opt.TryCast(out NormalGameOptionsV10 normalOpt))
            NormalGameOptionsV10.Serialize(writer, normalOpt);
        else if (opt.TryCast(out HideNSeekGameOptionsV10 hnsOpt))
            HideNSeekGameOptionsV10.Serialize(writer, hnsOpt);
        else
            Logger.Error("Option cast failed", ToString());

        writer.EndMessage();

        Il2CppStructArray<byte> optionArray = writer.ToByteArray(false);
        writer.Recycle();
        return optionArray;
    }

    protected virtual void SendGameOptions()
    {
        Il2CppStructArray<byte> optionArray = BuildOptionArray();
        SendOptionsArray(optionArray);
    }

    protected virtual IEnumerator SendGameOptionsAsync()
    {
        Il2CppStructArray<byte> optionArray = BuildOptionArray();
        yield return SendOptionsArrayAsync(optionArray);
    }

    private void SendOptionsArray(Il2CppStructArray<byte> optionArray)
    {
        int count = GameManager.Instance.LogicComponents.Count;

        for (byte i = 0; i < count; i++)
        {
            Il2CppSystem.Object logicComponent = GameManager.Instance.LogicComponents[i];
            if (logicComponent.TryCast<LogicOptions>(out _)) SendOptionsArray(optionArray, i, TargetClientId);
        }
    }

    private IEnumerator SendOptionsArrayAsync(Il2CppStructArray<byte> optionArray)
    {
        int count = GameManager.Instance.LogicComponents.Count;

        for (byte i = 0; i < count; i++)
        {
            Il2CppSystem.Object logicComponent = GameManager.Instance.LogicComponents[i];
            if (logicComponent.TryCast<LogicOptions>(out _)) SendOptionsArray(optionArray, i, TargetClientId);
            yield return WaitFrameIfNecessary();
        }
    }

    private static void SendOptionsArray(Il2CppStructArray<byte> optionArray, byte logicOptionsIndex, int targetClientId)
    {
        try
        {
            MessageWriter writer = MessageWriter.Get(SendOption.Reliable);

            writer.StartMessage(targetClientId == -1 ? Tags.GameData : Tags.GameDataTo);
            {
                writer.Write(AmongUsClient.Instance.GameId);
                if (targetClientId != -1) writer.WritePacked(targetClientId);

                writer.StartMessage(1);
                {
                    writer.WritePacked(GameManager.Instance.NetId);
                    writer.StartMessage(logicOptionsIndex);
                    {
                        writer.WriteBytesAndSize(optionArray);
                    }
                    writer.EndMessage();
                }
                writer.EndMessage();
            }

            writer.EndMessage();

            AmongUsClient.Instance.SendOrDisconnect(writer);
            writer.Recycle();
        }
        catch (Exception ex) { Logger.Fatal(ex.ToString(), "GameOptionsSender.SendOptionsArray"); }
    }

    public abstract IGameOptions BuildGameOptions();

    protected virtual bool AmValid()
    {
        return true;
    }

    #region Static

    public static readonly List<GameOptionsSender> AllSenders = [new NormalGameOptionsSender()];

    public static IEnumerator SendDirtyGameOptionsContinuously()
    {
        try
        {
            while (GameStates.InGame || GameStates.IsLobby)
            {
                for (var index = 0; index < AllSenders.Count; index++)
                {
                    yield return WaitFrameIfNecessary();
                    
                    if (index >= AllSenders.Count) break;
                    GameOptionsSender sender = AllSenders[index];

                    if (sender == null || !sender.AmValid())
                    {
                        AllSenders.RemoveAt(index);
                        index--;
                        continue;
                    }

                    if (sender.IsDirty)
                        yield return sender.SendGameOptionsAsync();

                    sender.IsDirty = false;
                }

                ForceWaitFrame = true;
                yield return WaitFrameIfNecessary();
            }
        }
        finally
        {
            ActiveCoroutine = null;
        }
    }

    protected static IEnumerator WaitFrameIfNecessary()
    {
        if (ForceWaitFrame || Stopwatch.ElapsedMilliseconds >= FrameBudget)
        {
            ForceWaitFrame = false;
            Stopwatch.Reset();
            yield return null;
            Stopwatch.Start();
        }
    }

    public static Coroutine ActiveCoroutine;
    private static readonly Stopwatch Stopwatch = new();
    private const int FrameBudget = 4; // in milliseconds
    protected static bool ForceWaitFrame;

    #endregion
}