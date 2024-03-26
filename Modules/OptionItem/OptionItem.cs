using EHR.Modules;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EHR;

public abstract class OptionItem
{
    #region static

    public static IReadOnlyList<OptionItem> AllOptions => _allOptions;
    private static readonly List<OptionItem> _allOptions = new(1024);
    public static IReadOnlyDictionary<int, OptionItem> FastOptions => _fastOptions;
    private static readonly Dictionary<int, OptionItem> _fastOptions = new(1024);
    public static int CurrentPreset { get; set; }

    #endregion

    // 必須情報 (コンストラクタで必ず設定させる必要がある値)
    public int Id { get; }
    public string Name { get; }
    public int DefaultValue { get; }
    public TabGroup Tab { get; }
    public bool IsSingleValue { get; }

    // 任意情報 (空・nullを許容する または、ほとんど初期値で問題ない値)
    public Color NameColor { get; protected set; }
    public OptionFormat ValueFormat { get; protected set; }
    public CustomGameMode GameMode { get; protected set; }
    public bool IsHeader { get; protected set; }
    public bool IsHidden { get; protected set; }
    public bool IsText { get; protected set; }

    public Dictionary<string, string> ReplacementDictionary
    {
        get => _replacementDictionary;
        set
        {
            if (value == null) _replacementDictionary?.Clear();
            else _replacementDictionary = value;
        }
    }

    private Dictionary<string, string> _replacementDictionary;

    public int[] AllValues { get; private set; } = new int[NumPresets];

    public int CurrentValue
    {
        get => GetValue();
        set => SetValue(value);
    }

    public int SingleValue { get; private set; }

    // 親子情報
    public OptionItem Parent { get; private set; }
    public List<OptionItem> Children;

    public OptionBehaviour OptionBehaviour;

    // イベント
    // eventキーワードにより、クラス外からのこのフィールドに対する以下の操作は禁止されます。
    // - 代入 (+=, -=を除く)
    // - 直接的な呼び出し
    public event EventHandler<UpdateValueEventArgs> UpdateValueEvent;

    // コンストラクタ
    public OptionItem(int id, string name, int defaultValue, TabGroup tab, bool isSingleValue)
    {
        // 必須情報の設定
        Id = id;
        Name = name;
        DefaultValue = defaultValue;
        Tab = tab;
        IsSingleValue = isSingleValue;

        // 任意情報の初期値設定
        NameColor = Color.white;
        ValueFormat = OptionFormat.None;
        GameMode = CustomGameMode.All;
        IsHeader = false;
        IsHidden = false;
        IsText = false;

        // オブジェクト初期化
        Children = [];

        // デフォルト値に設定
        if (Id == PresetId)
        {
            SingleValue = DefaultValue;
            CurrentPreset = SingleValue;
        }
        else if (IsSingleValue)
        {
            SingleValue = DefaultValue;
        }
        else
        {
            for (int i = 0; i < NumPresets; i++)
            {
                AllValues[i] = DefaultValue;
            }
        }

        if (_fastOptions.TryAdd(id, this))
        {
            _allOptions.Add(this);
        }
        else
        {
            Logger.Error($"Duplicate ID: {id} ({name})", "OptionItem");
        }
    }

    // Setter
    public OptionItem Do(Action<OptionItem> action)
    {
        action(this);
        return this;
    }

    public OptionItem SetColor(Color value) => Do(i => i.NameColor = value);
    public OptionItem SetValueFormat(OptionFormat value) => Do(i => i.ValueFormat = value);
    public OptionItem SetGameMode(CustomGameMode value) => Do(i => i.GameMode = value);
    public OptionItem SetHeader(bool value) => Do(i => i.IsHeader = value);
    public OptionItem SetHidden(bool value) => Do(i => i.IsHidden = value);
    public OptionItem SetText(bool value) => Do(i => i.IsText = value);

    public OptionItem SetParent(OptionItem parent) => Do(i =>
    {
        foreach (var role in Options.CustomRoleSpawnChances.Where(x => x.Value.Name == parent.Name))
        {
            var roleName = Translator.GetString(Enum.GetName(typeof(CustomRoles), role.Key));
            ReplacementDictionary ??= [];
            ReplacementDictionary.TryAdd(roleName, Utils.ColorString(Utils.GetRoleColor(role.Key), roleName));
            break;
        }

        i.Parent = parent;
        parent.SetChild(i);
    });

    public OptionItem SetChild(OptionItem child) => Do(i => i.Children.Add(child));

    public OptionItem RegisterUpdateValueEvent(EventHandler<UpdateValueEventArgs> handler)
        => Do(i => UpdateValueEvent += handler);

    // 置き換え辞書
    public OptionItem AddReplacement((string key, string value) kvp)
        => Do(i =>
        {
            ReplacementDictionary ??= [];
            ReplacementDictionary.Add(kvp.key, kvp.value);
        });

    public OptionItem RemoveReplacement(string key)
        => Do(i => ReplacementDictionary?.Remove(key));

    // Getter
    public virtual string GetName(bool disableColor = false, bool console = false)
    {
        return disableColor ? Translator.GetString(Name, ReplacementDictionary, console) : Utils.ColorString(NameColor, Translator.GetString(Name, ReplacementDictionary));
    }

    public virtual bool GetBool() => CurrentValue != 0 && (Parent == null || Parent.GetBool());
    public virtual int GetInt() => CurrentValue;
    public virtual float GetFloat() => CurrentValue;

    public virtual string GetString()
    {
        return ApplyFormat(CurrentValue.ToString());
    }

    public virtual int GetValue() => IsSingleValue ? SingleValue : AllValues[CurrentPreset];

    // 旧IsHidden関数
    public virtual bool IsHiddenOn(CustomGameMode mode)
    {
        return IsHidden || (GameMode != CustomGameMode.All && GameMode != mode);
    }

    public string ApplyFormat(string value)
    {
        if (ValueFormat == OptionFormat.None) return value;
        return string.Format(Translator.GetString("Format." + ValueFormat), value);
    }

    // 外部からの操作
    public virtual void Refresh()
    {
        if (OptionBehaviour is not null and StringOption opt)
        {
            opt.TitleText.text = GetName();
            opt.ValueText.text = GetString();
            opt.oldValue = opt.Value = CurrentValue;
        }
    }

    public void SetValue(int afterValue, bool doSave, bool doSync = true)
    {
        int beforeValue = CurrentValue;
        if (IsSingleValue)
        {
            SingleValue = afterValue;
        }
        else
        {
            AllValues[CurrentPreset] = afterValue;
        }

        CallUpdateValueEvent(beforeValue, afterValue);
        Refresh();
        if (doSync)
        {
            SyncAllOptions();
        }

        if (doSave)
        {
            OptionSaver.Save();
        }
    }

    public virtual void SetValue(int afterValue, bool doSync = true)
    {
        SetValue(afterValue, true, doSync);
    }

    public void SetAllValues(int[] values)
    {
        AllValues = values;
    }

    // 演算子オーバーロード
    public static OptionItem operator ++(OptionItem item)
        => item.Do(item => item.SetValue(item.CurrentValue + 1));

    public static OptionItem operator --(OptionItem item)
        => item.Do(item => item.SetValue(item.CurrentValue - 1));

    // 全体操作用
    public static void SwitchPreset(int newPreset)
    {
        CurrentPreset = Math.Clamp(newPreset, 0, NumPresets - 1);

        foreach (OptionItem op in AllOptions.ToArray())
        {
            op.Refresh();
        }

        SyncAllOptions();
    }

    public static void SyncAllOptions(int targetId = -1)
    {
        if (
            Main.AllPlayerControls.Length <= 1
            || AmongUsClient.Instance.AmHost == false
            || PlayerControl.LocalPlayer == null
        ) return;

        RPC.SyncCustomSettingsRPC(targetId);
    }


    // EventArgs
    private void CallUpdateValueEvent(int beforeValue, int currentValue)
    {
        if (UpdateValueEvent == null) return;
        try
        {
            UpdateValueEvent(this, new(beforeValue, currentValue));
        }
        catch (Exception ex)
        {
            Logger.Error($"[{Name}] - Exception occurred when calling UpdateValueEvent", "OptionItem.UpdateValueEvent");
            Logger.Exception(ex, "OptionItem.UpdateValueEvent");
        }
    }

    public class UpdateValueEventArgs(int beforeValue, int currentValue) : EventArgs
    {
        public int CurrentValue { get; set; } = currentValue;
        public int BeforeValue { get; set; } = beforeValue;
    }

    public const int NumPresets = 5;
    public const int PresetId = 0;
}

public enum TabGroup
{
    SystemSettings,
    GameSettings,
    TaskSettings,
    ImpostorRoles,
    CrewmateRoles,
    NeutralRoles,
    Addons,
    OtherRoles
}

public enum OptionFormat
{
    None,
    Players,
    Seconds,
    Percent,
    Times,
    Multiplier,
    Votes,
    Pieces,
    Health,
    Level,
}