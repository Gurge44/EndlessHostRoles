using System.Collections.Generic;
using UnityEngine;
using static EHR.Options;

namespace EHR.AddOns.Crewmate;

public class Workhorse : IAddon
{
    private const int Id = 15700;
    public static Color RoleColor = Utils.GetRoleColor(CustomRoles.Workhorse);
    private static List<byte> PlayerIdList = [];

    private static OptionItem SpawnChance;
    private static OptionItem OptionAssignOnlyToCrewmate;
    private static OptionItem OptionNumLongTasks;
    private static OptionItem OptionNumShortTasks;
    private static OptionItem OptionSnitchCanBeWorkhorse;

    private static bool AssignOnlyToCrewmate;
    private static int NumLongTasks;
    private static int NumShortTasks;
    public static (bool, int, int) TaskData => (false, NumLongTasks, NumShortTasks);
    public AddonTypes Type => AddonTypes.Harmful;

    public void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.Addons, CustomRoles.Workhorse, zeroOne: true);

        SpawnChance = new IntegerOptionItem(Id + 13, "WorkhorseSpawnChance", new(0, 100, 5), 65, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Workhorse])
            .SetValueFormat(OptionFormat.Percent);

        OptionAssignOnlyToCrewmate = new BooleanOptionItem(Id + 10, "AssignOnlyToCrewmate", true, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Workhorse]);

        OptionNumLongTasks = new IntegerOptionItem(Id + 11, "WorkhorseNumLongTasks", new(0, 5, 1), 1, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Workhorse])
            .SetValueFormat(OptionFormat.Pieces);

        OptionNumShortTasks = new IntegerOptionItem(Id + 12, "WorkhorseNumShortTasks", new(0, 5, 1), 1, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Workhorse])
            .SetValueFormat(OptionFormat.Pieces);

        OptionSnitchCanBeWorkhorse = new BooleanOptionItem(Id + 14, "SnitchCanBeWorkhorse", false, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Workhorse]);
    }

    public static void Init()
    {
        PlayerIdList = [];

        AssignOnlyToCrewmate = OptionAssignOnlyToCrewmate.GetBool();
        NumLongTasks = OptionNumLongTasks.GetInt();
        NumShortTasks = OptionNumShortTasks.GetInt();
    }

    private static void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
    }

    public static bool IsThisRole(byte playerId)
    {
        return PlayerIdList.Contains(playerId);
    }

    private static bool IsAssignTarget(PlayerControl pc)
    {
        if (!pc.IsAlive() || IsThisRole(pc.PlayerId)) return false;
        if (pc.Is(CustomRoles.LazyGuy) || pc.Is(CustomRoles.Lazy) || pc.Is(CustomRoles.Bloodlust)) return false;
        if (pc.GetCustomRole().GetVNRole(true) is CustomRoles.Impostor or CustomRoles.ImpostorEHR or CustomRoles.Shapeshifter or CustomRoles.ShapeshifterEHR or CustomRoles.Phantom or CustomRoles.PhantomEHR or CustomRoles.Viper or CustomRoles.ViperEHR) return false;

        TaskState taskState = pc.GetTaskState();
        if (taskState.CompletedTasksCount + 1 < taskState.AllTasksCount) return false;

        bool canBeTarget = Utils.HasTasks(pc.Data) && !OverrideTasksData.AllData.ContainsKey(pc.GetCustomRole());
        if (AssignOnlyToCrewmate) return canBeTarget && pc.Is(CustomRoleTypes.Crewmate);

        return canBeTarget;
    }

    public static bool OnCompleteTask(PlayerControl pc)
    {
        if (!CustomRoles.Workhorse.IsEnable() || PlayerIdList.Count >= CustomRoles.Workhorse.GetCount()) return false;

        if (CurrentGameMode != CustomGameMode.Standard) return false;

        if (pc.Is(CustomRoles.Snitch) && !OptionSnitchCanBeWorkhorse.GetBool()) return false;

        if (!IsAssignTarget(pc)) return false;

        if (IRandom.Instance.Next(100) >= SpawnChance.GetInt()) return false;

        pc.RpcSetCustomRole(CustomRoles.Workhorse);
        TaskState taskState = pc.GetTaskState();
        taskState.AllTasksCount += NumLongTasks + NumShortTasks;
        taskState.CompletedTasksCount++;

        if (AmongUsClient.Instance.AmHost)
        {
            Add(pc.PlayerId);
            pc.RpcResetTasks();
            pc.SyncSettings();
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }

        return true;
    }
}