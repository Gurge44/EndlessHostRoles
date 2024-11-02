using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace EHR.Modules
{
    public static class ChatBubbleShower
    {
        private static long LastChatBubbleShowTS;
        private static HashSet<(string Message, string Title)> Queue = [];
        
        public static void Update()
        {
            try
            {
                if (Queue.Count == 0) return;
            
                long now = Utils.TimeStamp;
                if (LastChatBubbleShowTS + 4 > now) return;
                LastChatBubbleShowTS = now;
            
                (string message, string title) = Queue.First();
                Queue.Remove((message, title));
            
                ChatController chat = DestroyableSingleton<HudManager>.Instance.Chat;
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
            catch (Exception e)
            {
                Utils.ThrowException(e);
            }
        }

        /// <summary>
        /// Displays a chat bubble with a message and a title during the round for the local player.
        /// </summary>
        /// <param name="message">The message to display in the chat bubble.</param>
        /// <param name="title">The title of the chat bubble.</param>
        public static void ShowChatBubbleInRound(string message, string title)
        {
            Queue.Add((message, title));
        }
    }
}