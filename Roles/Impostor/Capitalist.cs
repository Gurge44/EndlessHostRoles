using System.Collections.Generic;
using static EHR.Options;

namespace EHR.Impostor;

internal class Capitalist : RoleBase
{
    private static readonly Dictionary<byte, int> CapitalistAddTask = [];
    public static readonly Dictionary<byte, int> CapitalistAssignTask = [];
    public static bool On;
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(16600, TabGroup.ImpostorRoles, CustomRoles.Capitalist);

        CapitalistKillCooldown = new FloatOptionItem(16611, "KillCooldown", new(2.5f, 60f, 0.5f), 25f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Capitalist])
            .SetValueFormat(OptionFormat.Seconds);
    }

    public override void Add(byte playerId)
    {
        On = true;
    }

    public override void Init()
    {
        On = false;
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        hud.KillButton?.OverrideText(Translator.GetString("CapitalistButtonText"));
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = CapitalistKillCooldown.GetFloat();
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        return killer.CheckDoubleTrigger(target, () =>
        {
            CapitalistAddTask.TryAdd(target.PlayerId, 0);
            CapitalistAddTask[target.PlayerId]++;
            CapitalistAssignTask.TryAdd(target.PlayerId, 0);
            CapitalistAssignTask[target.PlayerId]++;
            Logger.Info($"{killer.GetRealName()} added a task for: {target.GetRealName()}", "Capitalist Add Task");
        });
    }

    public static bool AddTaskForPlayer(PlayerControl player)
    {
        if (CapitalistAddTask.TryGetValue(player.PlayerId, out int amount))
        {
            TaskState taskState = player.GetTaskState();
            taskState.AllTasksCount += amount;
            CapitalistAddTask.Remove(player.PlayerId);
            taskState.CompletedTasksCount++;
            player.RpcResetTasks(false);
            player.SyncSettings();
            Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player);
            return false;
        }

        return true;
    }
}