using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static EHR.Options;

namespace EHR.Roles.Crewmate;

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

    public override bool IsEnable => PlayerIdList.Count > 0;

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Snitch);
        OptionEnableTargetArrow = BooleanOptionItem.Create(Id + 10, "SnitchEnableTargetArrow", true, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Snitch]);
        OptionCanGetColoredArrow = BooleanOptionItem.Create(Id + 11, "SnitchCanGetArrowColor", true, TabGroup.CrewmateRoles).SetParent(OptionEnableTargetArrow);
        OptionCanFindNeutralKiller = BooleanOptionItem.Create(Id + 12, "SnitchCanFindNeutralKiller", true, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Snitch]);
        OptionCanFindMadmate = BooleanOptionItem.Create(Id + 14, "SnitchCanFindMadmate", true, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Snitch]);
        OptionRemainingTasks = IntegerOptionItem.Create(Id + 13, "SnitchRemainingTaskFound", new(0, 10, 1), 1, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Snitch]);
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

        EnableTargetArrow = OptionEnableTargetArrow.GetBool();
        CanGetColoredArrow = OptionCanGetColoredArrow.GetBool();
        CanFindNeutralKiller = OptionCanFindNeutralKiller.GetBool();
        CanFindMadmate = OptionCanFindMadmate.GetBool();
        RemainingTasksToBeFound = OptionRemainingTasks.GetInt();

        IsExposed[playerId] = false;
        IsComplete[playerId] = false;
    }

    public static bool IsThisRole(byte playerId) => PlayerIdList.Contains(playerId);

    private static bool GetExpose(PlayerControl pc)
    {
        if (!IsThisRole(pc.PlayerId) || !pc.IsAlive() || pc.Is(CustomRoles.Madmate)) return false;

        var snitchId = pc.PlayerId;
        return IsExposed[snitchId];
    }

    private static bool IsSnitchTarget(PlayerControl target) => (target.Is(CustomRoleTypes.Impostor) && !target.Is(CustomRoles.Trickster)) || (target.IsSnitchTarget() && CanFindNeutralKiller) || (target.Is(CustomRoles.Madmate) && CanFindMadmate) || (target.Is(CustomRoles.Rascal) && CanFindMadmate);

    public static void CheckTask(PlayerControl snitch)
    {
        if (!snitch.IsAlive() || snitch.Is(CustomRoles.Madmate)) return;

        var snitchId = snitch.PlayerId;
        var snitchTask = snitch.GetTaskState();

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

            var targetId = target.PlayerId;
            NameColorManager.Add(snitchId, targetId);

            if (!EnableTargetArrow) continue;

            TargetArrow.Add(snitchId, targetId);

            if (TargetList.Add(targetId))
            {
                if (CanGetColoredArrow)
                    TargetColorlist.Add(targetId, target.GetRoleColor());
            }
        }

        snitch.Notify(Translator.GetString("SnitchDoneTasks"));

        IsComplete[snitchId] = true;
    }

    public static string GetWarningMark(PlayerControl seer, PlayerControl target)
        => IsSnitchTarget(seer) && GetExpose(target) ? Utils.ColorString(RoleColor, " ★") : string.Empty;

    public static string GetWarningArrow(PlayerControl seer, PlayerControl target = null)
    {
        if (GameStates.IsMeeting || !IsSnitchTarget(seer)) return string.Empty;
        if (target != null && seer.PlayerId != target.PlayerId) return string.Empty;

        var exposedSnitch = PlayerIdList.Where(s => !Main.PlayerStates[s].IsDead && IsExposed[s]);
        var snitch = exposedSnitch as byte[] ?? exposedSnitch.ToArray();
        if (!snitch.Any()) return string.Empty;

        var warning = $"\n{Translator.GetString("Snitch")} ";
        if (EnableTargetArrow)
            warning += TargetArrow.GetArrows(seer, snitch);

        return Utils.ColorString(RoleColor, warning);
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool m = false)
    {
        if (seer.Is(CustomRoles.Madmate)) return string.Empty;
        if (!EnableTargetArrow || GameStates.IsMeeting) return string.Empty;
        if (target != null && seer.PlayerId != target.PlayerId) return string.Empty;
        var arrows = string.Empty;
        foreach (var targetId in TargetList)
        {
            var arrow = TargetArrow.GetArrows(seer, targetId);
            arrows += CanGetColoredArrow ? Utils.ColorString(TargetColorlist[targetId], arrow) : arrow;
        }

        return arrows;
    }

    public static void OnCompleteTask(PlayerControl player)
    {
        if (!IsThisRole(player.PlayerId) || player.Is(CustomRoles.Madmate)) return;
        CheckTask(player);
    }
}