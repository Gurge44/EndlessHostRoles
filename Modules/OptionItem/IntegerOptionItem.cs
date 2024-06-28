namespace EHR;

public class IntegerOptionItem(int id, string name, IntegerValueRule rule, int defaultValue, TabGroup tab, bool isSingleValue = false) : OptionItem(id, name, rule.GetNearestIndex(defaultValue), tab, isSingleValue)
{
    public readonly IntegerValueRule Rule = rule;

    // Getter
    public override int GetInt() => Rule.GetValueByIndex(CurrentValue);
    public override float GetFloat() => Rule.GetValueByIndex(CurrentValue);
    public override string GetString() => ApplyFormat(Rule.GetValueByIndex(CurrentValue).ToString());
    public override int GetValue() => Rule.RepeatIndex(base.GetValue());

    // Setter
    public override void SetValue(int value, bool doSync = true)
    {
        base.SetValue(Rule.RepeatIndex(value), doSync);
    }
}