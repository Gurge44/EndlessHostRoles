using HarmonyLib;
using Hazel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using static TOHE.Translator;

namespace TOHE.Roles.Crewmate;

public static class NiceSwapper
{
    private static readonly int Id = 1986523;

    public static OptionItem SwapMax;
    public static OptionItem HideMsg;
    public static OptionItem CanSwapSelf;
    public static OptionItem CanStartMeeting;
    public static OptionItem NiceSwapperAbilityUseGainWithEachTaskCompleted;

    public static List<byte> playerIdList = [];
    public static List<byte> Vote = [];
    public static List<byte> VoteTwo = [];
    public static Dictionary<byte, float> NiceSwappermax = [];
    public static void SetupCustomOption()
    {
        Options.SetupSingleRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.NiceSwapper, 1);
        SwapMax = IntegerOptionItem.Create(Id + 3, "NiceSwapperMax", new(0, 20, 1), 1, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.NiceSwapper])
            .SetValueFormat(OptionFormat.Times);
        CanSwapSelf = BooleanOptionItem.Create(Id + 2, "CanSwapSelfVotes", true, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.NiceSwapper]);
        CanStartMeeting = BooleanOptionItem.Create(Id + 4, "JesterCanUseButton", true, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.NiceSwapper]);
        NiceSwapperAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(Id + 6, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.1f), 0.3f, TabGroup.CrewmateRoles, false)
        .SetParent(Options.CustomRoleSpawnChances[CustomRoles.NiceSwapper])
        .SetValueFormat(OptionFormat.Times);
        HideMsg = BooleanOptionItem.Create(Id + 5, "SwapperHideMsg", true, TabGroup.CrewmateRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.NiceSwapper]);
    }
    public static void Init()
    {
        playerIdList = [];
        Vote = [];
        VoteTwo = [];
        NiceSwappermax = [];
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        NiceSwappermax.TryAdd(playerId, SwapMax.GetInt());
    }
    public static bool IsEnable => playerIdList.Count > 0;
    public static string GetNiceSwappermax(byte playerId) => Utils.ColorString((NiceSwappermax.TryGetValue(playerId, out var x) && x >= 1) ? Color.white : Color.red, NiceSwappermax.TryGetValue(playerId, out var changermax) ? $"<color=#777777>-</color> {Math.Round(changermax, 1)}" : "Invalid");
    public static bool SwapMsg(PlayerControl pc, string msg, bool isUI = false)
    {
        var originMsg = msg;

        if (!AmongUsClient.Instance.AmHost) return false;
        if (!GameStates.IsInGame || pc == null) return false;
        if (pc.GetCustomRole() != CustomRoles.NiceSwapper) return false;

        int operate = 0;
        msg = msg.ToLower().TrimStart().TrimEnd();
        if (CheckCommond(ref msg, "id|guesslist|gl编号|玩家编号|玩家id|id列表|玩家列表|列表|所有id|全部id")) operate = 1;
        else if (CheckCommond(ref msg, "sw|换票|换|swap|st", false)) operate = 2;
        else return false;

        if (!pc.IsAlive())
        {
            if (!isUI) Utils.SendMessage(GetString("SwapDead"), pc.PlayerId);
            pc.ShowPopUp(GetString("SwapDead"));
            return true;
        }

        if (operate == 1)
        {
            Utils.SendMessage(GuessManager.GetFormatString(), pc.PlayerId);
            return true;
        }
        else if (operate == 2)
        {
            if (HideMsg.GetBool() && !isUI) ChatManager.SendPreviousMessagesToAll();
            else if (pc.AmOwner && !isUI) Utils.SendMessage(originMsg, 255, pc.GetRealName());

            if (!MsgToPlayerAndRole(msg, out byte targetId, out string error))
            {
                Utils.SendMessage(error, pc.PlayerId);
                return true;
            }
            var target = Utils.GetPlayerById(targetId);
            if (target != null)
            {
                if (NiceSwappermax[pc.PlayerId] < 1)
                {
                    if (!isUI) Utils.SendMessage(GetString("NiceSwapperTrialMax"), pc.PlayerId);
                    pc.ShowPopUp(GetString("NiceSwapperTrialMax"));
                    return true;
                }

                var dp = target;
                target = dp;


                if (!Vote.Any() && !Vote.Contains(dp.PlayerId) && !VoteTwo.Contains(dp.PlayerId) && CanSwapSelf.GetBool()
            || !Vote.Any() && !Vote.Contains(dp.PlayerId) && !VoteTwo.Contains(dp.PlayerId) && dp != pc && !CanSwapSelf.GetBool())
                {
                    Vote.Add(dp.PlayerId);
                    if (HideMsg.GetBool() && !isUI)
                    {
                        ChatManager.SendPreviousMessagesToAll();
                    }
                    if (!isUI) Utils.SendMessage(GetString("Swap1"), pc.PlayerId);
                    //else pc.ShowPopUp(GetString("Swap1"));
                    Logger.Info($"{pc.GetNameWithRole().RemoveHtmlTags()} 选择 {target.GetNameWithRole()}", "Swapper");
                }
                else if (Vote.Count == 1 && !VoteTwo.Any() && !Vote.Contains(dp.PlayerId) && !VoteTwo.Contains(dp.PlayerId) && CanSwapSelf.GetBool()
                || Vote.Count == 1 && !VoteTwo.Any() && !Vote.Contains(dp.PlayerId) && !VoteTwo.Contains(dp.PlayerId) && dp != pc && !CanSwapSelf.GetBool())
                {
                    VoteTwo.Add(dp.PlayerId);
                    if (HideMsg.GetBool() && !isUI)
                    {
                        ChatManager.SendPreviousMessagesToAll();
                    }
                    if (!isUI) Utils.SendMessage(GetString("Swap2"), pc.PlayerId);
                    //else pc.ShowPopUp(GetString("Swap2"));
                    Logger.Info($"{pc.GetNameWithRole().RemoveHtmlTags()} 选择 {target.GetNameWithRole()}", "Swapper");
                }
                else if (Vote.Any() && Vote.Contains(dp.PlayerId))
                {
                    Vote.Remove(dp.PlayerId);
                    if (HideMsg.GetBool() && !isUI)
                    {
                        ChatManager.SendPreviousMessagesToAll();
                    }
                    if (!isUI) Utils.SendMessage(GetString("CancelSwap1"), pc.PlayerId);
                    //else pc.ShowPopUp(GetString("CancelSwap1"));
                    Logger.Info($"{pc.GetNameWithRole().RemoveHtmlTags()} 取消选择 {target.GetNameWithRole()}", "Swapper");
                }
                else if (VoteTwo.Contains(dp.PlayerId) && VoteTwo.Any())
                {
                    VoteTwo.Remove(dp.PlayerId);
                    if (HideMsg.GetBool() && !isUI)
                    {
                        ChatManager.SendPreviousMessagesToAll();
                    }
                    if (!isUI) Utils.SendMessage(GetString("CancelSwap2"), pc.PlayerId);
                    //else pc.ShowPopUp(GetString("CancelSwap2"));
                    Logger.Info($"{pc.GetNameWithRole().RemoveHtmlTags()} 取消选择 {target.GetNameWithRole()}", "Swapper");
                }
                else if (pc == dp && !CanSwapSelf.GetBool())
                {
                    if (HideMsg.GetBool() && !isUI)
                    {
                        ChatManager.SendPreviousMessagesToAll();
                    }
                    if (!isUI) Utils.SendMessage(GetString("CantSwapSelf"), pc.PlayerId);
                    else pc.ShowPopUp(GetString("CantSwapSelf"));
                }
                _ = new LateTask(() =>
                {
                    if (Vote.Any() && VoteTwo.Any())
                    {
                        PlayerControl player1 = new();
                        PlayerControl player2 = new();
                        foreach (byte swap1 in Vote.ToArray())
                        {
                            player1.PlayerId = swap1;
                        }
                        foreach (byte swap2 in Vote.ToArray())
                        {
                            player2.PlayerId = swap2;
                        }
                    }
                    Utils.NotifyRoles(isForMeeting: true, NoCache: true);
                }, 0.2f);
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
            result += mc[i];//匹配结果是完整的数字，此处可以不做拼接的
        }

        if (int.TryParse(result, out int num))
        {
            id = Convert.ToByte(num);
        }
        else
        {
            //并不是玩家编号，判断是否颜色
            //byte color = GetColorFromMsg(msg);
            //好吧我不知道怎么取某位玩家的颜色，等会了的时候再来把这里补上
            id = byte.MaxValue;
            error = GetString("SwapHelp");
            return false;
        }

        //判断选择的玩家是否合理
        PlayerControl target = Utils.GetPlayerById(id);
        if (target == null || target.Data.IsDead)
        {
            error = GetString("SwapNull");
            return false;
        }

        error = string.Empty;
        return true;
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
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetNiceSwapperVotes, SendOption.Reliable, -1);
        writer.Write(playerId);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ReceiveRPC(MessageReader reader, PlayerControl pc)
    {
        byte PlayerId = reader.ReadByte();
        SwapMsg(pc, $"/sw {PlayerId}");
        //if (HideMsg.GetBool()) GuessManager.TryHideMsg();
    }
    private static void SwapperOnClick(byte playerId, MeetingHud __instance)
    {
        Logger.Msg($"Click: ID {playerId}", "NiceSwapper UI");
        var pc = Utils.GetPlayerById(playerId);
        if (pc == null || !pc.IsAlive() || !GameStates.IsVoting) return;
        if (AmongUsClient.Instance.AmHost) SwapMsg(PlayerControl.LocalPlayer, $"/sw {playerId}", true);
        else SendRPC(playerId);
        if (PlayerControl.LocalPlayer.GetCustomRole() == CustomRoles.NiceSwapper && PlayerControl.LocalPlayer.IsAlive())
        {
            //MeetingHudUpdatePatch.ClearShootButton(__instance, true);
            bool forceAll = true;
            __instance.playerStates.ToList().ForEach(x => { if ((forceAll || !Main.PlayerStates.TryGetValue(x.TargetPlayerId, out var ps) || ps.IsDead) && x.transform.FindChild("ShootButton") != null) UnityEngine.Object.Destroy(x.transform.FindChild("ShootButton").gameObject); });
            CreateSwapperButton(__instance);
        }
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    class StartMeetingPatch
    {
        public static void Postfix(MeetingHud __instance)
        {
            if (PlayerControl.LocalPlayer.GetCustomRole() == CustomRoles.NiceSwapper && PlayerControl.LocalPlayer.IsAlive())
                CreateSwapperButton(__instance);
        }
    }
    public static void CreateSwapperButton(MeetingHud __instance)
    {
        foreach (PlayerVoteArea pva in __instance.playerStates.ToArray())
        {
            var pc = Utils.GetPlayerById(pva.TargetPlayerId);
            if (pc == null || !pc.IsAlive()) continue;
            GameObject template = pva.Buttons.transform.Find("CancelButton").gameObject;
            GameObject targetBox = UnityEngine.Object.Instantiate(template, pva.transform);
            targetBox.name = "ShootButton";
            targetBox.transform.localPosition = new Vector3(-0.35f, 0.03f, -1.31f);
            SpriteRenderer renderer = targetBox.GetComponent<SpriteRenderer>();
            if ((pc.PlayerId == pva.TargetPlayerId) && (Vote.Contains(pc.PlayerId) || VoteTwo.Contains(pc.PlayerId))) renderer.sprite = CustomButton.Get("SwapYes");
            else renderer.sprite = CustomButton.Get("SwapNo");
            PassiveButton button = targetBox.GetComponent<PassiveButton>();
            button.OnClick.RemoveAllListeners();
            button.OnClick.AddListener((Action)(() => SwapperOnClick(pva.TargetPlayerId, __instance)));
        }
    }
}


