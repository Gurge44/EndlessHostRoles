using System.Globalization;

namespace EHR;

public class FloatOptionItem(int id, string name, FloatValueRule rule, float defaultValue, TabGroup tab, bool isSingleValue = false) : OptionItem(id, name, rule.GetNearestIndex(defaultValue), tab, isSingleValue)
{
    // Getter
    public override int GetInt() => (int)rule.GetValueByIndex(CurrentValue);
    public override float GetFloat() => rule.GetValueByIndex(CurrentValue);
    public override string GetString() => ApplyFormat(((float)((int)(rule.GetValueByIndex(CurrentValue) * 100) * 1.0) / 100).ToString(CultureInfo.CurrentCulture));
    public override int GetValue() => rule.RepeatIndex(base.GetValue());

    // Setter
    public override void SetValue(int value, bool doSync = true)
    {
        base.SetValue(rule.RepeatIndex(value), doSync);
    }
}