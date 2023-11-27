using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TOHE.Roles.Crewmate;
using TOHE.Roles.Impostor;
using TOHE.Roles.Neutral;
using UnityEngine;
using static TOHE.Translator;

namespace TOHE;

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.CheckForEndVoting))]
class CheckForEndVotingPatch
{
    public static bool Prefix(MeetingHud __instance)
    {
        if (!AmongUsClient.Instance.AmHost) return true;
        if (Medic.IsEnable) Medic.OnCheckMark();
        //Meeting Skip with vote counting on keystroke (m + delete)
        var shouldSkip = false;
        if (Input.GetKeyDown(KeyCode.F6))
        {
            shouldSkip = true;
        }
        //
        var voteLog = Logger.Handler("Vote");
        try
        {
            List<MeetingHud.VoterState> statesList = [];
            MeetingHud.VoterState[] states;
            for (int i = 0; i < __instance.playerStates.Count; i++)
            {
                PlayerVoteArea pva = __instance.playerStates[i];
                if (pva == null) continue;
                PlayerControl pc = Utils.GetPlayerById(pva.TargetPlayerId);
                if (pc == null) continue;
                // Dictators who are not dead have already voted.

                // Take the initiative to rebel
                if (pva.DidVote && pc.PlayerId == pva.VotedFor && pva.VotedFor < 253 && !pc.Data.IsDead)
                {
                    if (Options.MadmateSpawnMode.GetInt() == 2 && Main.MadmateNum < CustomRoles.Madmate.GetCount() && Utils.CanBeMadmate(pc))
                    {
                        Main.MadmateNum++;
                        pc.RpcSetCustomRole(CustomRoles.Madmate);
                        ExtendedPlayerControl.RpcSetCustomRole(pc.PlayerId, CustomRoles.Madmate);
                        Utils.NotifyRoles(isForMeeting: true, SpecifySeer: pc, NoCache: true);
                        Logger.Info("Set role: " + pc?.Data?.PlayerName + " => " + pc.GetCustomRole().ToString() + " + " + CustomRoles.Madmate.ToString(), "Assign " + CustomRoles.Madmate.ToString());
                    }
                }

                // hypnotist hypnosis

                if (pc.Is(CustomRoles.Dictator) && pva.DidVote && pc.PlayerId != pva.VotedFor && pva.VotedFor < 253 && !pc.Data.IsDead)
                {
                    var voteTarget = Utils.GetPlayerById(pva.VotedFor);
                    TryAddAfterMeetingDeathPlayers(PlayerState.DeathReason.Suicide, pc.PlayerId);
                    statesList.Add(new()
                    {
                        VoterId = pva.TargetPlayerId,
                        VotedForId = pva.VotedFor
                    });
                    states = [.. statesList];
                    if (AntiBlackout.OverrideExiledPlayer)
                    {
                        __instance.RpcVotingComplete(states.ToArray(), null, true);
                        ExileControllerWrapUpPatch.AntiBlackout_LastExiled = voteTarget.Data;
                    }
                    else __instance.RpcVotingComplete(states.ToArray(), voteTarget.Data, false); // Normal processing

                    Logger.Info($"{voteTarget.GetNameWithRole().RemoveHtmlTags()} expelled by dictator", "Dictator");
                    CheckForDeathOnExile(PlayerState.DeathReason.Vote, pva.VotedFor);
                    Logger.Info("Dictatorship vote, forced end of meeting", "Special Phase");
                    voteTarget.SetRealKiller(pc);
                    Main.LastVotedPlayerInfo = voteTarget.Data;
                    if (Main.LastVotedPlayerInfo != null)
                        ConfirmEjections(Main.LastVotedPlayerInfo);
                    return true;
                }

                if (pva.DidVote && pva.VotedFor < 253 && !pc.Data.IsDead)
                {
                    var voteTarget = Utils.GetPlayerById(pva.VotedFor);
                    if (voteTarget != null)
                    {
                        switch (pc.GetCustomRole())
                        {
                            case CustomRoles.Divinator:
                                Divinator.OnVote(pc, voteTarget);
                                break;
                            case CustomRoles.Oracle:
                                Oracle.OnVote(pc, voteTarget);
                                break;
                            case CustomRoles.Eraser:
                                Eraser.OnVote(pc, voteTarget);
                                break;
                            case CustomRoles.Tether:
                                Tether.OnVote(pc, voteTarget);
                                break;
                            case CustomRoles.Ricochet:
                                Ricochet.OnVote(pc, voteTarget);
                                break;
                            case CustomRoles.Cleanser:
                                Cleanser.OnVote(pc, voteTarget);
                                break;
                            //case CustomRoles.Jailor:
                            //    Jailor.OnVote(pc, voteTarget);
                            //    break;
                            case CustomRoles.NiceEraser:
                                NiceEraser.OnVote(pc, voteTarget);
                                break;
                            case CustomRoles.Tracker:
                                Tracker.OnVote(pc, voteTarget);
                                break;
                            case CustomRoles.Godfather:
                                if (pc == null || voteTarget == null) break;
                                Main.GodfatherTarget = voteTarget.PlayerId;
                                break;
                        }
                    }
                    else if (pc.GetCustomRole() == CustomRoles.Godfather) Main.GodfatherTarget = byte.MaxValue;
                }
            }
            for (int i = 0; i < __instance.playerStates.Count; i++)
            {
                PlayerVoteArea ps = __instance.playerStates[i];
                if (shouldSkip) break; //Meeting Skip with vote counting on keystroke (m + delete)

                // Non-dead players are not voting
                if (!(Main.PlayerStates[ps.TargetPlayerId].IsDead || ps.DidVote)) return false;
            }

            GameData.PlayerInfo exiledPlayer = PlayerControl.LocalPlayer.Data;
            bool tie = false;

            for (var i = 0; i < __instance.playerStates.Length; i++)
            {
                PlayerVoteArea ps = __instance.playerStates[i];
                if (ps == null) continue;
                voteLog.Info(string.Format("{0,-2}{1}:{2,-3}{3}", ps.TargetPlayerId, Utils.PadRightV2($"({Utils.GetVoteName(ps.TargetPlayerId)})", 40), ps.VotedFor, $"({Utils.GetVoteName(ps.VotedFor)})"));
                var voter = Utils.GetPlayerById(ps.TargetPlayerId);
                if (voter == null || voter.Data == null || voter.Data.Disconnected) continue;
                if (Options.VoteMode.GetBool())
                {
                    if (ps.VotedFor == 253 && !voter.Data.IsDead && //スキップ
                        !(Options.WhenSkipVoteIgnoreFirstMeeting.GetBool() && MeetingStates.FirstMeeting) && //初手会議を除く
                        !(Options.WhenSkipVoteIgnoreNoDeadBody.GetBool() && !MeetingStates.IsExistDeadBody) && //死体がない時を除く
                        !(Options.WhenSkipVoteIgnoreEmergency.GetBool() && MeetingStates.IsEmergencyMeeting) //緊急ボタンを除く
                        )
                    {
                        switch (Options.GetWhenSkipVote())
                        {
                            case VoteMode.Suicide:
                                TryAddAfterMeetingDeathPlayers(PlayerState.DeathReason.Suicide, ps.TargetPlayerId);
                                voteLog.Info($"{voter.GetNameWithRole().RemoveHtmlTags()} Commit suicide for skipping voting");
                                break;
                            case VoteMode.SelfVote:
                                ps.VotedFor = ps.TargetPlayerId;
                                voteLog.Info($"{voter.GetNameWithRole().RemoveHtmlTags()} Self-voting due to skipping voting");
                                break;
                            default:
                                break;
                        }
                    }
                    if (ps.VotedFor == 254 && !voter.Data.IsDead)//無投票
                    {
                        switch (Options.GetWhenNonVote())
                        {
                            case VoteMode.Suicide:
                                TryAddAfterMeetingDeathPlayers(PlayerState.DeathReason.Suicide, ps.TargetPlayerId);
                                voteLog.Info($"{voter.GetNameWithRole().RemoveHtmlTags()} Committed suicide for not voting");
                                break;
                            case VoteMode.SelfVote:
                                ps.VotedFor = ps.TargetPlayerId;
                                voteLog.Info($"{voter.GetNameWithRole().RemoveHtmlTags()} Self-voting due to not voting");
                                break;
                            case VoteMode.Skip:
                                ps.VotedFor = 253;
                                voteLog.Info($"{voter.GetNameWithRole().RemoveHtmlTags()} Skip for not voting");
                                break;
                            default:
                                break;
                        }
                    }
                }

                //隐藏占卜师的票
                if (CheckRole(ps.TargetPlayerId, CustomRoles.Divinator) && Divinator.HideVote.GetBool()) continue;
                //隐藏抹除者的票
                if (CheckRole(ps.TargetPlayerId, CustomRoles.Eraser) && Eraser.HideVote.GetBool()) continue;
                if (CheckRole(ps.TargetPlayerId, CustomRoles.NiceEraser) && NiceEraser.HideVote.GetBool()) continue;

                if (CheckRole(ps.TargetPlayerId, CustomRoles.Tracker) && Tracker.HideVote.GetBool()) continue;

                if (CheckRole(ps.TargetPlayerId, CustomRoles.Oracle) && Oracle.HideVote.GetBool()) continue;

                //主动叛变模式下自票无效
                if (ps.TargetPlayerId == ps.VotedFor && Options.MadmateSpawnMode.GetInt() == 2) continue;

                statesList.Add(new MeetingHud.VoterState()
                {
                    VoterId = ps.TargetPlayerId,
                    VotedForId = ps.VotedFor
                });

                if (NiceSwapper.Vote.Any() && NiceSwapper.VoteTwo.Any())
                {
                    List<byte> NiceList1 = [];
                    List<byte> NiceList2 = [];
                    PlayerVoteArea pva = new();
                    var meetingHud = MeetingHud.Instance;
                    PlayerControl swap1 = null;
                    for (int i1 = 0; i1 < NiceSwapper.Vote.Count; i1++)
                    {
                        byte playerId = NiceSwapper.Vote[i1];
                        var player = Utils.GetPlayerById(playerId);
                        if (player != null)
                        {
                            swap1 = player;
                            break;
                        }
                    }
                    PlayerControl swap2 = null;
                    for (int i1 = 0; i1 < NiceSwapper.VoteTwo.Count; i1++)
                    {
                        byte playerId = NiceSwapper.VoteTwo[i1];
                        var player = Utils.GetPlayerById(playerId);
                        if (player != null)
                        {
                            swap2 = player;
                            break;
                        }
                    }
                    if (swap1 != null && swap2 != null)
                    {
                        for (int i1 = 0; i1 < meetingHud.playerStates.Count; i1++)
                        {
                            PlayerVoteArea playerVoteArea = meetingHud.playerStates[i1];
                            if (playerVoteArea.VotedFor != swap1.PlayerId) continue;
                            var voteAreaPlayer = Utils.GetPlayerById(playerVoteArea.TargetPlayerId);
                            playerVoteArea.UnsetVote();
                            meetingHud.CastVote(voteAreaPlayer.PlayerId, swap2.PlayerId);
                            playerVoteArea.VotedFor = swap2.PlayerId;
                            NiceList1.Add(voteAreaPlayer.PlayerId);
                        }
                        for (int i1 = 0; i1 < meetingHud.playerStates.Count; i1++)
                        {
                            PlayerVoteArea playerVoteArea = meetingHud.playerStates[i1];
                            if (playerVoteArea.VotedFor != swap2.PlayerId) continue;
                            var voteAreaPlayer = Utils.GetPlayerById(playerVoteArea.TargetPlayerId);
                            if (NiceList1.Contains(voteAreaPlayer.PlayerId)) continue;
                            playerVoteArea.UnsetVote();
                            meetingHud.CastVote(voteAreaPlayer.PlayerId, swap1.PlayerId);
                            playerVoteArea.VotedFor = swap1.PlayerId;
                        }
                        if (Main.NiceSwapSend == false)
                        {
                            Utils.SendMessage(string.Format(GetString("SwapVote"), swap1.GetRealName(), swap2.GetRealName()), 255, Utils.ColorString(Utils.GetRoleColor(CustomRoles.NiceSwapper), GetString("SwapTitle")));
                            Main.NiceSwapSend = true;
                            NiceList1.Clear();
                        }
                        NiceSwapper.Vote.Clear();
                        NiceSwapper.VoteTwo.Clear();
                    }
                }

                if (CheckRole(ps.TargetPlayerId, CustomRoles.Mayor) && !Options.MayorHideVote.GetBool()) //Mayorの投票数
                {
                    for (var i2 = 0; i2 < Options.MayorAdditionalVote.GetFloat(); i2++)
                    {
                        statesList.Add(new MeetingHud.VoterState()
                        {
                            VoterId = ps.TargetPlayerId,
                            VotedForId = ps.VotedFor
                        });
                    }
                }
                if (CheckRole(ps.TargetPlayerId, CustomRoles.Vindicator) && !Options.VindicatorHideVote.GetBool()) //Vindicator
                {
                    for (var i2 = 0; i2 < Options.VindicatorAdditionalVote.GetFloat(); i2++)
                    {
                        statesList.Add(new MeetingHud.VoterState()
                        {
                            VoterId = ps.TargetPlayerId,
                            VotedForId = ps.VotedFor
                        });
                    }
                }
            }
            states = [.. statesList];

            var VotingData = __instance.CustomCalculateVotes();
            byte exileId = byte.MaxValue;
            int max = 0;
            voteLog.Info("===Decision to expel player processing begins===");
            foreach (var data in VotingData)
            {
                voteLog.Info($"{data.Key}({Utils.GetVoteName(data.Key)}):{data.Value}票");
                if (data.Value > max)
                {
                    voteLog.Info(data.Key + " has higher votes (" + data.Value + ")");
                    exileId = data.Key;
                    max = data.Value;
                    tie = false;
                }
                else if (data.Value == max)
                {
                    voteLog.Info(data.Key + " and " + exileId + "have the same number of votes (" + data.Value + ")");
                    exileId = byte.MaxValue;
                    tie = true;
                }
                voteLog.Info($"expelID: {exileId}, maximum: {max} votes");
            }

            voteLog.Info($"Decide to evict player: {exileId} ({Utils.GetVoteName(exileId)})");

            bool braked = false;
            if (tie) //破平者判断
            {
                byte target = byte.MaxValue;
                foreach (var data in VotingData.Where(x => x.Key < 15 && x.Value == max))
                {
                    if (Main.BrakarVoteFor.Contains(data.Key))
                    {
                        if (target != byte.MaxValue)
                        {
                            target = byte.MaxValue;
                            break;
                        }
                        target = data.Key;
                    }
                }
                if (target != byte.MaxValue)
                {
                    Logger.Info("Tiebreaker overrides evicted players", "Brakar Vote");
                    exiledPlayer = Utils.GetPlayerInfoById(target);
                    tie = false;
                    braked = true;
                }
            }

            Collector.CollectAmount(VotingData, __instance);

            if (Options.VoteMode.GetBool() && Options.WhenTie.GetBool() && tie)
            {
                switch ((TieMode)Options.WhenTie.GetValue())
                {
                    case TieMode.Default:
                        exiledPlayer = GameData.Instance.AllPlayers.ToArray().FirstOrDefault(info => info.PlayerId == exileId);
                        break;
                    case TieMode.All:
                        var exileIds = VotingData.Where(x => x.Key < 15 && x.Value == max).Select(kvp => kvp.Key).ToArray();
                        for (int i = 0; i < exileIds.Length; i++)
                        {
                            byte playerId = exileIds[i];
                            Utils.GetPlayerById(playerId).SetRealKiller(null);
                        }

                        TryAddAfterMeetingDeathPlayers(PlayerState.DeathReason.Vote, exileIds);
                        exiledPlayer = null;
                        break;
                    case TieMode.Random:
                        exiledPlayer = GameData.Instance.AllPlayers.ToArray().OrderBy(_ => Guid.NewGuid()).FirstOrDefault(x => VotingData.TryGetValue(x.PlayerId, out int vote) && vote == max);
                        tie = false;
                        break;
                }
            }
            else if (!braked)
                exiledPlayer = GameData.Instance.AllPlayers.ToArray().FirstOrDefault(info => !tie && info.PlayerId == exileId);
            exiledPlayer?.Object.SetRealKiller(null);

            //RPC
            if (AntiBlackout.OverrideExiledPlayer)
            {
                __instance.RpcVotingComplete(states.ToArray(), null, true);
                ExileControllerWrapUpPatch.AntiBlackout_LastExiled = exiledPlayer;
            }
            else __instance.RpcVotingComplete(states.ToArray(), exiledPlayer, tie); //通常処理

            CheckForDeathOnExile(PlayerState.DeathReason.Vote, exileId);

            Main.LastVotedPlayerInfo = exiledPlayer;
            if (Main.LastVotedPlayerInfo != null)
                ConfirmEjections(Main.LastVotedPlayerInfo);

            return false;
        }
        catch (Exception ex)
        {
            Logger.SendInGame(string.Format(GetString("Error.MeetingException"), ex.Message), true);
            throw;
        }
    }

    // 参考：https://github.com/music-discussion/TownOfHost-TheOtherRoles
    private static void ConfirmEjections(GameData.PlayerInfo exiledPlayer)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (exiledPlayer == null) return;
        var exileId = exiledPlayer.PlayerId;
        if (exileId is < 0 or > 254) return;
        var realName = exiledPlayer.Object.GetRealName(isMeeting: true);
        Main.LastVotedPlayer = realName;

        var player = Utils.GetPlayerById(exiledPlayer.PlayerId);
        var role = GetString(exiledPlayer.GetCustomRole().ToString());
        var crole = exiledPlayer.GetCustomRole();
        var coloredRole = Utils.GetDisplayRoleName(exileId, true);
        if (Options.ConfirmEgoistOnEject.GetBool() && player.Is(CustomRoles.Egoist))
            coloredRole = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Egoist), GetRoleString("Temp.Blank") + coloredRole.RemoveHtmlTags());
        //   if (Options.ConfirmSidekickOnEject.GetBool() && player.Is(CustomRoles.Sidekick))
        //       coloredRole = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Sidekick), GetRoleString("Temp.Blank") + coloredRole.RemoveHtmlTags());
        if (Options.ConfirmLoversOnEject.GetBool() && player.Is(CustomRoles.Lovers))
            coloredRole = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Lovers), GetRoleString("Temp.Blank") + coloredRole.RemoveHtmlTags());
        if (Options.RascalAppearAsMadmate.GetBool() && player.Is(CustomRoles.Rascal))
            coloredRole = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Madmate), GetRoleString("Mad-") + coloredRole.RemoveHtmlTags());
        var name = string.Empty;
        int impnum = 0;
        int neutralnum = 0;

        if (CustomRolesHelper.RoleExist(CustomRoles.Bard))
        {
            Main.BardCreations++;
            try { name = ModUpdater.Get("https://v1.hitokoto.cn/?encode=text"); }
            catch { name = GetString("ByBardGetFailed"); }
            name += "\n\t\t——" + GetString("ByBard");
            goto EndOfSession;
        }

        foreach (PlayerControl pc in Main.AllAlivePlayerControls)
        {
            if (pc == exiledPlayer.Object) continue;
            var pc_role = pc.GetCustomRole();

            if (pc_role.IsImpostor()) impnum++;
            else if (pc_role.IsNeutralKilling()) neutralnum++;
        }
        switch (Options.CEMode.GetInt())
        {
            case 0:
                name = string.Format(GetString("PlayerExiled"), realName);
                break;
            case 1:
                if (player.GetCustomRole().IsImpostor() || player.Is(CustomRoles.Parasite) || player.Is(CustomRoles.Crewpostor) || player.Is(CustomRoles.Refugee) || player.Is(CustomRoles.Convict))
                    name = string.Format(GetString("BelongTo"), realName, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Impostor), GetString("TeamImpostor")));
                else if (player.GetCustomRole().IsCrewmate())
                    name = string.Format(GetString("IsGood"), realName);
                else if (player.GetCustomRole().IsNeutral() && !player.Is(CustomRoles.Parasite) && !player.Is(CustomRoles.Refugee) && !player.Is(CustomRoles.Crewpostor) && !player.Is(CustomRoles.Convict))
                    name = string.Format(GetString("BelongTo"), realName, Utils.ColorString(new Color32(127, 140, 141, byte.MaxValue), GetString("TeamNeutral")));
                break;
            case 2:
                name = string.Format(GetString("PlayerIsRole"), realName, coloredRole);
                if (Options.ShowTeamNextToRoleNameOnEject.GetBool())
                {
                    name += " (";
                    if (player.GetCustomRole().IsImpostor() || player.Is(CustomRoles.Madmate))
                        name += Utils.ColorString(new Color32(255, 25, 25, byte.MaxValue), GetString("TeamImpostor"));
                    else if (player.GetCustomRole().IsNeutral() || player.Is(CustomRoles.Charmed))
                        name += Utils.ColorString(new Color32(127, 140, 141, byte.MaxValue), GetString("TeamNeutral"));
                    else if (player.GetCustomRole().IsCrewmate())
                        name += Utils.ColorString(new Color32(140, 255, 255, byte.MaxValue), GetString("TeamCrewmate"));
                    name += ")";
                }
                break;
        }

        var DecidedWinner = false;

        if (crole == CustomRoles.Jester)
        {
            name = string.Format(GetString("ExiledJester"), realName, coloredRole);
            DecidedWinner = true;
        }
        if (Executioner.CheckExileTarget(exiledPlayer, DecidedWinner, true))
        {
            name = string.Format(GetString("ExiledExeTarget"), realName, coloredRole);
            DecidedWinner = true;
        }
        if (Main.AllPlayerControls.Any(x => x.Is(CustomRoles.Innocent) && !x.IsAlive() && x.GetRealKiller()?.PlayerId == exileId))
        {
            if (!(!Options.InnocentCanWinByImp.GetBool() && crole.IsImpostor()))
            {
                if (DecidedWinner) name += string.Format(GetString("ExiledInnocentTargetAddBelow"));
                else name = string.Format(GetString("ExiledInnocentTargetInOneLine"), realName, coloredRole);
                DecidedWinner = true;
            }
        }

        if (DecidedWinner) name += "<size=0>";
        else if (Options.ShowImpRemainOnEject.GetBool())
        {
            name += "\n";
            if (!Options.ShowNKRemainOnEject.GetBool()) neutralnum = 0;
            switch (impnum, neutralnum)
            {
                case (0, 0): // Crewmates win
                    name += GetString("GG");
                    break;
                case (> 0, > 0): // Both imps and neutrals remain
                    name += impnum switch
                    {
                        1 => "1 <color=#ff1919>Impostor</color> <color=#777777>&</color> ",
                        2 => "2 <color=#ff1919>Impostors</color> <color=#777777>&</color> ",
                        3 => "3 <color=#ff1919>Impostors</color> <color=#777777>&</color> ",
                        _ => string.Empty,
                    };
                    if (neutralnum == 1) name += "1 <color=#ffab1b>Neutral</color> <color=#777777>remains.</color>";
                    else name += $"{neutralnum} <color=#ffab1b>Neutrals</color> <color=#777777>remain.</color>";
                    break;
                case (> 0, 0): // Only imps remain
                    name += impnum switch
                    {
                        1 => GetString("OneImpRemain"),
                        2 => GetString("TwoImpRemain"),
                        3 => GetString("ThreeImpRemain"),
                        _ => string.Empty,
                    };
                    break;
                case (0, > 0): // Only neutrals remain
                    if (neutralnum == 1) name += GetString("OneNeutralRemain");
                    else name += string.Format(GetString("NeutralRemain"), neutralnum);
                    break;
            }
        }

    EndOfSession:


        name += "<size=0>";
        _ = new LateTask(() =>
        {
            Main.DoBlockNameChange = true;
            if (GameStates.IsInGame) player?.RpcSetName(name);
        }, 3.0f, "Change Exiled Player Name");
        _ = new LateTask(() =>
        {
            if (GameStates.IsInGame && !player.Data.Disconnected)
            {
                player?.RpcSetName(realName);
                Main.DoBlockNameChange = false;
            }
        }, 11.5f, "Change Exiled Player Name Back");
        _ = new LateTask(() =>
        {
            var text = name.Split('\n')[1];
            var r = IRandom.Instance;
            foreach (var pc in Main.AllAlivePlayerControls)
            {
                pc?.Notify(text, r.Next(7, 13));
            }
        }, 12f, log: false);
    }
    public static bool CheckRole(byte id, CustomRoles role)
    {
        var player = Main.AllPlayerControls.FirstOrDefault(pc => pc.PlayerId == id);
        return player != null && player.Is(role);
    }
    public static void TryAddAfterMeetingDeathPlayers(PlayerState.DeathReason deathReason, params byte[] playerIds)
    {
        var AddedIdList = new List<byte>();
        for (int i = 0; i < playerIds.Length; i++)
        {
            byte playerId = playerIds[i];
            if (Main.AfterMeetingDeathPlayers.TryAdd(playerId, deathReason))
                AddedIdList.Add(playerId);
        }

        CheckForDeathOnExile(deathReason, [.. AddedIdList]);
    }
    public static void CheckForDeathOnExile(PlayerState.DeathReason deathReason, params byte[] playerIds)
    {
        Witch.OnCheckForEndVoting(deathReason, playerIds);
        HexMaster.OnCheckForEndVoting(deathReason, playerIds);
        Virus.OnCheckForEndVoting(deathReason, playerIds);
        for (int i = 0; i < playerIds.Length; i++)
        {
            byte playerId = playerIds[i];
            //    Baker.OnCheckForEndVoting(playerId);

            //Loversの後追い
            if (CustomRoles.Lovers.IsEnable() && !Main.isLoversDead && Main.LoversPlayers.Find(lp => lp.PlayerId == playerId) != null)
                FixedUpdatePatch.LoversSuicide(playerId, true);
            //道連れチェック
            RevengeOnExile(playerId/*, deathReason*/);
        }
    }
    private static void RevengeOnExile(byte playerId/*, PlayerState.DeathReason deathReason*/)
    {
        var player = Utils.GetPlayerById(playerId);
        if (player == null) return;
        var target = PickRevengeTarget(player/*, deathReason*/);
        if (target == null) return;
        TryAddAfterMeetingDeathPlayers(PlayerState.DeathReason.Revenge, target.PlayerId);
        target.SetRealKiller(player);
        Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags()}の道連れ先:{target.GetNameWithRole().RemoveHtmlTags()}", "RevengeOnExile");
    }
    private static PlayerControl PickRevengeTarget(PlayerControl exiledplayer/*, PlayerState.DeathReason deathReason*/)//道連れ先選定
    {
        List<PlayerControl> TargetList = [];
        foreach (PlayerControl candidate in Main.AllAlivePlayerControls)
        {
            if (candidate == exiledplayer || Main.AfterMeetingDeathPlayers.ContainsKey(candidate.PlayerId))
                continue;
        }
        if (TargetList == null || !TargetList.Any()) return null;
        var rand = IRandom.Instance;
        var target = TargetList[rand.Next(TargetList.Count)];
        return target;
    }
}

static class ExtendedMeetingHud
{
    public static Dictionary<byte, int> CustomCalculateVotes(this MeetingHud __instance)
    {
        Logger.Info("===The vote counting process begins===", "Vote");
        Dictionary<byte, int> dic = [];
        Main.BrakarVoteFor = [];
        Collector.CollectorVoteFor = [];
        //| 投票された人 | 投票された回数 |
        for (int i = 0; i < __instance.playerStates.Length; i++)
        {
            PlayerVoteArea ps = __instance.playerStates[i];//该玩家面板里的所有会议中的玩家
            if (ps == null) continue;
            if (ps.VotedFor is not 252 and not byte.MaxValue and not 254)//该玩家面板里是否投了该玩家
            {
                // 默认票数1票
                int VoteNum = 1;

                // 投票给有效玩家时才进行的判断
                var target = Utils.GetPlayerById(ps.VotedFor);
                if (target != null)
                {
                    // 僵尸、活死人无法被票
                    if (target.Is(CustomRoles.Zombie)) VoteNum = 0;
                    // 记录破平者投票
                    if (CheckForEndVotingPatch.CheckRole(ps.TargetPlayerId, CustomRoles.Brakar))
                        if (!Main.BrakarVoteFor.Contains(target.PlayerId))
                            Main.BrakarVoteFor.Add(target.PlayerId);
                    // 集票者记录数据
                    Collector.CollectorVotes(target, ps);
                }

                //市长附加票数
                if (CheckForEndVotingPatch.CheckRole(ps.TargetPlayerId, CustomRoles.Mayor)
                    && ps.TargetPlayerId != ps.VotedFor
                    ) VoteNum += Options.MayorAdditionalVote.GetInt();
                if (CheckForEndVotingPatch.CheckRole(ps.TargetPlayerId, CustomRoles.Knighted)
                    && ps.TargetPlayerId != ps.VotedFor
                    ) VoteNum += 1;
                if (CheckForEndVotingPatch.CheckRole(ps.TargetPlayerId, CustomRoles.Glitch)
                    && ps.TargetPlayerId != ps.VotedFor
                    && !Glitch.CanVote.GetBool()
                    ) VoteNum = 0;
                if (CheckForEndVotingPatch.CheckRole(ps.TargetPlayerId, CustomRoles.Vindicator)
                    && ps.TargetPlayerId != ps.VotedFor
                    ) VoteNum += Options.VindicatorAdditionalVote.GetInt();
                if (Options.DualVotes.GetBool())
                {
                    if (CheckForEndVotingPatch.CheckRole(ps.TargetPlayerId, CustomRoles.DualPersonality)
                        && ps.TargetPlayerId != ps.VotedFor
                        ) VoteNum += VoteNum;
                }
                //窃票者附加票数
                if (CheckForEndVotingPatch.CheckRole(ps.TargetPlayerId, CustomRoles.TicketsStealer))
                    VoteNum += (int)(Main.AllPlayerControls.Count(x => x.GetRealKiller()?.PlayerId == ps.TargetPlayerId) * Options.TicketsPerKill.GetFloat());
                if (CheckForEndVotingPatch.CheckRole(ps.TargetPlayerId, CustomRoles.Pickpocket))
                    VoteNum += (int)(Main.AllPlayerControls.Count(x => x.GetRealKiller()?.PlayerId == ps.TargetPlayerId) * Pickpocket.VotesPerKill.GetFloat());

                // 主动叛变模式下自票无效
                if (ps.TargetPlayerId == ps.VotedFor && Options.MadmateSpawnMode.GetInt() == 2) VoteNum = 0;

                //投票を1追加 キーが定義されていない場合は1で上書きして定義
                dic[ps.VotedFor] = !dic.TryGetValue(ps.VotedFor, out int num) ? VoteNum : num + VoteNum;//统计该玩家被投的数量
            }
        }
        return dic;
    }
}
[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
class MeetingHudStartPatch
{
    public static void NotifyRoleSkillOnMeetingStart()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        List<(string MESSAGE, byte TARGET_ID, string TITLE)> msgToSend = [];

        void AddMsg(string text, byte sendTo = 255, string title = "")
            => msgToSend.Add((text, sendTo, title));

        ChatManager.SendPreviousMessagesToAll(); // To fix chatting during the ejection screen

        //首次会议技能提示
        if (Options.SendRoleDescriptionFirstMeeting.GetBool() && MeetingStates.FirstMeeting)
        {
            foreach (var pc in Main.AllAlivePlayerControls.Where(x => !x.IsModClient()).ToArray())
            {
                var role = pc.GetCustomRole();
                var sb = new StringBuilder();
                sb.Append(GetString(role.ToString()) + Utils.GetRoleMode(role) + pc.GetRoleInfo(true));
                if (Options.CustomRoleSpawnChances.TryGetValue(role, out var opt))
                    Utils.ShowChildrenSettings(opt, ref sb, command: true);
                var txt = sb.ToString();
                sb.Clear().Append(txt.RemoveHtmlTags());
                for (int i = 0; i < Main.PlayerStates[pc.PlayerId].SubRoles.Count; i++)
                {
                    CustomRoles subRole = Main.PlayerStates[pc.PlayerId].SubRoles[i];
                    sb.Append($"\n\n" + GetString($"{subRole}") + Utils.GetRoleMode(subRole) + GetString($"{subRole}InfoLong"));
                }

                //if (CustomRolesHelper.RoleExist(CustomRoles.Ntr) && (role is not CustomRoles.GM and not CustomRoles.Ntr))
                //    sb.Append($"\n\n" + GetString($"Lovers") + Utils.GetRoleMode(CustomRoles.Lovers) + GetString($"LoversInfoLong"));
                AddMsg(sb.ToString(), pc.PlayerId);
            }
        }

        if (msgToSend.Any())
        {
            var msgTemp = msgToSend.ToList();
            _ = new LateTask(() => { msgTemp.Do(x => Utils.SendMessage(x.MESSAGE, x.TARGET_ID, x.TITLE)); }, 3f, "Skill Description First Meeting");
        }
        msgToSend = [];

        //主动叛变模式提示
        if (Options.MadmateSpawnMode.GetInt() == 2 && CustomRoles.Madmate.GetCount() > 0)
            AddMsg(string.Format(GetString("Message.MadmateSelfVoteModeNotify"), GetString("MadmateSpawnMode.SelfVote")));
        //提示神存活
        if (CustomRoles.God.RoleExist() && Options.NotifyGodAlive.GetBool())
            AddMsg(GetString("GodNoticeAlive"), 255, Utils.ColorString(Utils.GetRoleColor(CustomRoles.God), GetString("GodAliveTitle")));
        //工作狂的生存技巧
        if (MeetingStates.FirstMeeting && CustomRoles.Workaholic.RoleExist() && Options.WorkaholicGiveAdviceAlive.GetBool() && !Options.WorkaholicCannotWinAtDeath.GetBool()/* && !Options.GhostIgnoreTasks.GetBool()*/)
        {
            foreach (var pc in Main.AllAlivePlayerControls.Where(x => x.Is(CustomRoles.Workaholic)).ToArray())
                Main.WorkaholicAlive.Add(pc.PlayerId);
            List<string> workaholicAliveList = [];
            for (int i = 0; i < Main.WorkaholicAlive.Count; i++)
            {
                byte whId = Main.WorkaholicAlive[i];
                workaholicAliveList.Add(Main.AllPlayerNames[whId]);
            }

            string separator = TranslationController.Instance.currentLanguage.languageID is SupportedLangs.English or SupportedLangs.Russian ? "], [" : "】, 【";
            AddMsg(string.Format(GetString("WorkaholicAdviceAlive"), string.Join(separator, workaholicAliveList)), 255, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Workaholic), GetString("WorkaholicAliveTitle")));
        }
        //Bait Notify
        if (MeetingStates.FirstMeeting && CustomRoles.Bait.RoleExist() && Options.BaitNotification.GetBool())
        {
            foreach (var pc in Main.AllAlivePlayerControls.Where(x => x.Is(CustomRoles.Bait)).ToArray())
                Main.BaitAlive.Add(pc.PlayerId);
            List<string> baitAliveList = [];
            for (int i = 0; i < Main.BaitAlive.Count; i++)
            {
                byte whId = Main.BaitAlive[i];
                baitAliveList.Add(Main.AllPlayerNames[whId]);
            }

            string separator = TranslationController.Instance.currentLanguage.languageID is SupportedLangs.English or SupportedLangs.Russian ? "], [" : "】, 【";
            AddMsg(string.Format(GetString("BaitAdviceAlive"), string.Join(separator, baitAliveList)), 255, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Bait), GetString("BaitAliveTitle")));
        }
        string MimicMsg = string.Empty;
        for (int i1 = 0; i1 < Main.AllPlayerControls.Length; i1++)
        {
            PlayerControl pc = Main.AllPlayerControls[i1];
            //黑手党死后技能提示
            if (pc.Is(CustomRoles.Mafia) && !pc.IsAlive())
                AddMsg(GetString("MafiaDeadMsg"), pc.PlayerId);
            if (pc.Is(CustomRoles.Retributionist) && !pc.IsAlive())
                AddMsg(GetString("RetributionistDeadMsg"), pc.PlayerId);
            //网红死亡消息提示
            for (int i = 0; i < Main.CyberStarDead.Count; i++)
            {
                byte csId = Main.CyberStarDead[i];
                if (!Options.ImpKnowCyberStarDead.GetBool() && pc.GetCustomRole().IsImpostor()) continue;
                if (!Options.NeutralKnowCyberStarDead.GetBool() && pc.GetCustomRole().IsNeutral()) continue;
                AddMsg(string.Format(GetString("CyberStarDead"), Main.AllPlayerNames[csId]), pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.CyberStar), GetString("CyberStarNewsTitle")));
            }

            //侦探报告线索
            if (Blackmailer.IsEnable && pc != null && Blackmailer.ForBlackmailer.Contains(pc.PlayerId))
            {
                var playername = pc.GetRealName();
                if (Doppelganger.DoppelVictim.ContainsKey(pc.PlayerId)) playername = Doppelganger.DoppelVictim[pc.PlayerId];
                AddMsg(string.Format(GetString("BlackmailerDead"), playername, pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Blackmailer), GetString("BlackmaileKillTitle"))));
            }
            if (Main.DetectiveNotify.ContainsKey(pc.PlayerId))
                AddMsg(Main.DetectiveNotify[pc.PlayerId], pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Detective), GetString("DetectiveNoticeTitle")));
            //宝箱怪的消息（记录）
            if (pc.Is(CustomRoles.Mimic) && !pc.IsAlive())
                Main.AllAlivePlayerControls.Where(x => x.GetRealKiller()?.PlayerId == pc.PlayerId).Do(x => MimicMsg += $"\n{x.GetNameWithRole(true)}");
            //入殓师的检查
            if (Mortician.msgToSend.ContainsKey(pc.PlayerId))
                AddMsg(Mortician.msgToSend[pc.PlayerId], pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Mortician), GetString("MorticianCheckTitle")));
            //通灵师的提示（自己）
            if (Mediumshiper.ContactPlayer.ContainsValue(pc.PlayerId))
                AddMsg(string.Format(GetString("MediumshipNotifySelf"), Main.AllPlayerNames[Mediumshiper.ContactPlayer.FirstOrDefault(x => x.Value == pc.PlayerId).Key], Mediumshiper.ContactLimit[pc.PlayerId]), pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Mediumshiper), GetString("MediumshipTitle")));
            //通灵师的提示（目标）
            if (Mediumshiper.ContactPlayer.ContainsKey(pc.PlayerId) && (!Mediumshiper.OnlyReceiveMsgFromCrew.GetBool() || pc.GetCustomRole().IsCrewmate()))
                AddMsg(string.Format(GetString("MediumshipNotifyTarget"), Main.AllPlayerNames[Mediumshiper.ContactPlayer[pc.PlayerId]]), pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Mediumshiper), GetString("MediumshipTitle")));
            if (Main.VirusNotify.ContainsKey(pc.PlayerId))
                AddMsg(Main.VirusNotify[pc.PlayerId], pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Virus), GetString("VirusNoticeTitle")));
            if (Enigma.MsgToSend.ContainsKey(pc.PlayerId))
                AddMsg(Enigma.MsgToSend[pc.PlayerId], pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Enigma), Enigma.MsgToSendTitle[pc.PlayerId]));
        }
        //宝箱怪的消息（合并）
        if (MimicMsg != string.Empty)
        {
            MimicMsg = GetString("MimicDeadMsg") + "\n" + MimicMsg;
            foreach (var ipc in Main.AllPlayerControls.Where(x => x.GetCustomRole().IsImpostorTeam()).ToArray())
                AddMsg(MimicMsg, ipc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Mimic), GetString("MimicMsgTitle")));
        }

        msgToSend.Do(x => Logger.Info($"To:{x.TARGET_ID} {x.TITLE} => {x.MESSAGE}", "Skill Notice OnMeeting Start"));

        //总体延迟发送
        _ = new LateTask(() => { msgToSend.Do(x => Utils.SendMessage(x.MESSAGE, x.TARGET_ID, x.TITLE)); }, 3f, "Skill Notice OnMeeting Start");

        Main.CyberStarDead.Clear();
        Main.DemolitionistDead.Clear();
        Main.ExpressSpeedUp.Clear();
        Main.DetectiveNotify.Clear();
        Main.VirusNotify.Clear();
        Mortician.msgToSend.Clear();
        Enigma.MsgToSend.Clear();
        //Pirate.OnMeetingStart();
    }
    public static void Prefix(/*MeetingHud __instance*/)
    {
        Logger.Info("------------Meeting Start------------", "Phase");
        ChatUpdatePatch.DoBlockChat = true;
        GameStates.AlreadyDied |= !Utils.IsAllAlive;
        Main.AllPlayerControls.Do(x => ReportDeadBodyPatch.WaitReport[x.PlayerId].Clear());
        MeetingStates.MeetingCalled = true;
    }
    public static void Postfix(MeetingHud __instance)
    {
        SoundManager.Instance.ChangeAmbienceVolume(0f);
        if (!GameStates.IsModHost) return;

        //提前储存赌怪游戏组件的模板
        GuessManager.textTemplate = UnityEngine.Object.Instantiate(__instance.playerStates[0].NameText);
        GuessManager.textTemplate.enabled = false;

        for (int i = 0; i < __instance.playerStates.Count; i++)
        {
            PlayerVoteArea pva = __instance.playerStates[i];
            var pc = Utils.GetPlayerById(pva.TargetPlayerId);
            if (pc == null) continue;
            var RoleTextData = Utils.GetRoleText(PlayerControl.LocalPlayer.PlayerId, pc.PlayerId);
            var roleTextMeeting = UnityEngine.Object.Instantiate(pva.NameText);
            roleTextMeeting.transform.SetParent(pva.NameText.transform);
            roleTextMeeting.transform.localPosition = new Vector3(0f, -0.18f, 0f);
            roleTextMeeting.fontSize = 1.4f;
            roleTextMeeting.text = RoleTextData.Item1;
            if (Main.VisibleTasksCount) roleTextMeeting.text += Utils.GetProgressText(pc);
            roleTextMeeting.color = RoleTextData.Item2;
            roleTextMeeting.gameObject.name = "RoleTextMeeting";
            roleTextMeeting.enableWordWrapping = false;
            roleTextMeeting.enabled =
                pc.AmOwner || //対象がLocalPlayer
                (Main.VisibleTasksCount && PlayerControl.LocalPlayer.Data.IsDead && Options.GhostCanSeeOtherRoles.GetBool()) || //LocalPlayerが死亡していて幽霊が他人の役職を見れるとき
                (PlayerControl.LocalPlayer.Is(CustomRoles.Mimic) && Main.VisibleTasksCount && pc.Data.IsDead && Options.MimicCanSeeDeadRoles.GetBool()) || //LocalPlayerが死亡していて幽霊が他人の役職を見れるとき
                (pc.Is(CustomRoles.Gravestone) && Main.VisibleTasksCount && pc.Data.IsDead) || //LocalPlayerが死亡していて幽霊が他人の役職を見れるとき
                (pc.Is(CustomRoles.Lovers) && PlayerControl.LocalPlayer.Is(CustomRoles.Lovers) && Options.LoverKnowRoles.GetBool()) ||
                //(pc.Is(CustomRoles.Ntr) && Options.LoverKnowRoles.GetBool()) ||
                (pc.Is(CustomRoleTypes.Impostor) && PlayerControl.LocalPlayer.Is(CustomRoleTypes.Impostor) && Options.ImpKnowAlliesRole.GetBool()) ||
                (pc.Is(CustomRoleTypes.Impostor) && PlayerControl.LocalPlayer.Is(CustomRoles.Madmate) && Options.MadmateKnowWhosImp.GetBool()) ||
                (pc.Is(CustomRoles.Madmate) && PlayerControl.LocalPlayer.Is(CustomRoleTypes.Impostor) && Options.ImpKnowWhosMadmate.GetBool()) ||
                (pc.Is(CustomRoleTypes.Impostor) && PlayerControl.LocalPlayer.Is(CustomRoles.Crewpostor) && Options.AlliesKnowCrewpostor.GetBool()) ||
                (pc.Is(CustomRoles.Crewpostor) && PlayerControl.LocalPlayer.Is(CustomRoleTypes.Impostor) && Options.CrewpostorKnowsAllies.GetBool()) ||
                (pc.Is(CustomRoles.Madmate) && PlayerControl.LocalPlayer.Is(CustomRoles.Madmate) && Options.MadmateKnowWhosMadmate.GetBool()) ||
                (pc.Is(CustomRoles.Rogue) && PlayerControl.LocalPlayer.Is(CustomRoles.Rogue) && Options.RogueKnowEachOther.GetBool() && Options.RogueKnowEachOtherRoles.GetBool()) ||
                (pc.Is(CustomRoles.Jackal) && PlayerControl.LocalPlayer.Is(CustomRoles.Sidekick)) ||
                (pc.Is(CustomRoles.Jackal) && PlayerControl.LocalPlayer.Is(CustomRoles.Recruit)) ||
                (pc.Is(CustomRoles.Sidekick) && PlayerControl.LocalPlayer.Is(CustomRoles.Sidekick)) ||
                (pc.Is(CustomRoles.Sidekick) && PlayerControl.LocalPlayer.Is(CustomRoles.Jackal)) ||
                (pc.Is(CustomRoles.Workaholic) && Options.WorkaholicVisibleToEveryone.GetBool()) ||
                (pc.Is(CustomRoles.Doctor) && !pc.GetCustomRole().IsEvilAddons() && Options.DoctorVisibleToEveryone.GetBool()) ||
                (pc.Is(CustomRoles.Mayor) && Options.MayorRevealWhenDoneTasks.GetBool() && pc.GetPlayerTaskState().IsTaskFinished) ||
                (pc.Is(CustomRoles.Marshall) && PlayerControl.LocalPlayer.Is(CustomRoleTypes.Crewmate) && pc.GetPlayerTaskState().IsTaskFinished) ||
                (Main.PlayerStates[pc.PlayerId].deathReason == PlayerState.DeathReason.Vote && Options.SeeEjectedRolesInMeeting.GetBool()) ||
                Totocalcio.KnowRole(PlayerControl.LocalPlayer, pc) ||
                Romantic.KnowRole(PlayerControl.LocalPlayer, pc) ||
                EvilDiviner.IsShowTargetRole(PlayerControl.LocalPlayer, pc) ||
                Ritualist.IsShowTargetRole(PlayerControl.LocalPlayer, pc) ||
                Lawyer.KnowRole(PlayerControl.LocalPlayer, pc) ||
                Executioner.KnowRole(PlayerControl.LocalPlayer, pc) ||
                Succubus.KnowRole(PlayerControl.LocalPlayer, pc) ||
                CursedSoul.KnowRole(PlayerControl.LocalPlayer, pc) ||
                Admirer.KnowRole(PlayerControl.LocalPlayer, pc) ||
                Amnesiac.KnowRole(PlayerControl.LocalPlayer, pc) ||
                Infectious.KnowRole(PlayerControl.LocalPlayer, pc) ||
                Virus.KnowRole(PlayerControl.LocalPlayer, pc) ||
                PlayerControl.LocalPlayer.IsRevealedPlayer(pc) ||
                PlayerControl.LocalPlayer.Is(CustomRoles.God) ||
                PlayerControl.LocalPlayer.Is(CustomRoles.GM) ||
                Main.GodMode.Value;

            //Baker.SendAliveMessage(pc);

            if (!PlayerControl.LocalPlayer.Data.IsDead && PlayerControl.LocalPlayer.IsRevealedPlayer(pc) && pc.Is(CustomRoles.Trickster))
            {
                roleTextMeeting.text = Farseer.RandomRole[PlayerControl.LocalPlayer.PlayerId];
                roleTextMeeting.text += Farseer.GetTaskState();
            }

            if (EvilTracker.IsTrackTarget(PlayerControl.LocalPlayer, pc) && EvilTracker.CanSeeLastRoomInMeeting)
            {
                roleTextMeeting.text = EvilTracker.GetArrowAndLastRoom(PlayerControl.LocalPlayer, pc);
                roleTextMeeting.enabled = true;
            }
            if (Tracker.IsTrackTarget(PlayerControl.LocalPlayer, pc) && Tracker.CanSeeLastRoomInMeeting)
            {
                roleTextMeeting.text = Tracker.GetArrowAndLastRoom(PlayerControl.LocalPlayer, pc);
                roleTextMeeting.enabled = true;
            }
        }

        if (Options.SyncButtonMode.GetBool())
        {
            Utils.SendMessage(string.Format(GetString("Message.SyncButtonLeft"), Options.SyncedButtonCount.GetFloat() - Options.UsedButtonCount));
            Logger.Info("The ship has " + (Options.SyncedButtonCount.GetFloat() - Options.UsedButtonCount) + " buttons left", "SyncButtonMode");
        }
        if (AntiBlackout.OverrideExiledPlayer)
        {
            _ = new LateTask(() =>
            {
                Utils.SendMessage(GetString("Warning.OverrideExiledPlayer"), 255, Utils.ColorString(Color.red, GetString("DefaultSystemMessageTitle")));
            }, 5f, "Warning OverrideExiledPlayer");
        }
        if (MeetingStates.FirstMeeting) TemplateManager.SendTemplate("OnFirstMeeting", noErr: true);
        TemplateManager.SendTemplate("OnMeeting", noErr: true);

        if (AmongUsClient.Instance.AmHost)
            NotifyRoleSkillOnMeetingStart();

        if (AmongUsClient.Instance.AmHost)
        {
            _ = _ = new LateTask(() =>
            {
                foreach (PlayerControl pc in Main.AllPlayerControls)
                {
                    pc.RpcSetNameEx(pc.GetRealName(isMeeting: true));
                }
                ChatUpdatePatch.DoBlockChat = false;
            }, 3f, "SetName To Chat");
        }

        for (int i = 0; i < __instance.playerStates.Count; i++)
        {
            PlayerVoteArea pva = __instance.playerStates[i];
            if (pva == null) continue;
            PlayerControl seer = PlayerControl.LocalPlayer;
            PlayerControl target = Utils.GetPlayerById(pva.TargetPlayerId);
            if (target == null) continue;

            var sb = new StringBuilder();

            //会議画面での名前変更
            //自分自身の名前の色を変更
            //NameColorManager準拠の処理
            pva.NameText.text = pva.NameText.text.ApplyNameColorData(seer, target, true);


            // Guesser Mode //
            if (Options.GuesserMode.GetBool())
            {
                if (Options.CrewmatesCanGuess.GetBool() && seer.GetCustomRole().IsCrewmate() && !seer.Is(CustomRoles.Judge) && !seer.Is(CustomRoles.NiceSwapper) && !seer.Is(CustomRoles.Lookout) && !seer.Is(CustomRoles.ParityCop))
                    if (!seer.Data.IsDead && !target.Data.IsDead)
                        pva.NameText.text = Utils.ColorString(Utils.GetRoleColor(seer.GetCustomRole()), target.PlayerId.ToString()) + " " + pva.NameText.text;
                if (Options.ImpostorsCanGuess.GetBool() && seer.GetCustomRole().IsImpostor() && !seer.Is(CustomRoles.Councillor))
                    if (!seer.Data.IsDead && !target.Data.IsDead)
                        pva.NameText.text = Utils.ColorString(Utils.GetRoleColor(seer.GetCustomRole()), target.PlayerId.ToString()) + " " + pva.NameText.text;
                if (Options.NeutralKillersCanGuess.GetBool() && seer.GetCustomRole().IsNK())
                    if (!seer.Data.IsDead && !target.Data.IsDead)
                        pva.NameText.text = Utils.ColorString(Utils.GetRoleColor(seer.GetCustomRole()), target.PlayerId.ToString()) + " " + pva.NameText.text;
                if (Options.PassiveNeutralsCanGuess.GetBool() && seer.GetCustomRole().IsNonNK() && !seer.Is(CustomRoles.Doomsayer))
                    if (!seer.Data.IsDead && !target.Data.IsDead)
                        pva.NameText.text = Utils.ColorString(Utils.GetRoleColor(seer.GetCustomRole()), target.PlayerId.ToString()) + " " + pva.NameText.text;

            }
            //とりあえずSnitchは会議中にもインポスターを確認することができる仕様にしていますが、変更する可能性があります。

            if (seer.KnowDeathReason(target))
                sb.Append($"({Utils.ColorString(Utils.GetRoleColor(CustomRoles.Doctor), Utils.GetVitalText(target.PlayerId))})");
            /*        if (seer.KnowDeadTeam(target))
                    {
                        if (target.Is(CustomRoleTypes.Crewmate) && !(target.Is(CustomRoles.Madmate) || target.Is(CustomRoles.Egoist) || target.Is(CustomRoles.Charmed) || target.Is(CustomRoles.Recruit) || target.Is(CustomRoles.Infected) || target.Is(CustomRoles.Contagious) || target.Is(CustomRoles.Rogue) || target.Is(CustomRoles.Rascal) || target.Is(CustomRoles.Soulless) || !target.Is(CustomRoles.Admired)))
                            sb.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Bait), "★"));

                        if (target.Is(CustomRoleTypes.Impostor) || target.Is(CustomRoles.Madmate) || target.Is(CustomRoles.Rascal) || target.Is(CustomRoles.Parasite) || target.Is(CustomRoles.Refugee) || target.Is(CustomRoles.Crewpostor) || target.Is(CustomRoles.Convict) || !target.Is(CustomRoles.Admired))
                            sb.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Impostor), "★"));

                        if (target.Is(CustomRoleTypes.Neutral) || target.Is(CustomRoles.Rogue) || target.Is(CustomRoles.Contagious) || target.Is(CustomRoles.Charmed) || target.Is(CustomRoles.Recruit) || target.Is(CustomRoles.Infected) || target.Is(CustomRoles.Egoist) || target.Is(CustomRoles.Soulless) || !target.Is(CustomRoles.Admired) || !target.Is(CustomRoles.Parasite) || !target.Is(CustomRoles.Refugee) || !target.Is(CustomRoles.Crewpostor) || !target.Is(CustomRoles.Convict))
                            sb.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Executioner), "★"));



                    }
                    if (seer.KnowLivingTeam(target))
                    {
                        if (target.Is(CustomRoleTypes.Crewmate) && !(target.Is(CustomRoles.Madmate) || target.Is(CustomRoles.Egoist) || target.Is(CustomRoles.Charmed) || target.Is(CustomRoles.Recruit) || target.Is(CustomRoles.Infected) || target.Is(CustomRoles.Contagious) || target.Is(CustomRoles.Rogue) || target.Is(CustomRoles.Rascal) || target.Is(CustomRoles.Admired)))
                            sb.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Bait), "★"));

                        if (target.Is(CustomRoleTypes.Impostor) || target.Is(CustomRoles.Madmate) || target.Is(CustomRoles.Rascal) || target.Is(CustomRoles.Parasite) || target.Is(CustomRoles.Refugee) || target.Is(CustomRoles.Crewpostor) || target.Is(CustomRoles.Convict) || !target.Is(CustomRoles.Admired))
                            sb.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Impostor), "★"));

                        if (target.Is(CustomRoleTypes.Neutral) || target.Is(CustomRoles.Rogue) || target.Is(CustomRoles.Contagious) || target.Is(CustomRoles.Charmed) || target.Is(CustomRoles.Recruit) || target.Is(CustomRoles.Infected) || target.Is(CustomRoles.Egoist) || target.Is(CustomRoles.Soulless) || !target.Is(CustomRoles.Admired) || !target.Is(CustomRoles.Parasite) || !target.Is(CustomRoles.Refugee) || !target.Is(CustomRoles.Crewpostor) || !target.Is(CustomRoles.Convict))
                            sb.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Executioner), "★"));



                    } */
            //インポスター表示
            switch (seer.GetCustomRole().GetCustomRoleTypes())
            {
                case CustomRoleTypes.Impostor:
                    if (target.Is(CustomRoles.Snitch) && target.Is(CustomRoles.Madmate) && target.GetPlayerTaskState().IsTaskFinished)
                        sb.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Impostor), "★")); //変更対象にSnitchマークをつける
                    sb.Append(Snitch.GetWarningMark(seer, target));
                    break;
                case CustomRoleTypes.Crewmate:
                    if (target.Is(CustomRoles.Marshall) && target.GetPlayerTaskState().IsTaskFinished)
                        sb.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Marshall), "★")); //変更対象にSnitchマークをつける
                    sb.Append(Marshall.GetWarningMark(seer, target));
                    break;
            }
            switch (seer.GetCustomRole())
            {
                case CustomRoles.Arsonist:
                    if (seer.IsDousedPlayer(target)) //seerがtargetに既にオイルを塗っている(完了)
                        sb.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Arsonist), "▲"));
                    break;
                case CustomRoles.Executioner:
                    sb.Append(Executioner.TargetMark(seer, target));
                    break;
                case CustomRoles.Lawyer:
                    //   sb.Append(Lawyer.TargetMark(seer, target));
                    break;
                //   case CustomRoles.Jackal:
                //   case CustomRoles.Sidekick:
                case CustomRoles.Poisoner:
                case CustomRoles.NSerialKiller:
                case CustomRoles.PlagueDoctor:
                case CustomRoles.Reckless:
                case CustomRoles.Magician:
                case CustomRoles.WeaponMaster:
                case CustomRoles.Pyromaniac:
                case CustomRoles.Eclipse:
                case CustomRoles.Vengeance:
                case CustomRoles.HeadHunter:
                case CustomRoles.Imitator:
                case CustomRoles.Werewolf:
                case CustomRoles.RuthlessRomantic:
                case CustomRoles.Pelican:
                case CustomRoles.DarkHide:
                case CustomRoles.BloodKnight:
                case CustomRoles.Infectious:
                case CustomRoles.Virus:
                case CustomRoles.Medusa:
                case CustomRoles.Succubus:
                case CustomRoles.Pickpocket:
                case CustomRoles.Ritualist:
                case CustomRoles.Traitor:
                case CustomRoles.Spiritcaller:
                    sb.Append(Snitch.GetWarningMark(seer, target));
                    break;
                case CustomRoles.Jackal:
                case CustomRoles.Sidekick:
                    //         if (target.Is(CustomRoles.Sidekick))
                    //       sb.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jackal), " ♥")); //変更対象にSnitchマークをつける
                    sb.Append(Snitch.GetWarningMark(seer, target));
                    break;
                /*         case CustomRoles.Monarch:
                             if (target.Is(CustomRoles.Knighted))
                             sb.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Knighted), " 亗")); //変更対象にSnitchマークをつける
                             break; */
                case CustomRoles.EvilTracker:
                    sb.Append(EvilTracker.GetTargetMark(seer, target));
                    break;
                case CustomRoles.Revolutionist:
                    if (seer.IsDrawPlayer(target)) //seerがtargetに既にオイルを塗っている(完了)
                        sb.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Revolutionist), "●"));
                    break;
                case CustomRoles.Psychic:
                    if (target.IsRedForPsy(seer) && !seer.Data.IsDead)
                        pva.NameText.text = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Impostor), pva.NameText.text);
                    break;
                case CustomRoles.Mafia:
                    if (seer.Data.IsDead && !target.Data.IsDead)
                        pva.NameText.text = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Mafia), target.PlayerId.ToString()) + " " + pva.NameText.text;
                    break;
                case CustomRoles.Retributionist:
                    if (seer.Data.IsDead && !target.Data.IsDead)
                        pva.NameText.text = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Retributionist), target.PlayerId.ToString()) + " " + pva.NameText.text;
                    break;
                case CustomRoles.NiceGuesser:
                case CustomRoles.EvilGuesser:
                    if (!seer.Data.IsDead && !target.Data.IsDead)
                        pva.NameText.text = Utils.ColorString(Utils.GetRoleColor(seer.Is(CustomRoles.NiceGuesser) ? CustomRoles.NiceGuesser : CustomRoles.EvilGuesser), target.PlayerId.ToString()) + " " + pva.NameText.text;
                    break;
                case CustomRoles.Guesser:
                    if (!seer.Data.IsDead && !target.Data.IsDead)
                        pva.NameText.text = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Guesser), target.PlayerId.ToString()) + " " + pva.NameText.text;
                    break;
                case CustomRoles.Judge:
                    if (!seer.Data.IsDead && !target.Data.IsDead)
                        pva.NameText.text = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Judge), target.PlayerId.ToString()) + " " + pva.NameText.text;
                    break;
                case CustomRoles.NiceSwapper:
                    if (!seer.Data.IsDead && !target.Data.IsDead)
                        pva.NameText.text = Utils.ColorString(Utils.GetRoleColor(CustomRoles.NiceSwapper), target.PlayerId.ToString()) + " " + pva.NameText.text;
                    break;
                case CustomRoles.Lookout:
                    if (!seer.Data.IsDead && !target.Data.IsDead)
                        pva.NameText.text = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Lookout), target.PlayerId.ToString()) + " " + pva.NameText.text;
                    break;
                case CustomRoles.Doomsayer:
                    if (!seer.Data.IsDead && !target.Data.IsDead)
                        pva.NameText.text = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Doomsayer), target.PlayerId.ToString()) + " " + pva.NameText.text;
                    break;
                case CustomRoles.ParityCop:
                    if (!seer.Data.IsDead && !target.Data.IsDead)
                        pva.NameText.text = Utils.ColorString(Utils.GetRoleColor(CustomRoles.ParityCop), target.PlayerId.ToString()) + " " + pva.NameText.text;
                    break;

                case CustomRoles.Councillor:
                    if (!seer.Data.IsDead && !target.Data.IsDead)
                        pva.NameText.text = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Councillor), target.PlayerId.ToString()) + " " + pva.NameText.text;

                    break;

                case CustomRoles.Gamer:
                    sb.Append(Gamer.TargetMark(seer, target));
                    sb.Append(Snitch.GetWarningMark(seer, target));
                    break;
                case CustomRoles.Tracker:
                    sb.Append(Tracker.GetTargetMark(seer, target));
                    break;
            }

            if (Blackmailer.ForBlackmailer.Contains(target.PlayerId))
                sb.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Blackmailer), "╳"));

            List<CustomRoles> list = seer.GetCustomSubRoles();
            for (int i1 = 0; i1 < list.Count; i1++)
            {
                CustomRoles SeerSubRole = list[i1];
                switch (SeerSubRole)
                {
                    case CustomRoles.Guesser:
                        if (!seer.Data.IsDead && !target.Data.IsDead)
                            pva.NameText.text = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Guesser), target.PlayerId.ToString()) + " " + pva.NameText.text;
                        break;
                }
            }

            List<CustomRoles> list1 = target.GetCustomSubRoles();
            for (int i1 = 0; i1 < list1.Count; i1++)
            {
                CustomRoles subRole = list1[i1];
                switch (subRole)
                {
                    case CustomRoles.Lovers:
                        if (seer.Is(CustomRoles.Lovers) || seer.Data.IsDead)
                        {
                            sb.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Lovers), "♥"));
                        }
                        break;
                        /*     case CustomRoles.Sidekick:
                             if (seer.Is(CustomRoles.Sidekick) && target.Is(CustomRoles.Sidekick) && Options.SidekickKnowOtherSidekick.GetBool())
                             {
                                 sb.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jackal), " ♥")); //変更対象にSnitchマークをつける
                             sb.Append(Snitch.GetWarningMark(seer, target));
                             }
                             break; */
                        //   case CustomRoles.Guesser:
                        //    if (!seer.Data.IsDead && !target.Data.IsDead)
                        //       pva.NameText.text = Utils.ColorString(Utils.GetRoleColor(seer.GetCustomRole()), target.PlayerId.ToString()) + " " + pva.NameText.text;

                        //    break;
                }
            }

            //海王相关显示
            //if ((seer.Is(CustomRoles.Ntr) || target.Is(CustomRoles.Ntr)) && !seer.Data.IsDead && !isLover)
            //    sb.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Lovers), "♥"));
            //else if (seer == target && CustomRolesHelper.RoleExist(CustomRoles.Ntr) && !isLover)
            //    sb.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Lovers), "♥"));

            //呪われている場合
            sb.Append(Witch.GetSpelledMark(target.PlayerId, true));
            sb.Append(HexMaster.GetHexedMark(target.PlayerId, true));
            //if (Baker.IsPoisoned(target))
            //    sb.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Famine), "θ"));


            //如果是大明星
            if (target.Is(CustomRoles.SuperStar) && Options.EveryOneKnowSuperStar.GetBool())
                sb.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.SuperStar), "★"));

            //球状闪电提示
            if (BallLightning.IsGhost(target))
                sb.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.BallLightning), "■"));

            //医生护盾提示
            if (seer.PlayerId == target.PlayerId && (Medic.InProtect(seer.PlayerId) || Medic.TempMarkProtected == seer.PlayerId) && (Medic.WhoCanSeeProtect.GetInt() == 0 || Medic.WhoCanSeeProtect.GetInt() == 2))
                sb.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Medic), " ●"));

            if (seer.Is(CustomRoles.Medic) && (Medic.InProtect(target.PlayerId) || Medic.TempMarkProtected == target.PlayerId) && (Medic.WhoCanSeeProtect.GetInt() == 0 || Medic.WhoCanSeeProtect.GetInt() == 1))
                sb.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Medic), " ●"));

            if (seer.Data.IsDead && Medic.InProtect(target.PlayerId) && !seer.Is(CustomRoles.Medic))
                sb.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Medic), " ●"));

            //赌徒提示
            sb.Append(Totocalcio.TargetMark(seer, target));
            sb.Append(Romantic.TargetMark(seer, target));
            sb.Append(Lawyer.LawyerMark(seer, target));
            sb.Append(PlagueDoctor.GetMarkOthers(seer, target, isForMeeting: true));

            //会議画面ではインポスター自身の名前にSnitchマークはつけません。

            pva.NameText.text += sb.ToString();
            pva.ColorBlindName.transform.localPosition -= new Vector3(1.35f, 0f, 0f);
        }
    }
}
[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Update))]
class MeetingHudUpdatePatch
{
    private static int bufferTime = 10;
    private static void ClearShootButton(MeetingHud __instance, bool forceAll = false)
     => __instance.playerStates.ToList().ForEach(x => { if ((forceAll || !Main.PlayerStates.TryGetValue(x.TargetPlayerId, out var ps) || ps.IsDead) && x.transform.FindChild("ShootButton") != null) UnityEngine.Object.Destroy(x.transform.FindChild("ShootButton").gameObject); });

    public static void Postfix(MeetingHud __instance)
    {
        try
        {
            // Meeting Skip with vote counting on keystroke (m + delete)
            if (AmongUsClient.Instance.AmHost && Input.GetKeyDown(KeyCode.F6))
            {
                __instance.CheckForEndVoting();
            }

            //if (Options.DisableCrackedGlass.GetBool()) __instance.CrackedGlass = null;

            if (AmongUsClient.Instance.AmHost && Input.GetMouseButtonUp(1) && Input.GetKey(KeyCode.LeftControl))
            {
                __instance.playerStates.DoIf(x => x.HighlightedFX.enabled, x =>
                {
                    var player = Utils.GetPlayerById(x.TargetPlayerId);
                    if (player != null && !player.Data.IsDead)
                    {
                        Main.PlayerStates[player.PlayerId].deathReason = PlayerState.DeathReason.Execution;
                        player.RpcExileV2();
                        Main.PlayerStates[player.PlayerId].SetDead();
                        Utils.SendMessage(string.Format(GetString("Message.Executed"), player.Data.PlayerName));
                        Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags()}を処刑しました", "Execution");
                        __instance.CheckForEndVoting();
                    }
                });
            }

            //投票结束时销毁全部技能按钮
            if (!GameStates.IsVoting && __instance.lastSecond < 1)
            {
                if (GameObject.Find("ShootButton") != null) ClearShootButton(__instance, true);
                return;
            }

            //会议技能UI处理
            bufferTime--;
            if (bufferTime < 0 && __instance.discussionTimer > 0)
            {
                bufferTime = 10;
                var myRole = PlayerControl.LocalPlayer.GetCustomRole();

                //若某玩家死亡则修复会议该玩家状态
                __instance.playerStates.Where(x => (!Main.PlayerStates.TryGetValue(x.TargetPlayerId, out var ps) || ps.IsDead) && !x.AmDead).Do(x => x.SetDead(x.DidReport, true));

                //若玩家死亡则销毁技能按钮
                if (myRole is CustomRoles.NiceGuesser or CustomRoles.EvilGuesser or CustomRoles.Judge or CustomRoles.NiceSwapper or CustomRoles.Councillor or CustomRoles.Guesser && !PlayerControl.LocalPlayer.IsAlive())
                    ClearShootButton(__instance, true);

                //若黑手党死亡则创建技能按钮
                if (myRole is CustomRoles.Mafia && !PlayerControl.LocalPlayer.IsAlive() && GameObject.Find("ShootButton") == null)
                    MafiaRevengeManager.CreateJudgeButton(__instance);
                if (myRole is CustomRoles.Retributionist && !PlayerControl.LocalPlayer.IsAlive() && GameObject.Find("ShootButton") == null)
                    RetributionistRevengeManager.CreateJudgeButton(__instance);
                //if (myRole is CustomRoles.NiceSwapper && PlayerControl.LocalPlayer.IsAlive())
                //    NiceSwapper.CreateSwapperButton(__instance);

                //销毁死亡玩家身上的技能按钮
                ClearShootButton(__instance);

            }
        }
        catch (Exception ex)
        {
            Logger.Fatal(ex.ToString(), "MeetingHudUpdatePatch.Postfix");
            Logger.Warn("All Players and their info:", "Debug for Fatal Error");
            foreach (PlayerControl pc in Main.AllPlayerControls)
            {
                Logger.Info($" {(pc.IsAlive() ? "Alive" : $"Dead ({Main.PlayerStates[pc.PlayerId].deathReason})")}, {Utils.GetProgressText(pc)}, {Utils.GetVitalText(pc.PlayerId)}", $"{pc.GetNameWithRole()} / {pc.PlayerId}");
            }
            Logger.Warn("-----------------", "Debug for Fatal Error");
            Logger.SendInGame("An error occured with this meeting. Please use /dump and send the log to the developer.\nSorry for the inconvenience.");
        }
    }
}
[HarmonyPatch(typeof(PlayerVoteArea), nameof(PlayerVoteArea.SetHighlighted))]
class SetHighlightedPatch
{
    public static bool Prefix(PlayerVoteArea __instance, bool value)
    {
        if (!AmongUsClient.Instance.AmHost) return true;
        if (!__instance.HighlightedFX) return false;
        __instance.HighlightedFX.enabled = value;
        return false;
    }
}
[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.OnDestroy))]
class MeetingHudOnDestroyPatch
{
    public static void Postfix()
    {
        MeetingStates.FirstMeeting = false;
        Logger.Info("------------End of meeting------------", "Phase");
        if (AmongUsClient.Instance.AmHost)
        {
            AntiBlackout.SetIsDead();
            Main.AllPlayerControls.Do(pc => RandomSpawn.CustomNetworkTransformPatch.NumOfTP[pc.PlayerId] = 0);

            Main.LastVotedPlayerInfo = null;
            EAC.MeetingTimes = 0;
        }
    }
}
[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.CastVote))]
class MeetingHudCastVotePatch
{
    public static bool Prefix()
    {
        return true; // return true to use the vote as a trigger
    }
    public static void Postfix(MeetingHud __instance, [HarmonyArgument(0)] byte srcPlayerId, [HarmonyArgument(1)] byte suspectPlayerId)
    {
        PlayerVoteArea pva_src = null;
        PlayerVoteArea pva_target = null;
        bool isSkip = false;
        for (int i = 0; i < __instance.playerStates.Count; i++)
        {
            if (__instance.playerStates[i].TargetPlayerId == srcPlayerId) pva_src = __instance.playerStates[i];
            if (__instance.playerStates[i].TargetPlayerId == suspectPlayerId) pva_target = __instance.playerStates[i];
        }
        if (pva_src == null)
        {
            Logger.Error("Src PlayerVoteArea not found", "MeetingHudCastVotePatch.Prefix");
            return;
        }
        if (pva_target == null)
        {
            //Logger.Warn("Target PlayerVoteArea not found => Vote treated as a Skip", "MeetingHudCastVotePatch.Prefix");
            isSkip = true;
        }
        var pc_src = Utils.GetPlayerById(srcPlayerId);
        var pc_target = Utils.GetPlayerById(suspectPlayerId);
        if (pc_src == null)
        {
            Logger.Error("Src PlayerControl is null", "MeetingHudCastVotePatch.Prefix");
            return;
        }
        if (pc_target == null)
        {
            //Logger.Warn("Target PlayerControl is null => Vote treated as a Skip", "MeetingHudCastVotePatch.Prefix");
            isSkip = true;
        }

        Logger.Info($"{pc_src.GetNameWithRole().RemoveHtmlTags()} => {(isSkip ? "Skip" : pc_target.GetNameWithRole().RemoveHtmlTags())}", "Vote");

        //pva_src.UnsetVote(); // Uncomment to use the vote as a trigger
        //__instance.RpcClearVote(pc_src.GetClientId()); // Uncomment to use the vote as a trigger
    }
}
[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.CmdCastVote))]
class MeetingHudCmdCastVotePatch
{
    public static bool Prefix()
    {
        return MeetingHudCastVotePatch.Prefix();
    }
    public static void Postfix(MeetingHud __instance, [HarmonyArgument(0)] byte srcPlayerId, [HarmonyArgument(1)] byte suspectPlayerId)
    {
        MeetingHudCastVotePatch.Postfix(__instance, srcPlayerId, suspectPlayerId);
    }
}