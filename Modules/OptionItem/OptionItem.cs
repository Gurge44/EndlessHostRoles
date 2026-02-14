using System;
using System.Collections.Generic;
using EHR.Modules;
using UnityEngine;

namespace EHR;

public abstract class OptionItem
{
    public const int NumPresets = 10;
    public const int PresetId = 0;
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
            SingleValue = DefaultValue;
        else
        {
            for (var i = 0; i < NumPresets; i++)
                AllValues[i] = DefaultValue;
        }

        if (FastOpts.TryAdd(id, this))
            Options.Add(this);
        else
            Logger.Error($"Duplicate ID: {id} ({name})", "OptionItem");
    }

    public int Id { get; }
    public string Name { get; }
    public int DefaultValue { get; }
    public TabGroup Tab { get; }
    public bool IsSingleValue { get; }

    public Color NameColor { get; set; }
    private OptionFormat ValueFormat { get; set; }
    public CustomGameMode GameMode { get; private set; }
    public bool IsHeader { get; protected set; }
    private bool IsHidden { get; set; }
    public bool IsText { get; protected set; }

    public TextOptionItem Header { get; set; } = null;

    public Dictionary<string, string> ReplacementDictionary
    {
        get => _replacementDictionary;
        set
        {
            if (value == null)
                _replacementDictionary?.Clear();
            else
                _replacementDictionary = value;
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

    public List<Action<OptionItem, int, int>> UpdateValueEvent;
    public bool UpdateValueEventRunsOnLoad { get; private set; }

    // Setter
    private OptionItem Do(Action<OptionItem> action)
    {
        action(this);
        return this;
    }

    public OptionItem SetColor(Color value)
    {
        return Do(i => i.NameColor = value);
    }

    public OptionItem SetValueFormat(OptionFormat value)
    {
        return Do(i => i.ValueFormat = value);
    }

    public OptionItem SetGameMode(CustomGameMode value)
    {
        return Do(i => i.GameMode = value);
    }

    public OptionItem SetHeader(bool value)
    {
        return Do(i => i.IsHeader = value);
    }

    public OptionItem SetHidden(bool value)
    {
        return Do(i => i.IsHidden = value);
    }

    public OptionItem SetText(bool value)
    {
        return Do(i => i.IsText = value);
    }

    public OptionItem SetParent(OptionItem parent)
    {
        return Do(i =>
        {
            foreach (KeyValuePair<CustomRoles, StringOptionItem> role in EHR.Options.CustomRoleSpawnChances)
            {
                if (role.Value.Name == parent.Name)
                {
                    string roleName = Translator.GetString(Enum.GetName(typeof(CustomRoles), role.Key));
                    ReplacementDictionary ??= [];
                    ReplacementDictionary.TryAdd(roleName, Utils.ColorString(Utils.GetRoleColor(role.Key), roleName));
                    break;
                }
            }

            i.Parent = parent;
            parent.SetChild(i);
        });
    }

    private void SetChild(OptionItem child)
    {
        Do(i => i.Children.Add(child));
    }

    /// <summary>
    ///     Register an event that will be called when the value of this option is updated.
    /// </summary>
    /// <param name="handler">
    ///     The action that has three parameters:
    ///     the first argument is the OptionItem instance that was updated,
    ///     the second one is the value before the update,
    ///     the third one is the value after the update.
    /// </param>
    /// <returns></returns>
    public OptionItem RegisterUpdateValueEvent(Action<OptionItem, int, int> handler)
    {
        UpdateValueEvent ??= [];
        return Do(_ => UpdateValueEvent.Add(handler));
    }

    public OptionItem SetRunEventOnLoad(bool value)
    {
        return Do(_ => UpdateValueEventRunsOnLoad = value);
    }

    public OptionItem AddReplacement((string key, string value) kvp)
    {
        return Do(_ =>
        {
            ReplacementDictionary ??= [];
            ReplacementDictionary.Add(kvp.key, kvp.value);
        });
    }

    public OptionItem RemoveReplacement(string key)
    {
        return Do(_ => ReplacementDictionary?.Remove(key));
    }

    // Getter
    public string GetName(bool disableColor = false, bool console = false)
    {
        if (Name.Contains("CTA.FLAG")) return Utils.ColorString(NameColor, Translator.GetString("CTA.TeamEnabled.Prefix") + Name[8..] + Translator.GetString("CTA.TeamEnabled.Suffix"));
        return disableColor ? Translator.GetString(Name, ReplacementDictionary, console) : Utils.ColorString(NameColor, Translator.GetString(Name, ReplacementDictionary));
    }

    public bool GetBool()
    {
        return (Parent == null || Parent.GetBool()) && Name switch
        {
            "LoverDieConsequence" => GetValue() == 1,
            "Bargainer.LensOfTruth.DurationSwitch" => GetValue() == 3,
            "BlackHoleDespawnMode" => GetValue() == 1,
            "CTF_TaggedPlayersGet" => GetValue() == 2,
            "CTF_GameEndCriteria" => true,
            _ => CurrentValue != 0
        };
    }

    public virtual int GetInt()
    {
        return CurrentValue;
    }

    public virtual float GetFloat()
    {
        return CurrentValue;
    }

    public virtual string GetString()
    {
        return ApplyFormat(CurrentValue.ToString());
    }

    public virtual int GetValue()
    {
        return IsSingleValue ? SingleValue : AllValues[CurrentPreset];
    }

    public bool IsCurrentlyHidden()
    {
        try
        {
            for (OptionItem current = this; current != null; current = current.Parent)
            {
                if (Hidden(current))
                    return true;
            }
        }
        catch (Exception e) { Utils.ThrowException(e); }

        return false;

        static bool Hidden(OptionItem oi)
        {
            if (oi.Header is { CollapsesSection: true }) return true;
            CustomGameMode mode = EHR.Options.CurrentGameMode;
            const CustomGameMode nd = CustomGameMode.NaturalDisasters;
            return (oi.IsHidden || (oi.GameMode != CustomGameMode.All && oi.GameMode != mode) ||
                    (oi.Name == "IntegrateNaturalDisasters" && mode == nd)) &&
                   !(oi.GameMode == nd && EHR.Options.IntegrateNaturalDisasters.GetBool());
        }
    }

    protected string ApplyFormat(string value)
    {
        if (ValueFormat == OptionFormat.None) return value;
        return string.Format(Translator.GetString("Format." + ValueFormat), value);
    }

    private void Refresh()
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
            SingleValue = afterValue;
        else
            AllValues[CurrentPreset] = afterValue;

        CallUpdateValueEvent(beforeValue, afterValue);
        Refresh();
        if (doSync) SyncAllOptions();
        if (doSave) OptionSaver.Save();
    }

    public virtual void SetValue(int afterValue, bool doSync = true)
    {
        SetValue(afterValue, true, doSync);
    }

    public void SetAllValues(int[] values)
    {
        if (values.Length == AllValues.Length)
            AllValues = values;
        else
        {
            for (var i = 0; i < values.Length; i++)
                AllValues[i] = values[i];
        }
    }

    public static OptionItem operator ++(OptionItem item)
    {
        return item.Do(item => item.SetValue(item.CurrentValue + 1));
    }

    public static OptionItem operator --(OptionItem item)
    {
        return item.Do(item => item.SetValue(item.CurrentValue - 1));
    }

    protected static void SwitchPreset(int newPreset)
    {
        CurrentPreset = Math.Clamp(newPreset, 0, NumPresets - 1);

        foreach (OptionItem op in AllOptions)
            op.Refresh();

        SyncAllOptions();
    }

    public static void SyncAllOptions(int targetId = -1)
    {
        if (
                Main.AllPlayerControls.Count <= 1
                || !AmongUsClient.Instance.AmHost
                || PlayerControl.LocalPlayer == null
            )
            return;

        RPC.SyncCustomSettingsRPC(targetId);
    }
    
    public void CallUpdateValueEvent(int beforeValue, int currentValue)
    {
        UpdateValueEvent?.ForEach(action =>
        {
            try { action(this, beforeValue, currentValue); }
            catch (Exception ex)
            {
                Logger.Error($"[{Name}] - Exception occurred when calling UpdateValueEvent", "OptionItem.UpdateValueEvent");
                Logger.Exception(ex, "OptionItem.UpdateValueEvent");
            }
        });
    }

    #region static

    public static IReadOnlyList<OptionItem> AllOptions => Options;
    private static readonly List<OptionItem> Options = new(1024);
    public static IReadOnlyDictionary<int, OptionItem> FastOptions => FastOpts;
    private static readonly Dictionary<int, OptionItem> FastOpts = new(1024);
    public static int CurrentPreset { get; private set; }

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
    CovenRoles,
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
