using AmongUs.GameOptions;
using HarmonyLib;
using Hazel;
using System.Collections.Generic;
using System.Linq;
using TOHE.Roles.Neutral;
using static TOHE.Translator;

namespace TOHE;

[HarmonyPatch(typeof(LogicGameFlowNormal), nameof(LogicGameFlowNormal.CheckEndCriteria))]
class GameEndChecker
{
    private static GameEndPredicate predicate;
    public static bool Prefix()
    {
        if (!AmongUsClient.Instance.AmHost) return true;

        //ゲーム終了判定済みなら中断
        if (predicate == null) return false;

        //ゲーム終了しないモードで廃村以外の場合は中断
        if (Options.NoGameEnd.GetBool() && CustomWinnerHolder.WinnerTeam is not CustomWinner.Draw and not CustomWinner.Error) return false;

        //廃村用に初期値を設定
        var reason = GameOverReason.ImpostorByKill;

        //ゲーム終了判定
        predicate.CheckForEndGame(out reason);

        // SoloKombat
        if (Options.CurrentGameMode == CustomGameMode.SoloKombat || Options.CurrentGameMode == CustomGameMode.FFA)
        {
            if (CustomWinnerHolder.WinnerIds.Any() || CustomWinnerHolder.WinnerTeam != CustomWinner.Default)
            {
                ShipStatus.Instance.enabled = false;
                StartEndGame(reason);
                predicate = null;
            }
            return false;
        }

        //ゲーム終了時
        if (CustomWinnerHolder.WinnerTeam != CustomWinner.Default)
        {
            //カモフラージュ強制解除
            Main.AllPlayerControls.Do(pc => Camouflage.RpcSetSkin(pc, ForceRevert: true, RevertToDefault: true));

            if (reason == GameOverReason.ImpostorBySabotage && (CustomRoles.Jackal.RoleExist() || CustomRoles.Sidekick.RoleExist()) && Jackal.CanWinBySabotageWhenNoImpAlive.GetBool() && !Main.AllAlivePlayerControls.Any(x => x.GetCustomRole().IsImpostorTeam()))
            {
                reason = GameOverReason.ImpostorByKill;
                CustomWinnerHolder.WinnerIds.Clear();
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Jackal);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Jackal);
            }

            switch (CustomWinnerHolder.WinnerTeam)
            {
                case CustomWinner.Crewmate:
                    Main.AllPlayerControls
                        .Where(pc => pc.Is(CustomRoleTypes.Crewmate) && !pc.Is(CustomRoles.Lovers) && !pc.Is(CustomRoles.Madmate) && !pc.Is(CustomRoles.Rogue) && !pc.Is(CustomRoles.Charmed) && !pc.Is(CustomRoles.Recruit) && !pc.Is(CustomRoles.Infected) && !pc.Is(CustomRoles.Contagious) && !pc.Is(CustomRoles.EvilSpirit) && !pc.Is(CustomRoles.Recruit) || pc.Is(CustomRoles.Admired))
                        .Do(pc => CustomWinnerHolder.WinnerIds.Add(pc.PlayerId));
                    break;
                case CustomWinner.Impostor:
                    Main.AllPlayerControls
                        .Where(pc => ((pc.Is(CustomRoleTypes.Impostor) && (!pc.Is(CustomRoles.DeadlyQuota) || Main.PlayerStates.Count(x => x.Value.GetRealKiller() == pc.PlayerId) >= Options.DQNumOfKillsNeeded.GetInt())) || pc.Is(CustomRoles.Madmate)) && !pc.Is(CustomRoles.Lovers) && !pc.Is(CustomRoles.Rogue) && !pc.Is(CustomRoles.Charmed) && !pc.Is(CustomRoles.Recruit) && !pc.Is(CustomRoles.Infected) && !pc.Is(CustomRoles.Contagious) && !pc.Is(CustomRoles.EvilSpirit) && !pc.Is(CustomRoles.Recruit) && !pc.Is(CustomRoles.Admired))
                        .Do(pc => CustomWinnerHolder.WinnerIds.Add(pc.PlayerId));
                    break;
                case CustomWinner.Succubus:
                    Main.AllPlayerControls
                        .Where(pc => pc.Is(CustomRoles.Succubus) || pc.Is(CustomRoles.Charmed) && !pc.Is(CustomRoles.Rogue) && !pc.Is(CustomRoles.Admired))
                        .Do(pc => CustomWinnerHolder.WinnerIds.Add(pc.PlayerId));
                    break;
                case CustomWinner.CursedSoul:
                    Main.AllPlayerControls
                        .Where(pc => pc.Is(CustomRoles.CursedSoul) || pc.Is(CustomRoles.Soulless) && !pc.Is(CustomRoles.Rogue) && !pc.Is(CustomRoles.Admired))
                        .Do(pc => CustomWinnerHolder.WinnerIds.Add(pc.PlayerId));
                    break;
                case CustomWinner.Infectious:
                    Main.AllPlayerControls
                        .Where(pc => pc.Is(CustomRoles.Infectious) || pc.Is(CustomRoles.Infected) && !pc.Is(CustomRoles.Rogue) && !pc.Is(CustomRoles.Admired))
                        .Do(pc => CustomWinnerHolder.WinnerIds.Add(pc.PlayerId));
                    break;
                case CustomWinner.Virus:
                    Main.AllPlayerControls
                        .Where(pc => pc.Is(CustomRoles.Virus) || pc.Is(CustomRoles.Contagious) && !pc.Is(CustomRoles.Rogue) && !pc.Is(CustomRoles.Admired))
                        .Do(pc => CustomWinnerHolder.WinnerIds.Add(pc.PlayerId));
                    break;
                case CustomWinner.Jackal:
                    Main.AllPlayerControls
                        .Where(pc => (pc.Is(CustomRoles.Jackal) || pc.Is(CustomRoles.Sidekick) || pc.Is(CustomRoles.Recruit)) && !pc.Is(CustomRoles.Lovers) && !pc.Is(CustomRoles.Infected) && !pc.Is(CustomRoles.Rogue) && !pc.Is(CustomRoles.Admired))
                        .Do(pc => CustomWinnerHolder.WinnerIds.Add(pc.PlayerId));
                    break;
                case CustomWinner.Spiritcaller:
                    Main.AllPlayerControls
                        .Where(pc => pc.Is(CustomRoles.Spiritcaller) || pc.Is(CustomRoles.EvilSpirit))
                        .Do(pc => CustomWinnerHolder.WinnerIds.Add(pc.PlayerId));
                    break;
                case CustomWinner.RuthlessRomantic:
                    foreach (var pc in Main.AllPlayerControls.Where(pc => pc.Is(CustomRoles.RuthlessRomantic)))
                    {
                        CustomWinnerHolder.WinnerIds.Add(Romantic.BetPlayer[pc.PlayerId]);
                    }
                    //Main.AllPlayerControls
                    //    .Where(pc => (pc.Is(CustomRoles.RuthlessRomantic) || (Romantic.BetPlayer.TryGetValue(pc.PlayerId, out var RomanticPartner)) && pc.PlayerId == RomanticPartner))
                    //    .Do(pc => CustomWinnerHolder.WinnerIds.Add(pc.PlayerId));
                    break;
            }
            if (CustomWinnerHolder.WinnerTeam is not CustomWinner.Draw and not CustomWinner.None and not CustomWinner.Error)
            {
                foreach (var pc in Main.AllPlayerControls)
                {
                    switch (pc.GetCustomRole())
                    {
                        case CustomRoles.DarkHide when !pc.Data.IsDead && ((CustomWinnerHolder.WinnerTeam == CustomWinner.Impostor && !reason.Equals(GameOverReason.ImpostorBySabotage)) || CustomWinnerHolder.WinnerTeam == CustomWinner.DarkHide || (CustomWinnerHolder.WinnerTeam == CustomWinner.Crewmate && !reason.Equals(GameOverReason.HumansByTask) && DarkHide.IsWinKill[pc.PlayerId] == true && DarkHide.SnatchesWin.GetBool())):
                            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.DarkHide);
                            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                            break;
                        case CustomRoles.Phantom when pc.GetPlayerTaskState().IsTaskFinished && pc.Data.IsDead && (CustomWinnerHolder.WinnerTeam == CustomWinner.Impostor || CustomWinnerHolder.WinnerTeam == CustomWinner.Crewmate || CustomWinnerHolder.WinnerTeam == CustomWinner.Jackal || CustomWinnerHolder.WinnerTeam == CustomWinner.BloodKnight || CustomWinnerHolder.WinnerTeam == CustomWinner.SerialKiller || CustomWinnerHolder.WinnerTeam == CustomWinner.Juggernaut || CustomWinnerHolder.WinnerTeam == CustomWinner.Ritualist || CustomWinnerHolder.WinnerTeam == CustomWinner.Poisoner || CustomWinnerHolder.WinnerTeam == CustomWinner.Succubus || CustomWinnerHolder.WinnerTeam == CustomWinner.Infectious || CustomWinnerHolder.WinnerTeam == CustomWinner.Jinx || CustomWinnerHolder.WinnerTeam == CustomWinner.Virus || CustomWinnerHolder.WinnerTeam == CustomWinner.Arsonist || CustomWinnerHolder.WinnerTeam == CustomWinner.Pelican || CustomWinnerHolder.WinnerTeam == CustomWinner.HexMaster || CustomWinnerHolder.WinnerTeam == CustomWinner.Wraith || CustomWinnerHolder.WinnerTeam == CustomWinner.Pestilence || CustomWinnerHolder.WinnerTeam == CustomWinner.Rogue || CustomWinnerHolder.WinnerTeam == CustomWinner.Spiritcaller) && Options.PhantomSnatchesWin.GetBool():
                            reason = GameOverReason.ImpostorByKill;
                            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Phantom);
                            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                            break;
                        case CustomRoles.CursedSoul when !pc.Data.IsDead && (CustomWinnerHolder.WinnerTeam == CustomWinner.Impostor || CustomWinnerHolder.WinnerTeam == CustomWinner.Crewmate || CustomWinnerHolder.WinnerTeam == CustomWinner.Jackal || CustomWinnerHolder.WinnerTeam == CustomWinner.BloodKnight || CustomWinnerHolder.WinnerTeam == CustomWinner.SerialKiller || CustomWinnerHolder.WinnerTeam == CustomWinner.Juggernaut || CustomWinnerHolder.WinnerTeam == CustomWinner.Ritualist || CustomWinnerHolder.WinnerTeam == CustomWinner.Poisoner || CustomWinnerHolder.WinnerTeam == CustomWinner.Succubus || CustomWinnerHolder.WinnerTeam == CustomWinner.Infectious || CustomWinnerHolder.WinnerTeam == CustomWinner.Jinx || CustomWinnerHolder.WinnerTeam == CustomWinner.Virus || CustomWinnerHolder.WinnerTeam == CustomWinner.Arsonist || CustomWinnerHolder.WinnerTeam == CustomWinner.Pelican || CustomWinnerHolder.WinnerTeam == CustomWinner.HexMaster || CustomWinnerHolder.WinnerTeam == CustomWinner.Wraith || CustomWinnerHolder.WinnerTeam == CustomWinner.Pestilence || CustomWinnerHolder.WinnerTeam == CustomWinner.Rogue || CustomWinnerHolder.WinnerTeam == CustomWinner.Jester || CustomWinnerHolder.WinnerTeam == CustomWinner.Executioner):
                            reason = GameOverReason.ImpostorByKill;
                            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.CursedSoul);
                            CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Soulless);
                            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                            break;
                        case CustomRoles.Opportunist when pc.IsAlive():
                            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                            CustomWinnerHolder.AdditionalWinnerTeams.Add(AdditionalWinners.Opportunist);
                            break;
                        case CustomRoles.Pursuer when pc.IsAlive() && CustomWinnerHolder.WinnerTeam != CustomWinner.Jester && CustomWinnerHolder.WinnerTeam != CustomWinner.Lovers && CustomWinnerHolder.WinnerTeam != CustomWinner.Terrorist && CustomWinnerHolder.WinnerTeam != CustomWinner.Executioner && CustomWinnerHolder.WinnerTeam != CustomWinner.Collector && CustomWinnerHolder.WinnerTeam != CustomWinner.Innocent && CustomWinnerHolder.WinnerTeam != CustomWinner.Youtuber:
                            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                            CustomWinnerHolder.AdditionalWinnerTeams.Add(AdditionalWinners.Pursuer);
                            break;
                        case CustomRoles.Sunnyboy when !pc.IsAlive():
                            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                            CustomWinnerHolder.AdditionalWinnerTeams.Add(AdditionalWinners.Sunnyboy);
                            break;
                        case CustomRoles.Maverick when pc.IsAlive():
                            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                            CustomWinnerHolder.AdditionalWinnerTeams.Add(AdditionalWinners.Maverick);
                            break;
                        case CustomRoles.Provocateur when Main.Provoked.TryGetValue(pc.PlayerId, out var tar) && !CustomWinnerHolder.WinnerIds.Contains(tar):
                            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                            CustomWinnerHolder.AdditionalWinnerTeams.Add(AdditionalWinners.Provocateur);
                            break;
                        case CustomRoles.FFF when CustomWinnerHolder.WinnerTeam != CustomWinner.Lovers && !CustomWinnerHolder.AdditionalWinnerTeams.Contains(AdditionalWinners.Lovers) && !CustomRolesHelper.RoleExist(CustomRoles.Lovers) && Main.AllPlayerControls.Where(x => x.Is(CustomRoles.Lovers)/* || x.Is(CustomRoles.Ntr)*/ && x.GetRealKiller()?.PlayerId == pc.PlayerId).Any():
                            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                            CustomWinnerHolder.AdditionalWinnerTeams.Add(AdditionalWinners.FFF);
                            break;
                        case CustomRoles.Totocalcio when Totocalcio.BetPlayer.TryGetValue(pc.PlayerId, out var betTarget) && (CustomWinnerHolder.WinnerIds.Contains(betTarget) || (Main.PlayerStates.TryGetValue(betTarget, out var ps) && CustomWinnerHolder.WinnerRoles.Contains(ps.MainRole))):
                            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                            CustomWinnerHolder.AdditionalWinnerTeams.Add(AdditionalWinners.Totocalcio);
                            break;
                        case CustomRoles.Romantic when Romantic.BetPlayer.TryGetValue(pc.PlayerId, out var betTarget) && (CustomWinnerHolder.WinnerIds.Contains(betTarget) || (Main.PlayerStates.TryGetValue(betTarget, out var ps) && CustomWinnerHolder.WinnerRoles.Contains(ps.MainRole))):
                            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                            CustomWinnerHolder.AdditionalWinnerTeams.Add(AdditionalWinners.Romantic);
                            break;
                        case CustomRoles.VengefulRomantic when VengefulRomantic.hasKilledKiller:
                            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                            CustomWinnerHolder.WinnerIds.Add(Romantic.BetPlayer[pc.PlayerId]);
                            CustomWinnerHolder.AdditionalWinnerTeams.Add(AdditionalWinners.VengefulRomantic);
                            break;
                        case CustomRoles.Lawyer when Lawyer.Target.TryGetValue(pc.PlayerId, out var lawyertarget) && (CustomWinnerHolder.WinnerIds.Contains(lawyertarget) || (Main.PlayerStates.TryGetValue(lawyertarget, out var ps) && CustomWinnerHolder.WinnerRoles.Contains(ps.MainRole))):
                            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                            CustomWinnerHolder.AdditionalWinnerTeams.Add(AdditionalWinners.Lawyer);
                            break;
                        case CustomRoles.Postman when Postman.IsFinished:
                            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                            CustomWinnerHolder.AdditionalWinnerTeams.Add(AdditionalWinners.Postman);
                            break;
                        default:
                            if (!Options.PhantomSnatchesWin.GetBool() && pc.GetCustomRole() == CustomRoles.Phantom && !pc.IsAlive() && pc.GetPlayerTaskState().IsTaskFinished)
                            {
                                CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                                CustomWinnerHolder.AdditionalWinnerTeams.Add(AdditionalWinners.Phantom);
                            }
                            break;
                    }
                }

                //利己主义者抢夺胜利（船员）
                if (CustomWinnerHolder.WinnerTeam == CustomWinner.Crewmate)
                {
                    foreach (var pc in Main.AllAlivePlayerControls.Where(x => x.GetCustomRole().IsCrewmate()))
                        if (pc.Is(CustomRoles.Egoist))
                        {
                            reason = GameOverReason.ImpostorByKill;
                            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Egoist);
                            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                        }
                }

                //利己主义者抢夺胜利（内鬼）
                if (CustomWinnerHolder.WinnerTeam == CustomWinner.Impostor)
                {
                    foreach (var pc in Main.AllAlivePlayerControls.Where(x => x.GetCustomRole().IsImpostor()))
                        if (pc.Is(CustomRoles.Egoist))
                        {
                            reason = GameOverReason.ImpostorByKill;
                            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Egoist);
                            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                        }
                }

                //神抢夺胜利
                if (CustomRolesHelper.RoleExist(CustomRoles.God))
                {
                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.God);
                    Main.AllPlayerControls
                        .Where(p => p.Is(CustomRoles.God) && p.IsAlive())
                        .Do(p => CustomWinnerHolder.WinnerIds.Add(p.PlayerId));
                }

                //恋人抢夺胜利
                else if (CustomRolesHelper.RoleExist(CustomRoles.Lovers) && !reason.Equals(GameOverReason.HumansByTask) && !(!Main.LoversPlayers.ToArray().All(p => p.IsAlive()) && Options.LoverSuicide.GetBool()) && CustomWinnerHolder.WinnerTeam is CustomWinner.Crewmate or CustomWinner.Impostor or CustomWinner.Jackal or CustomWinner.Pelican)
                {
                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Lovers);
                    Main.AllPlayerControls
                        .Where(p => p.Is(CustomRoles.Lovers))
                        .Do(p => CustomWinnerHolder.WinnerIds.Add(p.PlayerId));
                }

                //Lovers follow winner
                if (CustomWinnerHolder.WinnerTeam is not CustomWinner.Lovers and not CustomWinner.Crewmate and not CustomWinner.Impostor)
                {
                    foreach (var pc in Main.AllPlayerControls.Where(x => x.Is(CustomRoles.Lovers)))
                    {
                        if (CustomWinnerHolder.WinnerIds.Where(x => Utils.GetPlayerById(x).Is(CustomRoles.Lovers)).Any())
                        {
                            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                            CustomWinnerHolder.AdditionalWinnerTeams.Add(AdditionalWinners.Lovers);
                        }
                    }
                }


                //Ruthless Romantic partner win condition
                if (RuthlessRomantic.IsEnable && (CustomWinnerHolder.WinnerIds.Contains(RuthlessRomantic.playerIdList[0]) || (Main.PlayerStates.TryGetValue(RuthlessRomantic.playerIdList[0], out var pz) && CustomWinnerHolder.WinnerRoles.Contains(pz.MainRole))))
                    foreach (var pc in Main.AllPlayerControls)
                    {
                        if (Romantic.BetPlayer.TryGetValue(pc.PlayerId, out var betTarget))
                        {
                            CustomWinnerHolder.WinnerIds.Add(betTarget);
                        }
                    }

                //补充恋人胜利名单
                if (CustomWinnerHolder.WinnerTeam == CustomWinner.Lovers || CustomWinnerHolder.AdditionalWinnerTeams.Contains(AdditionalWinners.Lovers))
                {
                    Main.AllPlayerControls.Where(p => p.Is(CustomRoles.Lovers) && !CustomWinnerHolder.WinnerIds.Contains(p.PlayerId))
                                          .Do(p => CustomWinnerHolder.WinnerIds.Add(p.PlayerId));
                }


                //中立共同胜利
                if (Options.NeutralWinTogether.GetBool() && CustomWinnerHolder.WinnerIds.Where(x => Utils.GetPlayerById(x) != null && Utils.GetPlayerById(x).GetCustomRole().IsNeutral()).Any())
                {
                    foreach (var pc in Main.AllPlayerControls)
                        if (pc.GetCustomRole().IsNeutral() && !CustomWinnerHolder.WinnerIds.Contains(pc.PlayerId))
                            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                }
                else if (Options.NeutralRoleWinTogether.GetBool())
                {
                    foreach (var id in CustomWinnerHolder.WinnerIds)
                    {
                        var pc = Utils.GetPlayerById(id);
                        if (pc == null || !pc.GetCustomRole().IsNeutral()) continue;
                        foreach (var tar in Main.AllPlayerControls)
                            if (!CustomWinnerHolder.WinnerIds.Contains(tar.PlayerId) && tar.GetCustomRole() == pc.GetCustomRole())
                                CustomWinnerHolder.WinnerIds.Add(tar.PlayerId);
                    }
                }
            }
            ShipStatus.Instance.enabled = false;
            StartEndGame(reason);
            predicate = null;
        }
        return false;
    }
    public static void StartEndGame(GameOverReason reason)
    {
        var sender = new CustomRpcSender("EndGameSender", SendOption.Reliable, true);
        sender.StartMessage(-1); // 5: GameData
        MessageWriter writer = sender.stream;

        //ゴーストロール化
        List<byte> ReviveRequiredPlayerIds = new();
        var winner = CustomWinnerHolder.WinnerTeam;
        foreach (var pc in Main.AllPlayerControls)
        {
            if (winner == CustomWinner.Draw)
            {
                SetGhostRole(ToGhostImpostor: true);
                continue;
            }
            bool canWin = CustomWinnerHolder.WinnerIds.Contains(pc.PlayerId) ||
                    CustomWinnerHolder.WinnerRoles.Contains(pc.GetCustomRole());
            bool isCrewmateWin = reason.Equals(GameOverReason.HumansByVote) || reason.Equals(GameOverReason.HumansByTask);
            SetGhostRole(ToGhostImpostor: canWin ^ isCrewmateWin);

            void SetGhostRole(bool ToGhostImpostor)
            {
                if (!pc.Data.IsDead) ReviveRequiredPlayerIds.Add(pc.PlayerId);
                if (ToGhostImpostor)
                {
                    Logger.Info($"{pc.GetNameWithRole().RemoveHtmlTags()}: ImpostorGhostに変更", "ResetRoleAndEndGame");
                    sender.StartRpc(pc.NetId, RpcCalls.SetRole)
                        .Write((ushort)RoleTypes.ImpostorGhost)
                        .EndRpc();
                    pc.SetRole(RoleTypes.ImpostorGhost);
                }
                else
                {
                    Logger.Info($"{pc.GetNameWithRole().RemoveHtmlTags()}: CrewmateGhostに変更", "ResetRoleAndEndGame");
                    sender.StartRpc(pc.NetId, RpcCalls.SetRole)
                        .Write((ushort)RoleTypes.CrewmateGhost)
                        .EndRpc();
                    pc.SetRole(RoleTypes.Crewmate);
                }
            }
            SetEverythingUpPatch.LastWinsReason = winner is CustomWinner.Crewmate or CustomWinner.Impostor ? GetString($"GameOverReason.{reason}") : string.Empty;
        }

        // CustomWinnerHolderの情報の同期
        sender.StartRpc(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.EndGame);
        CustomWinnerHolder.WriteTo(sender.stream);
        sender.EndRpc();

        // GameDataによる蘇生処理
        writer.StartMessage(1); // Data
        {
            writer.WritePacked(GameData.Instance.NetId); // NetId
            foreach (var info in GameData.Instance.AllPlayers)
            {
                if (ReviveRequiredPlayerIds.Contains(info.PlayerId))
                {
                    // 蘇生&メッセージ書き込み
                    info.IsDead = false;
                    writer.StartMessage(info.PlayerId);
                    info.Serialize(writer);
                    writer.EndMessage();
                }
            }
            writer.EndMessage();
        }

        sender.EndMessage();

        // バニラ側のゲーム終了RPC
        writer.StartMessage(8); //8: EndGame
        {
            writer.Write(AmongUsClient.Instance.GameId); //GameId
            writer.Write((byte)reason); //GameoverReason
            writer.Write(false); //showAd
        }
        writer.EndMessage();

        sender.SendMessage();
    }

    public static void SetPredicateToNormal() => predicate = new NormalGameEndPredicate();
    public static void SetPredicateToSoloKombat() => predicate = new SoloKombatGameEndPredicate();
    public static void SetPredicateToFFA() => predicate = new FFAGameEndPredicate();

    // ===== ゲーム終了条件 =====
    // 通常ゲーム用
    class NormalGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForEndGame(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;
            if (CustomWinnerHolder.WinnerTeam != CustomWinner.Default) return false;
            if (CheckGameEndByLivingPlayers(out reason)) return true;
            if (CheckGameEndByTask(out reason)) return true;
            if (CheckGameEndBySabotage(out reason)) return true;

            return false;
        }

        public static bool CheckGameEndByLivingPlayers(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;

            if (CustomRolesHelper.RoleExist(CustomRoles.Sunnyboy) && Main.AllAlivePlayerControls.Count() > 1) return false;

            byte Imp = (byte)Utils.AlivePlayersCount(CountTypes.Impostor);
            byte Jackal = (byte)Utils.AlivePlayersCount(CountTypes.Jackal);
            byte Pel = (byte)Utils.AlivePlayersCount(CountTypes.Pelican);
            byte Crew = (byte)Utils.AlivePlayersCount(CountTypes.Crew);
            byte Gam = (byte)Utils.AlivePlayersCount(CountTypes.Gamer);
            byte BK = (byte)Utils.AlivePlayersCount(CountTypes.BloodKnight);
            byte Pois = (byte)Utils.AlivePlayersCount(CountTypes.Poisoner);
            byte CM = (byte)Utils.AlivePlayersCount(CountTypes.Succubus);
            byte Hex = (byte)Utils.AlivePlayersCount(CountTypes.HexMaster);
            byte Wraith = (byte)Utils.AlivePlayersCount(CountTypes.Wraith);
            byte Pestilence = (byte)Utils.AlivePlayersCount(CountTypes.Pestilence);
            byte PB = (byte)Utils.AlivePlayersCount(CountTypes.PlagueBearer);
            byte SK = (byte)Utils.AlivePlayersCount(CountTypes.NSerialKiller);
            byte MO = (byte)Utils.AlivePlayersCount(CountTypes.Mafioso);
            byte MG = (byte)Utils.AlivePlayersCount(CountTypes.Magician);
            byte RL = (byte)Utils.AlivePlayersCount(CountTypes.Reckless);
            byte EC = (byte)Utils.AlivePlayersCount(CountTypes.Eclipse);
            byte PM = (byte)Utils.AlivePlayersCount(CountTypes.Pyromaniac);
            byte HH = (byte)Utils.AlivePlayersCount(CountTypes.HeadHunter);
            byte VG = (byte)Utils.AlivePlayersCount(CountTypes.Vengeance);
            byte IM = (byte)Utils.AlivePlayersCount(CountTypes.Imitator);
            byte WW = (byte)Utils.AlivePlayersCount(CountTypes.Werewolf);
            byte RR = (byte)Utils.AlivePlayersCount(CountTypes.RuthlessRomantic);
            //byte Witch = Utils.AlivePlayersCount(CountTypes.NWitch);
            byte Juggy = (byte)Utils.AlivePlayersCount(CountTypes.Juggernaut);
            byte Vamp = (byte)Utils.AlivePlayersCount(CountTypes.Infectious);
            byte Virus = (byte)Utils.AlivePlayersCount(CountTypes.Virus);
            byte Rogue = (byte)Utils.AlivePlayersCount(CountTypes.Rogue);
            byte DH = (byte)Utils.AlivePlayersCount(CountTypes.DarkHide);
            byte Jinx = (byte)Utils.AlivePlayersCount(CountTypes.Jinx);
            byte Rit = (byte)Utils.AlivePlayersCount(CountTypes.Ritualist);
            byte PP = (byte)Utils.AlivePlayersCount(CountTypes.Pickpocket);
            byte Traitor = (byte)Utils.AlivePlayersCount(CountTypes.Traitor);
            byte Med = (byte)Utils.AlivePlayersCount(CountTypes.Medusa);
            byte SC = (byte)Utils.AlivePlayersCount(CountTypes.Spiritcaller);
            byte Glitch = (byte)Utils.AlivePlayersCount(CountTypes.Glitch);
            byte Arso = (byte)Utils.AlivePlayersCount(CountTypes.Arsonist);
            byte Bandit = (byte)Utils.AlivePlayersCount(CountTypes.Bandit);
            byte Agi = (byte)Utils.AlivePlayersCount(CountTypes.Agitater);

            foreach (var x in Main.AllAlivePlayerControls)
            {
                if (x.GetCustomRole().IsImpostor() && x.Is(CustomRoles.DualPersonality)) Imp++;
                if (x.GetCustomRole().IsCrewmate() && x.Is(CustomRoles.DualPersonality)) Crew++;
                if (x.GetCustomRole().IsImpostor() && x.Is(CustomRoles.Admired) && x.Is(CustomRoles.DualPersonality)) Crew++;
                if (x.GetCustomRole().IsNeutral() && x.Is(CustomRoles.Admired) && x.Is(CustomRoles.DualPersonality)) Crew++;
                if (x.GetCustomRole().IsCrewmate() && x.Is(CustomRoles.Admired) && x.Is(CustomRoles.DualPersonality)) Crew++;
                if (x.Is(CustomRoles.Charmed) && x.Is(CustomRoles.DualPersonality)) CM++;
                if (x.Is(CustomRoles.Sidekick) && x.Is(CustomRoles.DualPersonality)) Jackal++;
                if (x.Is(CustomRoles.Recruit) && x.Is(CustomRoles.DualPersonality)) Jackal++;
                if (x.Is(CustomRoles.Infected) && x.Is(CustomRoles.DualPersonality)) Vamp++;
                if (x.Is(CustomRoles.Contagious) && x.Is(CustomRoles.DualPersonality)) Virus++;
                if (x.Is(CustomRoles.Madmate) && x.Is(CustomRoles.DualPersonality)) Imp++;
            }

            //Imp += (byte)Main.AllAlivePlayerControls.Count(x => x.GetCustomRole().IsImpostor() && x.Is(CustomRoles.DualPersonality));
            //Crew += (byte)Main.AllAlivePlayerControls.Count(x => x.GetCustomRole().IsCrewmate() && x.Is(CustomRoles.DualPersonality));
            //Crew += (byte)Main.AllAlivePlayerControls.Count(x => x.GetCustomRole().IsImpostor() && x.Is(CustomRoles.Admired) && x.Is(CustomRoles.DualPersonality));
            //Crew += (byte)Main.AllAlivePlayerControls.Count(x => x.GetCustomRole().IsNeutral() && x.Is(CustomRoles.Admired) && x.Is(CustomRoles.DualPersonality));
            //Crew += (byte)Main.AllAlivePlayerControls.Count(x => x.GetCustomRole().IsCrewmate() && x.Is(CustomRoles.Admired) && x.Is(CustomRoles.DualPersonality));
            //CM += (byte)Main.AllAlivePlayerControls.Count(x => x.Is(CustomRoles.Charmed) && x.Is(CustomRoles.DualPersonality));
            //Jackal += (byte)Main.AllAlivePlayerControls.Count(x => x.Is(CustomRoles.Sidekick) && x.Is(CustomRoles.DualPersonality));
            //Jackal += (byte)Main.AllAlivePlayerControls.Count(x => x.Is(CustomRoles.Recruit) && x.Is(CustomRoles.DualPersonality));
            //Vamp += (byte)Main.AllAlivePlayerControls.Count(x => x.Is(CustomRoles.Infected) && x.Is(CustomRoles.DualPersonality));
            //Virus += (byte)Main.AllAlivePlayerControls.Count(x => x.Is(CustomRoles.Contagious) && x.Is(CustomRoles.DualPersonality));
            //Imp += (byte)Main.AllAlivePlayerControls.Count(x => x.Is(CustomRoles.Madmate) && x.Is(CustomRoles.DualPersonality));

            int totalNKAlive = new int[] { Jackal, Pel, Gam, BK, Pois, CM, Hex, Wraith, Pestilence, PB, SK, EC, PM, HH, VG, IM, WW, RR, Juggy, Vamp, Virus, Rogue, DH, Jinx, Rit, PP, Traitor, Med, SC, Glitch, Arso, Bandit, Agi, MO, MG, RL }.Sum();

            CustomWinner? winner = null;
            CustomRoles? rl = null;

            if (Main.AllAlivePlayerControls.All(p => p.Is(CustomRoles.Lovers)))
            {
                reason = GameOverReason.ImpostorByKill;
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Lovers);
                return true;
            }

            else if (totalNKAlive == 0)
            {
                if (Crew == 0 && Imp == 0)
                {
                    reason = GameOverReason.ImpostorByKill;
                    winner = CustomWinner.None;
                }
                else if (Crew <= Imp)
                {
                    reason = GameOverReason.ImpostorByKill;
                    winner = CustomWinner.Impostor;
                }
                else if (Imp == 0)
                {
                    reason = GameOverReason.HumansByVote;
                    winner = CustomWinner.Crewmate;
                }
                else return false;
                if (winner != null)
                    CustomWinnerHolder.ResetAndSetWinner((CustomWinner)winner);
                return true;
            }

            else
            {
                if (Imp >= 1) return false; // both imps and NKs are alive, game must continue
                else if (Crew > totalNKAlive) return false; // Imps are dead, but crew still outnumbers NKs (game must continue)
                else // Imps dead, Crew <= NK, Checking if all NKs alive are in 1 team
                {
                    if (Rit == totalNKAlive)
                    {
                        reason = GameOverReason.ImpostorByKill;
                        winner = CustomWinner.Ritualist;
                        rl = CustomRoles.Ritualist;
                    }
                    else if (Traitor == totalNKAlive)
                    {
                        reason = GameOverReason.ImpostorByKill;
                        winner = CustomWinner.Traitor;
                        rl = CustomRoles.Traitor;
                    }
                    else if (Med == totalNKAlive)
                    {
                        reason = GameOverReason.ImpostorByKill;
                        winner = CustomWinner.Medusa;
                        rl = CustomRoles.Medusa;
                    }
                    else if (PP == totalNKAlive)
                    {
                        reason = GameOverReason.ImpostorByKill;
                        winner = CustomWinner.Pickpocket;
                        rl = CustomRoles.Pickpocket;
                    }
                    else if (Jackal == totalNKAlive)
                    {
                        reason = GameOverReason.ImpostorByKill;
                        winner = CustomWinner.Jackal;
                    }
                    else if (Vamp == totalNKAlive)
                    {
                        reason = GameOverReason.ImpostorByKill;
                        winner = CustomWinner.Infectious;
                    }
                    else if (DH == totalNKAlive)
                    {
                        reason = GameOverReason.ImpostorByKill;
                        winner = CustomWinner.DarkHide;
                    }
                    else if (Rogue == totalNKAlive)
                    {
                        reason = GameOverReason.ImpostorByKill;
                        winner = CustomWinner.Rogue;
                    }
                    else if (Wraith == totalNKAlive)
                    {
                        reason = GameOverReason.ImpostorByKill;
                        winner = CustomWinner.Wraith;
                        rl = CustomRoles.Wraith;
                    }
                    else if (Agi == totalNKAlive)
                    {
                        reason = GameOverReason.ImpostorByKill;
                        winner = CustomWinner.Agitater;
                    }
                    else if (Pestilence == totalNKAlive)
                    {
                        reason = GameOverReason.ImpostorByKill;
                        winner = CustomWinner.Pestilence;
                        rl = CustomRoles.Pestilence;
                    }
                    else if (PB == totalNKAlive)
                    {
                        reason = GameOverReason.ImpostorByKill;
                        winner = CustomWinner.Plaguebearer;
                        rl = CustomRoles.PlagueBearer;
                    }
                    else if (Juggy == totalNKAlive)
                    {
                        reason = GameOverReason.ImpostorByKill;
                        winner = CustomWinner.Juggernaut;
                        rl = CustomRoles.Juggernaut;
                    }
                    else if (EC == totalNKAlive)
                    {
                        reason = GameOverReason.ImpostorByKill;
                        winner = CustomWinner.Eclipse;
                        rl = CustomRoles.Eclipse;
                    }
                    else if (HH == totalNKAlive)
                    {
                        reason = GameOverReason.ImpostorByKill;
                        winner = CustomWinner.HeadHunter;
                        rl = CustomRoles.HeadHunter;
                    }
                    else if (VG == totalNKAlive)
                    {
                        reason = GameOverReason.ImpostorByKill;
                        winner = CustomWinner.Vengeance;
                        rl = CustomRoles.Vengeance;
                    }
                    else if (PM == totalNKAlive)
                    {
                        reason = GameOverReason.ImpostorByKill;
                        winner = CustomWinner.Pyromaniac;
                        rl = CustomRoles.Pyromaniac;
                    }
                    else if (MG == totalNKAlive)
                    {
                        reason = GameOverReason.ImpostorByKill;
                        winner = CustomWinner.Magician;
                        rl = CustomRoles.Magician;
                    }
                    else if (MO == totalNKAlive)
                    {
                        reason = GameOverReason.ImpostorByKill;
                        winner = CustomWinner.Mafioso;
                        rl = CustomRoles.Mafioso;
                    }
                    else if (RL == totalNKAlive)
                    {
                        reason = GameOverReason.ImpostorByKill;
                        winner = CustomWinner.Reckless;
                        rl = CustomRoles.Reckless;
                    }
                    else if (Hex == totalNKAlive)
                    {
                        reason = GameOverReason.ImpostorByKill;
                        winner = CustomWinner.HexMaster;
                        rl = CustomRoles.HexMaster;
                    }
                    else if (Bandit == totalNKAlive)
                    {
                        reason = GameOverReason.ImpostorByKill;
                        winner = CustomWinner.Bandit;
                        rl = CustomRoles.Bandit;
                    }
                    else if (RR == totalNKAlive)
                    {
                        reason = GameOverReason.ImpostorByKill;
                        winner = CustomWinner.RuthlessRomantic;
                    }
                    else if (WW == totalNKAlive)
                    {
                        reason = GameOverReason.ImpostorByKill;
                        winner = CustomWinner.Werewolf;
                        rl = CustomRoles.Werewolf;
                    }
                    else if (IM == totalNKAlive)
                    {
                        reason = GameOverReason.ImpostorByKill;
                        winner = CustomWinner.Imitator;
                        rl = CustomRoles.Imitator;
                    }
                    else if (Arso == totalNKAlive)
                    {
                        reason = GameOverReason.ImpostorByKill;
                        winner = CustomWinner.Arsonist;
                        rl = CustomRoles.Arsonist;
                    }
                    else if (Glitch == totalNKAlive)
                    {
                        reason = GameOverReason.ImpostorByKill;
                        winner = CustomWinner.Glitch;
                        rl = CustomRoles.Glitch;
                    }
                    else if (Jinx == totalNKAlive)
                    {
                        reason = GameOverReason.ImpostorByKill;
                        winner = CustomWinner.Jinx;
                        rl = CustomRoles.Jinx;
                    }
                    else if (SK == totalNKAlive)
                    {
                        reason = GameOverReason.ImpostorByKill;
                        winner = CustomWinner.SerialKiller;
                        rl = CustomRoles.NSerialKiller;
                    }
                    else if (Pel == totalNKAlive)
                    {
                        reason = GameOverReason.ImpostorByKill;
                        winner = CustomWinner.Pelican;
                        rl = CustomRoles.Pelican;
                    }
                    else if (Gam == totalNKAlive)
                    {
                        reason = GameOverReason.ImpostorByKill;
                        winner = CustomWinner.Gamer;
                        rl = CustomRoles.Gamer;
                    }
                    else if (BK == totalNKAlive)
                    {
                        reason = GameOverReason.ImpostorByKill;
                        winner = CustomWinner.BloodKnight;
                        rl = CustomRoles.BloodKnight;
                    }
                    else if (Pois == totalNKAlive)
                    {
                        reason = GameOverReason.ImpostorByKill;
                        winner = CustomWinner.Poisoner;
                        rl = CustomRoles.Poisoner;
                    }
                    else if (Virus == totalNKAlive)
                    {
                        reason = GameOverReason.ImpostorByKill;
                        winner = CustomWinner.Virus;
                    }
                    else if (SC == totalNKAlive)
                    {
                        reason = GameOverReason.ImpostorByKill;
                        winner = CustomWinner.Spiritcaller;
                    }
                    else if (CM == totalNKAlive)
                    {
                        reason = GameOverReason.ImpostorByKill;
                        winner = CustomWinner.Succubus;
                    }
                    else return false;
                    if (winner != null) CustomWinnerHolder.ResetAndSetWinner((CustomWinner)winner);
                    if (rl != null) CustomWinnerHolder.WinnerRoles.Add((CustomRoles)rl);
                    return true;
                }
            }
        }
    }

    // 个人竞技模式用
    class SoloKombatGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForEndGame(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;
            if (CustomWinnerHolder.WinnerIds.Any()) return false;
            if (CheckGameEndByLivingPlayers(out reason)) return true;
            return false;
        }

        public static bool CheckGameEndByLivingPlayers(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;

            if (SoloKombatManager.RoundTime > 0) return false;

            var list = Main.AllPlayerControls.Where(x => !x.Is(CustomRoles.GM) && SoloKombatManager.GetRankOfScore(x.PlayerId) == 1);
            var winner = list.FirstOrDefault();

            CustomWinnerHolder.WinnerIds = new()
            {
                winner.PlayerId
            };

            Main.DoBlockNameChange = true;

            return true;
        }
    }
    class FFAGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForEndGame(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;
            if (CustomWinnerHolder.WinnerIds.Any()) return false;
            if (CheckGameEndByLivingPlayers(out reason)) return true;
            return false;
        }

        public static bool CheckGameEndByLivingPlayers(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;

            if (FFAManager.RoundTime <= 0)
            {
                var list = Main.AllPlayerControls.Where(x => !x.Is(CustomRoles.GM) && FFAManager.GetRankOfScore(x.PlayerId) == 1);
                var winner = list.FirstOrDefault();

                CustomWinnerHolder.WinnerIds = new()
                {
                    winner.PlayerId
                };

                Main.DoBlockNameChange = true;

                return true;
            }
            else if (Main.AllAlivePlayerControls.Count() == 1)
            {
                var winner = Main.AllAlivePlayerControls.FirstOrDefault();

                CustomWinnerHolder.WinnerIds = new()
                {
                    winner.PlayerId
                };

                Main.DoBlockNameChange = true;

                return true;
            }
            else if (!Main.AllAlivePlayerControls.Any())
            {
                FFAManager.RoundTime = 0;
                return true;
            }
            else return false;
        }
    }

    public abstract class GameEndPredicate
    {
        /// <summary>ゲームの終了条件をチェックし、CustomWinnerHolderに値を格納します。</summary>
        /// <params name="reason">バニラのゲーム終了処理に使用するGameOverReason</params>
        /// <returns>ゲーム終了の条件を満たしているかどうか</returns>
        public abstract bool CheckForEndGame(out GameOverReason reason);

        /// <summary>GameData.TotalTasksとCompletedTasksをもとにタスク勝利が可能かを判定します。</summary>
        public virtual bool CheckGameEndByTask(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;
            if (Options.DisableTaskWin.GetBool() || TaskState.InitialTotalTasks == 0) return false;

            if (GameData.Instance.TotalTasks <= GameData.Instance.CompletedTasks)
            {
                reason = GameOverReason.HumansByTask;
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Crewmate);
                return true;
            }
            return false;
        }
        /// <summary>ShipStatus.Systems内の要素をもとにサボタージュ勝利が可能かを判定します。</summary>
        public virtual bool CheckGameEndBySabotage(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;
            if (ShipStatus.Instance.Systems == null) return false;

            // TryGetValueは使用不可
            var systems = ShipStatus.Instance.Systems;
            LifeSuppSystemType LifeSupp;
            if (systems.ContainsKey(SystemTypes.LifeSupp) && // サボタージュ存在確認
                (LifeSupp = systems[SystemTypes.LifeSupp].TryCast<LifeSuppSystemType>()) != null && // キャスト可能確認
                LifeSupp.Countdown < 0f) // タイムアップ確認
            {
                // 酸素サボタージュ
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Impostor);
                reason = GameOverReason.ImpostorBySabotage;
                LifeSupp.Countdown = 10000f;
                return true;
            }

            ISystemType sys = null;
            if (systems.ContainsKey(SystemTypes.Reactor)) sys = systems[SystemTypes.Reactor];
            else if (systems.ContainsKey(SystemTypes.Laboratory)) sys = systems[SystemTypes.Laboratory];

            ICriticalSabotage critical;
            if (sys != null && // サボタージュ存在確認
                (critical = sys.TryCast<ICriticalSabotage>()) != null && // キャスト可能確認
                critical.Countdown < 0f) // タイムアップ確認
            {
                // リアクターサボタージュ
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Impostor);
                reason = GameOverReason.ImpostorBySabotage;
                critical.ClearSabotage();
                return true;
            }

            return false;
        }
    }
}