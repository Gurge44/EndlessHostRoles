namespace TOHE;

public class IntegerOptionItem(int id, string name, int defaultValue, TabGroup tab, bool isSingleValue, IntegerValueRule rule) : OptionItem(id, name, rule.GetNearestIndex(defaultValue), tab, isSingleValue)
{
    // 必須情報
    public IntegerValueRule Rule = rule;

    public static IntegerOptionItem Create(
        int id, string name, IntegerValueRule rule, int defaultValue, TabGroup tab, bool isSingleValue
    )
    {
        return new IntegerOptionItem(
            id, name, defaultValue, tab, isSingleValue, rule
        );
    }

    // Getter
    public override int GetInt() => Rule.GetValueByIndex(CurrentValue);
    public override float GetFloat() => Rule.GetValueByIndex(CurrentValue);
    public override string GetString()
    {
        return ApplyFormat(Rule.GetValueByIndex(CurrentValue).ToString());
    }
    public override int GetValue()
        => Rule.RepeatIndex(base.GetValue());

    // Setter
    public override void SetValue(int value, bool doSync = true)
    {
        base.SetValue(Rule.RepeatIndex(value), doSync);
    }
}