using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using UnityEngine;
using static TOHE.Options;

namespace TOHE.Roles.AddOns.Crewmate;

public static class Workhorse
{
    private static readonly int Id = 15700;
    public static Color RoleColor = Utils.GetRoleColor(CustomRoles.Workhorse);
    public static List<byte> playerIdList = [];

    private static OptionItem SpawnChance;
    private static OptionItem OptionAssignOnlyToCrewmate;
    private static OptionItem OptionNumLongTasks;
    private static OptionItem OptionNumShortTasks;
    private static OptionItem OptionSnitchCanBeWorkhorse;

    public static bool AssignOnlyToCrewmate;
    public static int NumLongTasks;
    public static int NumShortTasks;

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.Addons, CustomRoles.Workhorse, zeroOne: true);
        SpawnChance = IntegerOptionItem.Create(Id + 13, "WorkhorseSpawnChance", new(0, 100, 1), 65, TabGroup.Addons, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Workhorse])
            .SetValueFormat(OptionFormat.Percent);
        OptionAssignOnlyToCrewmate = BooleanOptionItem.Create(Id + 10, "AssignOnlyToCrewmate", true, TabGroup.Addons, false).SetParent(CustomRoleSpawnChances[CustomRoles.Workhorse]);
        OptionNumLongTasks = IntegerOptionItem.Create(Id + 11, "WorkhorseNumLongTasks", new(0, 5, 1), 1, TabGroup.Addons, false).SetParent(CustomRoleSpawnChances[CustomRoles.Workhorse])
            .SetValueFormat(OptionFormat.Pieces);
        OptionNumShortTasks = IntegerOptionItem.Create(Id + 12, "WorkhorseNumShortTasks", new(0, 5, 1), 1, TabGroup.Addons, false).SetParent(CustomRoleSpawnChances[CustomRoles.Workhorse])
            .SetValueFormat(OptionFormat.Pieces);
        OptionSnitchCanBeWorkhorse = BooleanOptionItem.Create(Id + 14, "SnitchCanBeWorkhorse", false, TabGroup.Addons, false).SetParent(CustomRoleSpawnChances[CustomRoles.Workhorse]);
    }
    public static void Init()
    {
        playerIdList = [];

        AssignOnlyToCrewmate = OptionAssignOnlyToCrewmate.GetBool();
        NumLongTasks = OptionNumLongTasks.GetInt();
        NumShortTasks = OptionNumShortTasks.GetInt();
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
    }
    public static bool IsEnable => playerIdList.Count > 0;
    public static bool IsThisRole(byte playerId) => playerIdList.Contains(playerId);
    public static (bool, int, int) TaskData => (false, NumLongTasks, NumShortTasks);
    private static bool IsAssignTarget(PlayerControl pc)
    {
        if (!pc.IsAlive() || IsThisRole(pc.PlayerId)) return false;
        if (pc.Is(CustomRoles.Needy) || pc.Is(CustomRoles.Lazy) || pc.Is(CustomRoles.Bloodlust)) return false;
        if (pc.GetCustomRole().GetDYRole() == RoleTypes.Impostor || pc.GetCustomRole().GetVNRole() is CustomRoles.Impostor or CustomRoles.Shapeshifter) return false;

        var taskState = pc.GetTaskState();
        if (taskState.CompletedTasksCount + 1 < taskState.AllTasksCount) return false;

        bool canBeTarget = Utils.HasTasks(pc.Data) && !OverrideTasksData.AllData.ContainsKey(pc.GetCustomRole());
        if (AssignOnlyToCrewmate) return canBeTarget && pc.Is(CustomRoleTypes.Crewmate);
        return canBeTarget;
    }
    public static bool OnCompleteTask(PlayerControl pc)
    {
        if (!CustomRoles.Workhorse.IsEnable() || playerIdList.Count >= CustomRoles.Workhorse.GetCount()) return false;
        if (pc.Is(CustomRoles.Snitch) && !OptionSnitchCanBeWorkhorse.GetBool()) return false;
        if (!IsAssignTarget(pc)) return false;
        if (IRandom.Instance.Next(100) >= SpawnChance.GetInt()) return false;

        pc.RpcSetCustomRole(CustomRoles.Workhorse);
        var taskState = pc.GetTaskState();
        taskState.AllTasksCount += NumLongTasks + NumShortTasks;
        taskState.CompletedTasksCount++; //今回の完了分加算

        if (AmongUsClient.Instance.AmHost)
        {
            Add(pc.PlayerId);
            GameData.Instance.RpcSetTasks(pc.PlayerId, Array.Empty<byte>()); //タスクを再配布
            pc.SyncSettings();
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }

        return true;
    }
}