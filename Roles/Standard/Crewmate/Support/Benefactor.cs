using System.Collections.Generic;
using System.Linq;
using static EHR.Translator;

namespace EHR.Roles;

internal class Benefactor : RoleBase
{
    private const int Id = 8670;
    private static List<byte> PlayerIdList = [];

    private static Dictionary<byte, List<int>> TaskIndex = [];
    public static HashSet<byte> ShieldedPlayers = [];
    private static int MaxTasksMarkedPerRound;

    private static OptionItem TaskMarkPerRoundOpt;
    private static OptionItem ShieldDuration;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Benefactor);

        TaskMarkPerRoundOpt = new IntegerOptionItem(Id + 10, "TaskMarkPerRound", new(1, 14, 1), 3, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Benefactor])
            .SetValueFormat(OptionFormat.Votes);

        ShieldDuration = new IntegerOptionItem(Id + 11, "AidDur", new(1, 30, 1), 10, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Benefactor])
            .SetValueFormat(OptionFormat.Seconds);

        Options.OverrideTasksData.Create(Id + 12, TabGroup.CrewmateRoles, CustomRoles.Benefactor);
    }

    public override void Init()
    {
        PlayerIdList = [];
        TaskIndex = [];
        ShieldedPlayers = [];
        MaxTasksMarkedPerRound = TaskMarkPerRoundOpt.GetInt();
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        playerId.SetAbilityUseLimit(MaxTasksMarkedPerRound);
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override void AfterMeetingTasks()
    {
        if (!IsEnable) return;

        ShieldedPlayers.Clear();
        TaskIndex.SetAllValues([]);
    }

    public static void OnTaskComplete(PlayerControl player, PlayerTask task) // Special case for Benefactor
    {
        if (player == null) return;

        byte playerId = player.PlayerId;

        if (player.Is(CustomRoles.Benefactor))
        {
            if (playerId.GetAbilityUseLimit() < 1) return;
            player.RpcRemoveAbilityUse();

            if (TaskIndex.TryGetValue(playerId, out var list)) list.Add(task.Index);
            else TaskIndex[playerId] = [task.Index];
            
            player.Notify(GetString("BenefactorTaskMarked"));
        }
        else
        {
            foreach (byte benefactorId in TaskIndex.Keys.ToArray())
            {
                if (TaskIndex[benefactorId].Contains(task.Index))
                {
                    PlayerControl benefactorPC = Utils.GetPlayerById(benefactorId);
                    if (benefactorPC == null) continue;

                    player.Notify(GetString("BenefactorTargetGotShieldNotify"));
                    TaskIndex[benefactorId].Remove(task.Index);
                    ShieldedPlayers.Add(playerId);
                    LateTask.New(() => ShieldedPlayers.Remove(playerId), ShieldDuration.GetInt());
                    Logger.Info($"{player.GetAllRoleName()} got a shield because the task was marked by {benefactorPC.GetNameWithRole()}", "Benefactor");
                }
            }
        }
    }
}