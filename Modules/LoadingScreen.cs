using UnityEngine;

namespace TOHE.Modules
{
    internal class LoadingScreen
    {
        public static SpriteRenderer LoadingAnimation;

        public static void UpdateLoadingAnimation()
        {
            try
            {
                LoadingAnimation = null;
                var tempButton = Object.Instantiate(ModManager.Instance.ModStamp);
                tempButton.sprite = Utils.LoadSprite("TOHE.Resources.Loading.png", 300f);
                LoadingAnimation = tempButton;

                var basePos = LoadingAnimation.transform.position;
                var x = basePos.x - 9.5f;
                var y = basePos.y - 3.5f;
                var z = basePos.z;
                LoadingAnimation.transform.position = new Vector3(x, y, z);
            }
            catch (System.Exception ex)
            {
                Logger.Error(ex.ToString(), "LoadingScreen.UpdateLoadingAnimation");
            }
        }

        public static void Update()
        {
            try
            {
                bool visible = AmongUsClient.Instance.IsGameStarted && !GameStates.IsCanMove && !GameStates.IsMeeting && !HudManager.Instance.Chat.IsOpenOrOpening && !PlayerControl.LocalPlayer.inVent && !PlayerControl.LocalPlayer.MyPhysics.Animations.IsPlayingSomeAnimation();

                if (LoadingAnimation == null && visible)
                {
                    UpdateLoadingAnimation();
                    return;
                }
                else if (!visible && LoadingAnimation != null)
                {
                    LoadingAnimation.forceRenderingOff = true;
                    LoadingAnimation.enabled = false;
                    LoadingAnimation = null;
                    return;
                }

                if (LoadingAnimation != null) LoadingAnimation?.transform?.Rotate(Vector3.forward, 200f * Time.deltaTime);
            }
            catch (System.Exception ex)
            {
                Logger.Error(ex.ToString(), "LoadingScreen.Update");
            }
        }
    }
}
