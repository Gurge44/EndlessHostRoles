namespace EHR;

public class IntegerOptionItem(int id, string name, IntegerValueRule rule, int defaultValue, TabGroup tab, bool isSingleValue = false) : OptionItem(id, name, rule.GetNearestIndex(defaultValue), tab, isSingleValue)
{
    // Getter
    public override int GetInt() => rule.GetValueByIndex(CurrentValue);
    public override float GetFloat() => rule.GetValueByIndex(CurrentValue);
    public override string GetString() => ApplyFormat(rule.GetValueByIndex(CurrentValue).ToString());
    public override int GetValue() => rule.RepeatIndex(base.GetValue());

    // Setter
    public override void SetValue(int value, bool doSync = true)
    {
        base.SetValue(rule.RepeatIndex(value), doSync);
    }
}