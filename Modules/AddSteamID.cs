#if !ANDROID
using System;
#endif

namespace EHR;

public static class AddSteamID
{
    private const string FilePath = "./steam_appid.txt";

    public static void AddSteamAppIdFile()
    {
#if !ANDROID
        try
        {
            if (!File.Exists(FilePath))
            {
                Logger.Warn("Creating a new steam_appid.txt file", "AddSteamID");
                File.Create(FilePath).Close();
                File.WriteAllText(FilePath, "945360");

                ModUpdater.ShowPopup(Translator.GetString("AppIDAdded"), StringNames.Close, true);
            }
        }
        catch (Exception e) { Utils.ThrowException(e); }
#endif
    }
}