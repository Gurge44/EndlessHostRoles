﻿using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Hazel;

namespace EHR.Coven;

public abstract class Coven : RoleBase
{
    public enum NecronomiconReceivePriorities
    {
        First,
        Random,
        Never
    }

    protected abstract NecronomiconReceivePriorities NecronomiconReceivePriority { get; }

    protected bool HasNecronomicon { get; private set; }

    protected virtual void OnReceiveNecronomicon() { }

    private static void GiveNecronomicon()
    {
        Dictionary<byte, PlayerState> psDict = Main.PlayerStates.Where(x => x.Value.Role is Coven { HasNecronomicon: false } coven && coven.NecronomiconReceivePriority != NecronomiconReceivePriorities.Never).ToDictionary(x => x.Key, x => x.Value);
        if (psDict.Count == 0) return;

        KeyValuePair<byte, PlayerState> receiver = psDict.Shuffle().OrderBy(x => ((Coven)x.Value.Role).NecronomiconReceivePriority).ThenByDescending(x => !x.Value.IsDead).First();

        var covenRole = (Coven)receiver.Value.Role;
        covenRole.HasNecronomicon = true;
        covenRole.OnReceiveNecronomicon();

        LateTask.New(() =>
        {
            var sender = CustomRpcSender.Create("CovenGiveNecronomicon", SendOption.Reliable);
            string[] holders = Main.PlayerStates.Where(s => s.Value.Role is Coven { HasNecronomicon: true }).Select(s => s.Key.ColoredPlayerName()).ToArray();
            Main.AllPlayerControls.Where(x => x.Is(Team.Coven)).Select(x => x.PlayerId).Without(receiver.Key).Do(x => Utils.SendMessage("\n", x, string.Format(Translator.GetString("PlayerReceivedTheNecronomicon"), receiver.Key.ColoredPlayerName(), Main.CovenColor, holders.Length, string.Join(", ", holders)), writer: sender, multiple: true));
            Utils.SendMessage("\n", receiver.Key, string.Format(Translator.GetString("YouReceivedTheNecronomicon"), Main.CovenColor), writer: sender, multiple: true);
            sender.SendMessage(dispose: sender.stream.Length <= 3);
        }, 12f, log: false);
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    public static class CovenMeetingStartPatch
    {
        public static int MeetingNum;

        public static void Postfix()
        {
            if (AmongUsClient.Instance.AmHost && ++MeetingNum >= Options.CovenReceiveNecronomiconAfterNumMeetings.GetInt())
                GiveNecronomicon();
        }
    }
}