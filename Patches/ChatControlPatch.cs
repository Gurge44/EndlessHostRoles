using AmongUs.Data;
using HarmonyLib;
using InnerNet;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using TOHE.Roles.Impostor;
using UnityEngine;

namespace TOHE;

[HarmonyPatch(typeof(ChatController), nameof(ChatController.Update))]
class ChatControllerUpdatePatch
{
    public static int CurrentHistorySelection = -1;

    private static SpriteRenderer quickChatIcon;
    private static SpriteRenderer openBanMenuIcon;
    private static SpriteRenderer openKeyboardIcon;

    public static void Prefix(ChatController __instance)
    {
        if (AmongUsClient.Instance.AmHost && DataManager.Settings.Multiplayer.ChatMode == QuickChatModes.QuickChatOnly)
            DataManager.Settings.Multiplayer.ChatMode = QuickChatModes.FreeChatOrQuickChat;
    }

    public static void Postfix(ChatController __instance)
    {
        if (Main.DarkTheme.Value)
        {
            __instance.freeChatField.background.color = new Color32(40, 40, 40, byte.MaxValue);
            __instance.freeChatField.textArea.compoText.Color(Color.white);
            __instance.freeChatField.textArea.outputText.color = Color.white;

            if (quickChatIcon == null) quickChatIcon = GameObject.Find("QuickChatIcon")?.transform?.GetComponent<SpriteRenderer>();
            else quickChatIcon.sprite = Utils.LoadSprite("TOHE.Resources.Images.DarkQuickChat.png", 100f);

            if (openBanMenuIcon == null) openBanMenuIcon = GameObject.Find("OpenBanMenuIcon")?.transform?.GetComponent<SpriteRenderer>();
            else openBanMenuIcon.sprite = Utils.LoadSprite("TOHE.Resources.Images.DarkReport.png", 100f);

            if (openKeyboardIcon == null) openKeyboardIcon = GameObject.Find("OpenKeyboardIcon")?.transform?.GetComponent<SpriteRenderer>();
            else openKeyboardIcon.sprite = Utils.LoadSprite("TOHE.Resources.Images.DarkKeyboard.png", 100f);
        }
        else
        {
            __instance.freeChatField.textArea.outputText.color = Color.black;
        }

        if (!__instance.freeChatField.textArea.hasFocus) return;
        __instance.freeChatField.textArea.characterLimit = AmongUsClient.Instance.AmHost ? 2000 : 300;

        if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.C))
            ClipboardHelper.PutClipboardString(__instance.freeChatField.textArea.text);
        if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.V))
            __instance.freeChatField.textArea.SetText(__instance.freeChatField.textArea.text + GUIUtility.systemCopyBuffer);
        if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.X))
        {
            ClipboardHelper.PutClipboardString(__instance.freeChatField.textArea.text);
            __instance.freeChatField.textArea.SetText("");
        }

        if (Input.GetKeyDown(KeyCode.UpArrow) && ChatCommands.ChatHistory.Count > 0)
        {
            CurrentHistorySelection = Mathf.Clamp(--CurrentHistorySelection, 0, ChatCommands.ChatHistory.Count - 1);
            __instance.freeChatField.textArea.SetText(ChatCommands.ChatHistory[CurrentHistorySelection]);
        }

        if (Input.GetKeyDown(KeyCode.DownArrow) && ChatCommands.ChatHistory.Count > 0)
        {
            CurrentHistorySelection++;
            __instance.freeChatField.textArea.SetText(CurrentHistorySelection < ChatCommands.ChatHistory.Count ? ChatCommands.ChatHistory[CurrentHistorySelection] : string.Empty);
        }
    }
}

public static class ChatManager
{
    public static bool cancel;
    private static readonly List<string> ChatHistory = [];
    private const int MaxHistorySize = 20;

    public static void ResetHistory()
    {
        ChatHistory.Clear();
    }

    public static bool CheckCommand(ref string msg, string command, bool exact = true)
    {
        var comList = command.Split('|');
        foreach (string str in comList)
        {
            if (exact)
            {
                if (msg == "/" + str) return true;
            }
            else
            {
                if (msg.StartsWith("/" + str))
                {
                    msg = msg.Replace("/" + str, string.Empty);
                    return true;
                }
            }
        }

        return false;
    }

    public static bool CheckName(ref string msg, string command, bool exact = true)
    {
        var comList = command.Split('|');
        foreach (string com in comList)
        {
            if (exact)
            {
                if (msg.Contains(com))
                {
                    return true;
                }
            }
            else
            {
                int index = msg.IndexOf(com, StringComparison.Ordinal);
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
        string playername = player.GetNameWithRole();
        message = message.ToLower().TrimStart().TrimEnd();

        if (!player.IsAlive() || !AmongUsClient.Instance.AmHost) return;

        if (Blackmailer.ForBlackmailer.Contains(player.PlayerId) && player.IsAlive())
        {
            cancel = true;
            return;
        }

        int operate = message switch
        {
            { } str when CheckCommand(ref str, "id|guesslist|gl编号|玩家编号|玩家id|id列表|玩家列表|列表|所有id|全部id|shoot|guess|bet|st|gs|bt|猜|赌|sp|jj|tl|trial|审判|判|审|xp|效颦|效|颦|sw|换票|换|swap", false) || CheckName(ref playername, "系统消息", false) => 1,
            { } str when CheckCommand(ref str, "up", false) => 2,
            { } str when CheckCommand(ref str, "r|role|m|myrole|n|now") => 4,
            _ => 3
        };

        switch (operate)
        {
            case 1 when player.IsAlive(): // Guessing Command & Such
                Logger.Info("Special Command", "ChatManager");
                cancel = true;
                break;
            case 2: // /up
                Logger.Info($"Command: {message}", "ChatManager");
                cancel = false;
                break;
            case 3: // In Lobby & Evertything Else
                string chatEntry = $"{player.PlayerId}: {message}";
                ChatHistory.Add(chatEntry);
                if (ChatHistory.Count > MaxHistorySize) ChatHistory.RemoveAt(0);
                cancel = false;
                break;
            case 4: // /r, /n, /m
                Logger.Info($"Command: {message}", "ChatManager");
                //if (!DontBlock) SendPreviousMessagesToAll(realMessagesOnly: true);
                break;
        }

        if (Options.CurrentGameMode == CustomGameMode.FFA && !message.StartsWith('/'))
        {
            FFAManager.UpdateLastChatMessage(player.GetRealName(), message);
        }
    }

    public static bool DontBlock;

    public static void SendPreviousMessagesToAll(bool realMessagesOnly = false)
    {
        if (!AmongUsClient.Instance.AmHost || !GameStates.IsModHost) return;
        ChatUpdatePatch.DoBlockChat = true;
        string msg = Utils.EmptyMessage();
        var totalAlive = Main.AllAlivePlayerControls.Length;
        var x = Main.AllAlivePlayerControls;
        var r = IRandom.Instance;

        var filtered = ChatHistory.Where(a => Utils.GetPlayerById(Convert.ToByte(a.Split(':')[0].Trim())).IsAlive()).ToArray();

        switch (realMessagesOnly)
        {
            case true when filtered.Length < 5:
                return;
            case false:
                for (int i = filtered.Length; i < 20; i++)
                {
                    var player = x[r.Next(0, totalAlive)];
                    DestroyableSingleton<HudManager>.Instance.Chat.AddChat(player, msg);
                    SendRPC(player, msg);
                }

                break;
        }

        foreach (string str in filtered)
        {
            var entryParts = str.Split(':');
            var senderId = entryParts[0].Trim();
            var senderMessage = entryParts[1].Trim();
            for (int j = 2; j < entryParts.Length; j++)
            {
                senderMessage += ':' + entryParts[j].Trim();
            }

            var senderPlayer = Utils.GetPlayerById(Convert.ToByte(senderId));
            if (senderPlayer == null) continue;

            DestroyableSingleton<HudManager>.Instance.Chat.AddChat(senderPlayer, senderMessage);
            SendRPC(senderPlayer, senderMessage);
        }

        ChatUpdatePatch.DoBlockChat = false;
    }

    private static void SendRPC(InnerNetObject senderPlayer, string senderMessage)
    {
        var writer = CustomRpcSender.Create("MessagesToSend");
        writer.StartMessage();
        writer.StartRpc(senderPlayer.NetId, (byte)RpcCalls.SendChat)
            .Write(senderMessage)
            .EndRpc();
        writer.EndMessage();
        writer.SendMessage();
    }
}