﻿using Hazel;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace EHR.Modules;

public static class CustomSoundsManager
{
    public static void RPCPlayCustomSound(this PlayerControl pc, string sound, bool force = false)
    {
        if (!force)
            if (!AmongUsClient.Instance.AmHost || !pc.IsModClient())
                return;
        if (pc == null || PlayerControl.LocalPlayer.PlayerId == pc.PlayerId)
        {
            Play(sound);
            return;
        }

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.PlayCustomSound, SendOption.Reliable, pc.GetClientId());
        writer.Write(sound);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void RPCPlayCustomSoundAll(string sound)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.PlayCustomSound, SendOption.Reliable);
        writer.Write(sound);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
        Play(sound);
    }

    public static void ReceiveRPC(MessageReader reader) => Play(reader.ReadString());


    private static readonly string SOUNDS_PATH = @$"{Environment.CurrentDirectory.Replace(@"\", "/")}/BepInEx/resources/";

    public static void Play(string sound)
    {
        if (!Constants.ShouldPlaySfx() || !Main.EnableCustomSoundEffect.Value) return;
        var path = SOUNDS_PATH + sound + ".wav";
        if (!Directory.Exists(SOUNDS_PATH)) Directory.CreateDirectory(SOUNDS_PATH);
        DirectoryInfo folder = new(SOUNDS_PATH);
        if ((folder.Attributes & FileAttributes.Hidden) != FileAttributes.Hidden)
            folder.Attributes = FileAttributes.Hidden;
        if (!File.Exists(path))
        {
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("EHR.Resources.Sounds." + sound + ".wav");
            if (stream == null)
            {
                Logger.Warn($"声音文件缺失：{sound}", "CustomSounds");
                return;
            }

            var fs = File.Create(path);
            stream.CopyTo(fs);
            fs.Close();
        }

        StartPlay(path);
        Logger.Msg($"Playing sound：{sound}", "CustomSounds");
    }

#pragma warning disable CA2101 // Specify marshaling for P/Invoke string arguments
    [DllImport("winmm.dll")]
#pragma warning restore CA2101 // Specify marshaling for P/Invoke string arguments
#pragma warning disable CA1401 // P/Invokes should not be visible
    public static extern bool PlaySound(string Filename, int Mod, int Flags);
#pragma warning restore CA1401 // P/Invokes should not be visible
    public static void StartPlay(string path) => PlaySound(@$"{path}", 0, 1); //第3个形参，把1换为9，连续播放
}