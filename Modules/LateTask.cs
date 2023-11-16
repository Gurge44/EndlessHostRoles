using System;
using System.Collections.Generic;

namespace TOHE;

class LateTask
{
    public string name;
    public float timer;
    public Action action;
    public bool log;
    public static List<LateTask> Tasks = [];
    public bool Run(float deltaTime)
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
    /// Creates a task that will be automatically completed after the specified amount of time
    /// </summary>
    /// <param name="action">The delayed task</param>
    /// <param name="time">The time to wait until the task is run</param>
    /// <param name="name">The name of the task</param>
    /// <param name="log">Whether to send log of the creation of the Late Task</param>
    public LateTask(Action action, float time, string name = "No Name Task", bool log = true)
    {
        this.action = action;
        timer = time;
        this.name = name;
        this.log = log;
        Tasks.Add(this);
        if (name != "" && log)
            Logger.Info("\"" + name + "\" is created", "LateTask");
    }
    public static void Update(float deltaTime)
    {
        var TasksToRemove = new List<LateTask>();
        foreach (var task in Tasks.ToArray())
        {
            try
            {
                if (task.Run(deltaTime))
                {
                    if (task.name != string.Empty && task.log)
                        Logger.Info($"\"{task.name}\" is finished", "LateTask");
                    TasksToRemove.Add(task);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"{ex.GetType()}: {ex.Message}  in \"{task.name}\"\n{ex.StackTrace}", "LateTask.Error", false);
                TasksToRemove.Add(task);
            }
        }
        TasksToRemove.ForEach(task => Tasks.Remove(task));
    }
}