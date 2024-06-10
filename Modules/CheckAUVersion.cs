using System;
using UnityEngine;

namespace EHR.Modules;

// https://github.com/tukasa0001/TownOfHost/blob/main/Modules/VersionChecker.cs
public static class VersionChecker
{
    public static bool IsSupported { get; private set; } = true;

    public static void Check()
    {
        var AmongUsVersion = Version.Parse(Application.version);
        Logger.Info($" {AmongUsVersion}", "Among Us Version Check");

        var SupportedVersion = Version.Parse(Main.SupportedAUVersion);
        Logger.Info($" {SupportedVersion}", "Supported Version Check");

        IsSupported = AmongUsVersion >= SupportedVersion;
        Logger.Info($" {IsSupported}", "Version Is Supported?");

        if (!IsSupported)
        {
            ErrorText.Instance.AddError(ErrorCode.UnsupportedVersion);
        }
    }
}