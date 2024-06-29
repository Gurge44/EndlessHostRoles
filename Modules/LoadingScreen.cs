using System;
using EHR.Patches;
using Rewired.Utils;
using UnityEngine;

namespace EHR.Modules
{
    internal static class LoadingScreen
    {
        private static SpriteRenderer LoadingAnimation;

        private static void UpdateLoadingAnimation()
        {
            try
            {
                if (!LoadingAnimation.IsNullOrDestroyed()) Object.Destroy(LoadingAnimation);

                LoadingAnimation = Object.Instantiate(ModManager.Instance.ModStamp);
                LoadingAnimation.sprite = Utils.LoadSprite("EHR.Resources.Loading.png", 300f);

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
                    var tempButton = Object.Instantiate(ModManager.Instance.ModStamp);
                    var basePos = tempButton.transform.position;
                    Object.Destroy(tempButton);

                    var x = basePos.x - 9.8f;
                    var y = basePos.y - 4.5f;
                    var z = basePos.z;

                    if (LoadingAnimation.transform.position != new Vector3(x, y, z)) LoadingAnimation.transform.position = new(x, y, z);

                    LoadingAnimation.transform.Rotate(Vector3.forward, 200f * Time.deltaTime);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString(), "LoadingScreen.Update");
            }
        }
    }
}