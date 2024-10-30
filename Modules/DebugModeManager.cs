namespace EHR
{
    public static class DebugModeManager
    {
        public static OptionItem EnableDebugMode;
        public static bool AmDebugger { get; private set; }
#if DEBUG
        true;
#else
#endif

        public static void Auth(HashAuth auth, string input)
        {
            AmDebugger |= auth.CheckString(input);
        }
    }
}