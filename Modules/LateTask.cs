using System;
using System.Collections.Generic;

namespace EHR;

class LateTask
{
    private static readonly List<LateTask> Tasks = [];
    public readonly Action action;
    public readonly bool log;
    public readonly string name;
    private float timer;

    /// <summary>
    /// Creates a task that will be automatically completed after the specified amount of time
    /// </summary>
    /// <param name="action">The delayed task</param>
    /// <param name="time">The time to wait until the task is run</param>
    /// <param name="name">The name of the task</param>
    /// <param name="log">Whether to send log of the creation and completion of the Late Task</param>
    private LateTask(Action action, float time, string name, bool log)
    {
        this.action = action;
        timer = time;
        this.name = name;
        this.log = log;
        Tasks.Add(this);
        if (log && name is not "" and not "No Name Task")
            Logger.Info("\"" + name + "\" is created", "LateTask");
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

    public static void New(Action action, float time, string name = "No Name Task", bool log = true) => _ = new LateTask(action, time, name, log);

    public static void Update(float deltaTime)
    {
        foreach (var task in Tasks.ToArray())
        {
            try
            {
                if (task.Run(deltaTime))
                {
                    if (task.name != string.Empty && task.log)
                        Logger.Info($"\"{task.name}\" is finished", "LateTask");
                    Tasks.Remove(task);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"{ex.GetType()}: {ex.Message}  in \"{task.name}\"\n{ex.StackTrace}", "LateTask.Error", false);
                Tasks.Remove(task);
            }
        }
    }
}