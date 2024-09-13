using System;
using System.Collections.Generic;
using static EHR.Options;

namespace EHR.Crewmate;

public class Marshall : RoleBase
{
    private const int Id = 9400;
    private static readonly List<byte> PlayerIdList = [];

    private static readonly Dictionary<SeeingTeam, OptionItem> SeeingTeamOptions = [];
    public static OptionItem CanBeGuessedOnTaskCompletion;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Marshall);
        Enum.GetValues<SeeingTeam>().Do(x =>
        {
            SeeingTeamOptions[x] = new BooleanOptionItem(Id + 2 + (int)x, $"{x}CanFindMarshall", false, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Marshall]);
        });
        CanBeGuessedOnTaskCompletion = new BooleanOptionItem(Id + 10, "MarshallCanBeGuessedOnTaskCompletion", false, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Marshall]);
        OverrideTasksData.Create(Id + 20, TabGroup.CrewmateRoles, CustomRoles.Marshall);
    }

    public override void Init()
    {
        PlayerIdList.Clear();
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
    }

    public static string GetWarningMark(PlayerControl seer, PlayerControl target) => CanSeeMarshall(seer) && PlayerIdList.Contains(target.PlayerId) && target.IsAlive() && target.GetTaskState().IsTaskFinished ? Utils.ColorString(Utils.GetRoleColor(CustomRoles.Marshall), "★") : string.Empty;

    public static bool CanSeeMarshall(PlayerControl seer)
    {
        var team = seer.GetTeam();
        var madmate = seer.IsMadmate();
        return team switch
        {
            Team.Crewmate when !madmate => true,
            Team.Neutral when seer.IsNeutralKiller() => SeeingTeamOptions[SeeingTeam.NK].GetBool(),
            Team.Neutral => SeeingTeamOptions[SeeingTeam.NNK].GetBool(),
            Team.Impostor when madmate => SeeingTeamOptions[SeeingTeam.Madmate].GetBool(),
            Team.Impostor => SeeingTeamOptions[SeeingTeam.Imp].GetBool(),
            _ => false
        };
    }

    enum SeeingTeam
    {
        NNK,
        NK,
        Imp,
        Madmate
    }
}