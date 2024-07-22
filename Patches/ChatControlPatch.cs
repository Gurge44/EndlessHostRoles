using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.Data;
using EHR.Impostor;
using EHR.Patches;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace EHR;

[HarmonyPatch(typeof(ChatController), nameof(ChatController.Update))]
class ChatControllerUpdatePatch
{
    public static int CurrentHistorySelection = -1;

    private static SpriteRenderer QuickChatIcon;
    private static SpriteRenderer OpenBanMenuIcon;
    private static SpriteRenderer OpenKeyboardIcon;

    public static void Prefix()
    {
        if (AmongUsClient.Instance.AmHost && DataManager.Settings.Multiplayer.ChatMode == QuickChatModes.QuickChatOnly)
            DataManager.Settings.Multiplayer.ChatMode = QuickChatModes.FreeChatOrQuickChat;
    }

    public static void Postfix(ChatController __instance)
    {
        if (Main.DarkTheme.Value)
        {
            __instance.freeChatField.background.color = new Color32(40, 40, 40, byte.MaxValue);
            if (!TextBoxTMPSetTextPatch.IsInvalidCommand)
            {
                __instance.freeChatField.textArea.compoText.Color(Color.white);
                __instance.freeChatField.textArea.outputText.color = Color.white;
            }

            __instance.quickChatField.background.color = new Color32(40, 40, 40, byte.MaxValue);
            __instance.quickChatField.text.color = Color.white;

            if (QuickChatIcon == null) QuickChatIcon = GameObject.Find("QuickChatIcon")?.transform.GetComponent<SpriteRenderer>();
            else QuickChatIcon.sprite = Utils.LoadSprite("EHR.Resources.Images.DarkQuickChat.png", 100f);

            if (OpenBanMenuIcon == null) OpenBanMenuIcon = GameObject.Find("OpenBanMenuIcon")?.transform.GetComponent<SpriteRenderer>();
            else OpenBanMenuIcon.sprite = Utils.LoadSprite("EHR.Resources.Images.DarkReport.png", 100f);

            if (OpenKeyboardIcon == null) OpenKeyboardIcon = GameObject.Find("OpenKeyboardIcon")?.transform.GetComponent<SpriteRenderer>();
            else OpenKeyboardIcon.sprite = Utils.LoadSprite("EHR.Resources.Images.DarkKeyboard.png", 100f);
        }
        else
        {
            __instance.freeChatField.textArea.outputText.color = Color.black;
        }

        if (!__instance.freeChatField.textArea.hasFocus) return;
        __instance.freeChatField.textArea.characterLimit = AmongUsClient.Instance.AmHost ? 2000 : 300;

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            TextBoxTMPSetTextPatch.OnTabPress(__instance);
        }

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

[HarmonyPatch(typeof(UrlFinder), nameof(UrlFinder.TryFindUrl))]
class UrlFinderPatch
{
    public static bool Prefix(ref bool __result)
    {
        __result = false;
        return false;
    }
}

public static class ChatManager
{
    private const int MaxHistorySize = 20;
    private static readonly List<string> ChatHistory = [];

    public static void ResetHistory()
    {
        ChatHistory.Clear();
    }

    private static bool CheckCommand(ref string msg, string command, bool exact = true)
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

    private static bool CheckName(ref string msg, string command, bool exact = true)
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

        if (Silencer.ForSilencer.Contains(player.PlayerId) && player.IsAlive())
        {
            return;
        }

        int operate = message switch
        {
            { } str when CheckCommand(ref str, "id|guesslist|gl编号|玩家编号|玩家id|id列表|玩家列表|列表|所有id|全部id|shoot|guess|bet|st|gs|bt|猜|赌|sp|jj|tl|trial|审判|判|审|xp|效颦|效|颦|sw|换票|换|swap", false) || CheckName(ref playername, "系统消息", false) => 1,
            { } str when CheckCommand(ref str, "up|ask|target|vote|chat|check", false) => 2,
            { } str when CheckCommand(ref str, "r|role|m|myrole|n|now") => 4,
            _ => 3
        };

        switch (operate)
        {
            case 1 when player.IsAlive(): // Guessing Command & Such
                Logger.Info("Special Command", "ChatManager");
                if (player.PlayerId == PlayerControl.LocalPlayer.PlayerId) break;
                LateTask.New(() =>
                {
                    if (!ChatCommands.LastSentCommand.ContainsKey(player.PlayerId))
                    {
                        GuessManager.GuesserMsg(player, message);
                        Logger.Info("Delayed Guess", "ChatManager");
                    }
                    else Logger.Info("Delayed Guess was not necessary", "ChatManager");
                }, 0.3f, "Trying Delayed Guess");
                break;
            case 2: // /up and role ability commands
                Logger.Info($"Command: {message}", "ChatManager");
                break;
            case 3: // In Lobby & Evertything Else
                string chatEntry = $"{player.PlayerId}: {message}";
                ChatHistory.Add(chatEntry);
                if (ChatHistory.Count > MaxHistorySize) ChatHistory.RemoveAt(0);
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

    public static void SendPreviousMessagesToAll(bool clear = false)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        ChatUpdatePatch.DoBlockChat = true;
        string msg = Utils.EmptyMessage;
        var totalAlive = Main.AllAlivePlayerControls.Length;
        if (totalAlive == 0) return;
        var x = Main.AllAlivePlayerControls;
        var r = IRandom.Instance;

        var filtered = ChatHistory.SkipLast(1).Where(a => Utils.GetPlayerById(Convert.ToByte(a.Split(':')[0].Trim())).IsAlive()).ToArray();

        for (int i = clear ? 0 : filtered.Length; i < 20; i++)
        {
            var player = x[r.Next(0, totalAlive)];
            DestroyableSingleton<HudManager>.Instance.Chat.AddChat(player, msg);
            SendRPC(player, msg);
        }

        if (!clear)
        {
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
        }

        ChatUpdatePatch.SendLastMessages();

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