namespace EHR;

public class PresetOptionItem(int defaultValue, TabGroup tab) : OptionItem(0, "Preset", defaultValue, tab, true)
{
    public readonly IntegerValueRule Rule = (0, NumPresets - 1, 1);

    // Getter
    public override int GetInt()
    {
        return Rule.GetValueByIndex(CurrentValue);
    }

    public override float GetFloat()
    {
        return Rule.GetValueByIndex(CurrentValue);
    }

    public override string GetString()
    {
        return CurrentValue switch
        {
            0 => Main.Preset1.Value == (string)Main.Preset1.DefaultValue ? Translator.GetString("Preset_1") : Main.Preset1.Value,
            1 => Main.Preset2.Value == (string)Main.Preset2.DefaultValue ? Translator.GetString("Preset_2") : Main.Preset2.Value,
            2 => Main.Preset3.Value == (string)Main.Preset3.DefaultValue ? Translator.GetString("Preset_3") : Main.Preset3.Value,
            3 => Main.Preset4.Value == (string)Main.Preset4.DefaultValue ? Translator.GetString("Preset_4") : Main.Preset4.Value,
            4 => Main.Preset5.Value == (string)Main.Preset5.DefaultValue ? Translator.GetString("Preset_5") : Main.Preset5.Value,
            5 => Main.Preset6.Value == (string)Main.Preset6.DefaultValue ? Translator.GetString("Preset_6") : Main.Preset6.Value,
            6 => Main.Preset7.Value == (string)Main.Preset7.DefaultValue ? Translator.GetString("Preset_7") : Main.Preset7.Value,
            7 => Main.Preset8.Value == (string)Main.Preset8.DefaultValue ? Translator.GetString("Preset_8") : Main.Preset8.Value,
            8 => Main.Preset9.Value == (string)Main.Preset9.DefaultValue ? Translator.GetString("Preset_9") : Main.Preset9.Value,
            9 => Main.Preset10.Value == (string)Main.Preset10.DefaultValue ? Translator.GetString("Preset_10") : Main.Preset10.Value,
            _ => null
        };
    }

    public override int GetValue()
    {
        return Rule.RepeatIndex(base.GetValue());
    }

    // Setter
    public override void SetValue(int value, bool doSync = true)
    {
        base.SetValue(Rule.RepeatIndex(value), doSync);
        SwitchPreset(Rule.RepeatIndex(value));
    }
}