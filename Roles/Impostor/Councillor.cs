﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EHR.Crewmate;
using EHR.Modules;
using HarmonyLib;
using Hazel;
using UnityEngine;
using static EHR.Translator;


namespace EHR.Impostor;

public class Councillor : RoleBase
{
    private const int Id = 900;
    private static List<byte> playerIdList = [];
    private static OptionItem MurderLimitPerGame;
    private static OptionItem MurderLimitPerMeeting;
    private static OptionItem TryHideMsg;
    private static OptionItem CanMurderMadmate;
    private static OptionItem CanMurderImpostor;
    private static OptionItem KillCooldown;
    public static OptionItem CouncillorAbilityUseGainWithEachKill;
    private static Dictionary<byte, int> MeetingKillLimit = [];

    private byte CouncillorId;
    public override bool IsEnable => playerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Councillor);
        KillCooldown = new FloatOptionItem(Id + 15, "KillCooldown", new(0f, 180f, 2.5f), 25f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Councillor])
            .SetValueFormat(OptionFormat.Seconds);
        MurderLimitPerGame = new IntegerOptionItem(Id + 10, "AbilityUseLimit", new(0, 15, 1), 0, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Councillor])
            .SetValueFormat(OptionFormat.Times);
        MurderLimitPerMeeting = new IntegerOptionItem(Id + 14, "MurderLimitPerMeeting", new(0, 5, 1), 1, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Councillor])
            .SetValueFormat(OptionFormat.Times);
        CanMurderMadmate = new BooleanOptionItem(Id + 12, "CouncillorCanMurderMadmate", true, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Councillor]);
        CanMurderImpostor = new BooleanOptionItem(Id + 16, "CouncillorCanMurderImpostor", true, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Councillor]);
        TryHideMsg = new BooleanOptionItem(Id + 11, "CouncillorTryHideMsg", true, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Councillor])
            .SetColor(Color.green);
        CouncillorAbilityUseGainWithEachKill = new FloatOptionItem(Id + 17, "AbilityUseGainWithEachKill", new(0f, 5f, 0.1f), 0.2f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Councillor])
            .SetValueFormat(OptionFormat.Times);
    }

    public override void Init()
    {
        playerIdList = [];
        MeetingKillLimit = [];
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        CouncillorId = playerId;
        MeetingKillLimit[playerId] = MurderLimitPerMeeting.GetInt();
        playerId.SetAbilityUseLimit(MurderLimitPerGame.GetInt());
    }

    public override void AfterMeetingTasks()
    {
        MeetingKillLimit[CouncillorId] = MurderLimitPerMeeting.GetInt();
    }

    public static bool MurderMsg(PlayerControl pc, string msg, bool isUI = false)
    {
        var originMsg = msg;

        if (!AmongUsClient.Instance.AmHost) return false;
        if (!GameStates.IsInGame || pc == null) return false;
        if (!pc.Is(CustomRoles.Councillor)) return false;

        int operate; // 1:ID 2:Kill
        msg = msg.ToLower().TrimStart().TrimEnd();
        if (CheckCommond(ref msg, "id|guesslist|gl编号|玩家编号|玩家id|id列表|玩家列表|列表|所有id|全部id")) operate = 1;
        else if (CheckCommond(ref msg, "shoot|guess|bet|st|gs|bt|猜|赌|sp|jj|tl|审判|判|审", false)) operate = 2;
        else return false;

        if (!pc.IsAlive())
        {
            Utils.SendMessage(GetString("CouncillorDead"), pc.PlayerId);
            return true;
        }

        switch (operate)
        {
            case 1:
                Utils.SendMessage(GuessManager.GetFormatString(), pc.PlayerId);
                return true;
            case 2:
            {
                if (TryHideMsg.GetBool()) ChatManager.SendPreviousMessagesToAll();
                else if (pc.AmOwner) Utils.SendMessage(originMsg, 255, pc.GetRealName());

                if (!MsgToPlayerAndRole(msg, out byte targetId, out string error))
                {
                    Utils.SendMessage(error, pc.PlayerId);
                    return true;
                }

                var target = Utils.GetPlayerById(targetId);
                if (target != null)
                {
                    Logger.Info($"{pc.GetNameWithRole().RemoveHtmlTags()} murdered {target.GetNameWithRole().RemoveHtmlTags()}", "Councillor");
                    bool CouncillorSuicide = true;
                    if (pc.GetAbilityUseLimit() < 1)
                    {
                        if (!isUI) Utils.SendMessage(GetString("CouncillorMurderMax"), pc.PlayerId);
                        else pc.ShowPopUp(GetString("CouncillorMurderMax"));
                        return true;
                    }

                    if (MeetingKillLimit[pc.PlayerId] < 1)
                    {
                        if (!isUI) Utils.SendMessage(GetString("CouncillorMurderMaxMeeting"), pc.PlayerId);
                        else pc.ShowPopUp(GetString("CouncillorMurderMaxMeeting"));
                        return true;
                    }

                    if (Jailor.playerIdList.Any(x => Main.PlayerStates[x].Role is Jailor { IsEnable: true } jl && jl.JailorTarget == targetId))
                    {
                        if (!isUI) Utils.SendMessage(GetString("CanNotTrialJailed"), pc.PlayerId, title: Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jailor), GetString("JailorTitle")));
                        else pc.ShowPopUp(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jailor), GetString("JailorTitle")) + "\n" + GetString("CanNotTrialJailed"));
                        return true;
                    }

                    bool NoSuicide = false;

                    if (pc.PlayerId == targetId)
                    {
                        if (!isUI) Utils.SendMessage(GetString("LaughToWhoMurderSelf"), pc.PlayerId, Utils.ColorString(Color.cyan, GetString("MessageFromKPD")));
                        else pc.ShowPopUp(Utils.ColorString(Color.cyan, GetString("MessageFromKPD")) + "\n" + GetString("LaughToWhoMurderSelf"));
                    }
                    else if (target.IsMadmate() && CanMurderMadmate.GetBool()) CouncillorSuicide = false;
                    else if (target.Is(CustomRoles.SuperStar)) NoSuicide = true;
                    else if (target.Is(CustomRoles.Snitch) && target.AllTasksCompleted()) NoSuicide = true;
                    else if (target.Is(CustomRoles.Guardian) && target.AllTasksCompleted()) NoSuicide = true;
                    else if (target.Is(CustomRoles.Merchant) && Merchant.IsBribedKiller(pc, target)) NoSuicide = true;
                    else if (target.IsImpostor() && CanMurderImpostor.GetBool()) CouncillorSuicide = false;
                    else if (target.IsCrewmate()) CouncillorSuicide = false;
                    else if (target.GetCustomRole().IsNeutral() && !target.Is(CustomRoles.Pestilence)) CouncillorSuicide = false;

                    if (NoSuicide) return true;

                    var dp = CouncillorSuicide ? pc : target;

                    string Name = dp.GetRealName();

                    pc.RpcRemoveAbilityUse();
                    MeetingKillLimit[pc.PlayerId]--;

                    LateTask.New(() =>
                    {
                        Main.PlayerStates[dp.PlayerId].deathReason = PlayerState.DeathReason.Trialed;
                        dp.SetRealKiller(pc);
                        dp.RpcGuesserMurderPlayer();

                        Utils.AfterPlayerDeathTasks(dp, true);

                        Utils.NotifyRoles(isForMeeting: false, NoCache: true);

                        LateTask.New(() => Utils.SendMessage(string.Format(GetString("MurderKill"), Name), 255, Utils.ColorString(Utils.GetRoleColor(CustomRoles.NiceGuesser), GetString("MurderKillTitle"))), 0.6f, "Guess Msg");
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
            error = GetString("MurderHelp");
            return false;
        }

        PlayerControl target = Utils.GetPlayerById(id);
        if (target == null || target.Data.IsDead)
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

    public static bool CheckCommond(ref string msg, string command, bool exact = true)
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
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.MeetingKill, SendOption.Reliable);
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
        var pc = Utils.GetPlayerById(playerId);
        if (pc == null || !pc.IsAlive() || !GameStates.IsVoting) return;
        if (AmongUsClient.Instance.AmHost) MurderMsg(PlayerControl.LocalPlayer, $"/tl {playerId}", true);
        else SendRPC(playerId);
    }

    public static void CreateCouncillorButton(MeetingHud __instance)
    {
        foreach (PlayerVoteArea pva in __instance.playerStates.ToArray())
        {
            var pc = Utils.GetPlayerById(pva.TargetPlayerId);
            if (pc == null || !pc.IsAlive()) continue;
            GameObject template = pva.Buttons.transform.Find("CancelButton").gameObject;
            GameObject targetBox = Object.Instantiate(template, pva.transform);
            targetBox.name = "ShootButton";
            targetBox.transform.localPosition = new(-0.35f, 0.03f, -1.31f);
            SpriteRenderer renderer = targetBox.GetComponent<SpriteRenderer>();
            renderer.sprite = CustomButton.Get("MeetingKillButton");
            PassiveButton button = targetBox.GetComponent<PassiveButton>();
            button.OnClick.RemoveAllListeners();
            button.OnClick.AddListener((Action)(() => CouncillorOnClick(pva.TargetPlayerId /*, __instance*/)));
        }
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    class StartMeetingPatch
    {
        public static void Postfix(MeetingHud __instance)
        {
            if (PlayerControl.LocalPlayer.Is(CustomRoles.Councillor) && PlayerControl.LocalPlayer.IsAlive())
                CreateCouncillorButton(__instance);
        }
    }
}