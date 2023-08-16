//using Hazel;
//using UnityEngine;
//using System.Collections.Generic;
//using static TOHE.Translator;
//using System.Linq;
//using System.Text.RegularExpressions;
//using System;

//namespace TOHE.Roles.Neutral;
//public static class Pirate
//{
//    private static readonly int Id = 333420;
//    private static List<byte> playerIdList = new();
//    private static byte PirateTarget;
//    private static Dictionary<byte, bool> DuelDone = new();
//    private static int pirateChose, targetChose;
//    public static int NumWin = 0;
//    private static List<string> OptionList = new()
//    {
//        "Rock",
//        "Paper",
//        "Scissors"
//    };


//    public static OptionItem SuccessfulDuelsToWin;
//    private static OptionItem TryHideMsg;


//    public static void SetupCustomOption()
//    {
//        Options.SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Pirate);
//        TryHideMsg = BooleanOptionItem.Create(Id + 10, "PirateTryHideMsg", true, TabGroup.NeutralRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Pirate])
//            .SetColor(Color.green);
//        SuccessfulDuelsToWin = IntegerOptionItem.Create(Id + 11, "SuccessfulDuelsToWin", new(1, 10, 1), 2, TabGroup.NeutralRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Pirate])
//            .SetValueFormat(OptionFormat.Times);
//    }

//    public static void Init()
//    {
//        playerIdList = new();
//        PirateTarget = byte.MaxValue;
//        DuelDone = new();
//        pirateChose = -1;
//        targetChose = -1;
//        NumWin = 0;
//    }

//    public static void Add(byte playerId)
//    {
//        playerIdList.Add(playerId);
//        DuelDone.Add(playerId, false);
//        if (!AmongUsClient.Instance.AmHost) return;
//        if (!Main.ResetCamPlayerList.Contains(playerId))
//            Main.ResetCamPlayerList.Add(playerId);
//    }

//    public static bool IsEnable => playerIdList.Count > 0;

//    public static void OnMeetingStart()
//    {
//        var pc = Utils.GetPlayerById(playerIdList[0]);
//        var tpc = Utils.GetPlayerById(PirateTarget);
//        if (!pc.IsAlive() || !tpc.IsAlive()) return;
//        Utils.SendMessage(GetString("PirateMeetingMsg"), pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Pirate), GetString("PirateTitle")));
//        Utils.SendMessage(GetString("PirateTargetMeetingMsg"), tpc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Pirate), GetString("PirateTitle")));
//    }

//    public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
//    {
//        if (PirateTarget != byte.MaxValue)
//        {
//            killer.Notify(GetString("PirateTargetAlreadyChosen"));
//            return false;
//        }
//        Logger.Msg($"{killer.GetNameWithRole()} chose a target {target.GetNameWithRole()}", "Pirate");
//        PirateTarget = target.PlayerId;
//        DuelDone.Add(PirateTarget, false);
//        //killer.RpcGuardAndKill(killer);
//        return false;
//    }
//    public static void AfterMeetingTask()
//    {
//        var pirateId = playerIdList[0];
//        if (PirateTarget != byte.MaxValue)
//        {
//            if (DuelDone[pirateId])
//            {
//                if (DuelDone[PirateTarget])
//                {
//                    if (targetChose == pirateChose)
//                    {
//                        NumWin++;
//                        if (Utils.GetPlayerById(PirateTarget).IsAlive())
//                        {
//                            CheckForEndVotingPatch.TryAddAfterMeetingDeathPlayers(PlayerState.DeathReason.Suicide, PirateTarget);
//                            Utils.GetPlayerById(PirateTarget).SetRealKiller(Utils.GetPlayerById(pirateId));
//                        }
//                    }
//                }
//                else
//                if (Utils.GetPlayerById(PirateTarget).IsAlive())
//                {
//                    CheckForEndVotingPatch.TryAddAfterMeetingDeathPlayers(PlayerState.DeathReason.Suicide, PirateTarget);
//                    Utils.GetPlayerById(PirateTarget).SetRealKiller(Utils.GetPlayerById(pirateId));
//                }
//            }
//        }
//        if (NumWin >= SuccessfulDuelsToWin.GetInt())
//        {
//            NumWin = SuccessfulDuelsToWin.GetInt();
//            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Pirate);
//            CustomWinnerHolder.WinnerIds.Add(pirateId);
//        }
//        DuelDone.Clear();
//        PirateTarget = byte.MaxValue;
//        foreach (byte playerId in playerIdList) { DuelDone.Add(playerId, false); }
//    }

//    public static bool DuelCheckMsg(PlayerControl pc, string msg, bool isUI = false)
//    {
//        var originMsg = msg;
//        if (!AmongUsClient.Instance.AmHost) return false;
//        if (!GameStates.IsInGame || pc == null) return false;
//        if (!pc.Is(CustomRoles.Pirate) && PirateTarget != pc.PlayerId) return false;


//        msg = msg.ToLower().TrimStart().TrimEnd();
//        bool operate = false;
//        if (CheckCommond(ref msg, "duel|rps")) operate = true;
//        else return false;

//        if (!pc.IsAlive())
//        {
//            Utils.SendMessage(GetString("PirateDead"), pc.PlayerId);
//            return true;
//        }

//        if (operate)
//        {

//            if (TryHideMsg.GetBool()) TryHideMsgForDuel();
//            else if (pc.AmOwner) Utils.SendMessage(originMsg, 255, pc.GetRealName());

//            if (!MsgToPlayerAndRole(msg, out int rpsOption, out string error))
//            {
//                Utils.SendMessage(error, pc.PlayerId);
//                return true;
//            }

//            Logger.Info($"{pc.GetNameWithRole()} selected {OptionList[rpsOption]}", "Pirate");

//            if (DuelDone[pc.PlayerId])
//            {
//                new LateTask(() =>
//                {
//                    if (!isUI) Utils.SendMessage(GetString("DuelAlreadyDone"), pc.PlayerId);
//                    else pc.ShowPopUp(GetString("DuelAlreadyDone"));
//                    Logger.Msg("Duel attempted more than once", "Pirate");
//                }, 0.2f, "Pirate");
//                return true;
//            }

//            else
//            {
//                if (pc.Is(CustomRoles.Pirate))
//                {
//                    pirateChose = rpsOption;

//                }
//                else
//                {
//                    targetChose = rpsOption;
//                    //new LateTask(() =>
//                    //{
//                    //    if (!isUI) Utils.SendMessage(String.Format(GetString("TargetDuelDone"), OptionList[pirateChose]), pc.PlayerId);
//                    //    else pc.ShowPopUp(String.Format(GetString("TargetDuelDone"), OptionList[pirateChose]));
//                    //    Logger.Msg($"Target chose {targetChose}", "Pirate");
//                    //}, 0.2f, "Pirate");
//                    //DuelDone[pc.PlayerId] = true;
//                    //return true;
//                }
//                new LateTask(() =>
//                {
//                    if (!isUI) Utils.SendMessage(String.Format(GetString("DuelDone"), OptionList[rpsOption]), pc.PlayerId);
//                    else pc.ShowPopUp(String.Format(GetString("DuelDone"), OptionList[rpsOption]));
//                }, 0.2f, "Pirate");
//                DuelDone[pc.PlayerId] = true;
//                return true;

//            }
//        }
//        return true;
//    }


//    private static bool MsgToPlayerAndRole(string msg, out int rpsOpt, out string error)
//    {
//        if (msg.StartsWith("/")) msg = msg.Replace("/", string.Empty);

//        Regex r = new("\\d+");
//        MatchCollection mc = r.Matches(msg);
//        string result = string.Empty;
//        for (int i = 0; i < mc.Count; i++)
//        {
//            result += mc[i];//匹配结果是完整的数字，此处可以不做拼接的
//        }

//        if (int.TryParse(result, out int num))
//        {
//            if (num < 0 || num > 2)
//            {
//                rpsOpt = -1;
//                error = GetString("DuelHelp");
//                return false;
//            }
//            else { rpsOpt = num; }
//        }
//        else
//        {
//            rpsOpt = -1;
//            error = GetString("DuelHelp");
//            return false;
//        }

//        error = string.Empty;
//        return true;
//    }

//    public static bool CheckCommond(ref string msg, string command)
//    {
//        var comList = command.Split('|');
//        for (int i = 0; i < comList.Count(); i++)
//        {
//            //if (exact)
//            //{
//            //    if (msg == "/" + comList[i]) return true;
//            //}
//            //else
//            //{
//            if (msg.StartsWith("/" + comList[i]))
//            {
//                msg = msg.Replace("/" + comList[i], string.Empty);
//                return true;
//            }
//            //}
//        }
//        return false;
//    }

//    public static void TryHideMsgForDuel()
//    {
//        ChatUpdatePatch.DoBlockChat = true;
//        List<CustomRoles> roles = Enum.GetValues(typeof(CustomRoles)).Cast<CustomRoles>().Where(x => x is not CustomRoles.NotAssigned and not CustomRoles.KB_Normal).ToList();
//        var rd = IRandom.Instance;
//        string msg;
//        string[] command = new string[] { "duel", "rps" };
//        for (int i = 0; i < 20; i++)
//        {
//            msg = "/";
//            if (rd.Next(1, 100) < 20)
//            {
//                msg += "id";
//            }
//            else
//            {
//                msg += command[rd.Next(0, command.Length - 1)];
//                msg += " ";
//                msg += rd.Next(0, 3).ToString();
//            }
//            var player = Main.AllAlivePlayerControls.ToArray()[rd.Next(0, Main.AllAlivePlayerControls.Count())];
//            DestroyableSingleton<HudManager>.Instance.Chat.AddChat(player, msg);
//            var writer = CustomRpcSender.Create("MessagesToSend", SendOption.None);
//            writer.StartMessage(-1);
//            writer.StartRpc(player.NetId, (byte)RpcCalls.SendChat)
//                .Write(msg)
//                .EndRpc();
//            writer.EndMessage();
//            writer.SendMessage();
//        }
//        ChatUpdatePatch.DoBlockChat = false;
//    }

//}