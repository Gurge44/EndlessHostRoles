using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace TOHE.Modules;

// https://github.com/tukasa0001/TownOfHost/blob/main/Modules/OptionSaver.cs
public static class OptionSaver
{
    private static readonly DirectoryInfo SaveDataDirectoryInfo = new("./TOHE_DATA/SaveData/");
    private static readonly FileInfo OptionSaverFileInfo = new($"{SaveDataDirectoryInfo.FullName}/Options.json");
    private static readonly LogHandler logger = Logger.Handler(nameof(OptionSaver));
    private static readonly FileInfo DefaultPresetFileInfo = new($"{SaveDataDirectoryInfo.FullName}/DefaultPreset.txt");
    private static int DefaultPresetNumber;

    public static int GetDefaultPresetNumber()
    {
        if (DefaultPresetFileInfo.Exists)
        {
            string presetNmber = File.ReadAllText(DefaultPresetFileInfo.FullName);
            if (int.TryParse(presetNmber, out int number) && number >= 0 && number <= 4) return number;
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
        if (!OptionSaverFileInfo.Exists)
        {
            OptionSaverFileInfo.Create().Dispose();
        }
        if (!DefaultPresetFileInfo.Exists)
        {
            DefaultPresetFileInfo.Create().Dispose();
        }
    }
    /// <summary>現在のオプションからjsonシリアライズ用のオブジェクトを生成</summary>
    private static SerializableOptionsData GenerateOptionsData()
    {
        Dictionary<int, int> singleOptions = [];
        Dictionary<int, int[]> presetOptions = [];
        foreach (OptionItem option in OptionItem.AllOptions.ToArray())
        {
            if (option.IsSingleValue)
            {
                if (!singleOptions.TryAdd(option.Id, option.SingleValue))
                {
                    Logger.Warn($"Duplicate SingleOption ID: {option.Id}", "Options Load");
                }
            }
            else if (!presetOptions.TryAdd(option.Id, option.AllValues))
            {
                Logger.Warn($"Duplicate preset option ID: {option.Id}", "Options Load");
            }
        }
        DefaultPresetNumber = singleOptions[0];
        return new SerializableOptionsData
        {
            Version = Version,
            SingleOptions = singleOptions,
            PresetOptions = presetOptions,
        };
    }
    /// <summary>デシリアライズされたオブジェクトを読み込み，オプション値を設定</summary>
    private static void LoadOptionsData(SerializableOptionsData serializableOptionsData)
    {
        if (serializableOptionsData.Version != Version)
        {
            // 今後バージョン間の移行方法を用意する場合，ここでバージョンごとの変換メソッドに振り分ける
            logger.Info($"読み込まれたオプションのバージョン {serializableOptionsData.Version} が現在のバージョン {Version} と一致しないためデフォルト値で上書きします");
            Save();
            return;
        }
        Dictionary<int, int> singleOptions = serializableOptionsData.SingleOptions;
        Dictionary<int, int[]> presetOptions = serializableOptionsData.PresetOptions;
        foreach (var singleOption in singleOptions)
        {
            var id = singleOption.Key;
            var value = singleOption.Value;
            if (OptionItem.FastOptions.TryGetValue(id, out var optionItem))
            {
                optionItem.SetValue(value, doSave: false);
            }
        }
        foreach (var presetOption in presetOptions)
        {
            var id = presetOption.Key;
            var values = presetOption.Value;
            if (OptionItem.FastOptions.TryGetValue(id, out var optionItem))
            {
                optionItem.SetAllValues(values);
            }
        }
    }
    /// <summary>現在のオプションをjsonファイルに保存</summary>
    public static void Save()
    {
        if (AmongUsClient.Instance != null && !AmongUsClient.Instance.AmHost) return;

        var jsonString = JsonSerializer.Serialize(GenerateOptionsData(), new JsonSerializerOptions { WriteIndented = true, });
        File.WriteAllText(OptionSaverFileInfo.FullName, jsonString);
        File.WriteAllText(DefaultPresetFileInfo.FullName, DefaultPresetNumber.ToString());
    }
    /// <summary>jsonファイルからオプションを読み込み</summary>
    public static void Load()
    {
        var jsonString = File.ReadAllText(OptionSaverFileInfo.FullName);
        // 空なら読み込まず，デフォルト値をセーブする
        if (jsonString.Length <= 0)
        {
            logger.Info("オプションデータが空のためデフォルト値を保存");
            Save();
            return;
        }
        LoadOptionsData(JsonSerializer.Deserialize<SerializableOptionsData>(jsonString));
    }

    /// <summary>json保存に適したオプションデータ</summary>
    public class SerializableOptionsData
    {
        public int Version { get; init; }
        /// <summary>プリセット外のオプション</summary>
        public Dictionary<int, int> SingleOptions { get; init; }
        /// <summary>プリセット内のオプション</summary>
        public Dictionary<int, int[]> PresetOptions { get; init; }
    }

    /// <summary>オプションの形式に互換性のない変更(プリセット数変更など)を加えるときはここの数字を上げる</summary>
    public static readonly int Version;
}
