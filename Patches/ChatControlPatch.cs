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
    public static bool cancel = false;
    private static readonly List<string> chatHistory = new();
    private const int maxHistorySize = 20;
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

    public static void SendMessage(PlayerControl player, string message)
    {
        int operate = 0;
        string msg = message;
        //string playername = player.GetNameWithRole();
        //message = message.ToLower().TrimStart().TrimEnd();
        if (!player.IsAlive() || !AmongUsClient.Instance.AmHost) return;
        if (GameStates.IsInGame) operate = 3;
        if (CheckCommand(ref msg, "id|guesslist|gl编号|玩家编号|玩家id|id列表|玩家列表|列表|所有id|全部id")) operate = 1;
        else if (CheckCommand(ref msg, "shoot|guess|bet|st|gs|bt|猜|赌|sp|jj|tl|trial|审判|判|审|compare|cmp|比较|duel|sw|换票|换|swap|st|finish|reveal", false)) operate = 2;
        if ((operate == 1) && player.IsAlive())
        {
            Logger.Info($"包含特殊信息，不记录", "ChatManager");
            cancel = true;
        }
        else if (operate == 2)
        {
            Logger.Info($"指令{msg}，不记录", "ChatManager");
            cancel = false;
        }
        else if (operate == 4)
        {
            Logger.Info($"指令{msg}，不记录", "ChatManager");
            SendPreviousMessagesToAll();
        }
        else if (operate == 3)
        {
            message = msg;
            string chatEntry = $"{player.PlayerId}: {message}";
            chatHistory.Add(chatEntry);

            if (chatHistory.Count > maxHistorySize)
            {
                chatHistory.RemoveAt(0);
            }
            cancel = false;
        }
    }

    public static void SendPreviousMessagesToAll()
    {
        var rd = IRandom.Instance;
        string msg;
        List<CustomRoles> roles = Enum.GetValues(typeof(CustomRoles)).Cast<CustomRoles>().ToList();
        string[] specialTexts = new string[] { "bet", "bt", "guess", "gs", "shoot", "st", "赌", "猜", "审判", "tl", "判", "审", "trial" };

        for (int i = chatHistory.Count; i < 30; i++)
        {
            msg = "/";
            if (rd.Next(1, 100) < 20)
            {
                msg += "id";
            }
            else
            {
                msg += specialTexts[rd.Next(0, specialTexts.Length - 1)];
                msg += rd.Next(1, 100) < 50 ? string.Empty : " ";
                msg += rd.Next(0, 15).ToString();
                msg += rd.Next(1, 100) < 50 ? string.Empty : " ";
                CustomRoles role = roles[rd.Next(0, roles.Count)];
                msg += rd.Next(1, 100) < 50 ? string.Empty : " ";
                msg += Utils.GetRoleName(role);
            }

            var player = Main.AllAlivePlayerControls.ToArray()[rd.Next(0, Main.AllAlivePlayerControls.Count())];
            DestroyableSingleton<HudManager>.Instance.Chat.AddChat(player, msg);
            var writer = CustomRpcSender.Create("MessagesToSend", SendOption.None);
            writer.StartMessage(-1);
            writer.StartRpc(player.NetId, (byte)RpcCalls.SendChat)
                .Write(msg)
                .EndRpc();
            writer.EndMessage();
            writer.SendMessage();
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
                senderPlayer.Revive();

                DestroyableSingleton<HudManager>.Instance.Chat.AddChat(senderPlayer, senderMessage);

                var writer = CustomRpcSender.Create("MessagesToSend", SendOption.None);
                writer.StartMessage(-1);
                writer.StartRpc(senderPlayer.NetId, (byte)RpcCalls.SendChat)
                    .Write(senderMessage)
                    .EndRpc();
                writer.EndMessage();
                writer.SendMessage();
                senderPlayer.Die(DeathReason.Kill, true);
            }
            else
            {
                DestroyableSingleton<HudManager>.Instance.Chat.AddChat(senderPlayer, senderMessage);
                var writer = CustomRpcSender.Create("MessagesToSend", SendOption.None);
                writer.StartMessage(-1);
                writer.StartRpc(senderPlayer.NetId, (byte)RpcCalls.SendChat)
                    .Write(senderMessage)
                    .EndRpc();
                writer.EndMessage();
                writer.SendMessage();
            }

            //foreach (var senderPlayer in Main.AllPlayerControls)
            //for (int j = 0; j < Main.AllPlayerControls.Count(); j++)
            //{
            //    var senderPlayer = Main.AllPlayerControls.ElementAt(j);
            //    if (senderPlayer.PlayerId.ToString() == senderId)
            //    {
            //        if (!senderPlayer.IsAlive())
            //        {
            //            var deathReason = (PlayerState.DeathReason)senderPlayer.PlayerId;
            //            senderPlayer.Revive();


            //            DestroyableSingleton<HudManager>.Instance.Chat.AddChat(senderPlayer, senderMessage);

            //            var writer = CustomRpcSender.Create("MessagesToSend", SendOption.None);
            //            writer.StartMessage(-1);
            //            writer.StartRpc(senderPlayer.NetId, (byte)RpcCalls.SendChat)
            //                .Write(senderMessage)
            //                .EndRpc();
            //            writer.EndMessage();
            //            writer.SendMessage();
            //            senderPlayer.Die((DeathReason)deathReason, true);
            //            //Main.PlayerStates[senderPlayer.PlayerId].deathReason = deathReason;
            //        }
            //        else
            //        {
            //            DestroyableSingleton<HudManager>.Instance.Chat.AddChat(senderPlayer, senderMessage);
            //            var writer = CustomRpcSender.Create("MessagesToSend", SendOption.None);
            //            writer.StartMessage(-1);
            //            writer.StartRpc(senderPlayer.NetId, (byte)RpcCalls.SendChat)
            //                .Write(senderMessage)
            //                .EndRpc();
            //            writer.EndMessage();
            //            writer.SendMessage();
            //        }
            //    }
            //}
        }
    }
}