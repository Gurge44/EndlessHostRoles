using System.Collections.Generic;
using static EHR.Translator;

namespace EHR.Roles;

public class MeetingManager : RoleBase
{
    public static List<byte> PlayerIdList = [];

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(642640, TabGroup.CrewmateRoles, CustomRoles.MeetingManager);
    }

    public override void Init()
    {
        PlayerIdList = [];
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public static void SendCommandUsedMessage(string command)
    {
        foreach (byte id in PlayerIdList)
            LateTask.New(() => Utils.SendMessage(string.Format(GetString("MeetingManagerMessageAboutCommand"), command), id, Utils.ColorString(Utils.GetRoleColor(CustomRoles.MeetingManager), GetString("MeetingManagerMessageTitle")), importance: MessageImportance.High), 1f, "Meeting Manager Messages");
    }

    public static void OnGuess(PlayerControl dp, PlayerControl pc)
    {
        foreach (byte id in PlayerIdList)
            LateTask.New(() => Utils.SendMessage(dp == pc ? string.Format(GetString("MeetingManagerMessageAboutMisguess"), dp.GetRealName().Replace("\n", " + ")) : string.Format(GetString("MeetingManagerMessageAboutGuessedRole"), dp.GetAllRoleName().Replace("\n", " + ")), id, Utils.ColorString(Utils.GetRoleColor(CustomRoles.MeetingManager), GetString("MeetingManagerMessageTitle")), importance: MessageImportance.High), 1f, "Meeting Manager Messages");
    }

    public static void OnTrial(PlayerControl dp, PlayerControl pc)
    {
        foreach (byte id in PlayerIdList)
            LateTask.New(() => Utils.SendMessage(dp == pc ? string.Format(GetString("MeetingManagerMessageAboutJudgeSuicide"), dp.GetRealName().Replace("\n", " + "), CustomRoles.Judge.ToColoredString()) : string.Format(GetString("MeetingManagerMessageAboutGuessedRole"), dp.GetAllRoleName().Replace("\n", " + ")), id, Utils.ColorString(Utils.GetRoleColor(CustomRoles.MeetingManager), GetString("MeetingManagerMessageTitle")), importance: MessageImportance.High), 1f, "Meeting Manager Messages");
    }

    public static void OnSwap(PlayerControl tg1, PlayerControl tg2)
    {
        foreach (byte id in PlayerIdList)
            LateTask.New(() => Utils.SendMessage(string.Format(GetString("MeetingManagerMessageAboutSwap"), CustomRoles.Swapper.ToColoredString(), tg1.GetRealName().Replace("\n", " + "), tg2.GetRealName().Replace("\n", " + ")), id, Utils.ColorString(Utils.GetRoleColor(CustomRoles.MeetingManager), GetString("MeetingManagerMessageTitle")), importance: MessageImportance.High), 1f, "Meeting Manager Messages");
    }

    public static void OnCompare(PlayerControl tg1, PlayerControl tg2)
    {
        foreach (byte id in PlayerIdList)
            LateTask.New(() => Utils.SendMessage(string.Format(GetString("MeetingManagerMessageAboutCompare"), CustomRoles.Inspector.ToColoredString(), tg1.GetRealName().Replace("\n", " + "), tg2.GetRealName().Replace("\n", " + ")), id, Utils.ColorString(Utils.GetRoleColor(CustomRoles.MeetingManager), GetString("MeetingManagerMessageTitle")), importance: MessageImportance.High), 1f, "Meeting Manager Messages");
    }
}