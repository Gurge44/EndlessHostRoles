using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace EHR;

internal static class LateTask
{
    /// <summary>
    ///     Creates a task that will be automatically completed after the specified amount of time
    /// </summary>
    /// <param name="action">The delayed task</param>
    /// <param name="time">The time to wait until the task is run</param>
    /// <param name="name">The name of the task</param>
    /// <param name="log">Whether to send log of the creation and completion of the Late Task</param>
    public static void New(Action action, float time, string name = "No Name Task", bool log = true, [CallerFilePath] string path = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
    {
        Main.Instance.StartCoroutine(CoLateTask(action, time, name, log, $"created at {path.Split('\\')[^1]}, by member {member}, at line {line}"));
    }

    private static IEnumerator CoLateTask(Action action, float time, string name, bool log, string callerData)
    {
        yield return new WaitForSeconds(time);
        try
        {
            action();
            if (name is not "" and not "No Name Task" && log)
                Logger.Info($"\"{name}\" is finished", "LateTask");
        }
        catch (Exception ex)
        {
            Logger.Error($"{ex.GetType()}: {ex.Message}  in \"{name}\" ({callerData})\n{ex.StackTrace}", "LateTask.Error", false);
        }
    }
}