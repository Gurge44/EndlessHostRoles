using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EHR.Patches;
using TMPro;
using UnityEngine;

namespace EHR.Modules;

public static class ChatBubbleShower
{
    private static long LastChatBubbleShowTS;
    private static readonly HashSet<(string Message, string Title)> Queue = [];

    public static void Update()
    {
        try
        {
            if (Queue.Count == 0 || ExileController.Instance || !HudManager.InstanceExists) return;

            long now = Utils.TimeStamp;
            int wait = GameStates.IsInTask ? 8 : 4;
            if (LastChatBubbleShowTS + wait > now) return;
            LastChatBubbleShowTS = now;

            (string message, string title) = Queue.First();
            Queue.Remove((message, title));

            ChatController chat = HudManager.Instance.Chat;

            if (GameStates.IsMeeting || chat.IsOpenOrOpening) ShowBubbleWithoutPlayer();
            else if (GameStates.IsLobby) Utils.SendMessage(message, PlayerControl.LocalPlayer.PlayerId, title);
            else Main.Instance.StartCoroutine(ShowTextOnHud());

            void ShowBubbleWithoutPlayer()
            {
                NetworkedPlayerInfo data = PlayerControl.LocalPlayer.Data;
                ChatBubble bubble = chat.GetPooledBubble();

                try
                {
                    bubble.transform.SetParent(chat.scroller.Inner);
                    bubble.transform.localScale = Vector3.one;
                    bubble.SetCosmetics(data);
                    bubble.gameObject.transform.Find("PoolablePlayer").gameObject.SetActive(false);
                    bubble.ColorBlindName.gameObject.SetActive(false);
                    bubble.SetLeft();
                    bubble.gameObject.transform.Find("NameText (TMP)").transform.localPosition += new Vector3(-0.7f, 0f);
                    bubble.gameObject.transform.Find("ChatText (TMP)").transform.localPosition += new Vector3(-0.7f, 0f);
                    chat.SetChatBubbleName(bubble, data, data.IsDead, false, PlayerNameColor.Get(data));
                    bubble.SetText(message);
                    bubble.AlignChildren();
                    chat.AlignAllBubbles();
                    bubble.NameText.text = title;
                    bubble.transform.Find("ChatText (TMP)").GetComponent<TextMeshPro>().color = new(1f, 1f, 1f, 1f);
                    bubble.transform.Find("Background").GetComponent<SpriteRenderer>().color = new(0.05f, 0.05f, 0.05f, 1f);
                    Transform xMark = bubble.transform.Find("PoolablePlayer/xMark");
                    if (xMark && xMark.GetComponent<SpriteRenderer>().enabled) bubble.transform.Find("Background").GetComponent<SpriteRenderer>().color = new(0.05f, 0.05f, 0.05f, 0.5f);
                }
                catch (Exception e)
                {
                    chat.chatBubblePool.Reclaim(bubble);
                    Utils.ThrowException(e);
                }
            }

            IEnumerator ShowTextOnHud()
            {
                const string color1 = "#00FFA5";
                const string color2 = "#00A5FF";
                var isColor1 = true;

                for (var i = 0; i < 4; i++)
                {
                    string color = isColor1 ? color1 : color2;
                    isColor1 = !isColor1;
                    var text = $"<b>{title}</b>\n<size=80%><color={color}>{message}</color></size>";
                    HudManagerPatch.AchievementUnlockedText = text;
                    yield return new WaitForSeconds(0.2f);
                }

                HudManagerPatch.AchievementUnlockedText = $"<b>{title}</b>\n<size=80%>{message}</size>";
                yield return new WaitForSeconds(7.25f);
                HudManagerPatch.AchievementUnlockedText = string.Empty;
            }
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }

    /// <summary>
    ///     Displays a chat bubble with a message and a title during the round for the local player.
    /// </summary>
    /// <param name="message">The message to display in the chat bubble.</param>
    /// <param name="title">The title of the chat bubble.</param>
    public static void ShowChatBubbleInRound(string message, string title)
    {
        Queue.Add((message, title));
    }
}