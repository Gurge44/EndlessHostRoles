using System;
using System.Collections.Generic;
using EHR.Patches;
using Rewired.Utils;
using UnityEngine;

namespace EHR.Modules
{
    internal static class LoadingScreen
    {
        const int HintCount = 36;
        const int JokeHintCount = 6;
        private static SpriteRenderer LoadingAnimation;
        private static readonly HashSet<int> ToldHints = [];

        private static void UpdateLoadingAnimation()
        {
            try
            {
                if (!LoadingAnimation.IsNullOrDestroyed()) Object.Destroy(LoadingAnimation);

                LoadingAnimation = Object.Instantiate(ModManager.Instance.ModStamp);
                LoadingAnimation.sprite = Utils.LoadSprite("EHR.Resources.Loading.png", 300f);
                LoadingAnimation.sortingOrder = 100;

                var basePos = LoadingAnimation.transform.position;
                var x = basePos.x - 9.8f;
                var y = basePos.y - 4.5f;
                var z = basePos.z;
                LoadingAnimation.transform.position = new(x, y, z);
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString(), "LoadingScreen.UpdateLoadingAnimation");
            }
        }

        public static string GetHint()
        {
            int index;
            if (ToldHints.Count == HintCount) ToldHints.Clear();
            do index = IRandom.Instance.Next(HintCount);
            while (!ToldHints.Add(index));
            bool joke = IRandom.Instance.Next(20) == 0;
            if (joke) index = IRandom.Instance.Next(40, 40 + JokeHintCount);
            string text = Translator.GetString($"LoadingHint.{index}");
            text = text.Insert(0, joke ? "<color=#ffff00>" : "<color=#00ffa5>");
            text = text.Insert(text.IndexOf('\n'), "</color><#ffffff>");
            text += "</color>";
            return text;
        }

        public static void Update()
        {
            try
            {
                var lp = PlayerControl.LocalPlayer;
                if (lp == null) return;
                var anims = lp.MyPhysics.Animations;

                bool visible = AmongUsClient.Instance.AmHost && AmongUsClient.Instance.IsGameStarted && !GameStates.IsCanMove && (!GameStates.IsInTask || ExileController.Instance) && !GameStates.IsMeeting && !HudManager.Instance.Chat.IsOpenOrOpening && !lp.inVent && !anims.IsPlayingAnyLadderAnimation() && !VentButtonDoClickPatch.Animating && !lp.onLadder;

                if (!visible && LoadingAnimation)
                {
                    Object.Destroy(LoadingAnimation);
                    return;
                }

                if (!LoadingAnimation && visible)
                {
                    UpdateLoadingAnimation();
                    return;
                }

                if (LoadingAnimation)
                {
                    var basePos = ModManager.Instance.ModStamp.transform.position;

                    var x = basePos.x - 9.8f;
                    var y = basePos.y - 4.5f;
                    var z = basePos.z;

                    if (LoadingAnimation.transform.position != new Vector3(x, y, z)) LoadingAnimation.transform.position = new(x, y, z);

                    LoadingAnimation.transform.Rotate(Vector3.forward, 200f * Time.deltaTime);
                }

                switch (visible)
                {
                    case false when ErrorText.HasHint:
                        ErrorText.RemoveHint();
                        return;
                    case true when !ErrorText.HasHint:
                        ErrorText.Instance.AddError(ErrorCode.LoadingHint);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString(), "LoadingScreen.Update");
            }
        }
    }
}