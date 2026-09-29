using System.Runtime.CompilerServices;

namespace EHR;

public static class CharHelpers
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsAscii(char c)
    {
        return c < 128;
    }
}