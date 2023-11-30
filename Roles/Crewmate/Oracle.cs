using Hazel;
using System.Collections.Generic;
using System.Linq;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Crewmate;

public static class Oracle
{
    private static readonly int Id = 7600;
    private static List<byte> playerIdList = [];

    public static OptionItem CheckLimitOpt;
    //  private static OptionItem OracleCheckMode;
    public static OptionItem HideVote;
    public static OptionItem FailChance;
    public static OptionItem OracleAbilityUseGainWithEachTaskCompleted;

    public static List<byte> didVote = [];
    public static Dictionary<byte, float> CheckLimit = [];

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Oracle);
        CheckLimitOpt = IntegerOptionItem.Create(Id + 10, "OracleSkillLimit", new(0, 10, 1), 0, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Oracle])
            .SetValueFormat(OptionFormat.Times);
        //    OracleCheckMode = BooleanOptionItem.Create(Id + 11, "AccurateCheckMode", false, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Oracle]);
        HideVote = BooleanOptionItem.Create(Id + 12, "OracleHideVote", false, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Oracle]);
        //  OverrideTasksData.Create(Id + 20, TabGroup.CrewmateRoles, CustomRoles.Oracle);
        FailChance = IntegerOptionItem.Create(Id + 13, "FailChance", new(0, 100, 5), 0, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Oracle])
            .SetValueFormat(OptionFormat.Percent);
        OracleAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(Id + 14, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.1f), 0.2f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Oracle])
            .SetValueFormat(OptionFormat.Times);
    }
    public static void Init()
    {
        playerIdList = [];
        CheckLimit = [];
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        CheckLimit.TryAdd(playerId, CheckLimitOpt.GetInt());
    }
    public static bool IsEnable => playerIdList.Count > 0;
    public static void SendRPC(byte playerId)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetOracleLimit, SendOption.Reliable, -1);
        writer.Write(playerId);
        writer.Write(CheckLimit[playerId]);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ReceiveRPC(MessageReader reader)
    {
        if (AmongUsClient.Instance.AmHost) return;

        byte playerId = reader.ReadByte();
        float uses = reader.ReadSingle();
        CheckLimit[playerId] = uses;
    }
    public static void OnVote(PlayerControl player, PlayerControl target)
    {
        if (player == null || target == null) return;
        if (didVote.Contains(player.PlayerId)) return;
        didVote.Add(player.PlayerId);

        if (CheckLimit[player.PlayerId] < 1)
        {
            Utils.SendMessage(GetString("OracleCheckReachLimit"), player.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Oracle), GetString("OracleCheckMsgTitle")));
            return;
        }

        CheckLimit[player.PlayerId] -= 1;
        SendRPC(player.PlayerId);

        if (player.PlayerId == target.PlayerId)
        {
            Utils.SendMessage(GetString("OracleCheckSelfMsg") + "\n\n" + string.Format(GetString("OracleCheckLimit"), CheckLimit[player.PlayerId]), player.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Oracle), GetString("OracleCheckMsgTitle")));
            return;
        }

        string text;

        if (target.GetCustomRole().IsImpostor()) text = "Imp";
        else if (target.GetCustomRole().IsNeutral()) text = "Neut";
        else text = "Crew";

        if (FailChance.GetInt() > 0)
        {
            int random_number_1 = IRandom.Instance.Next(1, 101);
            if (random_number_1 <= FailChance.GetInt())
            {
                int random_number_2 = IRandom.Instance.Next(1, 3);
                if (text == "Crew")
                {
                    if (random_number_2 == 1) text = "Neut";
                    if (random_number_2 == 2) text = "Imp";
                }
                if (text == "Neut")
                {
                    if (random_number_2 == 1) text = "Crew";
                    if (random_number_2 == 2) text = "Imp";
                }
                if (text == "Imp")
                {
                    if (random_number_2 == 1) text = "Neut";
                    if (random_number_2 == 2) text = "Crew";
                }
            }
        }

        string msg = string.Format(GetString("OracleCheck." + text), target.GetRealName());

        Utils.SendMessage(GetString("OracleCheck") + "\n" + msg + "\n\n" + string.Format(GetString("OracleCheckLimit"), CheckLimit[player.PlayerId]), player.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Oracle), GetString("OracleCheckMsgTitle")));

    }
}