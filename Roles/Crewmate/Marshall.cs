using System.Collections.Generic;
using static EHR.Options;

namespace EHR.Crewmate;

public class Marshall : RoleBase
{
    private const int Id = 9400;
    private static readonly List<byte> PlayerIdList = [];
    public static OptionItem OptionMadmateCanFindMarshall;
    public static bool MadmateCanFindMarshall;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Marshall);
        OptionMadmateCanFindMarshall = new BooleanOptionItem(Id + 14, "MadmateCanFindMarshall", false, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Marshall]);
        OverrideTasksData.Create(Id + 20, TabGroup.CrewmateRoles, CustomRoles.Marshall);
    }

    public override void Init()
    {
        PlayerIdList.Clear();
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        MadmateCanFindMarshall = OptionMadmateCanFindMarshall.GetBool();
    }

    public static string GetWarningMark(PlayerControl seer, PlayerControl target) => (seer.Is(Team.Crewmate) || (seer.Is(CustomRoles.Madmate) && MadmateCanFindMarshall)) && PlayerIdList.Contains(target.PlayerId) && target.IsAlive() && target.GetTaskState().IsTaskFinished ? Utils.ColorString(Utils.GetRoleColor(CustomRoles.Marshall), "★") : string.Empty;
}