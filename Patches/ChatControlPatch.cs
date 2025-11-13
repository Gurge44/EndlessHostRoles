using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.Data;
using EHR.Coven;
using EHR.Impostor;
using EHR.Patches;
using HarmonyLib;
using Hazel;
using InnerNet;
using UnityEngine;

namespace EHR;

[HarmonyPatch(typeof(ChatController), nameof(ChatController.Update))]
internal static class ChatControllerUpdatePatch
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

            if (!TextBoxPatch.IsInvalidCommand)
            {
                __instance.freeChatField.textArea.compoText.Color(Color.white);
                __instance.freeChatField.textArea.outputText.color = Color.white;
            }

            __instance.quickChatField.background.color = new Color32(40, 40, 40, byte.MaxValue);
            __instance.quickChatField.text.color = Color.white;

            if (QuickChatIcon == null)
                QuickChatIcon = GameObject.Find("QuickChatIcon")?.transform.GetComponent<SpriteRenderer>();
            else
                QuickChatIcon.sprite = Utils.LoadSprite("EHR.Resources.Images.DarkQuickChat.png", 100f);

            if (OpenBanMenuIcon == null)
                OpenBanMenuIcon = GameObject.Find("OpenBanMenuIcon")?.transform.GetComponent<SpriteRenderer>();
            else
                OpenBanMenuIcon.sprite = Utils.LoadSprite("EHR.Resources.Images.DarkReport.png", 100f);

            if (OpenKeyboardIcon == null)
                OpenKeyboardIcon = GameObject.Find("OpenKeyboardIcon")?.transform.GetComponent<SpriteRenderer>();
            else
                OpenKeyboardIcon.sprite = Utils.LoadSprite("EHR.Resources.Images.DarkKeyboard.png", 100f);
        }
        else __instance.freeChatField.textArea.outputText.color = Color.black;

        if (!__instance.freeChatField.textArea.hasFocus) return;

        __instance.freeChatField.textArea.characterLimit = 1200;

        if (Input.GetKeyDown(KeyCode.Tab)) TextBoxPatch.OnTabPress(__instance);

        if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.C))
            ClipboardHelper.PutClipboardString(__instance.freeChatField.textArea.text);

        if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.V))
            __instance.freeChatField.textArea.SetText(__instance.freeChatField.textArea.text + GUIUtility.systemCopyBuffer.Trim());

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
internal static class UrlFinderPatch
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
        string[] comList = command.Split('|');

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
        string[] comList = command.Split('|');

        foreach (string com in comList)
        {
            if (exact)
            {
                if (msg.Contains(com)) return true;
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
        string originalMessage = message.Trim();
        message = message.ToLower().Trim();

        if (!player.IsAlive() || !AmongUsClient.Instance.AmHost || (Silencer.ForSilencer.Contains(player.PlayerId) && player.IsAlive())) return;

        int operate = message switch
        {
            { } str when CheckCommand(ref str, "id|guesslist|gl编号|玩家编号|玩家id|id列表|玩家列表|列表|所有id|全部id|shoot|guess|bet|st|gs|bt|猜|赌|sp|jj|tl|trial|审判|判|审|xp|效颦|效|颦|sw|换票|换|swap", false) || CheckName(ref playername, "系统消息", false) => 1,
            { } str when CheckCommand(ref str, "up|ask|target|vote|chat|check|decree|assume|note|whisper", false) => 2,
            { } str when CheckCommand(ref str, "r|role|m|myrole|n|now") => 4,
            _ => 3
        };

        switch (operate)
        {
            case 1 when player.IsAlive(): // Guessing Command & Such
                Logger.Info("Special Command", "ChatManager");
                if (player.AmOwner) break;

                LateTask.New(() =>
                {
                    if (!ChatCommands.LastSentCommand.ContainsKey(player.PlayerId))
                    {
                        GuessManager.GuesserMsg(player, message);
                        Logger.Info("Delayed Guess", "ChatManager");
                    }
                    else
                        Logger.Info("Delayed Guess was not necessary", "ChatManager");
                }, 0.3f, "Trying Delayed Guess");

                break;
            case 2: // /up and role ability commands
            case 4: // /r, /n, /m
                Logger.Info($"Command: {message}", "ChatManager");
                break;
            case 3: // In Lobby & Evertything Else
                AddChatHistory(player, originalMessage);
                break;
        }

        switch (Options.CurrentGameMode)
        {
            case CustomGameMode.FFA when GameStates.InGame && !message.StartsWith('/'):
                FreeForAll.UpdateLastChatMessage(player.GetRealName(), message);
                break;
        }
    }

    public static void AddChatHistory(PlayerControl player, string message)
    {
        var chatEntry = $"{player.PlayerId}: {message}";
        ChatHistory.Add(chatEntry);
        if (ChatHistory.Count > MaxHistorySize) ChatHistory.RemoveAt(0);
    }

    public static void SendPreviousMessagesToAll()
    {
        if (!AmongUsClient.Instance.AmHost || !HudManager.InstanceExists) return;

        Logger.Info(" Sending Previous Messages To Everyone", "ChatManager");

        PlayerControl[] aapc = Main.AllAlivePlayerControls;
        if (aapc.Length == 0) return;

        string[] filtered = ChatHistory.Where(a => Utils.GetPlayerById(Convert.ToByte(a.Split(':')[0].Trim())).IsAlive()).ToArray();
        ChatController chat = HudManager.Instance.Chat;
        var writer = CustomRpcSender.Create("SendPreviousMessagesToAll", SendOption.Reliable);
        var hasValue = false;

        if (filtered.Length < 20) ClearChat(aapc);

        foreach (string str in filtered)
        {
            string[] entryParts = str.Split(':');
            string senderId = entryParts[0].Trim();
            string senderMessage = entryParts[1].Trim();
            for (var j = 2; j < entryParts.Length; j++) senderMessage += ':' + entryParts[j].Trim();

            PlayerControl senderPlayer = Utils.GetPlayerById(Convert.ToByte(senderId));
            if (senderPlayer == null) continue;

            chat.AddChat(senderPlayer, senderMessage);
            SendRPC(writer, senderPlayer, senderMessage);
            hasValue = true;

            if (writer.stream.Length > 500)
            {
                writer.SendMessage();
                writer = CustomRpcSender.Create("SendPreviousMessagesToAll", SendOption.Reliable);
                hasValue = false;
            }
        }

        hasValue |= ChatUpdatePatch.SendLastMessages(ref writer);
        writer.SendMessage(!hasValue);
    }

    private static void SendRPC(CustomRpcSender writer, PlayerControl senderPlayer, string senderMessage, int targetClientId = -1)
    {
        if (GameStates.IsLobby && senderPlayer.IsHost())
            senderMessage = senderMessage.Insert(0, new('\n', senderPlayer.GetRealName().Count(x => x == '\n')));

        writer.AutoStartRpc(senderPlayer.NetId, RpcCalls.SendChat, targetClientId)
            .Write(senderMessage)
            .EndRpc();
    }

    // Base from https://github.com/Rabek009/MoreGamemodes/blob/master/Modules/Utils.cs
    public static void ClearChat(params PlayerControl[] targets)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        PlayerControl player = Main.AllAlivePlayerControls.MinBy(x => x.PlayerId) ?? Main.AllPlayerControls.MinBy(x => x.PlayerId) ?? PlayerControl.LocalPlayer;
        if (player == null) return;

        if (targets.Length == 0 || targets.Length == PlayerControl.AllPlayerControls.Count) SendEmptyMessage(null);
        else targets.Do(SendEmptyMessage);
        return;

        void SendEmptyMessage(PlayerControl receiver)
        {
            bool toEveryone = receiver == null;
            bool toLocalPlayer = !toEveryone && receiver.AmOwner;
            if (HudManager.InstanceExists && (toLocalPlayer || toEveryone)) HudManager.Instance.Chat.AddChat(player, "<size=32767>.");
            if (toLocalPlayer) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(player.NetId, (byte)RpcCalls.SendChat, SendOption.Reliable, toEveryone ? -1 : receiver.OwnerId);
            writer.Write("<size=32767>.");
            writer.Write(true);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
    }
}