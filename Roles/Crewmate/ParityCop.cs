using System;
using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using EHR.Neutral;
using HarmonyLib;
using UnityEngine;
using static EHR.Options;
using static EHR.Translator;


namespace EHR.Crewmate;

public class ParityCop : RoleBase
{
    private const int Id = 6900;
    private static List<byte> playerIdList = [];
    public static Dictionary<byte, int> RoundCheckLimit = [];
    public static Dictionary<byte, byte> FirstPick = [];

    public static readonly string[] PcEgoistCountMode =
    [
        "EgoistCountMode.Original",
        "EgoistCountMode.Neutral"
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
    public static OptionItem AbilityChargesWhenFinishedTasks;

    public override bool IsEnable => playerIdList.Count > 0;

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.ParityCop);
        TryHideMsg = new BooleanOptionItem(Id + 10, "ParityCopTryHideMsg", true, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.ParityCop])
            .SetColor(Color.green);
        ParityCheckLimitMax = new IntegerOptionItem(Id + 11, "MaxParityCheckLimit", new(0, 20, 1), 2, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.ParityCop])
            .SetValueFormat(OptionFormat.Times);
        ParityCheckLimitPerMeeting = new IntegerOptionItem(Id + 12, "ParityCheckLimitPerMeeting", new(1, 20, 1), 1, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.ParityCop])
            .SetValueFormat(OptionFormat.Times);
        ParityCheckEgoistCountType = new StringOptionItem(Id + 13, "ParityCheckEgoistickCountMode", PcEgoistCountMode, 1, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.ParityCop]);
        ParityCheckBaitCountType = new BooleanOptionItem(Id + 14, "ParityCheckBaitCountMode", true, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.ParityCop]);
        ParityCheckTargetKnow = new BooleanOptionItem(Id + 15, "ParityCheckTargetKnow", true, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.ParityCop]);
        ParityCheckOtherTargetKnow = new BooleanOptionItem(Id + 16, "ParityCheckOtherTargetKnow", true, TabGroup.CrewmateRoles).SetParent(ParityCheckTargetKnow);
        ParityCheckRevealTargetTeam = new BooleanOptionItem(Id + 17, "ParityCheckRevealTarget", false, TabGroup.CrewmateRoles).SetParent(ParityCheckOtherTargetKnow);
        ParityAbilityUseGainWithEachTaskCompleted = new FloatOptionItem(Id + 18, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 1.5f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.ParityCop])
            .SetValueFormat(OptionFormat.Times);
        AbilityChargesWhenFinishedTasks = new FloatOptionItem(Id + 19, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.ParityCop])
            .SetValueFormat(OptionFormat.Times);
        OverrideTasksData.Create(Id + 20, TabGroup.CrewmateRoles, CustomRoles.ParityCop);
    }

    public static int ParityCheckEgoistInt() => ParityCheckEgoistCountType.GetString() == "EgoistCountMode.Original" ? 0 : 1;

    public override void Init()
    {
        playerIdList = [];
        RoundCheckLimit = [];
        FirstPick = [];
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        playerId.SetAbilityUseLimit(ParityCheckLimitMax.GetInt());
        RoundCheckLimit.Add(playerId, ParityCheckLimitPerMeeting.GetInt());
    }

    public override void OnReportDeadBody()
    {
        RoundCheckLimit.Clear();
        foreach (byte pc in playerIdList)
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

        int operate; // 1:ID 2:Check
        msg = msg.ToLower().TrimStart().TrimEnd();
        if (CheckCommand(ref msg, "id|guesslist|gl编号|玩家编号|玩家id|id列表|玩家列表|列表|所有id|全部id")) operate = 1;
        else if (CheckCommand(ref msg, "compare|cp|cmp|比较", false)) operate = 2;
        else return false;

        if (!pc.IsAlive())
        {
            Utils.SendMessage(GetString("ParityCopDead"), pc.PlayerId);
            return true;
        }

        switch (operate)
        {
            case 1:
                Utils.SendMessage(GuessManager.GetFormatString(), pc.PlayerId);
                return true;
            case 2:
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

                    bool outOfUses = pc.GetAbilityUseLimit() < 1;
                    if (outOfUses || RoundCheckLimit[pc.PlayerId] < 1)
                    {
                        if (outOfUses)
                        {
                            LateTask.New(() =>
                            {
                                if (!isUI) Utils.SendMessage(GetString("ParityCheckMax"), pc.PlayerId);
                                else pc.ShowPopUp(GetString("ParityCheckMax"));
                                Logger.Msg("Check attempted at max checks per game", "Parity Cop");
                            }, 0.2f, "ParityCop 0");
                        }
                        else
                        {
                            LateTask.New(() =>
                            {
                                if (!isUI) Utils.SendMessage(GetString("ParityCheckRound"), pc.PlayerId);
                                else pc.ShowPopUp(GetString("ParityCheckRound"));
                                Logger.Msg("Check attempted at max checks per meeting", "Parity Cop");
                            }, 0.2f, "ParityCop 1");
                        }

                        return true;
                    }

                    if (pc.PlayerId == target1.PlayerId || pc.PlayerId == target2.PlayerId)
                    {
                        LateTask.New(() =>
                        {
                            if (!isUI) Utils.SendMessage(GetString("ParityCheckSelf"), pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckTitle")));
                            else pc.ShowPopUp(Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckSelf")) + "\n" + GetString("ParityCheckTitle"));
                            Logger.Msg("Check attempted on self", "Parity Cop");
                        }, 0.2f, "ParityCop 2");
                        return true;
                    }

                    if (target1.GetCustomRole().IsRevealingRole(target1) || target1.GetCustomSubRoles().Any(role => role.IsRevealingRole(target1)) || target2.GetCustomRole().IsRevealingRole(target2) || target2.GetCustomSubRoles().Any(role => role.IsRevealingRole(target2)))
                    {
                        LateTask.New(() =>
                        {
                            if (!isUI) Utils.SendMessage(GetString("ParityCheckReveal"), pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckTitle")));
                            else pc.ShowPopUp(Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckReveal")) + "\n" + GetString("ParityCheckTitle"));
                            Logger.Msg("Check attempted on revealed role", "Parity Cop");
                        }, 0.2f, "ParityCop 3");
                        return true;
                    }

                    if (AreInSameTeam(target1, target2))
                    {
                        LateTask.New(() =>
                        {
                            if (!isUI) Utils.SendMessage(string.Format(GetString("ParityCheckTrue"), target1.GetRealName(), target2.GetRealName()), pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckTitle")));
                            else pc.ShowPopUp(Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckTrue")) + "\n" + GetString("ParityCheckTitle"));
                            Logger.Msg("Check attempt, result TRUE", "Parity Cop");
                        }, 0.2f, "ParityCop 4");
                    }
                    else
                    {
                        LateTask.New(() =>
                        {
                            if (!isUI) Utils.SendMessage(string.Format(GetString("ParityCheckFalse"), target1.GetRealName(), target2.GetRealName()), pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckTitle")));
                            else pc.ShowPopUp(Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckFalse")) + "\n" + GetString("ParityCheckTitle"));
                            Logger.Msg("Check attempt, result FALSE", "Parity Cop");
                        }, 0.2f, "ParityCop 5");
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
                        LateTask.New(() =>
                        {
                            Utils.SendMessage(textToSend, target1.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckTitle")));
                            Utils.SendMessage(textToSend1, target2.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckTitle")));
                            Logger.Msg("Check attempt, target1 notified", "Parity Cop");
                            Logger.Msg("Check attempt, target2 notified", "Parity Cop");
                        }, 0.2f, "ParityCop");

                        if (ParityCheckRevealTargetTeam.GetBool() && pc.AllTasksCompleted())
                        {
                            string roleT1 = string.Empty, roleT2 = string.Empty;
                            if (target1.Is(Team.Impostor) || target1.GetCustomSubRoles().Any(role => role.IsImpostorTeamV2())) roleT1 = "Impostor";
                            else if (target1.Is(Team.Neutral) || target1.GetCustomSubRoles().Any(role => role.IsNeutralTeamV2())) roleT1 = "Neutral";
                            else if (target1.Is(Team.Crewmate) && (target1.GetCustomSubRoles().Any(role => role.IsCrewmateTeamV2()) || (target1.GetCustomSubRoles().Count == 0)))

                                if (target2.Is(Team.Impostor) || target2.GetCustomSubRoles().Any(role => role.IsImpostorTeamV2())) roleT2 = "Impostor";
                                else if (target2.Is(Team.Neutral) || target2.GetCustomSubRoles().Any(role => role.IsNeutralTeamV2())) roleT2 = "Neutral";
                                else if (target2.Is(Team.Crewmate) && (target2.GetCustomSubRoles().Any(role => role.IsCrewmateTeamV2()) || target2.GetCustomSubRoles().Count == 0)) roleT2 = "Crewmate";

                            LateTask.New(() =>
                            {
                                Utils.SendMessage(string.Format(GetString("ParityCopTargetReveal"), target2.GetRealName(), roleT2), target1.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckTitle")));
                                Utils.SendMessage(string.Format(GetString("ParityCopTargetReveal"), target1.GetRealName(), roleT1), target2.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), GetString("ParityCheckTitle")));
                                Logger.Msg($"check attempt, target1 notified target2 as {roleT2} and target2 notified target1 as {roleT1}", "Parity Cop");
                            }, 0.3f, "ParityCop 6");
                        }
                    }

                    pc.RpcRemoveAbilityUse();
                    RoundCheckLimit[pc.PlayerId]--;
                }

                break;
            }
        }

        return true;
    }

    static bool AreInSameTeam(PlayerControl first, PlayerControl second)
    {
        var firstRole = first.GetCustomRole();
        var secondRole = second.GetCustomRole();

        var firstRoleClass = Main.PlayerStates[first.PlayerId].Role;
        var secondRoleClass = Main.PlayerStates[second.PlayerId].Role;

        var firstSubRoles = first.GetCustomSubRoles();
        var secondSubRoles = second.GetCustomSubRoles();

        var firstTeam = first.GetTeam();
        var secondTeam = second.GetTeam();

        switch (firstRoleClass)
        {
            case Lawyer when Lawyer.Target[first.PlayerId] == second.PlayerId: return true;
            case Totocalcio tc when tc.BetPlayer == second.PlayerId: return true;
            case Romantic when Romantic.HasPickedPartner && Romantic.PartnerId == second.PlayerId: return true;
            case Necromancer when secondRoleClass is Deathknight: return true;
        }

        switch (secondRoleClass)
        {
            case Lawyer when Lawyer.Target[second.PlayerId] == first.PlayerId: return true;
            case Totocalcio tc when tc.BetPlayer == first.PlayerId: return true;
            case Romantic when Romantic.HasPickedPartner && Romantic.PartnerId == first.PlayerId: return true;
            case Necromancer when firstRoleClass is Deathknight: return true;
        }

        if (CustomTeamManager.AreInSameCustomTeam(first.PlayerId, second.PlayerId)) return true;
        if (firstSubRoles.Contains(CustomRoles.Bloodlust) || secondSubRoles.Contains(CustomRoles.Bloodlust)) return false;
        if (first.IsNeutralKiller() && second.IsNeutralKiller()) return true;
        if (firstRole.IsNeutral() && secondRole.IsNeutral()) return false;

        return firstTeam == secondTeam;
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

        id1 = Convert.ToByte(num1);
        id2 = Convert.ToByte(num2);

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

    private static void ParityCopOnClick(byte playerId /*, MeetingHud __instance*/)
    {
        Logger.Msg($"Click: ID {playerId}", "Inspector UI");
        var pc = Utils.GetPlayerById(playerId);
        var lpcId = PlayerControl.LocalPlayer.PlayerId;
        if (pc == null || !pc.IsAlive() || !GameStates.IsVoting || !AmongUsClient.Instance.AmHost) return;
        if (FirstPick.TryGetValue(lpcId, out var firstPick))
        {
            ParityCheckMsg(PlayerControl.LocalPlayer, $"/cp {playerId} {firstPick}");
            FirstPick.Remove(lpcId);
        }
        else
        {
            FirstPick.Add(lpcId, playerId);
        }
    }

    public static void CreateParityCopButton(MeetingHud __instance)
    {
        foreach (var pva in __instance.playerStates)
        {
            var pc = Utils.GetPlayerById(pva.TargetPlayerId);
            if (pc == null || !pc.IsAlive()) continue;
            GameObject template = pva.Buttons.transform.Find("CancelButton").gameObject;
            GameObject targetBox = Object.Instantiate(template, pva.transform);
            targetBox.name = "ShootButton";
            targetBox.transform.localPosition = new(-0.35f, 0.03f, -1.31f);
            SpriteRenderer renderer = targetBox.GetComponent<SpriteRenderer>();
            renderer.sprite = CustomButton.Get("ParityCopIcon");
            PassiveButton button = targetBox.GetComponent<PassiveButton>();
            button.OnClick.RemoveAllListeners();
            button.OnClick.AddListener((Action)(() => ParityCopOnClick(pva.TargetPlayerId /*, __instance*/)));
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
}