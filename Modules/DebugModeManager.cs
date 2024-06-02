using UnityEngine;

namespace EHR;

public static class DebugModeManager
{
    public static bool AmDebugger { get; private set; } =
#if DEBUG
        true;
#else
    false;
#endif
    public static bool IsDebugMode => AmDebugger && EnableDebugMode != null && EnableDebugMode.GetBool();
    public static OptionItem EnableDebugMode;

    public static void Auth(HashAuth auth, string input)
    {
        AmDebugger = AmDebugger || auth.CheckString(input);
    }

    public static void SetupCustomOption()
    {
        EnableDebugMode = new BooleanOptionItem(2, "EnableDebugMode", false, TabGroup.SystemSettings, true)
            .SetHeader(true)
            .SetColor(Color.green)
            .SetHidden(!AmDebugger);
    }
}