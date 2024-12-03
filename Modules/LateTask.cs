using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace EHR;

internal class LateTask
{
    private static readonly List<LateTask> Tasks = [];
    private readonly Action action;
    private readonly string callerData;
    private readonly bool log;
    private readonly string name;
    private float timer;

    private LateTask(Action action, float time, string name, bool log, string callerData)
    {
        this.action = action;
        timer = time;
        this.name = name;
        this.log = log;
        this.callerData = callerData;
        Tasks.Add(this);
        if (log && name is not "" and not "No Name Task") Logger.Info("\"" + name + "\" is created", "LateTask");
    }

    private bool Run(float deltaTime)
    {
        timer -= deltaTime;

        if (timer <= 0)
        {
            action();
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Creates a task that will be automatically completed after the specified amount of time
    /// </summary>
    /// <param name="action">The delayed task</param>
    /// <param name="time">The time to wait until the task is run</param>
    /// <param name="name">The name of the task</param>
    /// <param name="log">Whether to send log of the creation and completion of the Late Task</param>
    public static void New(Action action, float time, string name = "No Name Task", bool log = true, [CallerFilePath] string path = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
    {
        _ = new LateTask(action, time, name, log, $"created at {path.Split('\\')[^1]}, by member {member}, at line {line}");
    }

    public static void Update(float deltaTime)
    {
        foreach (LateTask task in Tasks.ToArray())
        {
            try
            {
                if (task.Run(deltaTime))
                {
                    if (task.name is not "" and not "No Name Task" && task.log)
                        Logger.Info($"\"{task.name}\" is finished", "LateTask");

                    Tasks.Remove(task);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"{ex.GetType()}: {ex.Message}  in \"{task.name}\" ({task.callerData})\n{ex.StackTrace}", "LateTask.Error", false);
                Tasks.Remove(task);
            }
        }
    }
}