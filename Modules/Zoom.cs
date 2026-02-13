using System;
using System.Collections.Generic;
using UnityEngine;

namespace EHR;

// Credit: https://github.com/tugaru1975/TownOfPlus/TOPmods/Zoom.cs 
// Credit: https://github.com/Yumenopai/TownOfHost_Y
//[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class Zoom
{
    private static bool ResetButtons;

    public static void Postfix()
    {
        try
        {
            if (!Camera.main) return;

            if (((GameStates.IsShip && !GameStates.IsMeeting && GameStates.IsCanMove && !PlayerControl.LocalPlayer.IsAlive()) || (GameStates.IsLobby && GameStates.IsCanMove)) && !InGameRoleInfoMenu.Showing)
            {
                if (Camera.main.orthographicSize > 3.0f) ResetButtons = true;

                if (Input.touchSupported)
                {
                    if (Input.touchCount == 2)
                    {
                        Touch touch0 = Input.GetTouch(0);
                        Touch touch1 = Input.GetTouch(1);

                        Vector2 touch0PrevPos = touch0.position - touch0.deltaPosition;
                        Vector2 touch1PrevPos = touch1.position - touch1.deltaPosition;

                        float prevTouchDeltaMag = (touch0PrevPos - touch1PrevPos).magnitude;
                        float currentTouchDeltaMag = (touch0.position - touch1.position).magnitude;
                        float deltaMagnitudeDiff = currentTouchDeltaMag - prevTouchDeltaMag;

                        switch (deltaMagnitudeDiff)
                        {
                            case > 0:
                            {
                                if (Camera.main.orthographicSize > 3.0f)
                                    SetZoomSize();

                                break;
                            }
                            case < 0:
                            {
                                if (GameStates.IsDead || GameStates.IsFreePlay || DebugModeManager.AmDebugger || GameStates.IsLobby)
                                {
                                    if (Camera.main.orthographicSize < 18.0f)
                                        SetZoomSize(true);
                                }

                                break;
                            }
                        }
                    }
                }

                switch (Input.mouseScrollDelta.y)
                {
                    case > 0:
                    {
                        if (Camera.main.orthographicSize > 3.0f)
                            SetZoomSize();

                        break;
                    }
                    case < 0:
                    {
                        if (GameStates.IsDead || GameStates.IsFreePlay || DebugModeManager.AmDebugger || GameStates.IsLobby)
                        {
                            if (Camera.main.orthographicSize < 18.0f)
                                SetZoomSize(true);
                        }

                        break;
                    }
                }
                Flag.NewFlag("Zoom");
            }
            else
                Flag.Run(() => { SetZoomSize(reset: true); }, "Zoom");
        }
        catch { }
    }

    public static void SetZoomSize(bool times = false, bool reset = false)
    {
        if (!Camera.main || !HudManager.InstanceExists) return;

        var size = 1.5f;
        if (!times) size = 1 / size;

        if (reset)
        {
            Camera.main.orthographicSize = 3.0f;
            HudManager.Instance.UICamera.orthographicSize = 3.0f;
            HudManager.Instance.Chat.transform.localScale = Vector3.one;
            if (GameStates.IsMeeting) MeetingHud.Instance.transform.localScale = Vector3.one;
        }
        else
        {
            Camera.main.orthographicSize *= size;
            HudManager.Instance.UICamera.orthographicSize *= size;
        }

        HudManager.Instance?.ShadowQuad?.gameObject.SetActive((reset || Mathf.Approximately(Camera.main.orthographicSize, 3.0f)) && PlayerControl.LocalPlayer.IsAlive());

        if (ResetButtons)
        {
            ResolutionManager.ResolutionChanged.Invoke((float)Screen.width / Screen.height, Screen.width, Screen.height, Screen.fullScreen);
            ResetButtons = false;
        }
    }

    public static void OnFixedUpdate()
    {
        if (!Camera.main || !HudManager.InstanceExists) return;

        HudManager.Instance?.ShadowQuad?.gameObject.SetActive(Mathf.Approximately(Camera.main.orthographicSize, 3.0f) && PlayerControl.LocalPlayer.IsAlive());
    }
}

public static class Flag
{
    private static readonly List<string> OneTimeList = [];
    private static readonly List<string> FirstRunList = [];

    public static void Run(Action action, string type, bool firstrun = false)
    {
        if (OneTimeList.Contains(type) || (firstrun && !FirstRunList.Contains(type)))
        {
            if (!FirstRunList.Contains(type)) FirstRunList.Add(type);

            OneTimeList.Remove(type);
            action();
        }
    }

    public static void NewFlag(string type)
    {
        if (!OneTimeList.Contains(type)) OneTimeList.Add(type);
    }
}