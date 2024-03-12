﻿using System.IO;
using static TOHE.Translator;

namespace TOHE;

public class AddSteamID
{
        private static readonly string FilePath = @"./steam_appid.txt";
        public static void AddSteamAppIdFile()
        {
            if (!File.Exists(FilePath))
            {
            Logger.Warn("Creating a new steam_appid.txt file", "AddSteamID");
            File.Create(FilePath).Close();
            File.WriteAllText(FilePath, "945360");

            ModUpdater.ShowPopup(GetString("AppIDAdded"), StringNames.Close, true, true);
            }
        }
}
