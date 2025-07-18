using System;
using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using Hazel;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Crewmate;

public class Divinator : RoleBase
{
    private const int Id = 6700;
    private static int RolesPerCategory;
    private static List<byte> PlayerIdList = [];

    public static OptionItem CheckLimitOpt;
    public static OptionItem AccurateCheckMode;
    public static OptionItem HideVote;
    public static OptionItem ShowSpecificRole;
    public static OptionItem NumRolesListedForEachPlayer;
    public static OptionItem AbilityUseGainWithEachTaskCompleted;
    public static OptionItem AbilityChargesWhenFinishedTasks;
    public static OptionItem CancelVote;

    public static readonly List<byte> DidVote = [];

    private static Dictionary<byte, List<CustomRoles>> AllPlayerRoleList = [];

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Divinator);

        CheckLimitOpt = new IntegerOptionItem(Id + 10, "DivinatorSkillLimit", new(0, 20, 1), 1, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Divinator])
            .SetValueFormat(OptionFormat.Times);

        AccurateCheckMode = new BooleanOptionItem(Id + 12, "AccurateCheckMode", false, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Divinator]);

        ShowSpecificRole = new BooleanOptionItem(Id + 13, "ShowSpecificRole", false, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Divinator]);

        HideVote = new BooleanOptionItem(Id + 14, "DivinatorHideVote", false, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Divinator]);

        AbilityUseGainWithEachTaskCompleted = new FloatOptionItem(Id + 15, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 1f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Divinator])
            .SetValueFormat(OptionFormat.Times);

        AbilityChargesWhenFinishedTasks = new FloatOptionItem(Id + 16, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Divinator])
            .SetValueFormat(OptionFormat.Times);

        NumRolesListedForEachPlayer = new IntegerOptionItem(Id + 17, "NumRolesListedForEachPlayer", new(1, 10, 1), 5, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Divinator]);

        CancelVote = CreateVoteCancellingUseSetting(Id + 11, CustomRoles.Divinator, TabGroup.CrewmateRoles);
        OverrideTasksData.Create(Id + 21, TabGroup.CrewmateRoles, CustomRoles.Divinator);
    }

    public override void Init()
    {
        PlayerIdList = [];
        AllPlayerRoleList = [];
        RolesPerCategory = NumRolesListedForEachPlayer.GetInt();
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        playerId.SetAbilityUseLimit(CheckLimitOpt.GetFloat());

        LateTask.New(() =>
        {
            PlayerControl[] players = Main.AllPlayerControls;
            int rolesNeeded = players.Length * (RolesPerCategory - 1);

            (List<CustomRoles> RoleList, PlayerControl Player)[] roleList = Enum.GetValues<CustomRoles>()
                .Where(x => !x.IsVanilla() && !x.IsAdditionRole() && x is not CustomRoles.GM and not CustomRoles.Convict and not CustomRoles.Konan and not CustomRoles.NotAssigned && !x.IsForOtherGameMode() && !CustomRoleSelector.RoleResult.ContainsValue(x))
                .OrderBy(x => x.IsEnable() ? IRandom.Instance.Next(10) : IRandom.Instance.Next(10, 100))
                .Take(rolesNeeded)
                .Chunk(RolesPerCategory - 1)
                .Zip(players, (array, player) => (RoleList: array.ToList(), Player: player))
                .ToArray();

            roleList.Do(x => x.RoleList.Insert(IRandom.Instance.Next(x.RoleList.Count), x.Player.GetCustomRole()));
            AllPlayerRoleList = roleList.ToDictionary(x => x.Player.PlayerId, x => x.RoleList);

            Logger.Info(string.Join(" ---- ", AllPlayerRoleList.Select(x => $"ID {x.Key} ({x.Key.GetPlayer().GetNameWithRole()}): {string.Join(", ", x.Value)}")), "Divinator Roles");
        }, 8f, log: false);
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override bool OnVote(PlayerControl player, PlayerControl target)
    {
        if (player == null || target == null) return false;

        if (DidVote.Contains(player.PlayerId) || Main.DontCancelVoteList.Contains(player.PlayerId)) return false;

        DidVote.Add(player.PlayerId);

        if (player.GetAbilityUseLimit() < 1)
        {
            Utils.SendMessage(GetString("DivinatorCheckReachLimit"), player.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Divinator), GetString("DivinatorCheckMsgTitle")));
            return false;
        }

        player.RpcRemoveAbilityUse();

        if (player.PlayerId == target.PlayerId)
        {
            Utils.SendMessage(GetString("DivinatorCheckSelfMsg") + "\n\n" + string.Format(GetString("DivinatorCheckLimit"), player.GetAbilityUseLimit()), player.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Divinator), GetString("DivinatorCheckMsgTitle")), sendOption: SendOption.None);
            return false;
        }

        string msg;

        if ((player.AllTasksCompleted() || AccurateCheckMode.GetBool()) && ShowSpecificRole.GetBool())
            msg = string.Format(GetString("DivinatorCheck.TaskDone"), target.GetRealName(), target.GetCustomRole().ToColoredString());
        else
        {
            string roles = string.Join(", ", AllPlayerRoleList[target.PlayerId].Select(x => x.ToColoredString()));
            msg = string.Format(GetString("DivinatorCheckResult"), target.GetRealName(), roles);
        }

        Utils.SendMessage(GetString("DivinatorCheck") + "\n" + msg + "\n\n" + string.Format(GetString("DivinatorCheckLimit"), player.GetAbilityUseLimit()), player.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Divinator), GetString("DivinatorCheckMsgTitle")), sendOption: SendOption.None);

        Main.DontCancelVoteList.Add(player.PlayerId);
        return true;
    }

    public static void OnRoleChange(byte id, CustomRoles previousRole, CustomRoles newRole)
    {
        try
        {
            if (!AllPlayerRoleList.TryGetValue(id, out List<CustomRoles> list)) return;

            int index = list.IndexOf(previousRole);
            list.Remove(previousRole);
            list.Insert(index, newRole);
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }
}