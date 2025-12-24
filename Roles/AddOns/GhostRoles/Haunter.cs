using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;

namespace EHR.AddOns.GhostRoles;

internal class Haunter : IGhostRole
{
    public static HashSet<byte> AllHauntedPlayers = [];

    public static OptionItem TasksBeforeBeingKnown;
    private static OptionItem RevealCovenMembers;
    private static OptionItem RevealNeutralKillers;
    private static OptionItem RevealMadmates;
    private static OptionItem NumberOfReveals;
    private static OptionItem CanWinWithCrewmates;

    private static readonly string[] WinWithCrewOpts =
    [
        "RoleOff",
        "WWCO.IfFinishedTasks",
        "RoleOn"
    ];

    private byte HaunterId;
    private List<byte> WarnedImps = [];
    private long WarnTimeStamp;

    public Team Team => Team.Crewmate | Team.Neutral;
    public RoleTypes RoleTypes => RoleTypes.CrewmateGhost;
    public int Cooldown => 900;

    public void OnProtect(PlayerControl pc, PlayerControl target) { }

    public void OnAssign(PlayerControl pc)
    {
        HaunterId = pc.PlayerId;

        LateTask.New(() =>
        {
            TaskState taskState = pc.GetTaskState();
            if (taskState == null) return;

            taskState.HasTasks = true;
            taskState.CompletedTasksCount = 0;
            taskState.AllTasksCount = Utils.TotalTaskCount - Main.RealOptionsData.GetInt(Int32OptionNames.NumCommonTasks);

            pc.RpcResetTasks();
            pc.SyncSettings();
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }, 1f, "Haunter Assign");
    }

    public void SetupCustomOption()
    {
        Options.SetupRoleOptions(649300, TabGroup.OtherRoles, CustomRoles.Haunter);

        TasksBeforeBeingKnown = new IntegerOptionItem(649302, "Haunter.TasksBeforeBeingKnown", new(1, 10, 1), 1, TabGroup.OtherRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Haunter]);

        RevealCovenMembers = new BooleanOptionItem(649303, "Haunter.RevealCovenMembers", true, TabGroup.OtherRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Haunter]);

        RevealNeutralKillers = new BooleanOptionItem(649304, "Haunter.RevealNeutralKillers", true, TabGroup.OtherRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Haunter]);

        RevealMadmates = new BooleanOptionItem(649305, "Haunter.RevealMadmates", true, TabGroup.OtherRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Haunter]);

        NumberOfReveals = new IntegerOptionItem(649306, "Haunter.NumberOfReveals", new(1, 10, 1), 1, TabGroup.OtherRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Haunter]);

        CanWinWithCrewmates = new StringOptionItem(649307, "Haunter.CanWinWithCrewmates", WinWithCrewOpts, 1, TabGroup.OtherRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Haunter]);

        Options.OverrideTasksData.Create(649308, TabGroup.OtherRoles, CustomRoles.Haunter);
    }

    public void OnOneTaskLeft(PlayerControl pc)
    {
        if (WarnedImps.Count > 0) return;

        WarnedImps = [];

        IEnumerable<PlayerControl> filtered = Main.AllAlivePlayerControls.Where(x =>
        {
            return x.GetTeam() switch
            {
                Team.Impostor when x.GetCustomRole().IsMadmate() || x.Is(CustomRoles.Madmate) => RevealMadmates.GetBool(),
                Team.Impostor => true,
                Team.Neutral when x.IsNeutralKiller() => RevealNeutralKillers.GetBool(),
                Team.Coven => RevealCovenMembers.GetBool(),
                _ => false
            };
        });

        WarnTimeStamp = Utils.TimeStamp + 2;

        foreach (PlayerControl imp in filtered)
        {
            TargetArrow.Add(imp.PlayerId, pc.PlayerId);
            WarnedImps.Add(imp.PlayerId);
            imp.Notify(Translator.GetString("Haunter1TaskLeft"), 10f);
        }
    }

    public void OnFinishedTasks(PlayerControl pc)
    {
        if (WarnedImps.Count == 0) return;

        List<byte> targets = [];
        int numOfReveals = NumberOfReveals.GetInt();

        for (var i = 0; i < numOfReveals; i++)
        {
            int index = IRandom.Instance.Next(WarnedImps.Count);
            byte target = WarnedImps[index];
            targets.Add(target);
            WarnedImps.Remove(target);
            TargetArrow.Remove(target, pc.PlayerId);
        }

        AllHauntedPlayers.UnionWith(targets);

        var targetPcs = targets.ToValidPlayers();

        targetPcs.ForEach(x => x.Notify(Translator.GetString("HaunterRevealedYou"), 10f));
        WarnedImps.ToValidPlayers().ForEach(x => x.Notify(Translator.GetString("HaunterFinishedTasks"), 10f));

        targetPcs.ForEach(x => Utils.NotifyRoles(SpecifyTarget: x));
    }

    public void Update(PlayerControl pc)
    {
        if (WarnedImps.Count == 0 || WarnTimeStamp >= Utils.TimeStamp) return;

        if (WarnedImps.Any(imp => TargetArrow.GetArrows(Utils.GetPlayerById(imp), pc.PlayerId) == "・"))
        {
            foreach (byte imp in WarnedImps)
            {
                TargetArrow.Remove(imp, pc.PlayerId);
                Utils.GetPlayerById(imp)?.Notify(Translator.GetString("HaunterStopped"), 7f);
            }

            WarnedImps = [];
            Main.PlayerStates[pc.PlayerId].RemoveSubRole(CustomRoles.Haunter);
            GhostRolesManager.AssignedGhostRoles.Remove(pc.PlayerId);
            pc.Notify(Translator.GetString("HaunterStoppedSelf"), 7f);
        }
    }

    public static string GetSuffix(PlayerControl seer)
    {
        foreach ((CustomRoles Role, IGhostRole Instance) role in GhostRolesManager.AssignedGhostRoles.Values)
        {
            if (role.Instance is not Haunter haunter) continue;

            if (!haunter.WarnedImps.Contains(seer.PlayerId)) continue;

            return TargetArrow.GetArrows(seer, haunter.HaunterId);
        }

        return string.Empty;
    }

    public static bool CanWinWithCrew(PlayerControl pc)
    {
        return CanWinWithCrewmates.GetValue() switch
        {
            0 => false,
            1 => pc.GetTaskState().IsTaskFinished,
            _ => true
        };
    }
}