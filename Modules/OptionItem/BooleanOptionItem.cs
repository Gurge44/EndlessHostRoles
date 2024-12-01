namespace EHR;

public class BooleanOptionItem(int id, string name, bool defaultValue, TabGroup tab, bool isSingleValue = false) : OptionItem(id, name, defaultValue ? 1 : 0, tab, isSingleValue)
{
    private const string TextTrue = "ColoredOn";
    private const string TextFalse = "ColoredOff";

    // Getter
    public override string GetString()
    {
        return Translator.GetString(GetBool() ? TextTrue : TextFalse);
    }

    // Setter
    public override void SetValue(int value, bool doSync = true)
    {
        base.SetValue(value % 2 == 0 ? 0 : 1, doSync);
    }
}