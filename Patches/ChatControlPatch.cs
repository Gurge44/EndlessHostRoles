using AmongUs.Data;
using HarmonyLib;
using Hazel;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TOHE;

[HarmonyPatch(typeof(ChatController), nameof(ChatController.Update))]
class ChatControllerUpdatePatch
{
    public static int CurrentHistorySelection = -1;
    public static void Prefix()
    {
        if (AmongUsClient.Instance.AmHost && DataManager.Settings.Multiplayer.ChatMode == InnerNet.QuickChatModes.QuickChatOnly)
            DataManager.Settings.Multiplayer.ChatMode = InnerNet.QuickChatModes.FreeChatOrQuickChat; //コマンドを打つためにホストのみ常時フリーチャット開放
    }
    public static void Postfix(ChatController __instance)
    {
        if (!__instance.freeChatField.textArea.hasFocus) return;
        if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.C))
            ClipboardHelper.PutClipboardString(__instance.freeChatField.textArea.text);
        if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.V))
            __instance.freeChatField.textArea.SetText(__instance.freeChatField.textArea.text + GUIUtility.systemCopyBuffer);
        if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.X))
        {
            ClipboardHelper.PutClipboardString(__instance.freeChatField.textArea.text);
            __instance.freeChatField.textArea.SetText("");
        }
        if (Input.GetKeyDown(KeyCode.UpArrow) && ChatCommands.ChatHistory.Any())
        {
            CurrentHistorySelection = Mathf.Clamp(--CurrentHistorySelection, 0, ChatCommands.ChatHistory.Count - 1);
            __instance.freeChatField.textArea.SetText(ChatCommands.ChatHistory[CurrentHistorySelection]);
        }
        if (Input.GetKeyDown(KeyCode.DownArrow) && ChatCommands.ChatHistory.Any())
        {
            CurrentHistorySelection++;
            if (CurrentHistorySelection < ChatCommands.ChatHistory.Count)
                __instance.freeChatField.textArea.SetText(ChatCommands.ChatHistory[CurrentHistorySelection]);
            else __instance.freeChatField.textArea.SetText("");
        }
    }
}

public class ChatManager
{
    public static bool cancel;
    private static List<string> chatHistory = new();
    private const int maxHistorySize = 20;
    public static void ResetHistory()
    {
        chatHistory.Clear();
        chatHistory = new();
    }
    public static bool CheckCommand(ref string msg, string command, bool exact = true)
    {
        var comList = command.Split('|');
        for (int i = 0; i < comList.Length; i++)
        {
            if (exact)
            {
                if (msg == "/" + comList[i]) return true;
            }
            else
            {
                if (msg.StartsWith("/" + comList[i]))
                {
                    msg = msg.Replace("/" + comList[i], string.Empty);
                    return true;
                }
            }
        }
        return false;
    }
    public static bool CheckName(ref string msg, string command, bool exact = true)
    {
        var comList = command.Split('|');
        for (int i = 0; i < comList.Length; i++)
        {
            string com = comList[i];
            if (exact)
            {
                if (msg.Contains(com))
                {
                    return true;
                }
            }
            else
            {
                int index = msg.IndexOf(com);
                if (index != -1)
                {
                    msg = msg.Remove(index, com.Length);
                    return true;
                }
            }
        }
        return false;
    }
    public static void SendMessage(PlayerControl player, string message)
    {
        int operate;
        string playername = player.GetNameWithRole();
        message = message.ToLower().TrimStart().TrimEnd();

        if (!player.IsAlive() || !AmongUsClient.Instance.AmHost) return;

        if (!GameStates.IsInGame) operate = 3;
        else if (CheckCommand(ref message, "id|guesslist|gl编号|玩家编号|玩家id|id列表|玩家列表|列表|所有id|全部id|shoot|guess|bet|st|gs|bt|猜|赌|sp|jj|tl|trial|审判|判|审|xp|效颦|效|颦|sw|换票|换|swap", false) || CheckName(ref playername, "系统消息", false)) operate = 1;
        else if (CheckCommand(ref message, "up", false)) operate = 2;
        else if (CheckCommand(ref message, "r|role|m|myrole|n|now")) operate = 4;
        else operate = 3;

        switch (operate)
        {
            case 1 when player.IsAlive():
                Logger.Info($"Special Command", "ChatManager");
                cancel = true;
                break;
            case 2:
                Logger.Info($"Command: {message}", "ChatManager");
                cancel = false;
                break;
            case 4:
                Logger.Info($"Command: {message}", "ChatManager");
                SendPreviousMessagesToAll();
                break;
            case 3:
                {
                    string chatEntry = $"{player.PlayerId}: {message}";
                    chatHistory.Add(chatEntry);

                    if (chatHistory.Count > maxHistorySize)
                    {
                        chatHistory.RemoveAt(0);
                    }
                    cancel = false;
                    break;
                }
        }
    }
    public static void SendPreviousMessagesToAll()
    {
        string msg = "<size=0>.</size>";
        List<CustomRoles> roles = Enum.GetValues(typeof(CustomRoles)).Cast<CustomRoles>().Where(x => x is not CustomRoles.KB_Normal and not CustomRoles.Killer).ToList();
        string[] specialTexts = new string[] { "bet", "bt", "guess", "gs", "shoot", "st", "赌", "猜", "审判", "tl", "判", "审", "trial" };
        var totalAlive = Main.AllAlivePlayerControls.Count();
        var x = Main.AllAlivePlayerControls.ToArray();

        for (int i = chatHistory.Count; i < 20; i++)
        {
            var player = x[IRandom.Instance.Next(0, totalAlive)];
            DestroyableSingleton<HudManager>.Instance.Chat.AddChat(player, msg);
            SendRPC(player, msg);
        }

        for (int i = 0; i < chatHistory.Count; i++)
        {
            string entry = chatHistory[i];
            var entryParts = entry.Split(':');
            var senderId = entryParts[0].Trim();
            var senderMessage = entryParts[1].Trim();
            var senderPlayer = Utils.GetPlayerById(Convert.ToByte(senderId));

            if (!senderPlayer.IsAlive())
            {
                var deathReason = Main.PlayerStates[senderPlayer.PlayerId].deathReason;
                senderPlayer.Revive();

                DestroyableSingleton<HudManager>.Instance.Chat.AddChat(senderPlayer, senderMessage);
                SendRPC(senderPlayer, senderMessage);

                senderPlayer.Die(DeathReason.Kill, true);
                Main.PlayerStates[senderPlayer.PlayerId].deathReason = deathReason;
            }
            else
            {
                DestroyableSingleton<HudManager>.Instance.Chat.AddChat(senderPlayer, senderMessage);
                SendRPC(senderPlayer, senderMessage);
            }
        }
    }
    private static void SendRPC(PlayerControl senderPlayer, string senderMessage)
    {
        var writer = CustomRpcSender.Create("MessagesToSend", SendOption.None);
        writer.StartMessage(-1);
        writer.StartRpc(senderPlayer.NetId, (byte)RpcCalls.SendChat)
            .Write(senderMessage)
            .EndRpc();
        writer.EndMessage();
        writer.SendMessage();
    }
}