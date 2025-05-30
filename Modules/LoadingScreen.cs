﻿using System;
using System.Collections.Generic;
using EHR.Patches;
using Rewired.Utils;
using UnityEngine;

namespace EHR.Modules;

internal static class LoadingScreen
{
    private const int HintCount = 40;
    private const int JokeHintCount = 6;
    private static SpriteRenderer LoadingAnimation;
    private static readonly HashSet<int> ToldHints = [];
    private static float HintHideTimer;
    public static string Hint;

    private static void UpdateLoadingAnimation()
    {
        try
        {
            if (!LoadingAnimation.IsNullOrDestroyed()) Object.Destroy(LoadingAnimation);

            LoadingAnimation = Object.Instantiate(ModManager.Instance.ModStamp);
            LoadingAnimation.sprite = Utils.LoadSprite("EHR.Resources.Loading.png", 300f);
            LoadingAnimation.sortingOrder = 100;

            Vector3 basePos = LoadingAnimation.transform.position;
            float x = basePos.x - 9.8f;
            float y = basePos.y - 4.5f;
            float z = basePos.z;
            LoadingAnimation.transform.position = new(x, y, z);
        }
        catch (Exception ex) { Logger.Error(ex.ToString(), "LoadingScreen.UpdateLoadingAnimation"); }
    }

    private static void NewHint()
    {
        if (GameEndChecker.LoadingEndScreen)
        {
            Hint = string.Empty;
            return;
        }

        int index;
        if (ToldHints.Count == HintCount) ToldHints.Clear();

        do index = IRandom.Instance.Next(HintCount);
        while (!ToldHints.Add(index));

        bool joke = IRandom.Instance.Next(20) == 0;
        if (joke) index = IRandom.Instance.Next(40, 40 + JokeHintCount);

        string text = Translator.GetString($"LoadingHint.{index}");
        text = text.Insert(0, joke ? "<color=#ffff00>" : "<color=#00ffa5>");
        if (text.Contains('\n')) text = text.Insert(text.IndexOf('\n'), "</color><#ffffff>");
        text += "</color>";
        Hint = text;
    }

    public static void Update()
    {
        try
        {
            if (HintHideTimer <= 15f) HintHideTimer += Time.deltaTime;

            PlayerControl lp = PlayerControl.LocalPlayer;
            if (lp == null) return;

            PlayerAnimations anims = lp.MyPhysics.Animations;

            bool visible = (AmongUsClient.Instance.IsGameStarted && !GameStates.IsCanMove && (!GameStates.IsInTask || ExileController.Instance) && !GameStates.IsMeeting && !HudManager.Instance.Chat.IsOpenOrOpening && !lp.inVent && !anims.IsPlayingAnyLadderAnimation() && !VentButtonDoClickPatch.Animating && !lp.onLadder) || GameEndChecker.LoadingEndScreen;

            switch (visible)
            {
                case false when LoadingAnimation:
                    Object.Destroy(LoadingAnimation);
                    return;
                case true when !LoadingAnimation:
                    UpdateLoadingAnimation();
                    return;
            }

            if (LoadingAnimation)
            {
                Vector3 basePos = ModManager.Instance.ModStamp.transform.position;

                float x = basePos.x - 9.8f;
                float y = basePos.y - 4.5f;
                float z = basePos.z;

                if (LoadingAnimation.transform.position != new Vector3(x, y, z))
                    LoadingAnimation.transform.position = new(x, y, z);

                LoadingAnimation.transform.Rotate(Vector3.forward, 200f * Time.deltaTime);
            }

            visible &= !GameStates.IsEnded;

            switch (visible)
            {
                case false when ErrorText.HasHint:
                    ErrorText.RemoveHint();
                    return;
                case true when !ErrorText.HasHint:
                    if (HintHideTimer > 15f) NewHint();
                    HintHideTimer = 0f;
                    ErrorText.Instance.AddError(ErrorCode.LoadingHint);
                    break;
            }
        }
        catch (Exception ex) { Logger.Error(ex.ToString(), "LoadingScreen.Update"); }
    }
}