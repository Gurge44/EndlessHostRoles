using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using Hazel;
using UnityEngine;

namespace EHR;

public static class NameNotifyManager
{
    public static Dictionary<byte, Dictionary<string, long>> Notifies = [];
    public static void Reset() => Notifies = [];

    public static void Notify(this PlayerControl pc, string text, float time = 6f, bool log = true)
    {
        if (!AmongUsClient.Instance.AmHost || pc == null) return;
        if (!GameStates.IsInTask) return;
        if (!text.Contains("<color=") && !text.Contains("</color>")) text = Utils.ColorString(Color.white, text);
        if (!text.Contains("<size=")) text = $"<size=1.9>{text}</size>";
        var expireTS = Utils.TimeStamp + (long)time;
        if (!Notifies.TryGetValue(pc.PlayerId, out var notifies)) Notifies[pc.PlayerId] = new() { { text, expireTS } };
        else notifies[text] = expireTS;
        SendRPC(pc.PlayerId, text, expireTS);
        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        if (log) Logger.Info($"New name notify for {pc.GetNameWithRole().RemoveHtmlTags()}: {text} ({time}s)", "Name Notify");
    }

    public static void OnFixedUpdate(PlayerControl player)
    {
        if (!GameStates.IsInTask)
        {
            Reset();
            return;
        }

        bool removed = false;

        if (Notifies.TryGetValue(player.PlayerId, out var notifies))
        {
            foreach (var notify in notifies.ToArray())
            {
                if (notify.Value <= Utils.TimeStamp)
                {
                    notifies.Remove(notify.Key);
                    removed = true;
                }
            }
        }

        if (removed) Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player);
    }

    public static bool GetNameNotify(PlayerControl player, out string name)
    {
        name = string.Empty;
        if (!Notifies.TryGetValue(player.PlayerId, out var notifies)) return false;
        name = string.Join('\n', notifies.OrderBy(x => x.Value).Select(x => x.Key));
        return true;
    }

    private static void SendRPC(byte playerId, string text, long expireTS) // Only sent when adding a new notification
    {
        if (!AmongUsClient.Instance.AmHost) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncNameNotify, SendOption.Reliable);
        writer.Write(playerId);
        writer.Write(text);
        writer.Write(expireTS.ToString());
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        if (AmongUsClient.Instance.AmHost) return;
        byte playerId = reader.ReadByte();
        string text = reader.ReadString();
        long expireTS = long.Parse(reader.ReadString());
        if (!Notifies.TryGetValue(playerId, out var notifies)) Notifies[playerId] = new() { { text, expireTS } };
        else notifies[text] = expireTS;
        Logger.Info($"New name notify for {Main.AllPlayerNames[playerId]}: {text} ({expireTS - Utils.TimeStamp}s)", "Name Notify");
    }
}