using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EHR.Modules;
using Hazel;
using UnityEngine;
using static EHR.Translator;

namespace EHR.Roles;

public class Judge : RoleBase
{
    private const int Id = 9300;
    private static List<byte> PlayerIdList = [];

    private static OptionItem TrialLimitPerMeeting;
    private static OptionItem TrialLimitPerGame;
    private static OptionItem AbilityUseLimit;
    private static OptionItem CanTrialMadmate;
    private static OptionItem CanTrialConverted;
    private static OptionItem CanTrialCrewKilling;
    private static OptionItem CanTrialNeutralB;
    private static OptionItem CanTrialNeutralE;
    private static OptionItem CanTrialNeutralK;
    private static OptionItem CanTrialCoven;
    private static OptionItem TryHideMsg;
    public static OptionItem JudgeAbilityUseGainWithEachTaskCompleted;
    public static OptionItem AbilityChargesWhenFinishedTasks;

    private static Dictionary<byte, int> MeetingUseLimit = [];
    private static Dictionary<byte, int> TotalUseLimit = [];

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Judge);
        TrialLimitPerMeeting = new FloatOptionItem(Id + 10, "TrialLimitPerMeeting", new(0f, 15f, 1f), 1f, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Judge]).SetValueFormat(OptionFormat.Times);
        TrialLimitPerGame = new FloatOptionItem(Id + 9, "TrialLimitPerGame", new(0f, 30f, 1f), 3f, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Judge]).SetValueFormat(OptionFormat.Times);
        AbilityUseLimit = new FloatOptionItem(Id + 18, "AbilityUseLimit", new(0f, 30f, 0.5f), 1f, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Judge]).SetValueFormat(OptionFormat.Times);
        CanTrialMadmate = new BooleanOptionItem(Id + 12, "JudgeCanTrialMadmate", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Judge]);
        CanTrialConverted = new BooleanOptionItem(Id + 16, "JudgeCanTrialConverted", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Judge]);
        CanTrialCrewKilling = new BooleanOptionItem(Id + 13, "JudgeCanTrialnCrewKilling", false, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Judge]);
        CanTrialNeutralB = new BooleanOptionItem(Id + 14, "JudgeCanTrialNeutralB", false, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Judge]);
        CanTrialNeutralE = new BooleanOptionItem(Id + 17, "JudgeCanTrialNeutralE", false, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Judge]);
        CanTrialNeutralK = new BooleanOptionItem(Id + 15, "JudgeCanTrialNeutralK", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Judge]);
        CanTrialCoven = new BooleanOptionItem(Id + 21, "JudgeCanTrialCoven", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Judge]);
        TryHideMsg = new BooleanOptionItem(Id + 11, "JudgeTryHideMsg", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Judge]).SetColor(Color.green);
        JudgeAbilityUseGainWithEachTaskCompleted = new FloatOptionItem(Id + 19, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 0.3f, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Judge]).SetValueFormat(OptionFormat.Times);
        AbilityChargesWhenFinishedTasks = new FloatOptionItem(Id + 20, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Judge]).SetValueFormat(OptionFormat.Times);
    }

    public override void Init()
    {
        PlayerIdList = [];
        MeetingUseLimit = [];
        TotalUseLimit = [];
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        MeetingUseLimit[playerId] = TrialLimitPerMeeting.GetInt();
        TotalUseLimit[playerId] = TrialLimitPerGame.GetInt();
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetFloat());
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override void OnReportDeadBody()
    {
        byte[] list = [.. PlayerIdList];
        foreach (byte pid in list) MeetingUseLimit[pid] = TrialLimitPerMeeting.GetInt();
    }

    public static bool TrialMsg(PlayerControl pc, string msg, bool isUI = false)
    {
        if (!AmongUsClient.Instance.AmHost) return false;

        if (!GameStates.IsInGame || pc == null) return false;

        if (!pc.Is(CustomRoles.Judge)) return false;

        int operate; // 1:ID 2:Trial
        msg = msg.ToLower().TrimStart().TrimEnd();

        if (GuessManager.CheckCommand(ref msg, "id|guesslist|gl编号|玩家编号|玩家id|id列表|玩家列表|列表|所有id|全部id", true, out bool spamRequired))
            operate = 1;
        else if (GuessManager.CheckCommand(ref msg, "jj|tl|trial|审判|判|审", false, out spamRequired))
            operate = 2;
        else
            return false;

        if (!pc.IsAlive())
        {
            Utils.SendMessage(GetString("JudgeDead"), pc.PlayerId, importance: MessageImportance.Low);
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

                if (!MsgToPlayerAndRole(msg, out byte targetId, out string error))
                {
                    Utils.SendMessage(error, pc.PlayerId, importance: MessageImportance.Low);
                    return true;
                }

                PlayerControl target = Utils.GetPlayerById(targetId);

                if (target != null)
                {
                    Logger.Info($"{pc.GetNameWithRole().RemoveHtmlTags()} trialed {target.GetNameWithRole().RemoveHtmlTags()}", "Judge");
                    bool judgeSuicide;

                    if (pc.GetAbilityUseLimit() < 1 || MeetingUseLimit[pc.PlayerId] < 1 || TotalUseLimit[pc.PlayerId] < 1)
                    {
                        if (!isUI)
                            Utils.SendMessage(GetString("JudgeTrialMax"), pc.PlayerId);
                        else
                            pc.ShowPopUp(GetString("JudgeTrialMax"));

                        return true;
                    }

                    if (Jailor.PlayerIdList.Any(x => Main.PlayerStates[x].Role is Jailor { IsEnable: true } jl && jl.JailorTarget == target.PlayerId))
                    {
                        if (!isUI)
                            Utils.SendMessage(GetString("CanNotTrialJailed"), pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jailor), GetString("JailorTitle")));
                        else
                            pc.ShowPopUp(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jailor), GetString("JailorTitle")) + "\n" + GetString("CanNotTrialJailed"));

                        return true;
                    }

                    if (Medic.InProtect(target.PlayerId) && !Medic.JudgingIgnoreShield.GetBool())
                    {
                        if (!isUI)
                            Utils.SendMessage(GetString("CanNotTrialProtected"), pc.PlayerId, CustomRoles.Medic.ToColoredString());
                        else
                            pc.ShowPopUp($"{CustomRoles.Medic.ToColoredString()}\n{GetString("CanNotTrialProtected")}");

                        return true;
                    }

                    if (pc.PlayerId == target.PlayerId)
                    {
                        if (!isUI)
                            Utils.SendMessage(GetString("LaughToWhoTrialSelf"), pc.PlayerId, Utils.ColorString(Color.cyan, GetString("MessageFromKPD")), importance: MessageImportance.Low);
                        else
                            pc.ShowPopUp(Utils.ColorString(Color.cyan, GetString("MessageFromKPD")) + "\n" + GetString("LaughToWhoTrialSelf"));

                        judgeSuicide = true;
                    }
                    else if (pc.Is(CustomRoles.Madmate) || pc.Is(CustomRoles.Charmed) || pc.Is(CustomRoles.Contagious) || target.Is(CustomRoles.Rascal) || target.Is(CustomRoles.Madmate) && CanTrialMadmate.GetBool() || target.IsConverted() && CanTrialConverted.GetBool() || target.IsNeutralKiller() && CanTrialNeutralK.GetBool())
                        judgeSuicide = false;
                    else if (target.Is(CustomRoles.Pestilence) || target.Is(CustomRoles.Trickster))
                        judgeSuicide = true;
                    else
                    {
                        CustomRoles targetRole = target.GetCustomRole();

                        if (targetRole.GetCrewmateRoleCategory() == RoleOptionType.Crewmate_Killing && CanTrialCrewKilling.GetBool() || targetRole.GetNeutralRoleCategory() == RoleOptionType.Neutral_Benign && CanTrialNeutralB.GetBool() || targetRole.GetNeutralRoleCategory() is RoleOptionType.Neutral_Evil or RoleOptionType.Neutral_Pariah && CanTrialNeutralE.GetBool() || targetRole.IsNonNK() && !CanTrialNeutralB.GetBool() && !CanTrialNeutralE.GetBool() && targetRole.GetNeutralRoleCategory() is not RoleOptionType.Neutral_Benign and not RoleOptionType.Neutral_Evil and not RoleOptionType.Neutral_Pariah && CanTrialNeutralK.GetBool() || targetRole.IsCoven() && CanTrialCoven.GetBool() || targetRole.IsImpostor() || targetRole.IsMadmate() && CanTrialMadmate.GetBool() || targetRole is CustomRoles.Necromancer or CustomRoles.Deathknight or CustomRoles.Sidekick && CanTrialNeutralK.GetBool())
                            judgeSuicide = false;
                        else
                            judgeSuicide = true;
                    }

                    if (pc.GetCustomSubRoles().Any(x => x.IsConverted() || x == CustomRoles.Bloodlust) && !target.Is(CustomRoles.Pestilence)) judgeSuicide = false;

                    PlayerControl dp = judgeSuicide ? pc : target;

                    string name = dp.GetRealName();

                    pc.RpcRemoveAbilityUse();
                    MeetingUseLimit[pc.PlayerId]--;
                    TotalUseLimit[pc.PlayerId]--;

                    LateTask.New(() =>
                    {
                        Main.PlayerStates[dp.PlayerId].deathReason = PlayerState.DeathReason.Trialed;
                        dp.SetRealKiller(pc);
                        dp.RpcGuesserMurderPlayer();

                        MeetingManager.OnTrial(dp, pc);
                        Utils.AfterPlayerDeathTasks(dp, true);

                        LateTask.New(() => Utils.SendMessage(string.Format(GetString("TrialKill"), name), 255, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Judge), GetString("TrialKillTitle")), importance: MessageImportance.High), 0.6f, "Guess Msg");
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
        var result = string.Empty;
        for (var i = 0; i < mc.Count; i++) result += mc[i];

        if (int.TryParse(result, out int num))
            id = Convert.ToByte(num);
        else
        {
            id = byte.MaxValue;
            error = GetString("TrialHelp");
            return false;
        }

        PlayerControl target = Utils.GetPlayerById(id);

        if (target == null || !target.IsAlive())
        {
            error = GetString("TrialNull");
            return false;
        }

        error = string.Empty;
        return true;
    }

    public override void OnMeetingShapeshift(PlayerControl shapeshifter, PlayerControl target)
    {
        if (Starspawn.IsDayBreak) return;
        TrialMsg(shapeshifter, $"/tl {target.PlayerId}");
    }

    private static void SendRPC(byte playerId)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.Judge, SendOption.Reliable, AmongUsClient.Instance.HostId);
        writer.Write(playerId);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader, PlayerControl pc)
    {
        int playerId = reader.ReadByte();
        TrialMsg(pc, $"/tl {playerId}", true);
    }

    private static void JudgeOnClick(byte playerId /*, MeetingHud __instance*/)
    {
        Logger.Msg($"Click: ID {playerId}", "Judge UI");
        PlayerControl pc = Utils.GetPlayerById(playerId);
        if (pc == null || !pc.IsAlive() || !GameStates.IsVoting || Starspawn.IsDayBreak) return;

        if (AmongUsClient.Instance.AmHost)
            TrialMsg(PlayerControl.LocalPlayer, $"/tl {playerId}", true);
        else
            SendRPC(playerId);
    }

    private static void CreateJudgeButton(MeetingHud __instance)
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
            renderer.sprite = CustomButton.Get("JudgeIcon");
            var button = targetBox.GetComponent<PassiveButton>();
            button.OnClick.RemoveAllListeners();
            button.OnClick.AddListener((Action)(() => JudgeOnClick(pva.TargetPlayerId /*, __instance*/)));
        }
    }

    //[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    public static class StartMeetingPatch
    {
        public static void Postfix(MeetingHud __instance)
        {
            if (PlayerControl.LocalPlayer.Is(CustomRoles.Judge) && PlayerControl.LocalPlayer.IsAlive())
                CreateJudgeButton(__instance);
        }
    }

    public override void ManipulateGameEndCheckCrew(PlayerState playerState, out bool keepGameGoing, out int countsAs)
    {
        if (playerState.IsDead)
        {
            base.ManipulateGameEndCheckCrew(playerState, out keepGameGoing, out countsAs);
            return;
        }

        keepGameGoing = true;
        countsAs = 1;
    }
}

