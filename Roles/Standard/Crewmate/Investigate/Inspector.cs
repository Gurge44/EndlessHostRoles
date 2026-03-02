using System;
using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using Hazel;
using UnityEngine;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Roles;

public class Inspector : RoleBase
{
    private const int Id = 6900;
    private static List<byte> PlayerIdList = [];
    private static Dictionary<byte, int> RoundCheckLimit = [];
    private static Dictionary<byte, byte> FirstPick = [];

    private static readonly string[] InspectorEgoistCountMode =
    [
        "EgoistCountMode.Original",
        "EgoistCountMode.Neutral"
    ];

    private static OptionItem TryHideMsg;
    private static OptionItem InspectorCheckLimitMax;
    private static OptionItem InspectorCheckLimitPerMeeting;
    private static OptionItem InspectorCheckTargetKnow;
    private static OptionItem InspectorCheckOtherTargetKnow;
    private static OptionItem InspectorCheckEgoistCountType;
    public static OptionItem InspectorCheckBaitCountType;
    private static OptionItem InspectorCheckRevealTargetTeam;
    public static OptionItem InspectorAbilityUseGainWithEachTaskCompleted;
    public static OptionItem InspectorChargesWhenFinishedTasks;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Inspector);

        TryHideMsg = new BooleanOptionItem(Id + 10, "InspectorTryHideMsg", true, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Inspector])
            .SetColor(Color.green);

        InspectorCheckLimitMax = new IntegerOptionItem(Id + 11, "MaxInspectorCheckLimit", new(0, 20, 1), 2, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Inspector])
            .SetValueFormat(OptionFormat.Times);

        InspectorCheckLimitPerMeeting = new IntegerOptionItem(Id + 12, "InspectorCheckLimitPerMeeting", new(1, 20, 1), 1, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Inspector])
            .SetValueFormat(OptionFormat.Times);

        InspectorCheckEgoistCountType = new StringOptionItem(Id + 13, "InspectorCheckEgoistickCountMode", InspectorEgoistCountMode, 0, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Inspector]);

        InspectorCheckBaitCountType = new BooleanOptionItem(Id + 14, "InspectorCheckBaitCountMode", true, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Inspector]);

        InspectorCheckTargetKnow = new BooleanOptionItem(Id + 15, "InspectorCheckTargetKnow", true, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Inspector]);

        InspectorCheckOtherTargetKnow = new BooleanOptionItem(Id + 16, "InspectorCheckOtherTargetKnow", true, TabGroup.CrewmateRoles)
            .SetParent(InspectorCheckTargetKnow);

        InspectorCheckRevealTargetTeam = new BooleanOptionItem(Id + 17, "InspectorCheckRevealTarget", false, TabGroup.CrewmateRoles)
            .SetParent(InspectorCheckOtherTargetKnow);

        InspectorAbilityUseGainWithEachTaskCompleted = new FloatOptionItem(Id + 18, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 1.5f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Inspector])
            .SetValueFormat(OptionFormat.Times);

        InspectorChargesWhenFinishedTasks = new FloatOptionItem(Id + 19, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Inspector])
            .SetValueFormat(OptionFormat.Times);

        OverrideTasksData.Create(Id + 20, TabGroup.CrewmateRoles, CustomRoles.Inspector);
    }

    public static int InspectorCheckEgoistInt()
    {
        return InspectorCheckEgoistCountType.GetString() == "EgoistCountMode.Original" ? 0 : 1;
    }

    public override void Init()
    {
        PlayerIdList = [];
        RoundCheckLimit = [];
        FirstPick = [];
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        playerId.SetAbilityUseLimit(InspectorCheckLimitMax.GetFloat());
        RoundCheckLimit.Add(playerId, InspectorCheckLimitPerMeeting.GetInt());
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override void OnReportDeadBody()
    {
        RoundCheckLimit.Clear();
        foreach (byte pc in PlayerIdList) RoundCheckLimit.Add(pc, InspectorCheckLimitPerMeeting.GetInt());
    }

    public static bool InspectorCheckMsg(PlayerControl pc, string msg, bool isUI = false)
    {
        if (!AmongUsClient.Instance.AmHost) return false;

        if (!GameStates.IsInGame || pc == null) return false;

        if (!pc.Is(CustomRoles.Inspector)) return false;

        int operate; // 1:ID 2:Check
        msg = msg.ToLower().TrimStart().TrimEnd();

        if (GuessManager.CheckCommand(ref msg, "id|guesslist|gl编号|玩家编号|玩家id|id列表|玩家列表|列表|所有id|全部id", true, out bool spamRequired))
            operate = 1;
        else if (GuessManager.CheckCommand(ref msg, "compare|cp|cmp|比较", false, out spamRequired))
            operate = 2;
        else
            return false;

        if (!pc.IsAlive())
        {
            Utils.SendMessage(GetString("InspectorDead"), pc.PlayerId, importance: MessageImportance.Low);
            return true;
        }

        switch (operate)
        {
            case 1:
                Utils.SendMessage(GuessManager.GetFormatString(), pc.PlayerId);
                break;
            case 2:
            {
                if (TryHideMsg.GetBool() && !isUI && spamRequired)
                    Utils.SendMessage("\n", pc.PlayerId, GetString("NoSpamAnymoreUseCmd"));

                if (!MsgToPlayerAndRole(msg, out byte targetId1, out byte targetId2, out string error))
                {
                    Utils.SendMessage(error, pc.PlayerId);
                    return true;
                }

                PlayerControl target1 = Utils.GetPlayerById(targetId1);
                PlayerControl target2 = Utils.GetPlayerById(targetId2);

                if (target1 != null && target2 != null)
                {
                    Logger.Info($"{pc.GetNameWithRole().RemoveHtmlTags()} checked {target1.GetNameWithRole().RemoveHtmlTags()} and {target2.GetNameWithRole().RemoveHtmlTags()}", "Inspector");

                    bool outOfUses = pc.GetAbilityUseLimit() < 1;

                    if (outOfUses || RoundCheckLimit[pc.PlayerId] < 1)
                    {
                        if (outOfUses)
                        {
                            LateTask.New(() =>
                            {
                                if (!isUI)
                                    Utils.SendMessage(GetString("InspectorCheckMax"), pc.PlayerId);
                                else
                                    pc.ShowPopUp(GetString("InspectorCheckMax"));

                                Logger.Msg("Check attempted at max checks per game", "Inspector");
                            }, 0.2f, "Inspector 0");
                        }
                        else
                        {
                            LateTask.New(() =>
                            {
                                if (!isUI)
                                    Utils.SendMessage(GetString("InspectorCheckRound"), pc.PlayerId);
                                else
                                    pc.ShowPopUp(GetString("InspectorCheckRound"));

                                Logger.Msg("Check attempted at max checks per meeting", "Inspector");
                            }, 0.2f, "Inspector 1");
                        }

                        return true;
                    }

                    if (pc.PlayerId == target1.PlayerId || pc.PlayerId == target2.PlayerId)
                    {
                        LateTask.New(() =>
                        {
                            if (!isUI)
                                Utils.SendMessage(GetString("InspectorCheckSelf"), pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Inspector), GetString("InspectorCheckTitle")));
                            else
                                pc.ShowPopUp(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Inspector), GetString("InspectorCheckSelf")) + "\n" + GetString("InspectorCheckTitle"));

                            Logger.Msg("Check attempted on self", "Inspector");
                        }, 0.2f, "Inspector 2");

                        return true;
                    }

                    if (target1.PlayerId == target2.PlayerId)
                    {
                        LateTask.New(() =>
                        {
                            if (!isUI)
                                Utils.SendMessage(GetString("InspectorCheckSame"), pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Inspector), GetString("InspectorCheckTitle")));
                            else
                                pc.ShowPopUp(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Inspector), GetString("InspectorCheckSame")) + "\n" + GetString("InspectorCheckTitle"));

                            Logger.Msg("Check attempted on same player", "Inspector");
                        }, 0.2f, "Inspector 8");

                        return true;
                    }

                    if (target1.GetCustomRole().IsRevealingRole(target1) || target1.GetCustomSubRoles().Any(role => role.IsRevealingRole(target1)) || target2.GetCustomRole().IsRevealingRole(target2) || target2.GetCustomSubRoles().Any(role => role.IsRevealingRole(target2)))
                    {
                        LateTask.New(() =>
                        {
                            if (!isUI)
                                Utils.SendMessage(GetString("InspectorCheckReveal"), pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Inspector), GetString("InspectorCheckTitle")));
                            else
                                pc.ShowPopUp(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Inspector), GetString("InspectorCheckReveal")) + "\n" + GetString("InspectorCheckTitle"));

                            Logger.Msg("Check attempted on revealed role", "Inspector");
                        }, 0.2f, "Inspector 3");

                        return true;
                    }

                    if (AreInSameTeam(target1, target2))
                    {
                        LateTask.New(() =>
                        {
                            string format = string.Format(GetString("InspectorCheckTrue"), target1.GetRealName(), target2.GetRealName());

                            if (!isUI)
                                Utils.SendMessage(format, pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Inspector), GetString("InspectorCheckTitle")), importance: MessageImportance.High);
                            else
                                pc.ShowPopUp(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Inspector), format) + "\n" + GetString("InspectorCheckTitle"));

                            Logger.Msg("Check attempt, result TRUE", "Inspector");
                        }, 0.2f, "Inspector 4");
                    }
                    else
                    {
                        LateTask.New(() =>
                        {
                            string format = string.Format(GetString("InspectorCheckFalse"), target1.GetRealName(), target2.GetRealName());
                            
                            if (!isUI)
                                Utils.SendMessage(format, pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Inspector), GetString("InspectorCheckTitle")), importance: MessageImportance.High);
                            else
                                pc.ShowPopUp($"{Utils.ColorString(Utils.GetRoleColor(CustomRoles.Inspector), format)}\n{GetString("InspectorCheckTitle")}");

                            Logger.Msg("Check attempt, result FALSE", "Inspector");
                        }, 0.2f, "Inspector 5");
                    }

                    if (InspectorCheckTargetKnow.GetBool())
                    {
                        string textToSend = target1.GetRealName();
                        if (InspectorCheckOtherTargetKnow.GetBool()) textToSend += $" & {target2.GetRealName()}";

                        textToSend += GetString("InspectorCheckTargetMsg");

                        string textToSend1 = target2.GetRealName();
                        if (InspectorCheckOtherTargetKnow.GetBool()) textToSend1 += $" & {target1.GetRealName()}";

                        textToSend1 += GetString("InspectorCheckTargetMsg");

                        LateTask.New(() =>
                        {
                            Utils.SendMessage(textToSend, target1.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Inspector), GetString("InspectorCheckTitle")), importance: MessageImportance.High);
                            Utils.SendMessage(textToSend1, target2.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Inspector), GetString("InspectorCheckTitle")), importance: MessageImportance.High);
                            Logger.Msg("Check attempt, targets notified", "Inspector");
                        }, 0.2f, "Inspector 7");

                        if (InspectorCheckRevealTargetTeam.GetBool() && pc.AllTasksCompleted())
                        {
                            LateTask.New(() =>
                            {
                                Utils.SendMessage(string.Format(GetString("InspectorTargetReveal"), target2.GetRealName(), GetString(target2.GetTeam().ToString())), target1.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Inspector), GetString("InspectorCheckTitle")), importance: MessageImportance.High);
                                Utils.SendMessage(string.Format(GetString("InspectorTargetReveal"), target1.GetRealName(), GetString(target1.GetTeam().ToString())), target2.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Inspector), GetString("InspectorCheckTitle")), importance: MessageImportance.High);
                            }, 0.3f, "Inspector 6");
                        }
                    }

                    pc.RpcRemoveAbilityUse();
                    RoundCheckLimit[pc.PlayerId]--;

                    MeetingManager.OnCompare(target1, target2);
                }

                break;
            }
        }

        return true;
    }

    public static bool AreInSameTeam(PlayerControl first, PlayerControl second)
    {
        CustomRoles firstRole = first.GetCustomRole();
        CustomRoles secondRole = second.GetCustomRole();

        RoleBase firstRoleClass = Main.PlayerStates[first.PlayerId].Role;
        RoleBase secondRoleClass = Main.PlayerStates[second.PlayerId].Role;

        List<CustomRoles> firstSubRoles = first.GetCustomSubRoles();
        List<CustomRoles> secondSubRoles = second.GetCustomSubRoles();

        Team firstTeam = first.GetTeam();
        Team secondTeam = second.GetTeam();

        switch (firstRoleClass)
        {
            case Lawyer when Lawyer.Target[first.PlayerId] == second.PlayerId:
            case Follower tc when tc.BetPlayer == second.PlayerId:
            case Romantic when Romantic.HasPickedPartner && Romantic.PartnerId == second.PlayerId:
            case Necromancer when secondRoleClass is Deathknight:
                return true;
        }

        switch (secondRoleClass)
        {
            case Lawyer when Lawyer.Target[second.PlayerId] == first.PlayerId:
            case Follower tc when tc.BetPlayer == first.PlayerId:
            case Romantic when Romantic.HasPickedPartner && Romantic.PartnerId == first.PlayerId:
            case Necromancer when firstRoleClass is Deathknight:
                return true;
        }

        if (CustomTeamManager.AreInSameCustomTeam(first.PlayerId, second.PlayerId)) return true;
        if (firstSubRoles.Contains(CustomRoles.Bloodlust) || secondSubRoles.Contains(CustomRoles.Bloodlust)) return false;
        if (firstRole.IsNeutral() && secondRole.IsNeutral()) return false;

        if (firstSubRoles.Contains(CustomRoles.Rascal) && secondTeam != Team.Impostor) return false;
        if (secondSubRoles.Contains(CustomRoles.Rascal) && firstTeam != Team.Impostor) return false;

        return firstTeam == secondTeam || firstRole == secondRole;
    }

    private static bool MsgToPlayerAndRole(string msg, out byte id1, out byte id2, out string error)
    {
        if (msg.StartsWith("/")) msg = msg.Replace("/", string.Empty);

        msg = msg.TrimStart().TrimEnd();
        Logger.Msg(msg, "Inspector");

        string[] nums = msg.Split(" ");

        if (nums.Length < 2 || !int.TryParse(nums[0], out int num1) || !int.TryParse(nums[1], out int num2))
        {
            Logger.Msg($"nums.Length {nums.Length}, nums0 {nums[0]}, nums1 {nums[1]}", "Inspector");
            id1 = byte.MaxValue;
            id2 = byte.MaxValue;
            error = GetString("InspectorCheckHelp");
            return false;
        }

        id1 = Convert.ToByte(num1);
        id2 = Convert.ToByte(num2);

        PlayerControl target1 = Utils.GetPlayerById(id1);
        PlayerControl target2 = Utils.GetPlayerById(id2);

        if (target1 == null || !target1.IsAlive() || target2 == null || !target2.IsAlive())
        {
            error = GetString("InspectorCheckNull");
            return false;
        }

        error = string.Empty;
        return true;
    }

    public override void OnMeetingShapeshift(PlayerControl shapeshifter, PlayerControl target)
    {
        if (Starspawn.IsDayBreak) return;
        PickForCompare(target.PlayerId, shapeshifter.PlayerId);
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        byte playerId = reader.ReadByte();
        byte lpcId = reader.ReadByte();
        Logger.Msg($"RPC: Comparing ID {playerId}, Inspector ID {lpcId}", "Inspector UI");
        PickForCompare(playerId, lpcId);
    }

    private static void InspectorOnClick(byte playerId /*, MeetingHud __instance*/)
    {
        Logger.Msg($"Click: ID {playerId}", "Inspector UI");
        byte lpcId = PlayerControl.LocalPlayer.PlayerId;

        if (AmongUsClient.Instance.AmHost)
            PickForCompare(playerId, lpcId);
        else
        {
            MessageWriter w = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.InspectorCommand, SendOption.Reliable, AmongUsClient.Instance.HostId);
            w.Write(playerId);
            w.Write(lpcId);
            AmongUsClient.Instance.FinishRpcImmediately(w);
        }
    }

    private static void PickForCompare(byte playerId, byte lpcId)
    {
        PlayerControl pc = Utils.GetPlayerById(playerId);
        if (pc == null || !pc.IsAlive() || !GameStates.IsVoting || Starspawn.IsDayBreak) return;

        if (FirstPick.TryGetValue(lpcId, out byte firstPick))
        {
            InspectorCheckMsg(lpcId.GetPlayer(), $"/cp {playerId} {firstPick}", lpcId.IsPlayerModdedClient());
            FirstPick.Remove(lpcId);
        }
        else
            FirstPick.Add(lpcId, playerId);
    }

    private static void CreateInspectorButton(MeetingHud __instance)
    {
        foreach (PlayerVoteArea pva in __instance.playerStates)
        {
            PlayerControl pc = Utils.GetPlayerById(pva.TargetPlayerId);
            if (pc == null || !pc.IsAlive()) continue;

            GameObject template = pva.Buttons.transform.Find("CancelButton").gameObject;
            GameObject targetBox = Object.Instantiate(template, pva.transform);
            targetBox.name = "ShootButton";
            targetBox.transform.localPosition = new(-0.35f, 0.03f, -1.31f);
            var renderer = targetBox.GetComponent<SpriteRenderer>();
            renderer.sprite = Utils.LoadSprite("EHR.Resources.Images.Skills.InspectorIcon.png", 170f);
            var button = targetBox.GetComponent<PassiveButton>();
            button.OnClick.RemoveAllListeners();
            button.OnClick.AddListener((Action)(() => InspectorOnClick(pva.TargetPlayerId /*, __instance*/)));
        }
    }

    //[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    public static class StartMeetingPatch
    {
        public static void Postfix(MeetingHud __instance)
        {
            if (PlayerControl.LocalPlayer.Is(CustomRoles.Inspector) && PlayerControl.LocalPlayer.IsAlive())
                CreateInspectorButton(__instance);
        }
    }
}
