using System;
using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using UnityEngine;

namespace EHR;

public abstract class OptionItem
{
    public const int NumPresets = 10;
    private const int PresetId = 0;
    public readonly List<OptionItem> Children;

    private Dictionary<string, string> _replacementDictionary;

    public OptionBehaviour OptionBehaviour;

    protected OptionItem(int id, string name, int defaultValue, TabGroup tab, bool isSingleValue)
    {
        Id = id;
        Name = name;
        DefaultValue = defaultValue;
        Tab = tab;
        IsSingleValue = isSingleValue;

        NameColor = Color.white;
        ValueFormat = OptionFormat.None;
        GameMode = CustomGameMode.All;
        IsHeader = false;
        IsHidden = false;
        IsText = false;

        Children = [];

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

        if (FastOpts.TryAdd(id, this))
        {
            Options.Add(this);
        }
        else
        {
            Logger.Error($"Duplicate ID: {id} ({name})", "OptionItem");
        }
    }

    public int Id { get; }
    public string Name { get; }
    public int DefaultValue { get; }
    public TabGroup Tab { get; }
    public bool IsSingleValue { get; }

    private Color NameColor { get; set; }
    private OptionFormat ValueFormat { get; set; }
    public CustomGameMode GameMode { get; private set; }
    public bool IsHeader { get; protected set; }
    private bool IsHidden { get; set; }
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

    public int[] AllValues { get; private set; } = new int[NumPresets];

    public int CurrentValue
    {
        get => GetValue();
        set => SetValue(value);
    }

    public int SingleValue { get; private set; }

    public OptionItem Parent { get; private set; }

    public event EventHandler<UpdateValueEventArgs> UpdateValueEvent;

    // Setter
    private OptionItem Do(Action<OptionItem> action)
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
        foreach (var role in EHR.Options.CustomRoleSpawnChances.Where(x => x.Value.Name == parent.Name))
        {
            var roleName = Translator.GetString(Enum.GetName(typeof(CustomRoles), role.Key));
            ReplacementDictionary ??= [];
            ReplacementDictionary.TryAdd(roleName, Utils.ColorString(Utils.GetRoleColor(role.Key), roleName));
            break;
        }

        i.Parent = parent;
        parent.SetChild(i);
    });

    private OptionItem SetChild(OptionItem child) => Do(i => i.Children.Add(child));

    public OptionItem RegisterUpdateValueEvent(EventHandler<UpdateValueEventArgs> handler)
        => Do(_ => UpdateValueEvent += handler);

    public OptionItem AddReplacement((string key, string value) kvp)
        => Do(_ =>
        {
            ReplacementDictionary ??= [];
            ReplacementDictionary.Add(kvp.key, kvp.value);
        });

    public OptionItem RemoveReplacement(string key)
        => Do(_ => ReplacementDictionary?.Remove(key));

    // Getter
    public virtual string GetName(bool disableColor = false, bool console = false)
    {
        if (Name.Contains("CTA.FLAG"))
        {
            return Utils.ColorString(NameColor, Translator.GetString("CTA.TeamEnabled.Prefix") + Name[8..] + Translator.GetString("CTA.TeamEnabled.Suffix"));
        }

        return disableColor ? Translator.GetString(Name, ReplacementDictionary, console) : Utils.ColorString(NameColor, Translator.GetString(Name, ReplacementDictionary));
    }

    public virtual bool GetBool() => Name switch
    {
        "Bargainer.LensOfTruth.DurationSwitch" => GetValue() == 3 && (Parent == null || Parent.GetBool()),
        "BlackHoleDespawnMode" => GetValue() == 1 && (Parent == null || Parent.GetBool()),
        _ => CurrentValue != 0 && (Parent == null || Parent.GetBool())
    };

    public virtual int GetInt() => CurrentValue;
    public virtual float GetFloat() => CurrentValue;

    public virtual string GetString()
    {
        return ApplyFormat(CurrentValue.ToString());
    }

    public virtual int GetValue() => IsSingleValue ? SingleValue : AllValues[CurrentPreset];

    public virtual bool IsHiddenOn(CustomGameMode mode)
    {
        return CheckHidden() || (GameMode != CustomGameMode.All && GameMode != mode);
    }

    private bool CheckHidden()
    {
        var LastParent = this.Id;


        for (var i = 0; i < 5; i++)
        {
            if (AllOptions.First(x => x.Id == LastParent).Parent == null) break;
            LastParent = AllOptions.First(x => x.Id == LastParent).Parent.Id;
        }

        return this.IsHidden || this.Parent?.IsHidden == true || AllOptions.First(x => x.Id == LastParent).IsHidden;
    }

    protected string ApplyFormat(string value)
    {
        if (ValueFormat == OptionFormat.None) return value;
        return string.Format(Translator.GetString("Format." + ValueFormat), value);
    }

    protected virtual void Refresh()
    {
        if (OptionBehaviour is StringOption opt)
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
        if (values.Length == AllValues.Length) AllValues = values;
        else
        {
            for (int i = 0; i < values.Length; i++)
            {
                AllValues[i] = values[i];
            }
        }
    }

    public static OptionItem operator ++(OptionItem item)
        => item.Do(item => item.SetValue(item.CurrentValue + 1));

    public static OptionItem operator --(OptionItem item)
        => item.Do(item => item.SetValue(item.CurrentValue - 1));

    protected static void SwitchPreset(int newPreset)
    {
        CurrentPreset = Math.Clamp(newPreset, 0, NumPresets - 1);

        foreach (OptionItem op in AllOptions)
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

    #region static

    public static IReadOnlyList<OptionItem> AllOptions => Options;
    private static readonly List<OptionItem> Options = new(1024);
    public static IReadOnlyDictionary<int, OptionItem> FastOptions => FastOpts;
    private static readonly Dictionary<int, OptionItem> FastOpts = new(1024);
    public static int CurrentPreset { get; set; }

    #endregion
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
    Level
}