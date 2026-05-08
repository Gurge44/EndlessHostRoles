using EHR.Modules;
using Hazel;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace EHR;

public static class NameNotifyManager
{
    public static readonly Dictionary<byte, Dictionary<string, long>> Notifies = [];
    private static readonly List<string> ToRemove = [];
    private static readonly List<KeyValuePair<string, long>> NameList = [];
    private static readonly Comparison<KeyValuePair<string, long>> CompareByValue = static (a, b) => a.Value.CompareTo(b.Value);
    private static readonly StringBuilder Sb = new();
    private static long LastUpdate;

    public static void Reset()
    {
        Notifies.Clear();
    }

    public static void Notify(this PlayerControl pc, string text, float time = 6f, bool overrideAll = false, bool log = true, SendOption sendOption = SendOption.Reliable)
    {
        if (!AmongUsClient.Instance.AmHost || !pc) return;
        if (!GameStates.IsInTask) return;

        text = text.Trim();
        if (!text.Contains("<color=") && !text.Contains("</color>") && !text.Contains("<#")) text = Utils.ColorString(Color.white, text);
        if (!text.Contains("<size=")) text = "<size=1.9>" + text + "</size>";

        long expireTS = Utils.TimeStamp + (long)time;
        byte pcId = pc.PlayerId;
        bool alreadyContainsKey = false;

        if (overrideAll || !Notifies.TryGetValue(pcId, out Dictionary<string, long> notifies))
            Notifies[pc.PlayerId] = new() { { text, expireTS } };
        else
        {
            alreadyContainsKey = notifies.ContainsKey(text);
            notifies[text] = expireTS;
        }

        if (pc.IsNonHostModdedClient()) SendRPC(pcId, text, expireTS, overrideAll, sendOption);

        if (alreadyContainsKey)
        {
            if (log) Logger.Info($"Extended name notify for {pc.GetNameWithRole().RemoveHtmlTags()}: {text} ({time}s)", "Name Notify");
            return;
        }

        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc, SendOption: sendOption);
        if (log) Logger.Info($"New name notify for {pc.GetNameWithRole().RemoveHtmlTags()}: {text} ({time}s)", "Name Notify");
    }

    public static void OnFixedUpdate()
    {
        if (!GameStates.IsInTask)
        {
            Reset();
            return;
        }

        long now = Utils.TimeStamp;
        if (now == LastUpdate) return;
        LastUpdate = now;

        if (!AmongUsClient.Instance.AmHost || Notifies.Count == 0) return;

        var notifyEnumerator = Notifies.GetEnumerator();
        while (notifyEnumerator.MoveNext())
        {
            var pair = notifyEnumerator.Current;
            byte id = pair.Key;
            var dict = pair.Value;

            bool removedAny = false;
            ToRemove.Clear();

            var innerEnumerator = dict.GetEnumerator();
            while (innerEnumerator.MoveNext())
            {
                var innerCurrent = innerEnumerator.Current;
                if (innerCurrent.Value <= now)
                    ToRemove.Add(innerCurrent.Key);
            }

            if (ToRemove.Count > 0)
            {
                for (int index = 0; index < ToRemove.Count; index++)
                    dict.Remove(ToRemove[index]);

                removedAny = true;
            }

            if (removedAny)
            {
                PlayerControl pc = Utils.GetPlayerById(id);
                if (pc.IsAlive()) Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
            }
        }
    }

    public static bool GetNameNotify(PlayerControl player, out string name)
    {
        name = string.Empty;
        if (!Notifies.TryGetValue(player.PlayerId, out var notifies) || notifies.Count == 0) return false;

        NameList.Clear();
        var enumerator = notifies.GetEnumerator();

        while (enumerator.MoveNext())
            NameList.Add(enumerator.Current);

        if (NameList.Count >= 2) NameList.Sort(CompareByValue);

        Sb.Clear();
        for (int index = 0; index < NameList.Count; index++)
        {
            if (index > 0)
                Sb.Append('\n');

            Sb.Append(NameList[index].Key);
        }
        name = Sb.ToString();
        return true;
    }

    private static void SendRPC(byte playerId, string text, long expireTS, bool overrideAll, SendOption sendOption) // Only sent when adding a new notification
    {
        if (!AmongUsClient.Instance.AmHost) return;

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncNameNotify, sendOption);
        writer.Write(playerId);
        writer.Write(text);
        writer.Write(expireTS.ToString());
        writer.Write(overrideAll);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void SendRPC(CustomRpcSender sender, byte playerId, string text, long expireTS, bool overrideAll)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        sender.AutoStartRpc(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncNameNotify);
        sender.Write(playerId);
        sender.Write(text);
        sender.Write(expireTS.ToString());
        sender.Write(overrideAll);
        sender.EndRpc();
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        if (AmongUsClient.Instance.AmHost) return;

        byte playerId = reader.ReadByte();
        string text = reader.ReadString();
        long expireTS = long.Parse(reader.ReadString());
        bool overrideAll = reader.ReadBoolean();

        if (overrideAll || !Notifies.TryGetValue(playerId, out Dictionary<string, long> notifies))
            Notifies[playerId] = new() { { text, expireTS } };
        else
            notifies[text] = expireTS;

        Logger.Info($"New name notify for {Main.AllPlayerNames[playerId]}: {text} ({expireTS - Utils.TimeStamp}s)", "Name Notify");
    }
}