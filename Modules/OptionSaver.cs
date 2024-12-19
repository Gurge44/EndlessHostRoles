using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace EHR.Modules;

// https://github.com/tukasa0001/TownOfHost/blob/main/Modules/OptionSaver.cs
public static class OptionSaver
{
    private static readonly DirectoryInfo SaveDataDirectoryInfo = new("./EHR_DATA/SaveData/");
    private static readonly FileInfo OptionSaverFileInfo = new($"{SaveDataDirectoryInfo.FullName}/Options.json");
    private static readonly LogHandler Logger = EHR.Logger.Handler(nameof(OptionSaver));
    private static readonly FileInfo DefaultPresetFileInfo = new($"{SaveDataDirectoryInfo.FullName}/DefaultPreset.txt");
    private static int DefaultPresetNumber;

    public static readonly int Version;

    public static int GetDefaultPresetNumber()
    {
        if (DefaultPresetFileInfo.Exists)
        {
            string presetNmber = File.ReadAllText(DefaultPresetFileInfo.FullName);
            if (int.TryParse(presetNmber, out int number) && number is >= 0 and <= OptionItem.NumPresets - 1) return number;
        }

        return 0;
    }

    public static void Initialize()
    {
        if (!SaveDataDirectoryInfo.Exists)
        {
            SaveDataDirectoryInfo.Create();
            SaveDataDirectoryInfo.Attributes |= FileAttributes.Hidden;
        }

        if (!OptionSaverFileInfo.Exists) OptionSaverFileInfo.Create().Dispose();

        if (!DefaultPresetFileInfo.Exists) DefaultPresetFileInfo.Create().Dispose();
    }

    private static SerializableOptionsData GenerateOptionsData()
    {
        Dictionary<int, int> singleOptions = [];
        Dictionary<int, int[]> presetOptions = [];

        foreach (OptionItem option in OptionItem.AllOptions)
        {
            if (option.IsSingleValue)
            {
                if (!singleOptions.TryAdd(option.Id, option.SingleValue)) EHR.Logger.Warn($"Duplicate SingleOption ID: {option.Id}", "Options Load");
            }
            else if (!presetOptions.TryAdd(option.Id, option.AllValues)) EHR.Logger.Warn($"Duplicate preset option ID: {option.Id}", "Options Load");
        }

        DefaultPresetNumber = singleOptions[0];

        return new()
        {
            Version = Version,
            SingleOptions = singleOptions,
            PresetOptions = presetOptions
        };
    }

    private static void LoadOptionsData(SerializableOptionsData serializableOptionsData)
    {
        if (serializableOptionsData.Version != Version)
        {
            Logger.Info($"Loaded option version {serializableOptionsData.Version} does not match current version {Version}, overwriting with default value");
            Save();
            return;
        }

        Dictionary<int, int> singleOptions = serializableOptionsData.SingleOptions;
        Dictionary<int, int[]> presetOptions = serializableOptionsData.PresetOptions;

        foreach ((int id, int value) in singleOptions)
            if (OptionItem.FastOptions.TryGetValue(id, out OptionItem optionItem))
                optionItem.SetValue(value, doSave: false);

        foreach ((int id, int[] values) in presetOptions)
            if (OptionItem.FastOptions.TryGetValue(id, out OptionItem optionItem))
                optionItem.SetAllValues(values);
    }

    public static void Save()
    {
        if (AmongUsClient.Instance != null && !AmongUsClient.Instance.AmHost) return;

        string jsonString = JsonSerializer.Serialize(GenerateOptionsData(), new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(OptionSaverFileInfo.FullName, jsonString);
        File.WriteAllText(DefaultPresetFileInfo.FullName, DefaultPresetNumber.ToString());
    }

    public static void Load()
    {
        string jsonString = File.ReadAllText(OptionSaverFileInfo.FullName);

        if (jsonString.Length <= 0)
        {
            Logger.Info("Save default value as option data is empty");
            Save();
            return;
        }

        LoadOptionsData(JsonSerializer.Deserialize<SerializableOptionsData>(jsonString));
    }

    public class SerializableOptionsData
    {
        public int Version { get; init; }
        public Dictionary<int, int> SingleOptions { get; init; }
        public Dictionary<int, int[]> PresetOptions { get; init; }
    }
}