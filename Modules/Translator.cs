using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
#if !ANDROID
using Il2CppInterop.Runtime.InteropTypes.Arrays;
#endif

namespace EHR;

public static class Translator
{
    private const string LanguageFolderName = "Language";
    private static Dictionary<string, Dictionary<int, string>> TranslateMaps;
    public static Dictionary<CustomRoles, Dictionary<SupportedLangs, string>> OriginalRoleNames;

    public static void Init()
    {
        Logger.Info("Loading Custom Translations...", "Translator");
        LoadLangs();
        Logger.Info("Loaded Custom Translations", "Translator");
    }

    public static void LoadLangs()
    {
        try
        {
            // Get the directory containing the JSON files (e.g., EHR.Resources.Lang)
            var jsonDirectory = "EHR.Resources.Lang";
            // Get the assembly containing the resources
            var assembly = Assembly.GetExecutingAssembly();
            string[] jsonFileNames = GetJsonFileNames(assembly, jsonDirectory);

            TranslateMaps = [];

            if (jsonFileNames.Length == 0)
            {
                Logger.Warn("Json Translation files does not exist.", "Translator");
                return;
            }

            foreach (string jsonFileName in jsonFileNames)
            {
                // Read the JSON file content
                using Stream resourceStream = assembly.GetManifestResourceStream(jsonFileName);

                if (resourceStream != null)
                {
                    using StreamReader reader = new(resourceStream);
                    string jsonContent = reader.ReadToEnd();

                    // Deserialize the JSON into a dictionary
                    var jsonDictionary = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonContent);

                    if (jsonDictionary.TryGetValue("LanguageID", out string languageIdObj) && int.TryParse(languageIdObj, out int languageId))
                    {
                        // Remove the "LanguageID" entry
                        jsonDictionary.Remove("LanguageID");

                        // Handle the rest of the data and merge it into the resulting translation map
                        MergeJsonIntoTranslationMap(TranslateMaps, languageId, jsonDictionary);
                    }
                    else
                    {
                        //Logger.Warn(jsonDictionary["HostText"], "Translator");
                        Logger.Warn($"Invalid JSON format in {jsonFileName}: Missing or invalid 'LanguageID' field.", "Translator");
                    }
                }
            }

            // Convert the resulting translation map to JSON
            JsonSerializer.Serialize(TranslateMaps, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch (Exception ex) { Logger.Error($"Error: {ex}", "Translator"); }

        // Loading custom translation files
        if (!Directory.Exists($"{Main.DataPath}/{LanguageFolderName}")) Directory.CreateDirectory($"{Main.DataPath}/{LanguageFolderName}");

        try { OriginalRoleNames = Enum.GetValues<CustomRoles>().ToDictionary(x => x, x => Enum.GetValues<SupportedLangs>().ToDictionary(s => s, s => GetString($"{x}", s))); }
        catch (Exception e) { Utils.ThrowException(e); }
        
        // Creating a translation template
        CreateTemplateFile();

        foreach (SupportedLangs lang in Enum.GetValues<SupportedLangs>())
        {
            if (File.Exists($"{Main.DataPath}/{LanguageFolderName}/{lang}.dat"))
            {
                UpdateCustomTranslation($"{lang}.dat" /*, lang*/);
                LoadCustomTranslation($"{lang}.dat", lang);
            }
        }
    }

    private static void MergeJsonIntoTranslationMap(Dictionary<string, Dictionary<int, string>> translationMaps, int languageId, Dictionary<string, string> jsonDictionary)
    {
        foreach (KeyValuePair<string, string> kvp in jsonDictionary)
        {
            string textString = kvp.Key;

            if (kvp.Value is { } translation)
            {
                // If the textString is not already in the translation map, add it
                if (!translationMaps.ContainsKey(textString)) translationMaps[textString] = [];

                // Add or update the translation for the current id and textString
                translationMaps[textString][languageId] = translation.Replace("\\n", "\n").Replace("\\r", "\r");
            }
        }
    }

    // Function to get a list of JSON file names in a directory
    private static string[] GetJsonFileNames(Assembly assembly, string directoryName)
    {
        string[] resourceNames = assembly.GetManifestResourceNames();
        return resourceNames.Where(resourceName => resourceName.StartsWith(directoryName) && resourceName.EndsWith(".json")).ToArray();
    }

    public static string GetString(string s, Dictionary<string, string> replacementDic = null, bool console = false)
    {
        if (SubmergedCompatibility.IsSubmerged() && int.TryParse(s, out int roomNumber) && roomNumber is >= 128 and <= 135)
            s = $"SubmergedRoomName.{roomNumber}";
        
        if (GameStates.InGame && Options.CurrentGameMode == CustomGameMode.Deathrace && int.TryParse(s, out roomNumber) && Deathrace.CoordinateChecks.ContainsKey(roomNumber))
            s = "Deathrace.CoordinateCheck";
        
        SupportedLangs langId = TranslationController.InstanceExists ? TranslationController.Instance.currentLanguage.languageID : SupportedLangs.English;
        if (console) langId = SupportedLangs.English;

        if (Main.ForceOwnLanguage.Value) langId = GetUserTrueLang();

        int modLanguageId = 0;

        if (Options.IsLoaded)
        {
            modLanguageId = Options.ModLanguage.GetValue();
            if (modLanguageId != 0) langId = (SupportedLangs)(modLanguageId + 100 - 1);
        }

        string str = GetString(s, langId);

        if (replacementDic != null)
        {
            foreach (KeyValuePair<string, string> rd in replacementDic)
                str = str.Replace(rd.Key, rd.Value);
        }
        
        if (modLanguageId == 1) // Hungarian (none of the fonts support ő/ű and innersloth doesn't care, thankfully at least German has ö/ü)
            str = str.Replace("ő", "ö", StringComparison.CurrentCultureIgnoreCase).Replace("ű", "ü", StringComparison.CurrentCultureIgnoreCase);

        return str;
    }

    public static string GetString(string str, SupportedLangs langId)
    {
        var res = $"*{str}";

        try
        {
            if (TranslateMaps.TryGetValue(str, out Dictionary<int, string> dic) && (!dic.TryGetValue((int)langId, out res) || string.IsNullOrEmpty(res) || (langId is not SupportedLangs.SChinese and not SupportedLangs.TChinese && Regex.IsMatch(res, @"[\u4e00-\u9fa5]") && res == GetString(str, SupportedLangs.SChinese))))
                res = langId == SupportedLangs.English ? $"*{str}" : GetString(str, SupportedLangs.English);

            if (!TranslateMaps.ContainsKey(str) && Enum.GetValues<StringNames>().FindFirst(x => x.ToString() == str, out StringNames stringNames))
                res = GetString(stringNames);
        }
        catch (Exception ex)
        {
            Logger.Fatal($"Error oucured at [{str}] in the translation file", "Translator");
            Logger.Error("Here was the error:\n" + ex, "Translator");
        }

        return res;
    }

    public static string GetString(StringNames stringName)
    {
#if ANDROID
        return TranslationController.Instance.GetString(stringName);
#else
        return TranslationController.Instance.GetString(stringName, new Il2CppReferenceArray<Il2CppSystem.Object>(0));
#endif
    }

    public static string GetRoleString(string str, bool forUser = true)
    {
        SupportedLangs currentLanguage = TranslationController.Instance.currentLanguage.languageID;
        SupportedLangs lang = forUser ? currentLanguage : SupportedLangs.English;
        if (Main.ForceOwnLanguageRoleName.Value) lang = GetUserTrueLang();

        return GetString(str, lang);
    }

    public static SupportedLangs GetUserTrueLang()
    {
        try
        {
            string name = CultureInfo.CurrentUICulture.Name;
            if (name.StartsWith("en")) return SupportedLangs.English;
            if (name.StartsWith("zh_CHT")) return SupportedLangs.TChinese;
            if (name.StartsWith("zh")) return SupportedLangs.SChinese;
            if (name.StartsWith("ru")) return SupportedLangs.Russian;
            return TranslationController.Instance.currentLanguage.languageID;
        }
        catch { return SupportedLangs.English; }
    }

    private static void UpdateCustomTranslation(string filename /*, SupportedLangs lang*/)
    {
        var path = $"{Main.DataPath}/{LanguageFolderName}/{filename}";

        if (File.Exists(path))
        {
            Logger.Info("Updating Custom Translations", "UpdateCustomTranslation");

            try
            {
                List<string> textStrings = [];

                using (StreamReader reader = new(path, Encoding.GetEncoding("UTF-8")))
                {
                    while (reader.ReadLine() is { } line)
                    {
                        // Split the line by ':' to get the first part
                        string[] parts = line.Split(':');

                        // Check if there is at least one part before ':'
                        if (parts.Length >= 1)
                        {
                            // Trim any leading or trailing spaces and add it to the list
                            string textString = parts[0].Trim();
                            textStrings.Add(textString);
                        }
                    }
                }

                var sb = new StringBuilder();

                foreach (string templateString in TranslateMaps.Keys)
                {
                    if (!textStrings.Contains(templateString))
                        sb.Append($"{templateString}:\n");
                }

                using FileStream fileStream = new(path, FileMode.Append, FileAccess.Write);
                using StreamWriter writer = new(fileStream);
                writer.WriteLine(sb.ToString());
            }
            catch (Exception e) { Logger.Error("An error occurred: " + e.Message, "Translator"); }
        }
    }

    private static void LoadCustomTranslation(string filename, SupportedLangs lang)
    {
        var path = $"{Main.DataPath}/{LanguageFolderName}/{filename}";

        if (File.Exists(path))
        {
            Logger.Info($"Loading Custom Translation File: {filename}", "LoadCustomTranslation");

            try
            {
                using StreamReader sr = new(path, Encoding.GetEncoding("UTF-8"));

                while (sr.ReadLine() is { } text)
                {
                    string[] tmp = text.Split(':');

                    if (tmp.Length > 1 && tmp[1] != "")
                    {
                        try { TranslateMaps[tmp[0]][(int)lang] = string.Join(':', tmp[1..]).Replace("\\n", "\n").Replace("\\r", "\r"); }
                        catch (KeyNotFoundException) { Logger.Warn($"Invalid Key: {tmp[0]}", "LoadCustomTranslation"); }
                    }
                }
            }
            catch (ObjectDisposedException) { }
            catch (Exception e) { Logger.Error(e.ToString(), "Translator.LoadCustomTranslation"); }
        }
        else
            Logger.Error($"Custom Translation File Not Found: {filename}", "LoadCustomTranslation");
    }

    private static void CreateTemplateFile()
    {
        File.WriteAllText($"{Main.DataPath}/{LanguageFolderName}/template.dat", string.Join('\n', TranslateMaps.Keys.Select(x => $"{x}:")));
    }

    public static void ExportCustomTranslation()
    {
        LoadLangs();
        var sb = new StringBuilder();
        SupportedLangs lang = TranslationController.Instance.currentLanguage.languageID;

        foreach (KeyValuePair<string, Dictionary<int, string>> title in TranslateMaps)
        {
            string text = title.Value.GetValueOrDefault((int)lang, "");
            sb.Append($"{title.Key}:{text.Replace("\n", "\\n").Replace("\r", "\\r")}\n");
        }

        File.WriteAllText($"{Main.DataPath}/{LanguageFolderName}/export_{lang}.dat", sb.ToString());
    }

    public static string FixRoleName(this string infoLong, CustomRoles role)
    {
        return OriginalRoleNames.TryGetValue(role, out var d) && d.TryGetValue(GetUserTrueLang(), out var o) ? infoLong.Replace(o, role.ToColoredString(), StringComparison.OrdinalIgnoreCase) : infoLong;
    }
}