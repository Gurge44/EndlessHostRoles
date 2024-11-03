namespace EHR
{
    public class IntegerOptionItem(int id, string name, IntegerValueRule rule, int defaultValue, TabGroup tab, bool isSingleValue = false) : OptionItem(id, name, rule.GetNearestIndex(defaultValue), tab, isSingleValue)
    {
        public readonly IntegerValueRule Rule = rule;

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
            return ApplyFormat(Rule.GetValueByIndex(CurrentValue).ToString());
        }

        public override int GetValue()
        {
            return Rule.RepeatIndex(base.GetValue());
        }

        // Setter
        public override void SetValue(int value, bool doSync = true)
        {
            base.SetValue(Rule.RepeatIndex(value), doSync);
        }
    }
}