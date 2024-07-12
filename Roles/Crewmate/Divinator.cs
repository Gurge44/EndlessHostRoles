using System;
using System.Collections.Generic;
using System.Linq;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Crewmate;

public class Divinator : RoleBase
{
    private const int Id = 6700;
    private const int RolesPerCategory = 5;
    private static List<byte> playerIdList = [];

    public static OptionItem CheckLimitOpt;
    public static OptionItem AccurateCheckMode;
    public static OptionItem HideVote;
    public static OptionItem ShowSpecificRole;
    public static OptionItem AbilityUseGainWithEachTaskCompleted;
    public static OptionItem AbilityChargesWhenFinishedTasks;
    public static OptionItem CancelVote;

    public static List<byte> didVote = [];

    private static Dictionary<byte, List<CustomRoles>> AllPlayerRoleList = [];

    public override bool IsEnable => playerIdList.Count > 0;

    public static void SetupCustomOption()
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
        CancelVote = CreateVoteCancellingUseSetting(Id + 11, CustomRoles.Divinator, TabGroup.CrewmateRoles);
        OverrideTasksData.Create(Id + 21, TabGroup.CrewmateRoles, CustomRoles.Divinator);
    }

    public override void Init()
    {
        playerIdList = [];
        AllPlayerRoleList = [];
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        playerId.SetAbilityUseLimit(CheckLimitOpt.GetInt());

        var players = Main.AllAlivePlayerControls;
        int rolesNeeded = players.Length * (RolesPerCategory - 1);
        var roleList = Enum.GetValues<CustomRoles>()
            .Where(x => !x.IsVanilla() && !x.IsAdditionRole() && x is not CustomRoles.GM and not CustomRoles.Convict && !x.IsForOtherGameMode())
            .OrderBy(x => x.IsEnable() ? IRandom.Instance.Next(10) : IRandom.Instance.Next(10, 100))
            .Take(rolesNeeded)
            .Chunk(RolesPerCategory - 1)
            .Zip(players, (Array, Player) => (RoleList: Array.ToList(), Player))
            .ToArray();
        roleList.Do(x => x.RoleList.Insert(IRandom.Instance.Next(x.RoleList.Count), x.Player.GetCustomRole()));
        AllPlayerRoleList = roleList.ToDictionary(x => x.Player.PlayerId, x => x.RoleList);

        Logger.Info(string.Join(" ---- ", AllPlayerRoleList.Select(x => $"ID {x.Key}: {string.Join(", ", x.Value)}")), "Divinator Roles");
    }

    public static bool OnVote(PlayerControl player, PlayerControl target)
    {
        if (player == null || target == null) return false;
        if (didVote.Contains(player.PlayerId) || Main.DontCancelVoteList.Contains(player.PlayerId)) return false;
        didVote.Add(player.PlayerId);

        if (player.GetAbilityUseLimit() < 1)
        {
            Utils.SendMessage(GetString("DivinatorCheckReachLimit"), player.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Divinator), GetString("DivinatorCheckMsgTitle")));
            return false;
        }

        player.RpcRemoveAbilityUse();

        if (player.PlayerId == target.PlayerId)
        {
            Utils.SendMessage(GetString("DivinatorCheckSelfMsg") + "\n\n" + string.Format(GetString("DivinatorCheckLimit"), player.GetAbilityUseLimit()), player.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Divinator), GetString("DivinatorCheckMsgTitle")));
            return false;
        }

        string msg;

        if ((player.AllTasksCompleted() || AccurateCheckMode.GetBool()) && ShowSpecificRole.GetBool())
        {
            msg = string.Format(GetString("DivinatorCheck.TaskDone"), target.GetRealName(), GetString(target.GetCustomRole().ToString()));
        }
        else
        {
            string roles = string.Join(", ", AllPlayerRoleList[target.PlayerId].Select(x => GetString(x.ToString())));
            msg = string.Format(GetString("DivinatorCheckResult"), target.GetRealName(), roles);
        }

        Utils.SendMessage(GetString("DivinatorCheck") + "\n" + msg + "\n\n" + string.Format(GetString("DivinatorCheckLimit"), player.GetAbilityUseLimit()), player.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Divinator), GetString("DivinatorCheckMsgTitle")));

        Main.DontCancelVoteList.Add(player.PlayerId);
        return true;
    }
}