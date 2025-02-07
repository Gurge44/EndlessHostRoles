using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Hazel;

namespace EHR.Modules;

public static class CustomSoundsManager
{
    private static readonly string SOUNDS_PATH = @$"{Environment.CurrentDirectory.Replace(@"\", "/")}/BepInEx/resources/";

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

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.PlayCustomSound, HazelExtensions.SendOption, pc.GetClientId());
        writer.Write(sound);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void RPCPlayCustomSoundAll(string sound)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.PlayCustomSound, HazelExtensions.SendOption);
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
        if (!Constants.ShouldPlaySfx() || !Main.EnableCustomSoundEffect.Value) return;

        string path = SOUNDS_PATH + sound + ".wav";
        if (!Directory.Exists(SOUNDS_PATH)) Directory.CreateDirectory(SOUNDS_PATH);

        DirectoryInfo folder = new(SOUNDS_PATH);
        if ((folder.Attributes & FileAttributes.Hidden) != FileAttributes.Hidden) folder.Attributes = FileAttributes.Hidden;

        if (!File.Exists(path))
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("EHR.Resources.Sounds." + sound + ".wav");

            if (stream == null)
            {
                Logger.Warn($"声音文件缺失：{sound}", "CustomSounds");
                return;
            }

            FileStream fs = File.Create(path);
            stream.CopyTo(fs);
            fs.Close();
        }

        StartPlay(path);
        Logger.Msg($"Playing sound：{sound}", "CustomSounds");
    }

    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    private static extern bool PlaySound(string Filename, int Mod, int Flags);

    public static void StartPlay(string path)
    {
        PlaySound($"{path}", 0, 1);
        // The third parameter, replace 1 with 9, and play continuously
    }
}