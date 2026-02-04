namespace EHR;

public class TextOptionItem : OptionItem
{
    public IntegerValueRule Rule;

    public TextOptionItem(int id, string name, TabGroup tab, int defaultValue = 0, bool isSingleValue = false) : base(id, name, defaultValue, tab, isSingleValue)
    {
        IsText = true;
        IsHeader = true;
    }

    public bool CollapsesSection { get; set; } = false;

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
        return Translator.GetString(Name);
    }
}