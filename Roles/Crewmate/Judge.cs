using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EHR.Modules;
using HarmonyLib;
using Hazel;
using UnityEngine;
using static EHR.Translator;


namespace EHR.Crewmate;

public class Judge : RoleBase
{
    private const int Id = 9300;
    private static List<byte> playerIdList = [];

    public static OptionItem TrialLimitPerMeeting;
    public static OptionItem TrialLimitPerGame;

    private static OptionItem TryHideMsg;
    private static OptionItem CanTrialMadmate;

    private static OptionItem CanTrialCharmed;

    private static OptionItem CanTrialCrewKilling;
    private static OptionItem CanTrialNeutralB;
    private static OptionItem CanTrialNeutralK;
    private static OptionItem CanTrialNeutralE;
    private static OptionItem CanTrialNeutralC;
    public static OptionItem JudgeAbilityUseGainWithEachTaskCompleted;
    public static OptionItem AbilityChargesWhenFinishedTasks;

    private static Dictionary<byte, int> GlobalUseLimit = [];

    public override bool IsEnable => playerIdList.Count > 0;

    public static void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Judge);
        TrialLimitPerMeeting = new FloatOptionItem(Id + 10, "TrialLimitPerMeeting", new(0f, 15f, 1f), 1f, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Judge]).SetValueFormat(OptionFormat.Times);
        TrialLimitPerGame = new FloatOptionItem(Id + 9, "TrialLimitPerGame", new(0f, 30f, 1f), 3f, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Judge]).SetValueFormat(OptionFormat.Times);
        CanTrialMadmate = new BooleanOptionItem(Id + 12, "JudgeCanTrialMadmate", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Judge]);
        CanTrialCharmed = new BooleanOptionItem(Id + 16, "JudgeCanTrialCharmed", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Judge]);
        CanTrialCrewKilling = new BooleanOptionItem(Id + 13, "JudgeCanTrialnCrewKilling", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Judge]);
        CanTrialNeutralB = new BooleanOptionItem(Id + 14, "JudgeCanTrialNeutralB", false, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Judge]);
        CanTrialNeutralE = new BooleanOptionItem(Id + 17, "JudgeCanTrialNeutralE", false, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Judge]);
        CanTrialNeutralC = new BooleanOptionItem(Id + 18, "JudgeCanTrialNeutralC", false, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Judge]);
        CanTrialNeutralK = new BooleanOptionItem(Id + 15, "JudgeCanTrialNeutralK", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Judge]);
        TryHideMsg = new BooleanOptionItem(Id + 11, "JudgeTryHideMsg", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Judge]).SetColor(Color.green);
        JudgeAbilityUseGainWithEachTaskCompleted = new FloatOptionItem(Id + 19, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 0.3f, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Judge]).SetValueFormat(OptionFormat.Times);
        AbilityChargesWhenFinishedTasks = new FloatOptionItem(Id + 20, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Judge]).SetValueFormat(OptionFormat.Times);
    }

    public override void Init()
    {
        playerIdList = [];
        GlobalUseLimit = [];
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        playerId.SetAbilityUseLimit(TrialLimitPerMeeting.GetInt());
        GlobalUseLimit[playerId] = TrialLimitPerGame.GetInt();
    }

    public override void OnReportDeadBody()
    {
        byte[] list = [.. playerIdList];
        foreach (byte pid in list)
        {
            pid.SetAbilityUseLimit(TrialLimitPerMeeting.GetInt());
        }
    }

    public static bool TrialMsg(PlayerControl pc, string msg, bool isUI = false)
    {
        var originMsg = msg;

        if (!AmongUsClient.Instance.AmHost) return false;
        if (!GameStates.IsInGame || pc == null) return false;
        if (!pc.Is(CustomRoles.Judge)) return false;

        int operate; // 1:ID 2:Trial
        msg = msg.ToLower().TrimStart().TrimEnd();
        if (CheckCommand(ref msg, "id|guesslist|gl编号|玩家编号|玩家id|id列表|玩家列表|列表|所有id|全部id")) operate = 1;
        else if (CheckCommand(ref msg, "shoot|guess|bet|st|gs|bt|猜|赌|sp|jj|tl|trial|审判|判|审", false)) operate = 2;
        else return false;

        if (!pc.IsAlive())
        {
            Utils.SendMessage(GetString("JudgeDead"), pc.PlayerId);
            return true;
        }

        switch (operate)
        {
            case 1:
                Utils.SendMessage(GuessManager.GetFormatString(), pc.PlayerId);
                return true;
            case 2:
            {
                if (TryHideMsg.GetBool()) /*GuessManager.TryHideMsg();*/ ChatManager.SendPreviousMessagesToAll();
                else if (pc.AmOwner) Utils.SendMessage(originMsg, 255, pc.GetRealName());

                if (!MsgToPlayerAndRole(msg, out byte targetId, out string error))
                {
                    Utils.SendMessage(error, pc.PlayerId);
                    return true;
                }

                var target = Utils.GetPlayerById(targetId);
                if (target != null)
                {
                    Logger.Info($"{pc.GetNameWithRole().RemoveHtmlTags()} trialed {target.GetNameWithRole().RemoveHtmlTags()}", "Judge");
                    bool judgeSuicide;
                    if (pc.GetAbilityUseLimit() < 1 || GlobalUseLimit[pc.PlayerId] < 1)
                    {
                        if (!isUI) Utils.SendMessage(GetString("JudgeTrialMax"), pc.PlayerId);
                        else pc.ShowPopUp(GetString("JudgeTrialMax"));
                        return true;
                    }

                    if (Jailor.playerIdList.Any(x => Main.PlayerStates[x].Role is Jailor { IsEnable: true } jl && jl.JailorTarget == target.PlayerId))
                    {
                        if (!isUI) Utils.SendMessage(GetString("CanNotTrialJailed"), pc.PlayerId, title: Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jailor), GetString("JailorTitle")));
                        else pc.ShowPopUp(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jailor), GetString("JailorTitle")) + "\n" + GetString("CanNotTrialJailed"));
                        return true;
                    }

                    if (pc.PlayerId == target.PlayerId)
                    {
                        if (!isUI) Utils.SendMessage(GetString("LaughToWhoTrialSelf"), pc.PlayerId, Utils.ColorString(Color.cyan, GetString("MessageFromKPD")));
                        else pc.ShowPopUp(Utils.ColorString(Color.cyan, GetString("MessageFromKPD")) + "\n" + GetString("LaughToWhoTrialSelf"));
                        judgeSuicide = true;
                    }
                    else if (pc.Is(CustomRoles.Madmate)) judgeSuicide = false;
                    else if (pc.Is(CustomRoles.Charmed)) judgeSuicide = false;
                    else if (pc.Is(CustomRoles.Recruit)) judgeSuicide = false;
                    else if (pc.Is(CustomRoles.Contagious)) judgeSuicide = false;
                    else if (target.Is(CustomRoles.Rascal)) judgeSuicide = false;
                    else if (target.Is(CustomRoles.Pestilence)) judgeSuicide = true;
                    else if (target.Is(CustomRoles.Trickster)) judgeSuicide = true;
                    else if (target.Is(CustomRoles.Madmate) && CanTrialMadmate.GetBool()) judgeSuicide = false;
                    else if (target.Is(CustomRoles.Charmed) && CanTrialCharmed.GetBool()) judgeSuicide = false;
                    else if (target.IsNeutralKiller() && CanTrialNeutralK.GetBool()) judgeSuicide = false;
                    else
                    {
                        var targetRole = target.GetCustomRole();
                        if (targetRole.IsCK() && CanTrialCrewKilling.GetBool()) judgeSuicide = false;
                        else if (targetRole.IsNB() && CanTrialNeutralB.GetBool()) judgeSuicide = false;
                        else if (targetRole.IsNE() && CanTrialNeutralE.GetBool()) judgeSuicide = false;
                        else if (targetRole.IsNC() && CanTrialNeutralC.GetBool()) judgeSuicide = false;
                        else if (targetRole.IsNonNK() && !CanTrialNeutralB.GetBool() && !CanTrialNeutralE.GetBool() && !CanTrialNeutralC.GetBool() && !targetRole.IsNB() && !targetRole.IsNC() && !targetRole.IsNE() && CanTrialNeutralK.GetBool()) judgeSuicide = false;
                        else if (targetRole.IsImpostor()) judgeSuicide = false;
                        else if (targetRole.IsMadmate() && CanTrialMadmate.GetBool()) judgeSuicide = false;
                        else if (targetRole is CustomRoles.Necromancer or CustomRoles.Deathknight && CanTrialNeutralK.GetBool()) judgeSuicide = false;
                        else judgeSuicide = true;
                    }

                    var dp = judgeSuicide ? pc : target;

                    string Name = dp.GetRealName();

                    pc.RpcRemoveAbilityUse();
                    GlobalUseLimit[pc.PlayerId]--;

                    LateTask.New(() =>
                    {
                        Main.PlayerStates[dp.PlayerId].deathReason = PlayerState.DeathReason.Trialed;
                        dp.SetRealKiller(pc);
                        dp.RpcGuesserMurderPlayer();

                        Utils.AfterPlayerDeathTasks(dp, true);

                        Utils.NotifyRoles(isForMeeting: false, NoCache: true);

                        LateTask.New(() => { Utils.SendMessage(string.Format(GetString("TrialKill"), Name), 255, Utils.ColorString(Utils.GetRoleColor(CustomRoles.NiceGuesser), GetString("TrialKillTitle"))); }, 0.6f, "Guess Msg");
                    }, 0.2f, "Trial Kill");
                }

                break;
            }
        }

        return true;
    }

    private static bool MsgToPlayerAndRole(string msg, out byte id, out string error)
    {
        if (msg.StartsWith("/")) msg = msg.Replace("/", string.Empty);

        Regex r = new("\\d+");
        MatchCollection mc = r.Matches(msg);
        string result = string.Empty;
        for (int i = 0; i < mc.Count; i++)
        {
            result += mc[i];
        }

        if (int.TryParse(result, out int num))
        {
            id = Convert.ToByte(num);
        }
        else
        {
            id = byte.MaxValue;
            error = GetString("TrialHelp");
            return false;
        }

        PlayerControl target = Utils.GetPlayerById(id);
        if (target == null || target.Data.IsDead)
        {
            error = GetString("TrialNull");
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

    private static void SendRPC(byte playerId)
    {
        if (!Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.Judge, SendOption.Reliable);
        writer.Write(playerId);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader, PlayerControl pc)
    {
        int PlayerId = reader.ReadByte();
        TrialMsg(pc, $"/tl {PlayerId}", true);
    }

    private static void JudgeOnClick(byte playerId /*, MeetingHud __instance*/)
    {
        Logger.Msg($"Click: ID {playerId}", "Judge UI");
        var pc = Utils.GetPlayerById(playerId);
        if (pc == null || !pc.IsAlive() || !GameStates.IsVoting) return;
        if (AmongUsClient.Instance.AmHost) TrialMsg(PlayerControl.LocalPlayer, $"/tl {playerId}", true);
        else SendRPC(playerId);
    }

    public static void CreateJudgeButton(MeetingHud __instance)
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
            renderer.sprite = CustomButton.Get("JudgeIcon");
            PassiveButton button = targetBox.GetComponent<PassiveButton>();
            button.OnClick.RemoveAllListeners();
            button.OnClick.AddListener((Action)(() => JudgeOnClick(pva.TargetPlayerId /*, __instance*/)));
        }
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    class StartMeetingPatch
    {
        public static void Postfix(MeetingHud __instance)
        {
            if (PlayerControl.LocalPlayer.Is(CustomRoles.Judge) && PlayerControl.LocalPlayer.IsAlive())
                CreateJudgeButton(__instance);
        }
    }
}