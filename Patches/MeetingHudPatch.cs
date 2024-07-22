using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AmongUs.GameOptions;
using EHR.AddOns.Common;
using EHR.Crewmate;
using EHR.Impostor;
using EHR.Modules;
using EHR.Neutral;
using HarmonyLib;
using UnityEngine;
using static EHR.Translator;


namespace EHR.Patches;

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.CheckForEndVoting))]
class CheckForEndVotingPatch
{
    public static string EjectionText = string.Empty;
    public static bool RunRoleCode = true;

    public static bool Prefix(MeetingHud __instance)
    {
        if (!AmongUsClient.Instance.AmHost) return true;
        if (Medic.playerIdList.Count > 0) Medic.OnCheckMark();
        //Meeting Skip with vote counting on keystroke (m + delete)
        bool shouldSkip = Input.GetKeyDown(KeyCode.F6);

        //
        var voteLog = Logger.Handler("Vote");
        try
        {
            List<MeetingHud.VoterState> statesList = [];
            MeetingHud.VoterState[] states;
            foreach (var pva in __instance.playerStates)
            {
                if (pva == null) continue;
                PlayerControl pc = Utils.GetPlayerById(pva.TargetPlayerId);
                if (pc == null) continue;
                // Dictators who are not dead have already voted.

                // Take the initiative to rebel
                if (pva.DidVote && pc.PlayerId == pva.VotedFor && pva.VotedFor < 253 && !pc.Data.IsDead)
                {
                    if (Options.MadmateSpawnMode.GetInt() == 2 && Main.MadmateNum < CustomRoles.Madmate.GetCount() && pc.CanBeMadmate())
                    {
                        Main.MadmateNum++;
                        pc.RpcSetCustomRole(CustomRoles.Madmate);
                        ExtendedPlayerControl.RpcSetCustomRole(pc.PlayerId, CustomRoles.Madmate);
                        Utils.NotifyRoles(isForMeeting: true, SpecifySeer: pc, NoCache: true);
                        Logger.Info("Set role: " + pc?.Data?.PlayerName + " => " + pc.GetCustomRole() + " + " + CustomRoles.Madmate, "Assign " + CustomRoles.Madmate);
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
                        ConfirmEjections(Main.LastVotedPlayerInfo, false);
                    return true;
                }

                if (pva.DidVote && pva.VotedFor < 253 && !pc.Data.IsDead)
                {
                    var voteTarget = Utils.GetPlayerById(pva.VotedFor);
                    if (voteTarget != null && !pc.GetCustomRole().CancelsVote())
                        Main.PlayerStates[pc.PlayerId].Role.OnVote(pc, voteTarget);
                    else if (pc.Is(CustomRoles.Godfather)) Godfather.GodfatherTarget = byte.MaxValue;
                }
            }

            if (!shouldSkip && !__instance.playerStates.All(ps => ps == null || !Main.PlayerStates.TryGetValue(ps.TargetPlayerId, out var st) || st.IsDead || ps.DidVote || Utils.GetPlayerById(ps.TargetPlayerId) == null || Utils.GetPlayerById(ps.TargetPlayerId).Data == null || Utils.GetPlayerById(ps.TargetPlayerId).Data.Disconnected))
            {
                return false;
            }

            NetworkedPlayerInfo exiledPlayer = PlayerControl.LocalPlayer.Data;
            bool tie = false;
            EjectionText = string.Empty;

            foreach (var ps in __instance.playerStates)
            {
                if (ps == null) continue;
                voteLog.Info($"{ps.TargetPlayerId,-2}{$"({Utils.GetVoteName(ps.TargetPlayerId)})".PadRightV2(40)}:{ps.VotedFor,-3}({Utils.GetVoteName(ps.VotedFor)})");
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
                        }
                    }

                    if (ps.VotedFor == 254 && !voter.Data.IsDead) //無投票
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
                        }
                    }
                }

                if (CheckRole(ps.TargetPlayerId, CustomRoles.Divinator) && Divinator.HideVote.GetBool()) continue;
                if (CheckRole(ps.TargetPlayerId, CustomRoles.Eraser) && Eraser.HideVote.GetBool()) continue;
                if (CheckRole(ps.TargetPlayerId, CustomRoles.NiceEraser) && NiceEraser.HideVote.GetBool()) continue;
                if (CheckRole(ps.TargetPlayerId, CustomRoles.Scout) && Scout.HideVote.GetBool()) continue;
                if (CheckRole(ps.TargetPlayerId, CustomRoles.Oracle) && Oracle.HideVote.GetBool()) continue;

                if (ps.TargetPlayerId == ps.VotedFor && Options.MadmateSpawnMode.GetInt() == 2) continue;

                AddVote();

                if (Main.PlayerStates[ps.TargetPlayerId].Role is Adventurer { IsEnable: true } av && av.ActiveWeapons.Contains(Adventurer.Weapon.Proxy)) AddVote();
                if (CheckRole(ps.TargetPlayerId, CustomRoles.Mayor) && !Mayor.MayorHideVote.GetBool()) Loop.Times(Mayor.MayorAdditionalVote.GetInt(), _ => AddVote());
                if (CheckRole(ps.TargetPlayerId, CustomRoles.Vindicator) && !Options.VindicatorHideVote.GetBool()) Loop.Times(Options.VindicatorAdditionalVote.GetInt(), _ => AddVote());
                if (CheckRole(ps.TargetPlayerId, CustomRoles.Knighted)) AddVote();
                if (CheckRole(ps.TargetPlayerId, CustomRoles.DualPersonality) && Options.DualVotes.GetBool())
                {
                    int count = statesList.Count(x => x.VoterId == ps.TargetPlayerId && x.VotedForId == ps.VotedFor);
                    Loop.Times(count, _ => AddVote());
                }

                if (CheckRole(ps.TargetPlayerId, CustomRoles.TicketsStealer))
                {
                    int count = (int)(Main.AllPlayerControls.Count(x => x.GetRealKiller()?.PlayerId == ps.TargetPlayerId) * Options.TicketsPerKill.GetFloat());
                    Loop.Times(count, _ => AddVote());
                }

                if (CheckRole(ps.TargetPlayerId, CustomRoles.Pickpocket))
                {
                    int count = (int)(Main.AllPlayerControls.Count(x => x.GetRealKiller()?.PlayerId == ps.TargetPlayerId) * Pickpocket.VotesPerKill.GetFloat());
                    Loop.Times(count, _ => AddVote());
                }

                continue;

                void AddVote()
                {
                    statesList.Add(new()
                    {
                        VoterId = ps.TargetPlayerId,
                        VotedForId = ps.VotedFor
                    });
                }
            }

            if (RunRoleCode)
            {
                Blackmailer.OnCheckForEndVoting();
                NiceSwapper.OnCheckForEndVoting();
            }

            states = [.. statesList];

            var VotingData = __instance.CustomCalculateVotes();
            byte exileId = byte.MaxValue;
            int max = 0;
            voteLog.Info("===Decision to expel player processing begins===");
            foreach (var data in VotingData)
            {
                voteLog.Info($"{data.Key} ({Utils.GetVoteName(data.Key)}): {data.Value} votes");
                if (data.Value > max)
                {
                    voteLog.Info($"{data.Key} has higher votes ({data.Value})");
                    exileId = data.Key;
                    max = data.Value;
                    tie = false;
                }
                else if (data.Value == max)
                {
                    voteLog.Info($"{data.Key} and {exileId} have the same number of votes ({data.Value})");
                    exileId = byte.MaxValue;
                    tie = true;
                }

                voteLog.Info($"expelID: {exileId}, maximum: {max} votes");
            }

            voteLog.Info($"Decided to eject: {exileId} ({Utils.GetVoteName(exileId)})");

            bool braked = false;
            if (tie)
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
                        foreach (var playerId in exileIds)
                        {
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

            // RPC
            if (AntiBlackout.OverrideExiledPlayer)
            {
                __instance.RpcVotingComplete(states.ToArray(), null, true);
                ExileControllerWrapUpPatch.AntiBlackout_LastExiled = exiledPlayer;
            }
            else __instance.RpcVotingComplete(states.ToArray(), exiledPlayer, tie);

            CheckForDeathOnExile(PlayerState.DeathReason.Vote, exileId);

            Main.LastVotedPlayerInfo = exiledPlayer;
            if (Main.LastVotedPlayerInfo != null)
                ConfirmEjections(Main.LastVotedPlayerInfo, braked);

            if (QuizMaster.On) QuizMaster.Data.NumPlayersVotedLastMeeting = __instance.playerStates.Count(x => x.DidVote);

            return false;
        }
        catch (Exception ex)
        {
            Logger.SendInGame(string.Format(GetString("Error.MeetingException"), ex.Message) /*, true*/);
            throw;
        }
    }

    // Reference：https://github.com/music-discussion/TownOfHost-TheOtherRoles
    private static void ConfirmEjections(NetworkedPlayerInfo exiledPlayer, bool tiebreaker)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (exiledPlayer == null) return;
        var exileId = exiledPlayer.PlayerId;
        if (exileId > 254) return;
        var realName = exiledPlayer.Object.GetRealName(isMeeting: true);

        var player = Utils.GetPlayerById(exiledPlayer.PlayerId);
        var crole = exiledPlayer.GetCustomRole();
        var coloredRole = Utils.GetDisplayRoleName(exileId, true);

        if (crole == CustomRoles.LovingImpostor && !Options.ConfirmLoversOnEject.GetBool())
        {
            coloredRole = (Lovers.LovingImpostorRoleForOtherImps.GetValue() switch
            {
                0 => CustomRoles.ImpostorEHR,
                1 => Lovers.LovingImpostorRole,
                _ => CustomRoles.LovingImpostor
            }).ToColoredString();
        }

        if (Options.ConfirmEgoistOnEject.GetBool() && player.Is(CustomRoles.Egoist)) coloredRole = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Egoist), GetRoleString("Temp.Blank") + coloredRole.RemoveHtmlTags());
        if (Options.ConfirmLoversOnEject.GetBool() && Main.LoversPlayers.Any(x => x.PlayerId == player.PlayerId)) coloredRole = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Lovers), GetRoleString("Temp.Blank") + coloredRole.RemoveHtmlTags());
        if (Options.RascalAppearAsMadmate.GetBool() && player.Is(CustomRoles.Rascal)) coloredRole = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Madmate), GetRoleString("Mad-") + coloredRole.RemoveHtmlTags());

        var name = string.Empty;
        int impnum = 0;
        int neutralnum = 0;

        var DecidedWinner = false;

        if (CustomRoles.Bard.RoleExist())
        {
            Bard.OnMeetingHudDestroy(ref name);
            goto EndOfSession;
        }

        foreach (PlayerControl pc in Main.AllAlivePlayerControls)
        {
            if (pc == exiledPlayer.Object) continue;
            if (pc.GetCustomRole().IsImpostor()) impnum++;
            else if (Options.MadmateCountMode.GetValue() == 1 && (pc.GetCustomRole().IsMadmate() || pc.Is(CustomRoles.Madmate))) impnum++;
            else if (pc.IsNeutralKiller()) neutralnum++;
        }

        string coloredRealName = Utils.ColorString(Main.PlayerColors[player.PlayerId], realName);
        switch (Options.CEMode.GetInt())
        {
            case 0:
                name = string.Format(GetString("PlayerExiled"), coloredRealName);
                break;
            case 1:
                if (player.IsImpostor() || player.Is(CustomRoles.Parasite) || player.Is(CustomRoles.Crewpostor) || player.Is(CustomRoles.Refugee) || player.Is(CustomRoles.Convict))
                    name = string.Format(GetString("BelongTo"), coloredRealName, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Impostor), GetString("TeamImpostor")));
                else if (player.IsCrewmate())
                    name = string.Format(GetString("IsGood"), coloredRealName);
                else if (player.GetCustomRole().IsNeutral() && !player.Is(CustomRoles.Parasite) && !player.Is(CustomRoles.Refugee) && !player.Is(CustomRoles.Crewpostor) && !player.Is(CustomRoles.Convict))
                    name = string.Format(GetString("BelongTo"), coloredRealName, Utils.ColorString(new(255, 171, 27, byte.MaxValue), GetString("TeamNeutral")));
                break;
            case 2:
                name = string.Format(GetString("PlayerIsRole"), coloredRealName, coloredRole);
                if (Options.ShowTeamNextToRoleNameOnEject.GetBool())
                {
                    name += " (";
                    var team = CustomTeamManager.GetCustomTeam(player.PlayerId);
                    if (team != null) name += Utils.ColorString(team.RoleRevealScreenBackgroundColor == "*" || !ColorUtility.TryParseHtmlString(team.RoleRevealScreenBackgroundColor, out var color) ? Color.yellow : color, team.RoleRevealScreenTitle == "*" ? team.TeamName : team.RoleRevealScreenTitle);
                    else
                        name += player.GetTeam() switch
                        {
                            Team.Impostor => Utils.ColorString(new(255, 25, 25, byte.MaxValue), GetString("TeamImpostor")),
                            Team.Neutral => Utils.ColorString(new(255, 171, 27, byte.MaxValue), GetString("TeamNeutral")),
                            Team.Crewmate => Utils.ColorString(new(140, 255, 255, byte.MaxValue), GetString("TeamCrewmate")),
                            _ => "----"
                        };
                    name += ")";
                }

                break;
        }

        if (crole == CustomRoles.Jester)
        {
            name = string.Format(GetString("ExiledJester"), realName, coloredRole);
            DecidedWinner = true;
        }

        if (Executioner.CheckExileTarget(exiledPlayer, true))
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

        if (tiebreaker)
        {
            name += $" ({Utils.ColorString(Utils.GetRoleColor(CustomRoles.Brakar), GetString("Brakar"))})";
        }

        if (DecidedWinner) name += "<size=0>";
        else if (Options.ShowImpRemainOnEject.GetBool())
        {
            name += "\n";
            int actualNeutralnum = neutralnum;
            if (!Options.ShowNKRemainOnEject.GetBool()) neutralnum = 0;
            switch (impnum, neutralnum)
            {
                case (0, 0) when actualNeutralnum == 0: // Crewmates win
                    name += GetString("GG");
                    break;
                case (0, 0) when actualNeutralnum > 0:
                    name += GetString("IWonderWhatsLeft");
                    break;
                case (> 0, > 0): // Both imps and neutrals remain
                    name += impnum switch
                    {
                        1 => $"1 <color=#ff1919>{GetString("RemainingText.ImpSingle")}</color> <color=#777777>&</color> ",
                        2 => $"2 <color=#ff1919>{GetString("RemainingText.ImpPlural")}</color> <color=#777777>&</color> ",
                        3 => $"3 <color=#ff1919>{GetString("RemainingText.ImpPlural")}</color> <color=#777777>&</color> ",
                        _ => string.Empty
                    };
                    if (neutralnum == 1) name += $"1 <color=#ffab1b>{GetString("RemainingText.NKSingle")}</color> <color=#777777>{GetString("RemainingText.EjectionSuffix.NKSingle")}</color>";
                    else name += $"{neutralnum} <color=#ffab1b>{GetString("RemainingText.NKPlural")}</color> <color=#777777>{GetString("RemainingText.EjectionSuffix.NKPlural")}</color>";
                    break;
                case (> 0, 0): // Only imps remain
                    name += impnum switch
                    {
                        1 => GetString("OneImpRemain"),
                        2 => GetString("TwoImpRemain"),
                        3 => GetString("ThreeImpRemain"),
                        _ => string.Empty
                    };
                    break;
                case (0, > 0): // Only neutrals remain
                    if (neutralnum == 1) name += GetString("OneNeutralRemain");
                    else name += string.Format(GetString("NeutralRemain"), neutralnum);
                    break;
            }
        }

        EndOfSession:


        name = name.Replace("color=", string.Empty) + "<size=0>";
        EjectionText = name.Split('\n')[DecidedWinner ? 1 : 0];

        LateTask.New(() =>
        {
            Main.DoBlockNameChange = true;
            if (GameStates.IsInGame && player != null && !player.Data.Disconnected)
            {
                exiledPlayer.UpdateName(name, Utils.GetClientById(exiledPlayer.ClientId));
                player.RpcSetName(name);
            }
        }, 2.5f, "Change Exiled Player Name");
        LateTask.New(() =>
        {
            if (GameStates.IsInGame && player != null && !player.Data.Disconnected)
            {
                exiledPlayer.UpdateName(realName, Utils.GetClientById(exiledPlayer.ClientId));
                player.RpcSetName(realName);
                Main.DoBlockNameChange = false;
            }
        }, 11.5f, "Change Exiled Player Name Back");
    }

    public static bool CheckRole(byte id, CustomRoles role)
    {
        var s = Main.PlayerStates[id];
        return role.IsAdditionRole() ? s.SubRoles.Contains(role) : s.MainRole == role;
    }

    public static void TryAddAfterMeetingDeathPlayers(PlayerState.DeathReason deathReason, params byte[] playerIds)
    {
        var AddedIdList = playerIds.Where(playerId => Main.AfterMeetingDeathPlayers.TryAdd(playerId, deathReason)).ToList();

        CheckForDeathOnExile(deathReason, [.. AddedIdList]);
    }

    private static void CheckForDeathOnExile(PlayerState.DeathReason deathReason, params byte[] playerIds)
    {
        Witch.OnCheckForEndVoting(deathReason, playerIds);
        Virus.OnCheckForEndVoting(deathReason, playerIds);
        foreach (var playerId in playerIds)
        {
            var id = playerId;
            if (CustomRoles.Lovers.IsEnable() && !Main.IsLoversDead && Main.LoversPlayers.Find(lp => lp.PlayerId == id) != null)
                FixedUpdatePatch.LoversSuicide(playerId, true);
            if (Main.PlayerStates.TryGetValue(id, out var state) && state.SubRoles.Contains(CustomRoles.Avanger))
                RevengeOnExile(playerId /*, deathReason*/);
        }
    }

    private static void RevengeOnExile(byte playerId /*, PlayerState.DeathReason deathReason*/)
    {
        var player = Utils.GetPlayerById(playerId);
        if (player == null) return;
        var target = PickRevengeTarget(player /*, deathReason*/);
        if (target == null) return;
        TryAddAfterMeetingDeathPlayers(PlayerState.DeathReason.Revenge, target.PlayerId);
        target.SetRealKiller(player);
        Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags()}の道連れ先:{target.GetNameWithRole().RemoveHtmlTags()}", "RevengeOnExile");
    }

    private static PlayerControl PickRevengeTarget(PlayerControl exiledplayer /*, PlayerState.DeathReason deathReason*/)
    {
        List<PlayerControl> TargetList = [];
        TargetList.AddRange(Main.AllAlivePlayerControls.Where(candidate => candidate != exiledplayer && !Main.AfterMeetingDeathPlayers.ContainsKey(candidate.PlayerId)));

        if (TargetList.Count == 0) return null;
        var target = TargetList.RandomElement();
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
        foreach (var ps in __instance.playerStates)
        {
            if (ps == null) continue;
            if (ps.VotedFor is not 252 and not byte.MaxValue and not 254)
            {
                int VoteNum = 1;

                var target = Utils.GetPlayerById(ps.VotedFor);
                if (target != null)
                {
                    if (target.Is(CustomRoles.Zombie)) VoteNum = 0;
                    if (CheckForEndVotingPatch.CheckRole(ps.TargetPlayerId, CustomRoles.Brakar))
                        if (!Main.BrakarVoteFor.Contains(target.PlayerId))
                            Main.BrakarVoteFor.Add(target.PlayerId);
                    Collector.CollectorVotes(target, ps);
                }

                if (CheckForEndVotingPatch.CheckRole(ps.TargetPlayerId, CustomRoles.Mayor)) VoteNum += Mayor.MayorAdditionalVote.GetInt();
                if (CheckForEndVotingPatch.CheckRole(ps.TargetPlayerId, CustomRoles.Knighted)) VoteNum += 1;
                if (CheckForEndVotingPatch.CheckRole(ps.TargetPlayerId, CustomRoles.Glitch) && !Glitch.CanVote.GetBool()) VoteNum = 0;
                if (CheckForEndVotingPatch.CheckRole(ps.TargetPlayerId, CustomRoles.Vindicator)) VoteNum += Options.VindicatorAdditionalVote.GetInt();
                if (CheckForEndVotingPatch.CheckRole(ps.TargetPlayerId, CustomRoles.DualPersonality) && Options.DualVotes.GetBool()) VoteNum += VoteNum;
                if (CheckForEndVotingPatch.CheckRole(ps.TargetPlayerId, CustomRoles.TicketsStealer)) VoteNum += (int)(Main.AllPlayerControls.Count(x => x.GetRealKiller()?.PlayerId == ps.TargetPlayerId) * Options.TicketsPerKill.GetFloat());
                if (CheckForEndVotingPatch.CheckRole(ps.TargetPlayerId, CustomRoles.Pickpocket)) VoteNum += (int)(Main.AllPlayerControls.Count(x => x.GetRealKiller()?.PlayerId == ps.TargetPlayerId) * Pickpocket.VotesPerKill.GetFloat());
                if (Main.PlayerStates[ps.TargetPlayerId].Role is Adventurer { IsEnable: true } av && av.ActiveWeapons.Contains(Adventurer.Weapon.Proxy)) VoteNum++;
                if (ps.TargetPlayerId == ps.VotedFor && Options.MadmateSpawnMode.GetInt() == 2) VoteNum = 0;

                dic[ps.VotedFor] = !dic.TryGetValue(ps.VotedFor, out int num) ? VoteNum : num + VoteNum;
            }
        }

        return dic;
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
class MeetingHudStartPatch
{
    private static void NotifyRoleSkillOnMeetingStart()
    {
        if (!AmongUsClient.Instance.AmHost) return;

        List<(string MESSAGE, byte TARGET_ID, string TITLE)> msgToSend = [];

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
                foreach (var subRole in Main.PlayerStates[pc.PlayerId].SubRoles)
                {
                    sb.Append("\n\n" + GetString($"{subRole}") + Utils.GetRoleMode(subRole) + GetString($"{subRole}InfoLong"));
                }

                AddMsg(sb.ToString(), pc.PlayerId);
            }
        }

        if (msgToSend.Count > 0)
        {
            LateTask.New(() => { msgToSend.Do(x => Utils.SendMessage(x.MESSAGE, x.TARGET_ID, x.TITLE)); }, 3f, "Skill Description First Meeting");
        }

        if (Options.MadmateSpawnMode.GetInt() == 2 && CustomRoles.Madmate.GetCount() > 0)
            AddMsg(string.Format(GetString("Message.MadmateSelfVoteModeNotify"), GetString("MadmateSpawnMode.SelfVote")));

        if (CustomRoles.God.RoleExist() && Options.NotifyGodAlive.GetBool())
            AddMsg(GetString("GodNoticeAlive"), 255, Utils.ColorString(Utils.GetRoleColor(CustomRoles.God), GetString("GodAliveTitle")));

        if (MeetingStates.FirstMeeting && CustomRoles.Workaholic.RoleExist() && Workaholic.WorkaholicGiveAdviceAlive.GetBool() && !Workaholic.WorkaholicCannotWinAtDeath.GetBool() /* && !Options.GhostIgnoreTasks.GetBool()*/)
        {
            foreach (var pc in Main.AllAlivePlayerControls.Where(x => x.Is(CustomRoles.Workaholic)).ToArray())
                Workaholic.WorkaholicAlive.Add(pc.PlayerId);
            List<string> workaholicAliveList = [];
            workaholicAliveList.AddRange(Workaholic.WorkaholicAlive.Select(whId => Main.AllPlayerNames[whId]));

            string separator = TranslationController.Instance.currentLanguage.languageID is SupportedLangs.English or SupportedLangs.Russian ? "], [" : "】, 【";
            AddMsg(string.Format(GetString("WorkaholicAdviceAlive"), string.Join(separator, workaholicAliveList)), 255, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Workaholic), GetString("WorkaholicAliveTitle")));
        }

        // Bait Notify
        if (MeetingStates.FirstMeeting && CustomRoles.Bait.RoleExist() && Options.BaitNotification.GetBool())
        {
            foreach (var pc in Main.AllAlivePlayerControls.Where(x => x.Is(CustomRoles.Bait)).ToArray())
                Main.BaitAlive.Add(pc.PlayerId);
            List<string> baitAliveList = [];
            baitAliveList.AddRange(Main.BaitAlive.Select(whId => Main.AllPlayerNames[whId]));

            string separator = TranslationController.Instance.currentLanguage.languageID is SupportedLangs.English or SupportedLangs.Russian ? "], [" : "】, 【";
            AddMsg(string.Format(GetString("BaitAdviceAlive"), string.Join(separator, baitAliveList)), 255, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Bait), GetString("BaitAliveTitle")));
        }

        string MimicMsg = string.Empty;
        foreach (var pc in Main.AllPlayerControls)
        {
            if (pc.Is(CustomRoles.Mafia) && !pc.IsAlive())
                AddMsg(GetString("MafiaDeadMsg"), pc.PlayerId);

            foreach (var csId in Main.CyberStarDead)
            {
                if (!Options.ImpKnowCyberStarDead.GetBool() && pc.GetCustomRole().IsImpostor()) continue;
                if (!Options.NeutralKnowCyberStarDead.GetBool() && pc.GetCustomRole().IsNeutral()) continue;
                AddMsg(string.Format(GetString("CyberStarDead"), Main.AllPlayerNames[csId]), pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.CyberStar), GetString("CyberStarNewsTitle")));
            }

            if (pc != null && Silencer.ForSilencer.Contains(pc.PlayerId))
            {
                var playername = pc.GetRealName();
                if (Doppelganger.DoppelVictim.TryGetValue(pc.PlayerId, out string value)) playername = value;
                AddMsg(string.Format(GetString("SilencerDead"), playername, pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Silencer), GetString("SilencerKillTitle"))));
            }

            if (Detective.DetectiveNotify.TryGetValue(pc.PlayerId, out string value1))
                AddMsg(value1, pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Detective), GetString("DetectiveNoticeTitle")));
            if (Main.SleuthMsgs.TryGetValue(pc.PlayerId, out string msg))
                AddMsg(msg, pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Sleuth), GetString("Sleuth")));

            if (pc.Is(CustomRoles.Mimic) && !pc.IsAlive())
            {
                var pc1 = pc;
                Main.AllAlivePlayerControls.Where(x => x.GetRealKiller()?.PlayerId == pc1.PlayerId).Do(x => MimicMsg += $"\n{x.GetNameWithRole(true)}");
            }

            if (Mortician.msgToSend.TryGetValue(pc.PlayerId, out string value2))
                AddMsg(value2, pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Mortician), GetString("MorticianCheckTitle")));
            if (Mediumshiper.ContactPlayer.ContainsValue(pc.PlayerId))
                AddMsg(string.Format(GetString("MediumshipNotifySelf"), Main.AllPlayerNames[Mediumshiper.ContactPlayer.FirstOrDefault(x => x.Value == pc.PlayerId).Key], pc.GetAbilityUseLimit()), pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Mediumshiper), GetString("MediumshipTitle")));
            if (Mediumshiper.ContactPlayer.ContainsKey(pc.PlayerId) && (!Mediumshiper.OnlyReceiveMsgFromCrew.GetBool() || pc.IsCrewmate()))
                AddMsg(string.Format(GetString("MediumshipNotifyTarget"), Main.AllPlayerNames[Mediumshiper.ContactPlayer[pc.PlayerId]]), pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Mediumshiper), GetString("MediumshipTitle")));
            if (Virus.VirusNotify.TryGetValue(pc.PlayerId, out string value3))
                AddMsg(value3, pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Virus), GetString("VirusNoticeTitle")));
            if (Enigma.MsgToSend.TryGetValue(pc.PlayerId, out string value4))
                AddMsg(value4, pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Enigma), Enigma.MsgToSendTitle[pc.PlayerId]));
            if (QuizMaster.On && QuizMaster.MessagesToSend.TryGetValue(pc.PlayerId, out string value5))
            {
                AddMsg(value5, pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.QuizMaster), GetString("QuizMaster.QuestionSample.Title")));
                QuizMaster.QuizMasters.Do(x => AddMsg(value5, x.QuizMasterId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.QuizMaster), GetString("QuizMaster.QuestionSample.Title.Self"))));
            }
        }

        if (MimicMsg != string.Empty)
        {
            MimicMsg = GetString("MimicDeadMsg") + "\n" + MimicMsg;
            foreach (var ipc in Main.AllPlayerControls.Where(x => x.GetCustomRole().IsImpostorTeam()).ToArray())
                AddMsg(MimicMsg, ipc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Mimic), GetString("MimicMsgTitle")));
        }

        if (msgToSend.Count > 0)
        {
            LateTask.New(() => { msgToSend.Do(x => Utils.SendMessage(x.MESSAGE, x.TARGET_ID, x.TITLE)); }, 6f, "Meeting Start Notify");
        }

        Main.CyberStarDead.Clear();
        Express.SpeedNormal.Clear();
        Express.SpeedUp.Clear();
        Detective.DetectiveNotify.Clear();
        Main.SleuthMsgs.Clear();
        Virus.VirusNotify.Clear();
        Mortician.msgToSend.Clear();
        Enigma.MsgToSend.Clear();
        return;

        void AddMsg(string text, byte sendTo = 255, string title = "") => msgToSend.Add((text, sendTo, title));
    }

    public static void Prefix( /*MeetingHud __instance*/)
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

        GuessManager.TextTemplate = Object.Instantiate(__instance.playerStates[0].NameText);
        GuessManager.TextTemplate.enabled = false;

        foreach (var pva in __instance.playerStates)
        {
            var pc = Utils.GetPlayerById(pva.TargetPlayerId);
            if (pc == null) continue;
            var RoleTextData = Utils.GetRoleText(PlayerControl.LocalPlayer.PlayerId, pc.PlayerId);
            var roleTextMeeting = Object.Instantiate(pva.NameText, pva.NameText.transform, true);
            roleTextMeeting.transform.localPosition = new(0f, -0.18f, 0f);
            roleTextMeeting.fontSize = 1.4f;
            roleTextMeeting.text = RoleTextData.Item1;
            if (Main.VisibleTasksCount) roleTextMeeting.text += Utils.GetProgressText(pc);
            roleTextMeeting.color = RoleTextData.Item2;
            roleTextMeeting.gameObject.name = "RoleTextMeeting";
            roleTextMeeting.enableWordWrapping = false;
            roleTextMeeting.enabled =
                pc.AmOwner ||
                (Main.VisibleTasksCount && PlayerControl.LocalPlayer.Data.IsDead && Options.GhostCanSeeOtherRoles.GetBool()) ||
                (PlayerControl.LocalPlayer.Is(CustomRoles.Mimic) && Main.VisibleTasksCount && pc.Data.IsDead && Options.MimicCanSeeDeadRoles.GetBool()) ||
                (pc.Is(CustomRoles.Gravestone) && Main.VisibleTasksCount && pc.Data.IsDead) ||
                (Main.LoversPlayers.TrueForAll(x => x.PlayerId == pc.PlayerId || x.PlayerId == PlayerControl.LocalPlayer.PlayerId) && Main.LoversPlayers.Count == 2 && Lovers.LoverKnowRoles.GetBool()) ||
                (pc.Is(CustomRoleTypes.Impostor) && PlayerControl.LocalPlayer.Is(CustomRoleTypes.Impostor) && Options.ImpKnowAlliesRole.GetBool()) ||
                (pc.Is(CustomRoleTypes.Impostor) && PlayerControl.LocalPlayer.Is(CustomRoles.Madmate) && Options.MadmateKnowWhosImp.GetBool()) ||
                (pc.Is(CustomRoles.Madmate) && PlayerControl.LocalPlayer.Is(CustomRoleTypes.Impostor) && Options.ImpKnowWhosMadmate.GetBool()) ||
                (pc.Is(CustomRoleTypes.Impostor) && PlayerControl.LocalPlayer.Is(CustomRoles.Crewpostor) && Options.AlliesKnowCrewpostor.GetBool()) ||
                (pc.Is(CustomRoles.Crewpostor) && PlayerControl.LocalPlayer.Is(CustomRoleTypes.Impostor) && Options.CrewpostorKnowsAllies.GetBool()) ||
                (pc.Is(CustomRoles.Madmate) && PlayerControl.LocalPlayer.Is(CustomRoles.Madmate) && Options.MadmateKnowWhosMadmate.GetBool()) ||
                ((pc.Is(CustomRoles.Jackal) || pc.Is(CustomRoles.Sidekick) || pc.Is(CustomRoles.Recruit)) && (PlayerControl.LocalPlayer.Is(CustomRoles.Sidekick) || PlayerControl.LocalPlayer.Is(CustomRoles.Recruit) || PlayerControl.LocalPlayer.Is(CustomRoles.Jackal))) ||
                (pc.Is(CustomRoles.Workaholic) && Workaholic.WorkaholicVisibleToEveryone.GetBool()) ||
                (pc.Is(CustomRoles.Doctor) && !pc.HasEvilAddon() && Options.DoctorVisibleToEveryone.GetBool()) ||
                (pc.Is(CustomRoles.Mayor) && Mayor.MayorRevealWhenDoneTasks.GetBool() && pc.GetTaskState().IsTaskFinished) ||
                (pc.Is(CustomRoles.Marshall) && PlayerControl.LocalPlayer.Is(CustomRoleTypes.Crewmate) && pc.GetTaskState().IsTaskFinished) ||
                (Main.PlayerStates[pc.PlayerId].deathReason == PlayerState.DeathReason.Vote && Options.SeeEjectedRolesInMeeting.GetBool()) ||
                CustomTeamManager.AreInSameCustomTeam(pc.PlayerId, PlayerControl.LocalPlayer.PlayerId) && CustomTeamManager.IsSettingEnabledForPlayerTeam(pc.PlayerId, CTAOption.KnowRoles) ||
                Main.PlayerStates.Values.Any(x => x.Role.KnowRole(PlayerControl.LocalPlayer, pc)) ||
                Markseeker.PlayerIdList.Any(x => Main.PlayerStates[x].Role is Markseeker { IsEnable: true, TargetRevealed: true } ms && ms.MarkedId == pc.PlayerId) ||
                PlayerControl.LocalPlayer.IsRevealedPlayer(pc) ||
                PlayerControl.LocalPlayer.Is(CustomRoles.God) ||
                PlayerControl.LocalPlayer.Is(CustomRoles.GM) ||
                Main.GodMode.Value;


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

            if (Scout.IsTrackTarget(PlayerControl.LocalPlayer, pc) && Scout.CanSeeLastRoomInMeeting)
            {
                roleTextMeeting.text = Scout.GetArrowAndLastRoom(PlayerControl.LocalPlayer, pc);
                roleTextMeeting.enabled = true;
            }
        }

        if (Options.SyncButtonMode.GetBool())
        {
            Utils.SendMessage(string.Format(GetString("Message.SyncButtonLeft"), Options.SyncedButtonCount.GetFloat() - Options.UsedButtonCount));
            Logger.Info("The ship has " + (Options.SyncedButtonCount.GetFloat() - Options.UsedButtonCount) + " buttons left", "SyncButtonMode");
        }

        if (AntiBlackout.OverrideExiledPlayer && MeetingStates.FirstMeeting)
        {
            LateTask.New(() => { Utils.SendMessage(GetString("Warning.OverrideExiledPlayer"), 255, Utils.ColorString(Color.red, GetString("DefaultSystemMessageTitle"))); }, 5f, "Warning OverrideExiledPlayer");
        }

        TemplateManager.SendTemplate("OnMeeting", noErr: true);
        if (MeetingStates.FirstMeeting) TemplateManager.SendTemplate("OnFirstMeeting", noErr: true);

        if (AmongUsClient.Instance.AmHost)
            NotifyRoleSkillOnMeetingStart();

        if (AmongUsClient.Instance.AmHost)
        {
            LateTask.New(() =>
            {
                foreach (PlayerControl pc in Main.AllPlayerControls)
                {
                    pc.RpcSetNameEx(pc.GetRealName(isMeeting: true));
                }

                ChatUpdatePatch.DoBlockChat = false;
            }, 3f, "SetName To Chat");
        }

        foreach (var pva in __instance.playerStates)
        {
            if (pva == null) continue;
            PlayerControl seer = PlayerControl.LocalPlayer;
            PlayerControl target = Utils.GetPlayerById(pva.TargetPlayerId);
            if (target == null) continue;

            if (seer.GetCustomRole().GetDYRole() is RoleTypes.Shapeshifter or RoleTypes.Phantom)
            {
                target.cosmetics.SetNameColor(Color.white);
                pva.NameText.color = Color.white;
            }

            var sb = new StringBuilder();

            // Name Color Manager
            pva.NameText.text = pva.NameText.text.ApplyNameColorData(seer, target, true);

            var seerRole = seer.GetCustomRole();

            if (seer.KnowDeathReason(target))
                sb.Append($"({Utils.ColorString(Utils.GetRoleColor(CustomRoles.Doctor), Utils.GetVitalText(target.PlayerId))})");

            switch (seer.GetCustomRoleTypes())
            {
                case CustomRoleTypes.Impostor:
                    if (target.Is(CustomRoles.Snitch) && target.Is(CustomRoles.Madmate) && target.GetTaskState().IsTaskFinished)
                        sb.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Impostor), "★"));
                    break;
                case CustomRoleTypes.Crewmate:
                    if (target.Is(CustomRoles.Marshall) && target.GetTaskState().IsTaskFinished)
                        sb.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Marshall), "★"));
                    sb.Append(Marshall.GetWarningMark(seer, target));
                    break;
            }

            if (seer.IsSnitchTarget())
            {
                sb.Append(Snitch.GetWarningMark(seer, target));
            }

            switch (seerRole)
            {
                case CustomRoles.PlagueBearer when PlagueBearer.IsPlagued(seer.PlayerId, target.PlayerId):
                    sb.Append($"<color={Utils.GetRoleColorCode(CustomRoles.PlagueBearer)}>●</color>");
                    break;
                case CustomRoles.Arsonist:
                    if (seer.IsDousedPlayer(target))
                        sb.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Arsonist), "▲"));
                    break;
                case CustomRoles.Executioner:
                    sb.Append(Executioner.TargetMark(seer, target));
                    break;
                case CustomRoles.EvilTracker:
                    sb.Append(EvilTracker.GetTargetMark(seer, target));
                    break;
                case CustomRoles.Revolutionist:
                    if (seer.IsDrawPlayer(target))
                        sb.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Revolutionist), "●"));
                    break;
                case CustomRoles.Psychic:
                    if (Psychic.IsRedForPsy(target, seer) && !seer.Data.IsDead)
                        pva.NameText.text = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Impostor), pva.NameText.text);
                    break;
                case CustomRoles.Gamer:
                    sb.Append(Gamer.TargetMark(seer, target));
                    sb.Append(Snitch.GetWarningMark(seer, target));
                    break;
                case CustomRoles.Scout:
                    sb.Append(Scout.GetTargetMark(seer, target));
                    break;
            }

            if (Silencer.ForSilencer.Contains(target.PlayerId))
                sb.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Silencer), "╳"));

            if (Main.LoversPlayers.Any(x => x.PlayerId == target.PlayerId) && (Main.LoversPlayers.Any(x => x.PlayerId == seer.PlayerId) || seer.Data.IsDead))
            {
                sb.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Lovers), "♥"));
            }

            sb.Append(Witch.GetSpelledMark(target.PlayerId, true));

            if (target.Is(CustomRoles.SuperStar) && Options.EveryOneKnowSuperStar.GetBool())
                sb.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.SuperStar), "★"));

            if (BallLightning.IsGhost(target))
                sb.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.BallLightning), "■"));

            if (seer.PlayerId == target.PlayerId && (Medic.InProtect(seer.PlayerId) || Medic.TempMarkProtectedList.Contains(seer.PlayerId)) && (Medic.WhoCanSeeProtect.GetInt() is 0 or 2))
                sb.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Medic), " ●"));

            if (seer.Is(CustomRoles.Medic) && (Medic.InProtect(target.PlayerId) || Medic.TempMarkProtectedList.Contains(target.PlayerId)) && (Medic.WhoCanSeeProtect.GetInt() is 0 or 1))
                sb.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Medic), " ●"));

            if (seer.Data.IsDead && Medic.InProtect(target.PlayerId) && !seer.Is(CustomRoles.Medic))
                sb.Append(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Medic), " ●"));

            sb.Append(Totocalcio.TargetMark(seer, target));
            sb.Append(Romantic.TargetMark(seer, target));
            sb.Append(Lawyer.LawyerMark(seer, target));
            sb.Append(PlagueDoctor.GetMarkOthers(seer, target));

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
        => __instance.playerStates.ToList().ForEach(x =>
        {
            if ((forceAll || !Main.PlayerStates.TryGetValue(x.TargetPlayerId, out var ps) || ps.IsDead) && x.transform.FindChild("ShootButton") != null) Object.Destroy(x.transform.FindChild("ShootButton").gameObject);
        });

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

            if (DestroyableSingleton<HudManager>.Instance.Chat.IsOpenOrOpening)
            {
                GuessManager.DestroyIDLabels();
            }

            if (!GameStates.IsVoting && __instance.lastSecond < 1)
            {
                if (GameObject.Find("ShootButton") != null) ClearShootButton(__instance, true);
                return;
            }

            bufferTime--;
            if (bufferTime < 0 && __instance.discussionTimer > 0)
            {
                bufferTime = 10;
                var myRole = PlayerControl.LocalPlayer.GetCustomRole();

                __instance.playerStates.Where(x => (!Main.PlayerStates.TryGetValue(x.TargetPlayerId, out var ps) || ps.IsDead) && !x.AmDead).Do(x => x.SetDead(x.DidReport, true));

                switch (myRole)
                {
                    case CustomRoles.NiceGuesser or CustomRoles.EvilGuesser or CustomRoles.Judge or CustomRoles.NiceSwapper or CustomRoles.Councillor or CustomRoles.Guesser when !PlayerControl.LocalPlayer.IsAlive():
                        ClearShootButton(__instance, true);
                        break;
                    case CustomRoles.Mafia when !PlayerControl.LocalPlayer.IsAlive() && GameObject.Find("ShootButton") == null:
                        Mafia.CreateJudgeButton(__instance);
                        break;
                }

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
        }
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.CastVote))]
class MeetingHudCastVotePatch
{
    private static readonly Dictionary<byte, (MeetingHud MEETING_HUD, PlayerVoteArea SOURCE_PLAYER_VOTE_AREA, PlayerControl SOURCE_PLAYER)> ShouldCancelVoteList = [];

    public static bool Prefix(MeetingHud __instance, [HarmonyArgument(0)] byte srcPlayerId, [HarmonyArgument(1)] byte suspectPlayerId)
    {
        PlayerVoteArea pva_src = null;
        PlayerVoteArea pva_target = null;
        bool isSkip = false;
        foreach (var t in __instance.playerStates)
        {
            if (t.TargetPlayerId == srcPlayerId) pva_src = t;
            if (t.TargetPlayerId == suspectPlayerId) pva_target = t;
        }

        if (pva_src == null)
        {
            Logger.Error("Src PlayerVoteArea not found", "MeetingHudCastVotePatch.Prefix");
            return true;
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
            return true;
        }

        if (pc_target == null)
        {
            //Logger.Warn("Target PlayerControl is null => Vote treated as a Skip", "MeetingHudCastVotePatch.Prefix");
            isSkip = true;
        }

        bool isVoteCanceled = false;

        if (!Main.DontCancelVoteList.Contains(srcPlayerId) && !isSkip && pc_src.GetCustomRole().CancelsVote() && Main.PlayerStates[srcPlayerId].Role.OnVote(pc_src, pc_target))
        {
            ShouldCancelVoteList.TryAdd(srcPlayerId, (__instance, pva_src, pc_src));
            isVoteCanceled = true;
        }

        Logger.Info($"{pc_src.GetNameWithRole().RemoveHtmlTags()} => {(isSkip ? "Skip" : pc_target.GetNameWithRole().RemoveHtmlTags())}{(isVoteCanceled ? " (Canceled)" : string.Empty)}", "Vote");

        return isSkip || !isVoteCanceled; // return false to use the vote as a trigger; skips and invalid votes are never canceled
    }

    public static void Postfix([HarmonyArgument(0)] byte srcPlayerId)
    {
        if (!ShouldCancelVoteList.TryGetValue(srcPlayerId, out var info)) return;

        MeetingHud __instance = info.MEETING_HUD;
        PlayerVoteArea pva_src = info.SOURCE_PLAYER_VOTE_AREA;
        PlayerControl pc_src = info.SOURCE_PLAYER;

        try
        {
            pva_src.UnsetVote();
        }
        catch
        {
        }

        __instance.RpcClearVote(pc_src.GetClientId());

        ShouldCancelVoteList.Remove(srcPlayerId);
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.CmdCastVote))]
class MeetingHudCmdCastVotePatch
{
    public static bool Prefix(MeetingHud __instance, [HarmonyArgument(0)] byte srcPlayerId, [HarmonyArgument(1)] byte suspectPlayerId)
    {
        return MeetingHudCastVotePatch.Prefix(__instance, srcPlayerId, suspectPlayerId);
    }

    public static void Postfix([HarmonyArgument(0)] byte srcPlayerId)
    {
        MeetingHudCastVotePatch.Postfix(srcPlayerId);
    }
}