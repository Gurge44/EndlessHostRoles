using UnityEngine;

namespace EHR;

public static class OperatingSystem
{
    private static readonly bool Windows = Application.platform == RuntimePlatform.WindowsPlayer;
    private static readonly bool Android = Application.platform == RuntimePlatform.Android;

    public static bool IsWindows() => Windows;
    public static bool IsAndroid() => Android;
}