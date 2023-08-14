using HarmonyLib;
using Hazel;
using Rewired.UI.ControlMapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Crewmate;
public static class ParityCop
{
    private static readonly int Id = 6900;
    private static List<byte> playerIdList = new();
    public static Dictionary<byte, int> MaxCheckLimit = new();
    public static Dictionary<byte, int> RoundCheckLimit = new();
    public static readonly string[] pcEgoistCountMode =
    {
        "EgoistCountMode.Original",
        "EgoistCountMode.Neutral",
    };



    private static OptionItem TryHideMsg;
    public static OptionItem ParityCheckLimitMax;
    public static OptionItem ParityCheckLimitPerMeeting;
    private static OptionItem ParityCheckTargetKnow;
    private static OptionItem ParityCheckOtherTargetKnow;
    public static OptionItem ParityCheckEgoistCountType;
    public static OptionItem ParityCheckBaitCountType;
    public static OptionItem ParityCheckRevealTargetTeam;


    public static void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.ParityCop);
        TryHideMsg = BooleanOptionItem.Create(Id + 10, "ParityCopTryHideMsg", true, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.ParityCop])
            .SetColor(Color.green);
        ParityCheckLimitMax = IntegerOptionItem.Create(Id + 11, "MaxParityCheckLimit", new(1, 99, 1), 5, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.ParityCop])
            .SetValueFormat(OptionFormat.Times);
        ParityCheckLimitPerMeeting = IntegerOptionItem.Create(Id + 12, "ParityCheckLimitPerMeeting", new(1, 99, 1), 1, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.ParityCop])
            .SetValueFormat(OptionFormat.Times);
        ParityCheckEgoistCountType = StringOptionItem.Create(Id + 13, "ParityCheckEgoistickCountMode", pcEgoistCountMode, 1, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.ParityCop]);
        ParityCheckBaitCountType = BooleanOptionItem.Create(Id + 14, "ParityCheckBaitCountMode", true, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.ParityCop]);
        ParityCheckTargetKnow = BooleanOptionItem.Create(Id + 15, "ParityCheckTargetKnow", false, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.ParityCop]);
        ParityCheckOtherTargetKnow = BooleanOptionItem.Create(Id + 16, "ParityCheckOtherTargetKnow", false, TabGroup.CrewmateRoles, false).SetParent(ParityCheckTargetKnow);
        ParityCheckRevealTargetTeam = BooleanOptionItem.Create(Id + 17, "ParityCheckRevealTarget", false, TabGroup.CrewmateRoles, false).SetParent(ParityCheckOtherTargetKnow);
        OverrideTasksData.Create(Id + 20, TabGroup.CrewmateRoles, CustomRoles.ParityCop);
    }
    public static int ParityCheckEgoistInt()
    {
        if (ParityCheckEgoistCountType.GetString() == "EgoistCountMode.Original") return 0;
        else return 1;
    }
    public static void Init()
    {
        playerIdList = new();
        MaxCheckLimit = new();
        RoundCheckLimit = new();
    }

    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        MaxCheckLimit.Add(playerId, ParityCheckLimitMax.GetInt());
        RoundCheckLimit.Add(playerId, ParityCheckLimitPerMeeting.GetInt());
    }
    public static bool IsEnable => playerIdList.Count > 0;
    public static void OnReportDeadBody()
    {
        RoundCheckLimit.Clear();
        foreach (var pc in playerIdList) RoundCheckLimit.Add(pc, ParityCheckLimitPerMeeting.GetInt());
    }

    public static bool ParityCheckMsg(PlayerControl pc, string msg, bool isUI = false)
    {
        var originMsg = msg;

        if (!AmongUsClient.Instance.AmHost) return false;
        if (!GameStates.IsInGame || pc == null) return false;
        if (!pc.Is(CustomRoles.ParityCop)) return false;

        int operate = 0; // 1:ID 2:猜测
        msg = msg.ToLower().TrimStart().TrimEnd();
        if (CheckCommond(ref msg, "id|guesslist|gl编号|玩家编号|玩家id|id列表|玩家列表|列表|所有id|全部id")) operate = 1;
       else if (CheckCommond(ref msg, "compare|cp|cmp|比较", false)) operate = 2;
        else return false;

        if (!pc.IsAlive())
        {
            Utils.SendMessage(GetString("ParityCopDead"), pc.PlayerId);
            return true;
        }

        if (operate == 1)
        {
            Utils.SendMessage(GuessManager.GetFormatString(), pc.PlayerId);
            return true;
        }
        else if (operate == 2)
        {

            if (TryHideMsg.GetBool()) TryHideMsgForCompare();
            else if (pc.AmOwner) Utils.SendMessage(originMsg, 255, pc.GetRealName());

            if (!MsgToPlayerAndRole(msg, out byte targetId1, out byte targetId2, out string error))
            {
                Utils.SendMessage(error, pc.PlayerId);
                return true;
            }
            var target1 = Utils.GetPlayerById(targetId1);
            var target2 = Utils.GetPlayerById(targetId2);
            if (target1 != null && target2 != null)
            {
                Logger.Info($"{pc.GetNameWithRole()} checked {target1.GetNameWithRole()} and {target2.GetNameWithRole()}", "ParityCop");

                if (MaxCheckLimit[pc.PlayerId] < 1 || RoundCheckLimit[pc.PlayerId] < 1)
                {
                    if (MaxCheckLimit[pc.PlayerId] < 1) 
                    {
                        new LateTask(() =>
                        {
                            if (!isUI) Utils.SendMessage(GetString("ParityCheckMax"), pc.PlayerId);
                            else pc.ShowPopUp(GetString("ParityCheckMax"));
                            Logger.Msg("Check attempted at max checks per game","Parity Cop");
                        }, 0.2f, "ParityCop");
                    }
                    else
                    {
                        new LateTask(() =>
                        {
                            if (!isUI) Utils.SendMessage(GetString("ParityCheckRound"), pc.PlayerId);
                            else pc.ShowPopUp(GetString("ParityCheckRound"));
                            Logger.Msg("Check attempted at max checks per meeting","Parity Cop");
                        }, 0.2f, "ParityCop");
                    }
                    return true;
                }
                if (pc.PlayerId == target1.PlayerId || pc.PlayerId == target2.PlayerId)
                {
                    new LateTask(() =>
                    {
                        if (!isUI) Utils.SendMessage(GetString("ParityCheckSelf"), pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckTitle")));
                        else pc.ShowPopUp(Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckSelf")) + "\n" + GetString("ParityCheckTitle"));
                        Logger.Msg("Check attempted on self","Parity Cop");
                    }, 0.2f, "ParityCop");
                    return true;
                }
                else if (target1.GetCustomRole().IsRevealingRole(target1) || target1.GetCustomSubRoles().Any(role => role.IsRevealingRole(target1)) || target2.GetCustomRole().IsRevealingRole(target2) || target2.GetCustomSubRoles().Any(role => role.IsRevealingRole(target2)))
                {
                    new LateTask(() =>
                    {
                        if (!isUI) Utils.SendMessage(GetString("ParityCheckReveal"), pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckTitle")));
                        else pc.ShowPopUp(Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckReveal")) + "\n" + GetString("ParityCheckTitle"));
                        Logger.Msg("Check attempted on revealed role","Parity Cop");
                    }, 0.2f, "ParityCop");
                    return true;
                }
                else
                {
                   
                    if (((target1.GetCustomRole().IsImpostorTeamV2() || target1.GetCustomSubRoles().Any(role => role.IsImpostorTeamV2())) && (target2.GetCustomRole().IsImpostorTeamV2() || target2.GetCustomSubRoles().Any(role => role.IsImpostorTeamV2()))) ||
                    ((target1.GetCustomRole().IsNeutralTeamV2() || target1.GetCustomSubRoles().Any(role => role.IsNeutralTeamV2())) && (target2.GetCustomRole().IsNeutralTeamV2() || target2.GetCustomSubRoles().Any(role => role.IsNeutralTeamV2()))) ||
                    ((target1.GetCustomRole().IsCrewmateTeamV2() && (target1.GetCustomSubRoles().Any(role => role.IsCrewmateTeamV2()) || (target1.GetCustomSubRoles().Count == 0))) && (target2.GetCustomRole().IsCrewmateTeamV2() && (target2.GetCustomSubRoles().Any(role => role.IsCrewmateTeamV2()) || target2.GetCustomSubRoles().Count == 0))))
                    {
                        new LateTask(() =>
                        {
                            if (!isUI) Utils.SendMessage(string.Format(GetString("ParityCheckTrue"),target1.GetRealName(),target2.GetRealName()), pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckTitle")));
                            else pc.ShowPopUp(Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckTrue")) + "\n" + GetString("ParityCheckTitle"));
                            Logger.Msg("Check attempt, result TRUE","Parity Cop");
                        }, 0.2f, "ParityCop");
                    }
                    else
                    {
                        new LateTask(() =>
                        {
                            if (!isUI) Utils.SendMessage(string.Format(GetString("ParityCheckFalse"), target1.GetRealName(), target2.GetRealName()), pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckTitle")));
                            else pc.ShowPopUp(Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckFalse")) + "\n" + GetString("ParityCheckTitle"));
                            Logger.Msg("Check attempt, result FALSE","Parity Cop");
                        }, 0.2f, "ParityCop");
                    }

                    if (ParityCheckTargetKnow.GetBool())
                    {
                        string textToSend = $"{target1.GetRealName()}";
                        if (ParityCheckOtherTargetKnow.GetBool())
                            textToSend = textToSend + $" and {target2.GetRealName()}";
                        textToSend = textToSend + GetString("ParityCheckTargetMsg");

                        string textToSend1 = $"{target2.GetRealName()}";
                        if (ParityCheckOtherTargetKnow.GetBool())
                            textToSend1 = textToSend1 + $" and {target1.GetRealName()}";
                        textToSend1 = textToSend1 + GetString("ParityCheckTargetMsg");
                        new LateTask(() =>
                        {
                            Utils.SendMessage(textToSend, target1.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckTitle")));
                            Utils.SendMessage(textToSend1, target2.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckTitle")));
                            Logger.Msg("Check attempt, target1 notified", "Parity Cop");
                            Logger.Msg("Check attempt, target2 notified","Parity Cop");
                        }, 0.2f, "ParityCop");

                        if (ParityCheckRevealTargetTeam.GetBool() && pc.AllTasksCompleted())
                        {
                            string roleT1 = "", roleT2 = "";
                            if (target1.GetCustomRole().IsImpostorTeamV2() || target1.GetCustomSubRoles().Any(role => role.IsImpostorTeamV2())) roleT1 = "Impostor";
                            else if (target1.GetCustomRole().IsNeutralTeamV2() || target1.GetCustomSubRoles().Any(role => role.IsNeutralTeamV2())) roleT1 = "Neutral";
                            else if (target1.GetCustomRole().IsCrewmateTeamV2() && (target1.GetCustomSubRoles().Any(role => role.IsCrewmateTeamV2()) || (target1.GetCustomSubRoles().Count == 0)))

                            if (target2.GetCustomRole().IsImpostorTeamV2() || target2.GetCustomSubRoles().Any(role => role.IsImpostorTeamV2())) roleT2 = "Impostor";
                            else if (target2.GetCustomRole().IsNeutralTeamV2() || target2.GetCustomSubRoles().Any(role => role.IsNeutralTeamV2())) roleT2 = "Neutral";
                            else if ((target2.GetCustomRole().IsCrewmateTeamV2() && (target2.GetCustomSubRoles().Any(role => role.IsCrewmateTeamV2()) || target2.GetCustomSubRoles().Count == 0))) roleT2 = "Crewmate";

                            new LateTask(() =>
                            { 
                                Utils.SendMessage(string.Format(GetString("ParityCopTargetReveal"),target2.GetRealName(),roleT2), target1.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckTitle")));
                                Utils.SendMessage(string.Format(GetString("ParityCopTargetReveal"), target1.GetRealName(), roleT1), target2.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckTitle")));
                                Logger.Msg($"check attempt, target1 notified target2 as {roleT2} and target2 notified target1 as {roleT1}", "Parity Cop");
                            }, 0.3f,"ParityCop");
                        }
                    }
                    MaxCheckLimit[pc.PlayerId]--;
                    RoundCheckLimit[pc.PlayerId]--;
                }
            }
        }
        return true;
    }

    private static bool MsgToPlayerAndRole(string msg, out byte id1, out byte id2, out string error)
    {
        if (msg.StartsWith("/")) msg = msg.Replace("/", string.Empty);
        msg = msg.TrimStart().TrimEnd();
        Logger.Msg(msg, "ParityCop");

        string[] nums = msg.Split(" ");
        if (nums.Length != 2 || !int.TryParse(nums[0], out int num1) || !int.TryParse(nums[1], out int num2))
        {
            Logger.Msg($"nums.Length {nums.Length}, nums0 {nums[0]}, nums1 {nums[1]}", "ParityCop");
            id1 = byte.MaxValue;
            id2 = byte.MaxValue;
            error = GetString("ParityCheckHelp");
            return false;
        }
        else
        {
            id1 = Convert.ToByte(num1);
            id2 = Convert.ToByte(num2);
        }

        //判断选择的玩家是否合理
        PlayerControl target1 = Utils.GetPlayerById(id1);
        PlayerControl target2 = Utils.GetPlayerById(id2);
        if (target1 == null || target1.Data.IsDead || target2 == null || target2.Data.IsDead)
        {
            error = GetString("ParityCheckNull");
            return false;
        }

        error = string.Empty;
        return true;
    }
    public static bool CheckCommond(ref string msg, string command, bool exact = true)
    {
        var comList = command.Split('|');
        for (int i = 0; i < comList.Count(); i++)
        {
            if (exact)
            {
                if (msg == "/" + comList[i]) return true;
            }
            else
            {
                if (msg.StartsWith("/" + comList[i]))
                {
                    msg = msg.Replace("/" + comList[i], string.Empty);
                    return true;
                }
            }
        }
        return false;
    }
    public static void TryHideMsgForCompare()
    {
        ChatUpdatePatch.DoBlockChat = true;
        List<CustomRoles> roles = Enum.GetValues(typeof(CustomRoles)).Cast<CustomRoles>().Where(x => x is not CustomRoles.NotAssigned and not CustomRoles.KB_Normal).ToList();
        var rd = IRandom.Instance;
        string msg;
        string[] command = new string[] { "cp", "cmp", "compare", "比较" };
        for (int i = 0; i < 20; i++)
        {
            msg = "/";
            if (rd.Next(1, 100) < 20)
            {
                msg += "id";
            }
            else
            {
                msg += command[rd.Next(0, command.Length - 1)];
                msg += " ";
                msg += rd.Next(0, 15).ToString();
                msg +=  " ";
                msg += rd.Next(0, 15).ToString();

            }
            var player = Main.AllAlivePlayerControls.ToArray()[rd.Next(0, Main.AllAlivePlayerControls.Count())];
            DestroyableSingleton<HudManager>.Instance.Chat.AddChat(player, msg);
            var writer = CustomRpcSender.Create("MessagesToSend", SendOption.None);
            writer.StartMessage(-1);
            writer.StartRpc(player.NetId, (byte)RpcCalls.SendChat)
                .Write(msg)
                .EndRpc();
            writer.EndMessage();
            writer.SendMessage();
        }
        ChatUpdatePatch.DoBlockChat = false;
    }
}