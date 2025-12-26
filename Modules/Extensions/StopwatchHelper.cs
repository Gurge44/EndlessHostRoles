using System.Diagnostics;

namespace EHR.Modules.Extensions;

public static class StopwatchHelper
{
    public static int GetRemainingTime(this Stopwatch stopwatch, int totalTime) => totalTime - stopwatch.Elapsed.Seconds;
}