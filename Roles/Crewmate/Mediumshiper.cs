using System.Collections.Generic;
using System.Linq;
using static EHR.Translator;

namespace EHR.Crewmate;

public class Mediumshiper : RoleBase
{
    private const int Id = 7200;
    public static List<byte> playerIdList = [];

    public static OptionItem ContactLimitOpt;
    public static OptionItem OnlyReceiveMsgFromCrew;
    public static OptionItem MediumAbilityUseGainWithEachTaskCompleted;
    public static OptionItem AbilityChargesWhenFinishedTasks;

    public static Dictionary<byte, byte> ContactPlayer = [];

    public override bool IsEnable => playerIdList.Count > 0;

    public static void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Mediumshiper);
        ContactLimitOpt = new IntegerOptionItem(Id + 10, "MediumshiperContactLimit", new(0, 15, 1), 1, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Mediumshiper])
            .SetValueFormat(OptionFormat.Times);
        OnlyReceiveMsgFromCrew = new BooleanOptionItem(Id + 11, "MediumshiperOnlyReceiveMsgFromCrew", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Mediumshiper]);
        MediumAbilityUseGainWithEachTaskCompleted = new FloatOptionItem(Id + 12, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 1f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Mediumshiper])
            .SetValueFormat(OptionFormat.Times);
        AbilityChargesWhenFinishedTasks = new FloatOptionItem(Id + 13, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Mediumshiper])
            .SetValueFormat(OptionFormat.Times);
    }

    public override void Init()
    {
        playerIdList = [];
        ContactPlayer = [];
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        playerId.SetAbilityUseLimit(ContactLimitOpt.GetInt());
    }

    public static void OnReportDeadBody(NetworkedPlayerInfo target)
    {
        ContactPlayer = [];
        if (target == null || target.Object == null) return;
        foreach (var pc in Main.AllAlivePlayerControls.Where(x => playerIdList.Contains(x.PlayerId) && x.PlayerId != target.PlayerId).ToArray())
        {
            if (pc.GetAbilityUseLimit() < 1) continue;
            pc.RpcRemoveAbilityUse();
            ContactPlayer.TryAdd(target.PlayerId, pc.PlayerId);
            Logger.Info($"Medium Connection：{pc.GetNameWithRole().RemoveHtmlTags()} => {target.PlayerName}", "Mediumshiper");
        }
    }

    public static bool MsMsg(PlayerControl pc, string msg)
    {
        if (!AmongUsClient.Instance.AmHost) return false;
        if (!GameStates.IsMeeting || pc == null) return false;
        if (!ContactPlayer.ContainsKey(pc.PlayerId)) return false;
        if (OnlyReceiveMsgFromCrew.GetBool() && !pc.IsCrewmate()) return false;
        if (pc.IsAlive()) return false;
        msg = msg.ToLower().Trim();
        if (!CheckCommand(ref msg, "通灵|ms|mediumship|medium", false)) return false;

        bool ans;
        if (msg.Contains('n') || msg.Contains(GetString("No")) || msg.Contains('错') || msg.Contains("不是")) ans = false;
        else if (msg.Contains('y') || msg.Contains(GetString("Yes")) || msg.Contains('对')) ans = true;
        else
        {
            Utils.SendMessage(GetString("MediumshipHelp"), pc.PlayerId);
            return true;
        }

        Utils.SendMessage(GetString("Mediumship" + (ans ? "Yes" : "No")), ContactPlayer[pc.PlayerId], Utils.ColorString(Utils.GetRoleColor(CustomRoles.Mediumshiper), GetString("MediumshipTitle")));
        Utils.SendMessage(GetString("MediumshipDone"), pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Mediumshiper), GetString("MediumshipTitle")));

        ContactPlayer.Remove(pc.PlayerId);

        return true;
    }

    private static bool CheckCommand(ref string msg, string command, bool exact = true)
    {
        var comList = command.Split('|');
        foreach (string str in comList)
        {
            if (exact && msg == "/" + str)
            {
                return true;
            }

            if (msg.StartsWith("/" + str))
            {
                msg = msg.Replace("/" + str, string.Empty);
                return true;
            }
        }

        return false;
    }
}