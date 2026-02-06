using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Hazel;

namespace EHR.Modules;

public static class CustomSoundsManager
{
    private static readonly string SoundsPath = $"{Environment.CurrentDirectory.Replace(@"\", "/")}/BepInEx/resources/";

    public static void RPCPlayCustomSound(this PlayerControl pc, string sound, bool force = false)
    {
        if (!OperatingSystem.IsWindows()) return;

        if (!force)
        {
            if (!AmongUsClient.Instance.AmHost || !pc.IsModdedClient())
                return;
        }

        if (pc == null || PlayerControl.LocalPlayer.PlayerId == pc.PlayerId)
        {
            Play(sound);
            return;
        }

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.PlayCustomSound, SendOption.Reliable, pc.OwnerId);
        writer.Write(sound);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void RPCPlayCustomSoundAll(string sound)
    {
        if (!AmongUsClient.Instance.AmHost || !OperatingSystem.IsWindows()) return;

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.PlayCustomSound, SendOption.Reliable);
        writer.Write(sound);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
        Play(sound);
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        Play(reader.ReadString());
    }

    public static void Play(string sound)
    {
        if (!Constants.ShouldPlaySfx() || !Main.EnableCustomSoundEffect.Value || !OperatingSystem.IsWindows()) return;

        string path = SoundsPath + sound + ".wav";
        if (!Directory.Exists(SoundsPath)) Directory.CreateDirectory(SoundsPath);

        DirectoryInfo folder = new(SoundsPath);
        if ((folder.Attributes & FileAttributes.Hidden) != FileAttributes.Hidden) folder.Attributes = FileAttributes.Hidden;

        if (!File.Exists(path))
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("EHR.Resources.Sounds." + sound + ".wav");

            if (stream == null)
            {
                Logger.Warn($"Could not find sound: {sound}", "CustomSounds");
                return;
            }

            FileStream fs = File.Create(path);
            stream.CopyTo(fs);
            fs.Close();
        }

        StartPlay(path);
        Logger.Msg($"Playing sound: {sound}", "CustomSounds");
    }

    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    private static extern bool PlaySound(string Filename, int Mod, int Flags);

    public static void StartPlay(string path)
    {
        PlaySound($"{path}", 0, 1);
        // The third parameter, replace 1 with 9, and play continuously
    }
}