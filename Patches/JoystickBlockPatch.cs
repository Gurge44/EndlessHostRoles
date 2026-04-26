using System;
using HarmonyLib;
using UnityEngine;

namespace EHR.Patches;

[HarmonyPatch]
public static class JoystickBlockPatch
{
    private static bool ShouldBlockJoystickInput()
    {
        if (!OperatingSystem.IsAndroid()) return false;
        if (!ClientControlGUI.Instance || !ClientControlGUI.Instance.IsOpen) return false;

        foreach (Touch touch in Input.touches)
        {
            Vector2 guiPos = new(touch.position.x, Screen.height - touch.position.y);
            if (ClientControlGUI.PanelRect.Contains(guiPos) || ClientControlGUI.ToggleRect.Contains(guiPos))
                return true;
        }

        return false;
    }

    [HarmonyPatch(typeof(VirtualJoystickController), nameof(VirtualJoystickController.CheckDrag))]
    [HarmonyPrefix]
    public static bool VirtualJoystickCheckDrag(ref DragState __result)
    {
        if (!ShouldBlockJoystickInput()) return true;
        __result = DragState.NoTouch;
        return false;
    }

    [HarmonyPatch(typeof(ScreenJoystick), "FixedUpdate")]
    [HarmonyPrefix]
    public static bool ScreenJoystickFixedUpdate() => !ShouldBlockJoystickInput();
}
