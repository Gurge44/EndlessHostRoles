using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static EHR.Options;

namespace EHR.Crewmate;

public class Snitch : RoleBase
{
    private const int Id = 8000;
    private static readonly List<byte> PlayerIdList = [];
    private static readonly Color RoleColor = Utils.GetRoleColor(CustomRoles.Snitch);

    private static OptionItem OptionEnableTargetArrow;
    private static OptionItem OptionCanGetColoredArrow;
    private static OptionItem OptionCanFindNeutralKiller;
    private static OptionItem OptionCanFindMadmate;
    private static OptionItem OptionRemainingTasks;

    private static bool EnableTargetArrow;
    private static bool CanGetColoredArrow;
    private static bool CanFindNeutralKiller;
    private static bool CanFindMadmate;
    public static int RemainingTasksToBeFound;

    public static readonly Dictionary<byte, bool> IsExposed = [];
    public static readonly Dictionary<byte, bool> IsComplete = [];

    private static readonly HashSet<byte> TargetList = [];
    private static readonly Dictionary<byte, Color> TargetColorlist = [];
    private byte SnitchId;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Snitch);
        OptionEnableTargetArrow = new BooleanOptionItem(Id + 10, "SnitchEnableTargetArrow", true, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Snitch]);
        OptionCanGetColoredArrow = new BooleanOptionItem(Id + 11, "SnitchCanGetArrowColor", true, TabGroup.CrewmateRoles).SetParent(OptionEnableTargetArrow);
        OptionCanFindNeutralKiller = new BooleanOptionItem(Id + 12, "SnitchCanFindNeutralKiller", true, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Snitch]);
        OptionCanFindMadmate = new BooleanOptionItem(Id + 14, "SnitchCanFindMadmate", true, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Snitch]);
        OptionRemainingTasks = new IntegerOptionItem(Id + 13, "SnitchRemainingTaskFound", new(0, 10, 1), 1, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Snitch]);
        OverrideTasksData.Create(Id + 20, TabGroup.CrewmateRoles, CustomRoles.Snitch);
    }

    public override void Init()
    {
        PlayerIdList.Clear();

        IsExposed.Clear();
        IsComplete.Clear();

        TargetList.Clear();
        TargetColorlist.Clear();
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        SnitchId = playerId;

        EnableTargetArrow = OptionEnableTargetArrow.GetBool();
        CanGetColoredArrow = OptionCanGetColoredArrow.GetBool();
        CanFindNeutralKiller = OptionCanFindNeutralKiller.GetBool();
        CanFindMadmate = OptionCanFindMadmate.GetBool();
        RemainingTasksToBeFound = OptionRemainingTasks.GetInt();

        IsExposed[playerId] = false;
        IsComplete[playerId] = false;
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public static bool IsSnitch(byte playerId)
    {
        return PlayerIdList.Contains(playerId);
    }

    private static bool GetExpose(PlayerControl pc)
    {
        if (!IsSnitch(pc.PlayerId) || !pc.IsAlive() || pc.Is(CustomRoles.Madmate)) return false;

        byte snitchId = pc.PlayerId;
        return IsExposed[snitchId];
    }

    private static bool IsSnitchTarget(PlayerControl target)
    {
        return (target.Is(CustomRoleTypes.Impostor) && !target.Is(CustomRoles.Trickster)) || (target.IsSnitchTarget() && CanFindNeutralKiller) || (target.Is(CustomRoles.Madmate) && CanFindMadmate) || (target.Is(CustomRoles.Rascal) && CanFindMadmate);
    }

    public static void CheckTask(PlayerControl snitch)
    {
        if (!snitch.IsAlive() || snitch.Is(CustomRoles.Madmate)) return;

        byte snitchId = snitch.PlayerId;
        TaskState snitchTask = snitch.GetTaskState();

        if (!IsExposed[snitchId] && snitchTask.RemainingTasksCount <= RemainingTasksToBeFound)
        {
            foreach (PlayerControl target in Main.AllAlivePlayerControls)
            {
                if (!IsSnitchTarget(target)) continue;

                TargetArrow.Add(target.PlayerId, snitchId);
            }

            IsExposed[snitchId] = true;
        }

        if (IsComplete[snitchId] || !snitchTask.IsTaskFinished) return;

        foreach (PlayerControl target in Main.AllAlivePlayerControls)
        {
            if (!IsSnitchTarget(target)) continue;

            byte targetId = target.PlayerId;
            NameColorManager.Add(snitchId, targetId);

            if (!EnableTargetArrow) continue;

            TargetArrow.Add(snitchId, targetId);

            if (TargetList.Add(targetId))
                if (CanGetColoredArrow)
                    TargetColorlist.Add(targetId, target.GetRoleColor());
        }

        snitch.Notify(Translator.GetString("SnitchDoneTasks"));

        IsComplete[snitchId] = true;
    }

    public static string GetWarningMark(PlayerControl seer, PlayerControl target)
    {
        return IsSnitchTarget(seer) && GetExpose(target) ? Utils.ColorString(RoleColor, " ★") : string.Empty;
    }

    public static string GetWarningArrow(PlayerControl seer, PlayerControl target = null)
    {
        if (GameStates.IsMeeting || !IsSnitchTarget(seer)) return string.Empty;

        if (target != null && seer.PlayerId != target.PlayerId) return string.Empty;

        IEnumerable<byte> exposedSnitch = PlayerIdList.Where(s => !Main.PlayerStates[s].IsDead && IsExposed[s]);
        byte[] snitch = exposedSnitch as byte[] ?? exposedSnitch.ToArray();
        if (snitch.Length == 0) return string.Empty;

        var warning = $"\n{Translator.GetString("Snitch")} ";

        if (EnableTargetArrow)
            warning += TargetArrow.GetArrows(seer, snitch);
        else
            warning += "⚠";

        return Utils.ColorString(RoleColor, warning);
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.Is(CustomRoles.Madmate)) return string.Empty;

        if (!EnableTargetArrow || GameStates.IsMeeting || seer.PlayerId != SnitchId) return string.Empty;

        if (target != null && seer.PlayerId != target.PlayerId) return string.Empty;

        var arrows = string.Empty;

        foreach (byte targetId in TargetList)
        {
            string arrow = TargetArrow.GetArrows(seer, targetId);
            arrows += CanGetColoredArrow ? Utils.ColorString(TargetColorlist[targetId], arrow) : arrow;
        }

        return arrows;
    }

    public static void OnCompleteTask(PlayerControl player)
    {
        if (!IsSnitch(player.PlayerId) || player.Is(CustomRoles.Madmate)) return;

        CheckTask(player);
    }
}