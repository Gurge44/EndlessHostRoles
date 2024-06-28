using System.Collections.Generic;

namespace EHR;

public class StringOptionItem(int id, string name, IList<string> selections, int defaultValue, TabGroup tab, bool isSingleValue = false, bool noTranslation = false) : OptionItem(id, name, defaultValue, tab, isSingleValue)
{
    public readonly bool noTranslation = noTranslation;
    public readonly IntegerValueRule Rule = (0, selections.Count - 1, 1);
    public readonly IList<string> Selections = selections;

    // Getter
    public override int GetInt() => Rule.GetValueByIndex(CurrentValue);
    public override float GetFloat() => Rule.GetValueByIndex(CurrentValue);

    public override string GetString()
    {
        var str = Selections[Rule.GetValueByIndex(CurrentValue)];
        return noTranslation ? str : Translator.GetString(str);
    }

    public int GetChance()
    {
        switch (Selections.Count)
        {
            // For 0% or 100%
            case 2:
                return CurrentValue * 100;
            // EHR’s career generation mode
            case 3:
                return CurrentValue;
            // For 0% to 100% or 5% to 100%
            default:
                var offset = Options.Rates.Length - Selections.Count;
                var index = CurrentValue + offset;
                var rate = index * 5;
                return rate;
        }
    }

    public override int GetValue() => Rule.RepeatIndex(base.GetValue());

    // Setter
    public override void SetValue(int value, bool doSync = true)
    {
        base.SetValue(Rule.RepeatIndex(value), doSync);
    }
}