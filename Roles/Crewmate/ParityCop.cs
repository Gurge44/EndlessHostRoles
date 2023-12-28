using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Crewmate;
public static class ParityCop
{
    private static readonly int Id = 6900;
    private static List<byte> playerIdList = [];
    public static Dictionary<byte, float> MaxCheckLimit = [];
    public static Dictionary<byte, int> RoundCheckLimit = [];
    public static Dictionary<byte, byte> FirstPick = [];
    public static readonly string[] pcEgoistCountMode =
    [
        "EgoistCountMode.Original",
        "EgoistCountMode.Neutral",
    ];

    private static OptionItem TryHideMsg;
    public static OptionItem ParityCheckLimitMax;
    public static OptionItem ParityCheckLimitPerMeeting;
    private static OptionItem ParityCheckTargetKnow;
    private static OptionItem ParityCheckOtherTargetKnow;
    public static OptionItem ParityCheckEgoistCountType;
    public static OptionItem ParityCheckBaitCountType;
    public static OptionItem ParityCheckRevealTargetTeam;
    public static OptionItem ParityAbilityUseGainWithEachTaskCompleted;

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.ParityCop);
        TryHideMsg = BooleanOptionItem.Create(Id + 10, "ParityCopTryHideMsg", true, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.ParityCop])
            .SetColor(Color.green);
        ParityCheckLimitMax = IntegerOptionItem.Create(Id + 11, "MaxParityCheckLimit", new(0, 20, 1), 2, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.ParityCop])
            .SetValueFormat(OptionFormat.Times);
        ParityCheckLimitPerMeeting = IntegerOptionItem.Create(Id + 12, "ParityCheckLimitPerMeeting", new(1, 20, 1), 1, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.ParityCop])
            .SetValueFormat(OptionFormat.Times);
        ParityCheckEgoistCountType = StringOptionItem.Create(Id + 13, "ParityCheckEgoistickCountMode", pcEgoistCountMode, 1, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.ParityCop]);
        ParityCheckBaitCountType = BooleanOptionItem.Create(Id + 14, "ParityCheckBaitCountMode", true, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.ParityCop]);
        ParityCheckTargetKnow = BooleanOptionItem.Create(Id + 15, "ParityCheckTargetKnow", true, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.ParityCop]);
        ParityCheckOtherTargetKnow = BooleanOptionItem.Create(Id + 16, "ParityCheckOtherTargetKnow", true, TabGroup.CrewmateRoles, false).SetParent(ParityCheckTargetKnow);
        ParityCheckRevealTargetTeam = BooleanOptionItem.Create(Id + 17, "ParityCheckRevealTarget", false, TabGroup.CrewmateRoles, false).SetParent(ParityCheckOtherTargetKnow);
        ParityAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(Id + 18, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.1f), 1.5f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.ParityCop])
            .SetValueFormat(OptionFormat.Times);
        OverrideTasksData.Create(Id + 20, TabGroup.CrewmateRoles, CustomRoles.ParityCop);
    }
    public static int ParityCheckEgoistInt()
    {
        if (ParityCheckEgoistCountType.GetString() == "EgoistCountMode.Original") return 0;
        else return 1;
    }
    public static void Init()
    {
        playerIdList = [];
        MaxCheckLimit = [];
        RoundCheckLimit = [];
        FirstPick = [];
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
        foreach (byte pc in playerIdList.ToArray())
        {
            RoundCheckLimit.Add(pc, ParityCheckLimitPerMeeting.GetInt());
        }
    }

    public static bool ParityCheckMsg(PlayerControl pc, string msg, bool isUI = false)
    {
        var originMsg = msg;

        if (!AmongUsClient.Instance.AmHost) return false;
        if (!GameStates.IsInGame || pc == null) return false;
        if (!pc.Is(CustomRoles.ParityCop)) return false;

        int operate = 0; // 1:ID 2:猜测
        msg = msg.ToLower().TrimStart().TrimEnd();
        if (CheckCommand(ref msg, "id|guesslist|gl编号|玩家编号|玩家id|id列表|玩家列表|列表|所有id|全部id")) operate = 1;
        else if (CheckCommand(ref msg, "compare|cp|cmp|比较", false)) operate = 2;
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

            if (TryHideMsg.GetBool()) /*TryHideMsgForCompare();*/ ChatManager.SendPreviousMessagesToAll();
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
                Logger.Info($"{pc.GetNameWithRole().RemoveHtmlTags()} checked {target1.GetNameWithRole().RemoveHtmlTags()} and {target2.GetNameWithRole().RemoveHtmlTags()}", "ParityCop");

                if (MaxCheckLimit[pc.PlayerId] < 1 || RoundCheckLimit[pc.PlayerId] < 1)
                {
                    if (MaxCheckLimit[pc.PlayerId] < 1)
                    {
                        _ = new LateTask(() =>
                        {
                            if (!isUI) Utils.SendMessage(GetString("ParityCheckMax"), pc.PlayerId);
                            else pc.ShowPopUp(GetString("ParityCheckMax"));
                            Logger.Msg("Check attempted at max checks per game", "Parity Cop");
                        }, 0.2f, "ParityCop");
                    }
                    else
                    {
                        _ = new LateTask(() =>
                        {
                            if (!isUI) Utils.SendMessage(GetString("ParityCheckRound"), pc.PlayerId);
                            else pc.ShowPopUp(GetString("ParityCheckRound"));
                            Logger.Msg("Check attempted at max checks per meeting", "Parity Cop");
                        }, 0.2f, "ParityCop");
                    }
                    return true;
                }
                if (pc.PlayerId == target1.PlayerId || pc.PlayerId == target2.PlayerId)
                {
                    _ = new LateTask(() =>
                    {
                        if (!isUI) Utils.SendMessage(GetString("ParityCheckSelf"), pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckTitle")));
                        else pc.ShowPopUp(Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckSelf")) + "\n" + GetString("ParityCheckTitle"));
                        Logger.Msg("Check attempted on self", "Parity Cop");
                    }, 0.2f, "ParityCop");
                    return true;
                }
                else if (target1.GetCustomRole().IsRevealingRole(target1) || target1.GetCustomSubRoles().Any(role => role.IsRevealingRole(target1)) || target2.GetCustomRole().IsRevealingRole(target2) || target2.GetCustomSubRoles().Any(role => role.IsRevealingRole(target2)))
                {
                    _ = new LateTask(() =>
                    {
                        if (!isUI) Utils.SendMessage(GetString("ParityCheckReveal"), pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckTitle")));
                        else pc.ShowPopUp(Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckReveal")) + "\n" + GetString("ParityCheckTitle"));
                        Logger.Msg("Check attempted on revealed role", "Parity Cop");
                    }, 0.2f, "ParityCop");
                    return true;
                }
                else
                {

                    if (((target1.Is(Team.Impostor) || target1.GetCustomSubRoles().Any(role => role.IsImpostorTeamV3())) && (target2.Is(Team.Impostor) || target2.GetCustomSubRoles().Any(role => role.IsImpostorTeamV3()))) ||
                    ((target1.Is(Team.Neutral) || target1.GetCustomSubRoles().Any(role => role.IsNeutralTeamV2())) && (target2.Is(Team.Neutral) || target2.GetCustomSubRoles().Any(role => role.IsNeutralTeamV2()))) ||
                    (target1.Is(Team.Crewmate) && (target1.GetCustomSubRoles().Any(role => role.IsCrewmateTeamV2()) || (target1.GetCustomSubRoles().Count == 0)) && target2.Is(Team.Crewmate) && (target2.GetCustomSubRoles().Any(role => role.IsCrewmateTeamV2()) || target2.GetCustomSubRoles().Count == 0)))
                    {
                        _ = new LateTask(() =>
                        {
                            if (!isUI) Utils.SendMessage(string.Format(GetString("ParityCheckTrue"), target1.GetRealName(), target2.GetRealName()), pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckTitle")));
                            else pc.ShowPopUp(Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckTrue")) + "\n" + GetString("ParityCheckTitle"));
                            Logger.Msg("Check attempt, result TRUE", "Parity Cop");
                        }, 0.2f, "ParityCop");
                    }
                    else
                    {
                        _ = new LateTask(() =>
                        {
                            if (!isUI) Utils.SendMessage(string.Format(GetString("ParityCheckFalse"), target1.GetRealName(), target2.GetRealName()), pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckTitle")));
                            else pc.ShowPopUp(Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckFalse")) + "\n" + GetString("ParityCheckTitle"));
                            Logger.Msg("Check attempt, result FALSE", "Parity Cop");
                        }, 0.2f, "ParityCop");
                    }

                    if (ParityCheckTargetKnow.GetBool())
                    {
                        string textToSend = $"{target1.GetRealName()}";
                        if (ParityCheckOtherTargetKnow.GetBool())
                            textToSend += $" and {target2.GetRealName()}";
                        textToSend += GetString("ParityCheckTargetMsg");

                        string textToSend1 = $"{target2.GetRealName()}";
                        if (ParityCheckOtherTargetKnow.GetBool())
                            textToSend1 += $" and {target1.GetRealName()}";
                        textToSend1 += GetString("ParityCheckTargetMsg");
                        _ = new LateTask(() =>
                        {
                            Utils.SendMessage(textToSend, target1.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckTitle")));
                            Utils.SendMessage(textToSend1, target2.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckTitle")));
                            Logger.Msg("Check attempt, target1 notified", "Parity Cop");
                            Logger.Msg("Check attempt, target2 notified", "Parity Cop");
                        }, 0.2f, "ParityCop");

                        if (ParityCheckRevealTargetTeam.GetBool() && pc.AllTasksCompleted())
                        {
                            string roleT1 = string.Empty, roleT2 = string.Empty;
                            if (target1.Is(Team.Impostor) || target1.GetCustomSubRoles().Any(role => role.IsImpostorTeamV3())) roleT1 = "Impostor";
                            else if (target1.Is(Team.Neutral) || target1.GetCustomSubRoles().Any(role => role.IsNeutralTeamV2())) roleT1 = "Neutral";
                            else if (target1.Is(Team.Crewmate) && (target1.GetCustomSubRoles().Any(role => role.IsCrewmateTeamV2()) || (target1.GetCustomSubRoles().Count == 0)))

                                if (target2.Is(Team.Impostor) || target2.GetCustomSubRoles().Any(role => role.IsImpostorTeamV3())) roleT2 = "Impostor";
                                else if (target2.Is(Team.Neutral) || target2.GetCustomSubRoles().Any(role => role.IsNeutralTeamV2())) roleT2 = "Neutral";
                                else if (target2.Is(Team.Crewmate) && (target2.GetCustomSubRoles().Any(role => role.IsCrewmateTeamV2()) || target2.GetCustomSubRoles().Count == 0)) roleT2 = "Crewmate";

                            _ = new LateTask(() =>
                            {
                                Utils.SendMessage(string.Format(GetString("ParityCopTargetReveal"), target2.GetRealName(), roleT2), target1.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckTitle")));
                                Utils.SendMessage(string.Format(GetString("ParityCopTargetReveal"), target1.GetRealName(), roleT1), target2.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckTitle")));
                                Logger.Msg($"check attempt, target1 notified target2 as {roleT2} and target2 notified target1 as {roleT1}", "Parity Cop");
                            }, 0.3f, "ParityCop");
                        }
                    }
                    MaxCheckLimit[pc.PlayerId] -= 1;
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
    public static bool CheckCommand(ref string msg, string command, bool exact = true)
    {
        var comList = command.Split('|');
        foreach (string str in comList)
        {
            if (exact)
            {
                if (msg == "/" + str) return true;
            }
            else
            {
                if (msg.StartsWith("/" + str))
                {
                    msg = msg.Replace("/" + str, string.Empty);
                    return true;
                }
            }
        }
        return false;
    }
    //public static void TryHideMsgForCompare()
    //{
    //    ChatUpdatePatch.DoBlockChat = true;
    //    List<CustomRoles> roles = Enum.GetValues(typeof(CustomRoles)).Cast<CustomRoles>().Where(x => x is not CustomRoles.NotAssigned and not CustomRoles.KB_Normal).ToList();
    //    var rd = IRandom.Instance;
    //    string msg;
    //    string[] command = new string[] { "cp", "cmp", "compare", "比较" };
    //    for (int i = 0; i < 20; i++)
    //    {
    //        msg = "/";
    //        if (rd.Next(1, 100) < 20)
    //        {
    //            msg += "id";
    //        }
    //        else
    //        {
    //            msg += command[rd.Next(0, command.Length - 1)];
    //            msg += " ";
    //            msg += rd.Next(0, 15).ToString();
    //            msg += " ";
    //            msg += rd.Next(0, 15).ToString();

    //        }
    //        var player = Main.AllAlivePlayerControls[rd.Next(0, Main.AllAlivePlayerControls.Count())];
    //        DestroyableSingleton<HudManager>.Instance.Chat.AddChat(player, msg);
    //        var writer = CustomRpcSender.Create("MessagesToSend", SendOption.None);
    //        writer.StartMessage(-1);
    //        writer.StartRpc(player.NetId, (byte)RpcCalls.SendChat)
    //            .Write(msg)
    //            .EndRpc();
    //        writer.EndMessage();
    //        writer.SendMessage();
    //    }
    //    ChatUpdatePatch.DoBlockChat = false;
    //}
    private static void ParityCopOnClick(byte playerId, MeetingHud __instance)
    {
        Logger.Msg($"Click: ID {playerId}", "Inspector UI");
        var pc = Utils.GetPlayerById(playerId);
        var lpcId = PlayerControl.LocalPlayer.PlayerId;
        if (pc == null || !pc.IsAlive() || !GameStates.IsVoting || !AmongUsClient.Instance.AmHost) return;
        if (FirstPick.TryGetValue(lpcId, out var firstPick))
        {
            ParityCheckMsg(PlayerControl.LocalPlayer, $"/cp {playerId} {firstPick}", true);
            FirstPick.Remove(lpcId);
        }
        else
        {
            FirstPick.Add(lpcId, playerId);
        }
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    class StartMeetingPatch
    {
        public static void Postfix(MeetingHud __instance)
        {
            if (PlayerControl.LocalPlayer.Is(CustomRoles.ParityCop) && PlayerControl.LocalPlayer.IsAlive() && AmongUsClient.Instance.AmHost)
                CreateParityCopButton(__instance);
        }
    }
    public static void CreateParityCopButton(MeetingHud __instance)
    {
        foreach (var pva in __instance.playerStates)
        {
            var pc = Utils.GetPlayerById(pva.TargetPlayerId);
            if (pc == null || !pc.IsAlive()) continue;
            GameObject template = pva.Buttons.transform.Find("CancelButton").gameObject;
            GameObject targetBox = UnityEngine.Object.Instantiate(template, pva.transform);
            targetBox.name = "ShootButton";
            targetBox.transform.localPosition = new Vector3(-0.35f, 0.03f, -1.31f);
            SpriteRenderer renderer = targetBox.GetComponent<SpriteRenderer>();
            renderer.sprite = CustomButton.Get("ParityCopIcon");
            PassiveButton button = targetBox.GetComponent<PassiveButton>();
            button.OnClick.RemoveAllListeners();
            button.OnClick.AddListener((Action)(() => ParityCopOnClick(pva.TargetPlayerId, __instance)));
        }
    }
}