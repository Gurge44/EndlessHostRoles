using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EHR.Modules;
using Hazel;
using UnityEngine;
using static EHR.Translator;

namespace EHR.Roles;

public class Councillor : RoleBase
{
    private const int Id = 900;
    private static List<byte> PlayerIdList = [];

    private static OptionItem KillCooldown;
    private static OptionItem AbilityUseLimit;
    private static OptionItem MurderLimitPerGame;
    private static OptionItem MurderLimitPerMeeting;
    private static OptionItem MakeEvilJudgeClear;
    private static OptionItem CanMurderMadmate;
    private static OptionItem CanMurderImpostor;
    private static OptionItem TryHideMsg;
    public static OptionItem CouncillorAbilityUseGainWithEachKill;
    
    private static Dictionary<byte, int> MeetingKillLimit = [];
    private static Dictionary<byte, int> TotalKillLimit = [];

    private byte CouncillorId;
    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Councillor);

        KillCooldown = new FloatOptionItem(Id + 15, "KillCooldown", new(0f, 180f, 0.5f), 25f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Councillor])
            .SetValueFormat(OptionFormat.Seconds);

        AbilityUseLimit = new FloatOptionItem(Id + 13, "AbilityUseLimit", new(0, 20, 0.05f), 0, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Councillor])
            .SetValueFormat(OptionFormat.Times);

        MurderLimitPerGame = new IntegerOptionItem(Id + 10, "MurderLimitPerGame", new(0, 15, 1), 3, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Councillor])
            .SetValueFormat(OptionFormat.Times);

        MurderLimitPerMeeting = new IntegerOptionItem(Id + 14, "MurderLimitPerMeeting", new(0, 5, 1), 1, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Councillor])
            .SetValueFormat(OptionFormat.Times);

        MakeEvilJudgeClear = new BooleanOptionItem(Id + 18, "CouncillorMakeEvilJudgeClear", true, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Councillor]);

        CanMurderMadmate = new BooleanOptionItem(Id + 12, "CouncillorCanMurderMadmate", true, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Councillor]);

        CanMurderImpostor = new BooleanOptionItem(Id + 16, "CouncillorCanMurderImpostor", true, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Councillor]);

        TryHideMsg = new BooleanOptionItem(Id + 11, "CouncillorTryHideMsg", true, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Councillor])
            .SetColor(Color.green);

        CouncillorAbilityUseGainWithEachKill = new FloatOptionItem(Id + 17, "AbilityUseGainWithEachKill", new(0f, 5f, 0.1f), 0.3f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Councillor])
            .SetValueFormat(OptionFormat.Times);
    }

    public override void Init()
    {
        PlayerIdList = [];
        MeetingKillLimit = [];
        TotalKillLimit = [];
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        CouncillorId = playerId;
        MeetingKillLimit[playerId] = MurderLimitPerMeeting.GetInt();
        TotalKillLimit[playerId] = MurderLimitPerGame.GetInt();
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetFloat());
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override void AfterMeetingTasks()
    {
        MeetingKillLimit[CouncillorId] = MurderLimitPerMeeting.GetInt();
    }

    public static bool MurderMsg(PlayerControl pc, string msg, bool isUI = false)
    {
        if (!AmongUsClient.Instance.AmHost) return false;

        if (!GameStates.IsInGame || pc == null) return false;

        if (!pc.Is(CustomRoles.Councillor)) return false;

        int operate; // 1:ID 2:Kill
        msg = msg.ToLower().TrimStart().TrimEnd();

        if (GuessManager.CheckCommand(ref msg, "id|guesslist|gl编号|玩家编号|玩家id|id列表|玩家列表|列表|所有id|全部id", true, out bool spamRequired))
            operate = 1;
        else if (GuessManager.CheckCommand(ref msg, "shoot|guess|bet|st|gs|bt|猜|赌|sp|jj|tl|审判|判|审", false, out spamRequired))
            operate = 2;
        else
            return false;

        if (!pc.IsAlive())
        {
            Utils.SendMessage(GetString("CouncillorDead"), pc.PlayerId, importance: MessageImportance.Low);
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
                    Utils.SendMessage(error, pc.PlayerId);
                    return true;
                }

                PlayerControl target = Utils.GetPlayerById(targetId);

                if (target != null)
                {
                    Logger.Info($"{pc.GetNameWithRole().RemoveHtmlTags()} murdered {target.GetNameWithRole().RemoveHtmlTags()}", "Councillor");
                    var councillorSuicide = true;

                    if (pc.GetAbilityUseLimit() < 1)
                    {
                        if (!isUI)
                            Utils.SendMessage(GetString("CouncillorMurderMax"), pc.PlayerId);
                        else
                            pc.ShowPopUp(GetString("CouncillorMurderMax"));

                        return true;
                    }

                    if (MeetingKillLimit[pc.PlayerId] < 1)
                    {
                        if (!isUI)
                            Utils.SendMessage(GetString("CouncillorMurderMaxMeeting"), pc.PlayerId);
                        else
                            pc.ShowPopUp(GetString("CouncillorMurderMaxMeeting"));

                        return true;
                    }

                    if (TotalKillLimit[pc.PlayerId] < 1)
                    {
                        if (!isUI)
                            Utils.SendMessage(GetString("MurderMaxGame"), pc.PlayerId);
                        else
                            pc.ShowPopUp(GetString("MurderMaxGame"));

                        return true;
                    }

                    if (Jailor.PlayerIdList.Any(x => Main.PlayerStates[x].Role is Jailor { IsEnable: true } jl && jl.JailorTarget == targetId))
                    {
                        if (!isUI)
                            Utils.SendMessage(GetString("CanNotTrialJailed"), pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jailor), GetString("JailorTitle")));
                        else
                            pc.ShowPopUp(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jailor), GetString("JailorTitle")) + "\n" + GetString("CanNotTrialJailed"));

                        return true;
                    }

                    var noSuicide = false;

                    if (pc.PlayerId == targetId)
                    {
                        if (!isUI)
                            Utils.SendMessage(GetString("LaughToWhoMurderSelf"), pc.PlayerId, Utils.ColorString(Color.cyan, GetString("MessageFromKPD")));
                        else
                            pc.ShowPopUp(Utils.ColorString(Color.cyan, GetString("MessageFromKPD")) + "\n" + GetString("LaughToWhoMurderSelf"));
                    }
                    else if (target.IsMadmate() && CanMurderMadmate.GetBool())
                        councillorSuicide = false;
                    else if (target.Is(CustomRoles.SuperStar) || target.Is(CustomRoles.Snitch) && target.AllTasksCompleted() || target.Is(CustomRoles.Guardian) && target.AllTasksCompleted() || target.Is(CustomRoles.Merchant) && Merchant.IsBribedKiller(pc, target))
                        noSuicide = true;
                    else if (target.IsImpostor() && CanMurderImpostor.GetBool() || target.IsCrewmate() || target.GetCustomRole().IsNeutral() && !target.Is(CustomRoles.Pestilence) || target.Is(CustomRoleTypes.Coven))
                        councillorSuicide = false;

                    if (noSuicide) return true;

                    PlayerControl dp = councillorSuicide ? pc : target;

                    string name = dp.GetRealName();

                    pc.RpcRemoveAbilityUse();
                    MeetingKillLimit[pc.PlayerId]--;
                    TotalKillLimit[pc.PlayerId]--;

                    LateTask.New(() =>
                    {
                        Main.PlayerStates[dp.PlayerId].deathReason = PlayerState.DeathReason.Trialed;
                        dp.SetRealKiller(pc);
                        dp.RpcGuesserMurderPlayer();

                        Utils.AfterPlayerDeathTasks(dp, true);

                        LateTask.New(() =>
                        {
                            if (!MakeEvilJudgeClear.GetBool())
                                Utils.SendMessage(string.Format(GetString("TrialKill"), name), 255, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Judge), GetString("TrialKillTitle")), importance: MessageImportance.High);
                            else
                                Utils.SendMessage(string.Format(GetString("MurderKill"), name), 255, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Councillor), GetString("MurderKillTitle")), importance: MessageImportance.High);
                        }, 0.6f, "Guess Msg");
                        
                    }, 0.2f, "Murder Kill");
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
            error = GetString("MurderHelp");
            return false;
        }

        PlayerControl target = Utils.GetPlayerById(id);

        if (target == null || !target.IsAlive())
        {
            error = GetString("MurderNull");
            return false;
        }

        error = string.Empty;
        return true;
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    public override void OnMeetingShapeshift(PlayerControl shapeshifter, PlayerControl target)
    {
        if (Starspawn.IsDayBreak) return;
        MurderMsg(shapeshifter, $"/tl {target.PlayerId}");
    }

    private static void SendRPC(byte playerId)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.MeetingKill, SendOption.Reliable, AmongUsClient.Instance.HostId);
        writer.Write(playerId);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader, PlayerControl pc)
    {
        int PlayerId = reader.ReadByte();
        MurderMsg(pc, $"/tl {PlayerId}", true);
    }

    private static void CouncillorOnClick(byte playerId /*, MeetingHud __instance*/)
    {
        Logger.Msg($"Click: ID {playerId}", "Councillor UI");
        PlayerControl pc = Utils.GetPlayerById(playerId);
        if (pc == null || !pc.IsAlive() || !GameStates.IsVoting || Starspawn.IsDayBreak) return;

        if (AmongUsClient.Instance.AmHost)
            MurderMsg(PlayerControl.LocalPlayer, $"/tl {playerId}", true);
        else
            SendRPC(playerId);
    }

    public static void CreateCouncillorButton(MeetingHud __instance)
    {
        foreach (PlayerVoteArea pva in __instance.playerStates.ToArray())
        {
            PlayerControl pc = Utils.GetPlayerById(pva.TargetPlayerId);
            if (pc == null || !pc.IsAlive()) continue;

            GameObject template = pva.Buttons.transform.Find("CancelButton").gameObject;
            GameObject targetBox = Object.Instantiate(template, pva.transform);
            targetBox.name = "ShootButton";
            targetBox.transform.localPosition = new(-0.35f, 0.03f, -1.31f);
            var renderer = targetBox.GetComponent<SpriteRenderer>();
            renderer.sprite = Utils.LoadSprite("EHR.Resources.Images.Skills.MeetingKillButton.png", 140f);
            var button = targetBox.GetComponent<PassiveButton>();
            button.OnClick.RemoveAllListeners();
            button.OnClick.AddListener((Action)(() => CouncillorOnClick(pva.TargetPlayerId /*, __instance*/)));
        }
    }

    //[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    public static class StartMeetingPatch
    {
        public static void Postfix(MeetingHud __instance)
        {
            if (PlayerControl.LocalPlayer.Is(CustomRoles.Councillor) && PlayerControl.LocalPlayer.IsAlive())
                CreateCouncillorButton(__instance);
        }
    }

}
