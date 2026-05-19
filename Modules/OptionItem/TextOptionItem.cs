namespace EHR;

public class TextOptionItem : OptionItem
{
    public TextOptionItem(int id, string name, TabGroup tab, int defaultValue = 0, bool isSingleValue = false) : base(id, name, defaultValue, tab, isSingleValue)
    {
        IsText = true;
        IsHeader = true;
    }

    public bool CollapsesSection { get; set; }

    // Getter
    public override string GetString()
    {
        return Translator.GetString(Name);
    }
}