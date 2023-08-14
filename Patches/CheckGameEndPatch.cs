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
        if (Options.CurrentGameMode == CustomGameMode.SoloKombat)
        {
            if (CustomWinnerHolder.WinnerIds.Count > 0 || CustomWinnerHolder.WinnerTeam != CustomWinner.Default)
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
                        .Where(pc => (pc.Is(CustomRoleTypes.Impostor) || pc.Is(CustomRoles.Madmate)) && !pc.Is(CustomRoles.Lovers) && !pc.Is(CustomRoles.Rogue) && !pc.Is(CustomRoles.Charmed) && !pc.Is(CustomRoles.Recruit) && !pc.Is(CustomRoles.Infected) && !pc.Is(CustomRoles.Contagious) && !pc.Is(CustomRoles.EvilSpirit) && !pc.Is(CustomRoles.Recruit) && !pc.Is(CustomRoles.Admired))
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
                        .Where(pc => (pc.Is(CustomRoles.Spiritcaller) || pc.Is(CustomRoles.EvilSpirit)))
                        .Do(pc => CustomWinnerHolder.WinnerIds.Add(pc.PlayerId));
                    break;
                case CustomWinner.RuthlessRomantic:
                    Main.AllPlayerControls
                        .Where(pc => (pc.Is(CustomRoles.RuthlessRomantic) || (Romantic.BetPlayer.TryGetValue(pc.PlayerId, out var RomanticPartner)) && pc.PlayerId == RomanticPartner))
                        .Do(pc => CustomWinnerHolder.WinnerIds.Add(pc.PlayerId));
                    break;
            }
            if (CustomWinnerHolder.WinnerTeam is not CustomWinner.Draw and not CustomWinner.None and not CustomWinner.Error)
            {

                //潜藏者抢夺胜利
                foreach (var pc in Main.AllPlayerControls)
                {
                    if (pc.Is(CustomRoles.DarkHide) && !pc.Data.IsDead
                        && ((CustomWinnerHolder.WinnerTeam == CustomWinner.Impostor && !reason.Equals(GameOverReason.ImpostorBySabotage)) || CustomWinnerHolder.WinnerTeam == CustomWinner.DarkHide
                        || (CustomWinnerHolder.WinnerTeam == CustomWinner.Crewmate && !reason.Equals(GameOverReason.HumansByTask) && (DarkHide.IsWinKill[pc.PlayerId] == true && DarkHide.SnatchesWin.GetBool()))))
                    {
                        CustomWinnerHolder.ResetAndSetWinner(CustomWinner.DarkHide);
                        CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                    }
                }
                foreach (var pc in Main.AllPlayerControls)
                {
                    if (pc.Is(CustomRoles.Phantom) && pc.GetPlayerTaskState().IsTaskFinished && pc.Data.IsDead
                        && (((CustomWinnerHolder.WinnerTeam == CustomWinner.Impostor || CustomWinnerHolder.WinnerTeam == CustomWinner.Crewmate || CustomWinnerHolder.WinnerTeam == CustomWinner.Jackal || CustomWinnerHolder.WinnerTeam == CustomWinner.BloodKnight || CustomWinnerHolder.WinnerTeam == CustomWinner.SerialKiller || CustomWinnerHolder.WinnerTeam == CustomWinner.Juggernaut || CustomWinnerHolder.WinnerTeam == CustomWinner.Ritualist || CustomWinnerHolder.WinnerTeam == CustomWinner.Poisoner || CustomWinnerHolder.WinnerTeam == CustomWinner.Succubus || CustomWinnerHolder.WinnerTeam == CustomWinner.Infectious  || CustomWinnerHolder.WinnerTeam == CustomWinner.Jinx || CustomWinnerHolder.WinnerTeam == CustomWinner.Virus || CustomWinnerHolder.WinnerTeam == CustomWinner.Arsonist || CustomWinnerHolder.WinnerTeam == CustomWinner.Pelican || CustomWinnerHolder.WinnerTeam == CustomWinner.HexMaster || CustomWinnerHolder.WinnerTeam == CustomWinner.Wraith || CustomWinnerHolder.WinnerTeam == CustomWinner.Pestilence || CustomWinnerHolder.WinnerTeam == CustomWinner.Rogue || CustomWinnerHolder.WinnerTeam == CustomWinner.Spiritcaller) && (Options.PhantomSnatchesWin.GetBool()))))
                    {
                        reason = GameOverReason.ImpostorByKill;
                        CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Phantom);
                        CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                    }
                }
                foreach (var pc in Main.AllPlayerControls)
                {
                    if (pc.Is(CustomRoles.Famine) && !pc.Data.IsDead
                        && (((CustomWinnerHolder.WinnerTeam == CustomWinner.Impostor || CustomWinnerHolder.WinnerTeam == CustomWinner.Crewmate || CustomWinnerHolder.WinnerTeam == CustomWinner.Jackal || CustomWinnerHolder.WinnerTeam == CustomWinner.BloodKnight || CustomWinnerHolder.WinnerTeam == CustomWinner.SerialKiller || CustomWinnerHolder.WinnerTeam == CustomWinner.Juggernaut || CustomWinnerHolder.WinnerTeam == CustomWinner.Ritualist || CustomWinnerHolder.WinnerTeam == CustomWinner.Poisoner || CustomWinnerHolder.WinnerTeam == CustomWinner.Succubus || CustomWinnerHolder.WinnerTeam == CustomWinner.Infectious  || CustomWinnerHolder.WinnerTeam == CustomWinner.Jinx || CustomWinnerHolder.WinnerTeam == CustomWinner.Virus || CustomWinnerHolder.WinnerTeam == CustomWinner.Arsonist || CustomWinnerHolder.WinnerTeam == CustomWinner.Pelican || CustomWinnerHolder.WinnerTeam == CustomWinner.HexMaster || CustomWinnerHolder.WinnerTeam == CustomWinner.Wraith || CustomWinnerHolder.WinnerTeam == CustomWinner.Pestilence || CustomWinnerHolder.WinnerTeam == CustomWinner.Rogue))))
                    {
                        reason = GameOverReason.ImpostorByKill;
                        CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Famine);
                        CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                    }
                }
                foreach (var pc in Main.AllPlayerControls)
                {
                    if (pc.Is(CustomRoles.CursedSoul) && !pc.Data.IsDead
                        && (((CustomWinnerHolder.WinnerTeam == CustomWinner.Impostor || CustomWinnerHolder.WinnerTeam == CustomWinner.Crewmate || CustomWinnerHolder.WinnerTeam == CustomWinner.Jackal || CustomWinnerHolder.WinnerTeam == CustomWinner.BloodKnight || CustomWinnerHolder.WinnerTeam == CustomWinner.SerialKiller || CustomWinnerHolder.WinnerTeam == CustomWinner.Juggernaut || CustomWinnerHolder.WinnerTeam == CustomWinner.Ritualist || CustomWinnerHolder.WinnerTeam == CustomWinner.Poisoner || CustomWinnerHolder.WinnerTeam == CustomWinner.Succubus || CustomWinnerHolder.WinnerTeam == CustomWinner.Infectious  || CustomWinnerHolder.WinnerTeam == CustomWinner.Jinx || CustomWinnerHolder.WinnerTeam == CustomWinner.Virus || CustomWinnerHolder.WinnerTeam == CustomWinner.Arsonist || CustomWinnerHolder.WinnerTeam == CustomWinner.Pelican || CustomWinnerHolder.WinnerTeam == CustomWinner.HexMaster || CustomWinnerHolder.WinnerTeam == CustomWinner.Wraith || CustomWinnerHolder.WinnerTeam == CustomWinner.Pestilence || CustomWinnerHolder.WinnerTeam == CustomWinner.Rogue || CustomWinnerHolder.WinnerTeam == CustomWinner.Jester || CustomWinnerHolder.WinnerTeam == CustomWinner.Executioner))))
                    {
                        reason = GameOverReason.ImpostorByKill;
                        CustomWinnerHolder.ResetAndSetWinner(CustomWinner.CursedSoul);
                        CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Soulless);
                        CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
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
                else if (CustomRolesHelper.RoleExist(CustomRoles.Lovers) && !reason.Equals(GameOverReason.HumansByTask))
                {
                    if (!(!Main.LoversPlayers.ToArray().All(p => p.IsAlive()) && Options.LoverSuicide.GetBool()))
                    {
                        if (CustomWinnerHolder.WinnerTeam is CustomWinner.Crewmate or CustomWinner.Impostor or CustomWinner.Jackal or CustomWinner.Pelican)
                        {
                            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Lovers);
                            Main.AllPlayerControls
                                .Where(p => p.Is(CustomRoles.Lovers))
                                .Do(p => CustomWinnerHolder.WinnerIds.Add(p.PlayerId));
                        }
                    }
                }

                //追加胜利
                foreach (var pc in Main.AllPlayerControls)
                {
                    //Opportunist
                    if (pc.Is(CustomRoles.Opportunist) && pc.IsAlive())
                    {
                        CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                        CustomWinnerHolder.AdditionalWinnerTeams.Add(AdditionalWinners.Opportunist);
                    }
                    //Witch
                    if (pc.Is(CustomRoles.NWitch) && pc.IsAlive() && CustomWinnerHolder.WinnerTeam != CustomWinner.Crewmate && CustomWinnerHolder.WinnerTeam != CustomWinner.Lovers)
                    {
                        CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                        CustomWinnerHolder.AdditionalWinnerTeams.Add(AdditionalWinners.Witch);
                    }
                    if (pc.Is(CustomRoles.Pursuer) && pc.IsAlive() && CustomWinnerHolder.WinnerTeam != CustomWinner.Jester && CustomWinnerHolder.WinnerTeam != CustomWinner.Lovers && CustomWinnerHolder.WinnerTeam != CustomWinner.Terrorist && CustomWinnerHolder.WinnerTeam != CustomWinner.Executioner && CustomWinnerHolder.WinnerTeam != CustomWinner.Collector && CustomWinnerHolder.WinnerTeam != CustomWinner.Innocent && CustomWinnerHolder.WinnerTeam != CustomWinner.Youtuber)
                    {
                        CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                        CustomWinnerHolder.AdditionalWinnerTeams.Add(AdditionalWinners.Pursuer);
                    }
                    //Sunnyboy
                    if (pc.Is(CustomRoles.Sunnyboy) && !pc.IsAlive())
                    {
                        CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                        CustomWinnerHolder.AdditionalWinnerTeams.Add(AdditionalWinners.Sunnyboy);
                    }
                    if (pc.Is(CustomRoles.Maverick) && pc.IsAlive())
                    {
                        CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                        CustomWinnerHolder.AdditionalWinnerTeams.Add(AdditionalWinners.Maverick);
                    }
                    if (!Options.PhantomSnatchesWin.GetBool())
                    {
                        if (pc.Is(CustomRoles.Phantom) && !pc.IsAlive() && pc.GetPlayerTaskState().IsTaskFinished)
                        {
                            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                            CustomWinnerHolder.AdditionalWinnerTeams.Add(AdditionalWinners.Phantom);
                        }
                    }
                    //自爆卡车来咯
                    if (pc.Is(CustomRoles.Provocateur) && Main.Provoked.TryGetValue(pc.PlayerId, out var tar))
                    {
                        if (!CustomWinnerHolder.WinnerIds.Contains(tar))
                        {
                            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                            CustomWinnerHolder.AdditionalWinnerTeams.Add(AdditionalWinners.Provocateur);
                        }
                    }
                }

                //Lovers follow winner
                if (CustomWinnerHolder.WinnerTeam is not CustomWinner.Lovers and not CustomWinner.Crewmate and not CustomWinner.Impostor)
                {
                    foreach (var pc in Main.AllPlayerControls.Where(x => x.Is(CustomRoles.Lovers)))
                    {
                        if (CustomWinnerHolder.WinnerIds.Where(x => Utils.GetPlayerById(x).Is(CustomRoles.Lovers)).Count() > 0)
                        {
                            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                            CustomWinnerHolder.AdditionalWinnerTeams.Add(AdditionalWinners.Lovers);
                        }
                    }
                }
                

                //FFF
                if (CustomWinnerHolder.WinnerTeam != CustomWinner.Lovers && !CustomWinnerHolder.AdditionalWinnerTeams.Contains(AdditionalWinners.Lovers) && !CustomRolesHelper.RoleExist(CustomRoles.Lovers) && !CustomRolesHelper.RoleExist(CustomRoles.Ntr))
                {
                    foreach (var pc in Main.AllPlayerControls.Where(x => x.Is(CustomRoles.FFF)))
                    {
                        if (Main.AllPlayerControls.Where(x => (x.Is(CustomRoles.Lovers) || x.Is(CustomRoles.Ntr)) && x.GetRealKiller()?.PlayerId == pc.PlayerId).Count() > 0)
                        {
                            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                            CustomWinnerHolder.AdditionalWinnerTeams.Add(AdditionalWinners.FFF);
                        }
                    }
                }
                //Follower win condition
                foreach (var pc in Main.AllPlayerControls.Where(x => x.Is(CustomRoles.Totocalcio)))
                {
                    if (Totocalcio.BetPlayer.TryGetValue(pc.PlayerId, out var betTarget) && (
                        CustomWinnerHolder.WinnerIds.Contains(betTarget) ||
                        (Main.PlayerStates.TryGetValue(betTarget, out var ps) && CustomWinnerHolder.WinnerRoles.Contains(ps.MainRole)
                        )))
                    {
                        CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                        CustomWinnerHolder.AdditionalWinnerTeams.Add(AdditionalWinners.Totocalcio);
                    }
                }
                //Romantic win condition
                foreach (var pc in Main.AllPlayerControls.Where(x => x.Is(CustomRoles.Romantic)))
                {
                    if (Romantic.BetPlayer.TryGetValue(pc.PlayerId, out var betTarget) && (
                        CustomWinnerHolder.WinnerIds.Contains(betTarget) ||
                        (Main.PlayerStates.TryGetValue(betTarget, out var ps) && CustomWinnerHolder.WinnerRoles.Contains(ps.MainRole)
                        )))
                    {
                        CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                        CustomWinnerHolder.AdditionalWinnerTeams.Add(AdditionalWinners.Romantic);
                    }
                }
                //Vengeful Romantic win condition
                foreach (var pc in Main.AllPlayerControls.Where(x => x.Is(CustomRoles.VengefulRomantic)))
                {
                    if (VengefulRomantic.hasKilledKiller)
                    {
                        CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                        if ((Romantic.BetPlayer.TryGetValue(pc.PlayerId, out var RomanticPartner)) && pc.PlayerId == RomanticPartner)
                            CustomWinnerHolder.WinnerIds.Add(RomanticPartner);
                        CustomWinnerHolder.AdditionalWinnerTeams.Add(AdditionalWinners.VengefulRomantic);
                    }
                }
                //Lawyer win condition
                foreach (var pc in Main.AllPlayerControls.Where(x => x.Is(CustomRoles.Lawyer)))
                {
                    if (Lawyer.Target.TryGetValue(pc.PlayerId, out var lawyertarget) && (
                        CustomWinnerHolder.WinnerIds.Contains(lawyertarget) ||
                        (Main.PlayerStates.TryGetValue(lawyertarget, out var ps) && CustomWinnerHolder.WinnerRoles.Contains(ps.MainRole)
                        )))
                    {
                        CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                        CustomWinnerHolder.AdditionalWinnerTeams.Add(AdditionalWinners.Lawyer);
                    }
                }
                //中立共同胜利
                if (Options.NeutralWinTogether.GetBool() && CustomWinnerHolder.WinnerIds.Where(x => Utils.GetPlayerById(x) != null && Utils.GetPlayerById(x).GetCustomRole().IsNeutral()).Count() >= 1)
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

                //补充恋人胜利名单
                if (CustomWinnerHolder.WinnerTeam == CustomWinner.Lovers || CustomWinnerHolder.AdditionalWinnerTeams.Contains(AdditionalWinners.Lovers))
                {
                    Main.AllPlayerControls
                                .Where(p => p.Is(CustomRoles.Lovers) && !CustomWinnerHolder.WinnerIds.Contains(p.PlayerId))
                                .Do(p => CustomWinnerHolder.WinnerIds.Add(p.PlayerId));
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
                    Logger.Info($"{pc.GetNameWithRole()}: ImpostorGhostに変更", "ResetRoleAndEndGame");
                    sender.StartRpc(pc.NetId, RpcCalls.SetRole)
                        .Write((ushort)RoleTypes.ImpostorGhost)
                        .EndRpc();
                    pc.SetRole(RoleTypes.ImpostorGhost);
                }
                else
                {
                    Logger.Info($"{pc.GetNameWithRole()}: CrewmateGhostに変更", "ResetRoleAndEndGame");
                    sender.StartRpc(pc.NetId, RpcCalls.SetRole)
                        .Write((ushort)RoleTypes.CrewmateGhost)
                        .EndRpc();
                    pc.SetRole(RoleTypes.Crewmate);
                }
            }
            SetEverythingUpPatch.LastWinsReason = winner is CustomWinner.Crewmate or CustomWinner.Impostor ? GetString($"GameOverReason.{reason}") : "";
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

        public bool CheckGameEndByLivingPlayers(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;

            if (CustomRolesHelper.RoleExist(CustomRoles.Sunnyboy) && Main.AllAlivePlayerControls.Count() > 1) return false;

            int Imp = Utils.AlivePlayersCount(CountTypes.Impostor);
            int Jackal = Utils.AlivePlayersCount(CountTypes.Jackal);
            int Pel = Utils.AlivePlayersCount(CountTypes.Pelican);
            int Crew = Utils.AlivePlayersCount(CountTypes.Crew);
            int Gam = Utils.AlivePlayersCount(CountTypes.Gamer);
            int BK = Utils.AlivePlayersCount(CountTypes.BloodKnight);
            int Pois = Utils.AlivePlayersCount(CountTypes.Poisoner);
            int CM = Utils.AlivePlayersCount(CountTypes.Succubus);
            int Hex = Utils.AlivePlayersCount(CountTypes.HexMaster);
            int Wraith = Utils.AlivePlayersCount(CountTypes.Wraith);
            int Pestilence = Utils.AlivePlayersCount(CountTypes.Pestilence);
            int PB = Utils.AlivePlayersCount(CountTypes.PlagueBearer);
            int SK = Utils.AlivePlayersCount(CountTypes.NSerialKiller);
            int RR = Utils.AlivePlayersCount(CountTypes.RuthlessRomantic);
            int Witch = Utils.AlivePlayersCount(CountTypes.NWitch);
            int Juggy = Utils.AlivePlayersCount(CountTypes.Juggernaut);
            int Vamp = Utils.AlivePlayersCount(CountTypes.Infectious);
            int Virus = Utils.AlivePlayersCount(CountTypes.Virus);
            int Rogue = Utils.AlivePlayersCount(CountTypes.Rogue);
            int DH = Utils.AlivePlayersCount(CountTypes.DarkHide);
            int Jinx = Utils.AlivePlayersCount(CountTypes.Jinx);
            int Rit = Utils.AlivePlayersCount(CountTypes.Ritualist);
            int PP = Utils.AlivePlayersCount(CountTypes.Pickpocket);
            int Traitor = Utils.AlivePlayersCount(CountTypes.Traitor);
            int Med = Utils.AlivePlayersCount(CountTypes.Medusa);
            int SC = Utils.AlivePlayersCount(CountTypes.Spiritcaller);
            int Glitch = Utils.AlivePlayersCount(CountTypes.Glitch);
            int Arso = Utils.AlivePlayersCount(CountTypes.Arsonist);

            Imp += Main.AllAlivePlayerControls.Count(x => x.GetCustomRole().IsImpostor() && x.Is(CustomRoles.DualPersonality));
            Crew += Main.AllAlivePlayerControls.Count(x => x.GetCustomRole().IsCrewmate() && x.Is(CustomRoles.DualPersonality));
            Crew += Main.AllAlivePlayerControls.Count(x => (x.GetCustomRole().IsImpostor() && x.Is(CustomRoles.Admired)) && x.Is(CustomRoles.DualPersonality));
            Crew += Main.AllAlivePlayerControls.Count(x => (x.GetCustomRole().IsNeutral() && x.Is(CustomRoles.Admired)) && x.Is(CustomRoles.DualPersonality));
            Crew += Main.AllAlivePlayerControls.Count(x => (x.GetCustomRole().IsCrewmate() && x.Is(CustomRoles.Admired)) && x.Is(CustomRoles.DualPersonality));
            CM += Main.AllAlivePlayerControls.Count(x => x.Is(CustomRoles.Charmed) && x.Is(CustomRoles.DualPersonality));
            Jackal += Main.AllAlivePlayerControls.Count(x => x.Is(CustomRoles.Sidekick) && x.Is(CustomRoles.DualPersonality));
            Jackal += Main.AllAlivePlayerControls.Count(x => x.Is(CustomRoles.Recruit) && x.Is(CustomRoles.DualPersonality));
            Vamp += Main.AllAlivePlayerControls.Count(x => x.Is(CustomRoles.Infected) && x.Is(CustomRoles.DualPersonality));
            Virus += Main.AllAlivePlayerControls.Count(x => x.Is(CustomRoles.Contagious) && x.Is(CustomRoles.DualPersonality));
            Imp += Main.AllAlivePlayerControls.Count(x => x.Is(CustomRoles.Madmate) && x.Is(CustomRoles.DualPersonality));

            if (Imp == 0 && Crew == 0 && Rit == 0 && Traitor == 0 && Med == 0 && PP == 0 && Jackal == 0 && Vamp == 0 && DH == 0 && Rogue == 0 && Wraith == 0 && Pestilence == 0 && PB == 0 && Juggy == 0 && Arso == 0 && Glitch == 0 && Jinx == 0 && SK == 0 && Hex == 0 && Pel == 0 && Gam == 0 && BK == 0 && Pois == 0 && Virus == 0 && SC == 0 && CM == 0) //全灭
            {
                reason = GameOverReason.ImpostorByKill;
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.None);
            }
            else if (Main.AllAlivePlayerControls.All(p => p.Is(CustomRoles.Lovers))) //恋人胜利
            {
                reason = GameOverReason.ImpostorByKill;
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Lovers);
            }
            else if (Jackal == 0 && Pel == 0 && Traitor == 0 && Med == 0 && PP == 0 && Rit == 0 && Gam == 0 && Vamp == 0 && DH == 0 && Rogue == 0 && Juggy == 0 && Arso == 0 && Glitch == 0 && Jinx == 0 && Wraith == 0 && Pestilence == 0 && PB == 0 && SK == 0 && Hex == 0 && BK == 0 && Pois == 0 && Virus == 0 && SC == 0 && CM == 0 && Crew <= Imp) //内鬼胜利
            {
                reason = GameOverReason.ImpostorByKill;
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Impostor);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Parasite);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Refugee);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Crewpostor);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Convict);
            }
            else if (Imp == 0 && Pel == 0 && Traitor == 0 && Med == 0 && Rit == 0 && PP == 0 && Gam == 0 && Vamp == 0 && DH == 0 && Rogue == 0 && Wraith == 0 && Pestilence == 0 && PB == 0 && Juggy == 0 && Arso == 0 && Glitch == 0 && Jinx == 0 && SK == 0 && Hex == 0 && BK == 0 && Pois == 0 && Virus == 0 && SC == 0 && CM == 0 && Crew <= Jackal) //豺狼胜利
            {
                reason = GameOverReason.ImpostorByKill;
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Jackal);
                //   CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Jackal);
                //   CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Sidekick);
            }
            else if (Imp == 0 && Jackal == 0 && Rit == 0 && Traitor == 0 && Med == 0 && PP == 0 && Gam == 0 && Vamp == 0 && DH == 0 && Rogue == 0 && Juggy == 0 && Arso == 0 && Glitch == 0 && Wraith == 0 && Pestilence == 0 && PB == 0 && SK == 0 && Jinx == 0 && Hex == 0 && Pois == 0 && Virus == 0 && SC == 0 && CM == 0 && BK == 0 && Crew <= Pel) //鹈鹕胜利
            {
                reason = GameOverReason.ImpostorByKill;
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Pelican);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Pelican);
            }
            else if (Imp == 0 && Jackal == 0 && PP == 0 && Traitor == 0 && Med == 0 && Rit == 0 && Pel == 0 && Vamp == 0 && DH == 0 && Rogue == 0 && Juggy == 0 && Arso == 0 && Glitch == 0 && Wraith == 0 && Pestilence == 0 && PB == 0 && SK == 0 && Hex == 0 && Jinx == 0 && Pois == 0 && Virus == 0 && SC == 0 && CM == 0 && BK == 0 && Crew <= Gam) //玩家胜利
            {
                reason = GameOverReason.ImpostorByKill;
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Gamer);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Gamer);
            }
            else if (Imp == 0 && Jackal == 0 && PP == 0 && Traitor == 0 && Med == 0 && Rit == 0 && Pel == 0 && Vamp == 0 && DH == 0 && Rogue == 0 && Jinx == 0 && Juggy == 0 && Arso == 0 && Glitch == 0 && Wraith == 0 && Pestilence == 0 && PB == 0 && SK == 0 && Hex == 0 && Pois == 0 && Virus == 0 && SC == 0 && CM == 0 && Gam == 0 && Crew <= BK) //嗜血骑士胜利
            {
                reason = GameOverReason.ImpostorByKill;
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.BloodKnight);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.BloodKnight);
            }
            else if (Imp == 0 && Jackal == 0 && PP == 0 && Traitor == 0 && Med == 0 && Pel == 0 && Rit == 0 && Vamp == 0 && DH == 0 && Rogue == 0 && Jinx == 0 && Juggy == 0 && Arso == 0 && Glitch == 0 && Wraith == 0 && Pestilence == 0 && PB == 0 && SK == 0 && Hex == 0 && BK == 0 && Gam == 0 && Virus == 0 && SC == 0 && CM == 0 && Crew <= Pois) //嗜血骑士胜利
            {
                reason = GameOverReason.ImpostorByKill;
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Poisoner);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Poisoner);
            }
            else if (Imp == 0 && Jackal == 0 && PP == 0 && Traitor == 0 && Med == 0 && Pel == 0 && Vamp == 0 && Rit == 0 && Jinx == 0 && DH == 0 && Rogue == 0 && Juggy == 0 && Arso == 0 && Glitch == 0 && Wraith == 0 && Pestilence == 0 && PB == 0 && SK == 0 && Pois == 0 && Virus == 0 && SC == 0 && BK == 0 && Gam == 0 && CM == 0 && Crew <= Hex) //嗜血骑士胜利
            {
                reason = GameOverReason.ImpostorByKill;
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.HexMaster);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.HexMaster);
            }
            else if (Imp == 0 && Jackal == 0 && PP == 0 && Traitor == 0 && Med == 0 && Pel == 0 && Vamp == 0 && DH == 0 && Rit == 0 && Jinx == 0 && Rogue == 0 && Juggy == 0 && Arso == 0 && Glitch == 0 && Hex == 0 && Pestilence == 0 && PB == 0 && SK == 0 && Pois == 0 && Virus == 0 && SC == 0 && BK == 0 && Gam == 0 && CM == 0 && Crew <= Wraith) //嗜血骑士胜利
            {
                reason = GameOverReason.ImpostorByKill;
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Wraith);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Wraith);
            }
            else if (Imp == 0 && Jackal == 0 && PP == 0 && Traitor == 0 && Med == 0 && Pel == 0 && Vamp == 0 && DH == 0 && Rit == 0 && Jinx == 0 && Rogue == 0 && Juggy == 0 && Arso == 0 && Glitch == 0 && Hex == 0 && Wraith == 0 && PB == 0 && SK == 0 && Pois == 0 && Virus == 0 && SC == 0 && BK == 0 && Gam == 0 && CM == 0 && Crew <= Pestilence) //嗜血骑士胜利
            {
                reason = GameOverReason.ImpostorByKill;
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Pestilence);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Pestilence);
            }
            else if (Imp == 0 && Jackal == 0 && PP == 0 && Traitor == 0 && Med == 0 && Pel == 0 && Vamp == 0 && DH == 0 && Rit == 0 && Jinx == 0 && Rogue == 0 && Juggy == 0 && Arso == 0 && Glitch == 0 && Hex == 0 && Wraith == 0 && Pestilence == 0 && SK == 0 && Pois == 0 && Virus == 0 && SC == 0 && BK == 0 && Gam == 0 && CM == 0 && Crew <= PB) //嗜血骑士胜利
            {
                reason = GameOverReason.ImpostorByKill;
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Plaguebearer);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.PlagueBearer);
            }
            else if (Imp == 0 && Jackal == 0 && PP == 0 && Traitor == 0 && Med == 0 && Pel == 0 && Vamp == 0 && DH == 0 && Rogue == 0 && Rit == 0 && Jinx == 0 && Juggy == 0 && Arso == 0 && Glitch == 0 && Hex == 0 && Wraith == 0 && Pestilence == 0 && PB == 0 && Pois == 0 && Virus == 0 && SC == 0 && BK == 0 && Gam == 0 && CM == 0 && Crew <= SK) //嗜血骑士胜利
            {
                reason = GameOverReason.ImpostorByKill;
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.SerialKiller);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.NSerialKiller);
            }
            else if (Imp == 0 && Jackal == 0 && PP == 0 && Traitor == 0 && Med == 0 && Pel == 0 && Vamp == 0 && DH == 0 && Rogue == 0 && Rit == 0 && Jinx == 0 && Juggy == 0 && Arso == 0 && Glitch == 0 && Hex == 0 && Wraith == 0 && Pestilence == 0 && PB == 0 && Pois == 0 && Virus == 0 && SC == 0 && BK == 0 && Gam == 0 && CM == 0 && Crew <= RR) //嗜血骑士胜利
            {
                reason = GameOverReason.ImpostorByKill;
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.RuthlessRomantic);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.RuthlessRomantic);
            }
            else if (Imp == 0 && Jackal == 0 && PP == 0 && Traitor == 0 && Med == 0 && Arso == 0 && Pel == 0 && Vamp == 0 && DH == 0 && Rogue == 0 && Glitch == 0 && SK == 0 && Rit == 0 && Jinx == 0 && Hex == 0 && Wraith == 0 && Pestilence == 0 && PB == 0 && Pois == 0 && Virus == 0 && SC == 0 && BK == 0 && Gam == 0 && CM == 0 && Crew <= Juggy) //嗜血骑士胜利
            {
                reason = GameOverReason.ImpostorByKill;
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Juggernaut);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Juggernaut);
            }
            else if (Imp == 0 && Jackal == 0 && PP == 0 && Traitor == 0 && Med == 0 && Juggy == 0 && Pel == 0 && Vamp == 0 && DH == 0 && Rogue == 0 && Glitch == 0 && SK == 0 && Rit == 0 && Jinx == 0 && Hex == 0 && Wraith == 0 && Pestilence == 0 && PB == 0 && Pois == 0 && Virus == 0 && SC == 0 && BK == 0 && Gam == 0 && CM == 0 && Crew <= Arso) //嗜血骑士胜利
            {
                reason = GameOverReason.ImpostorByKill;
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Arsonist);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Arsonist);
            }
            else if (Imp == 0 && Jackal == 0 && PP == 0 && Traitor == 0 && Med == 0 && Pel == 0 && Rit == 0 && Vamp == 0 && DH == 0 && Rogue == 0 && Jinx == 0 && Juggy == 0 && Arso == 0 && Glitch == 0 && Hex == 0 && SK == 0 && Wraith == 0 && Pestilence == 0 && PB == 0 && BK == 0 && Gam == 0 && Pois == 0 && Virus == 0 && SC == 0 && Crew <= CM) //嗜血骑士胜利
            {
                reason = GameOverReason.ImpostorByKill;
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Succubus);
            }
            else if (Imp == 0 && Jackal == 0 && PP == 0 && Traitor == 0 && Med == 0 && Pel == 0 && CM == 0 && Rit == 0 && DH == 0 && Rogue == 0 && Juggy == 0 && Arso == 0 && Glitch == 0 && Hex == 0 && SK == 0 && Jinx == 0 && Wraith == 0 && Pestilence == 0 && PB == 0 && BK == 0 && Gam == 0 && Pois == 0 && Virus == 0 && SC == 0 && Crew <= Vamp) //嗜血骑士胜利
            {
                reason = GameOverReason.ImpostorByKill;
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Infectious);
            }
            else if (Imp == 0 && Jackal == 0 && Pel == 0 && Traitor == 0 && Med == 0 && PP == 0 && Vamp == 0 && DH == 0 && Rit == 0 && Rogue == 0 && CM == 0 && Jinx == 0 && Juggy == 0 && Arso == 0 && Glitch == 0 && Hex == 0 && SK == 0 && Wraith == 0 && Pestilence == 0 && PB == 0 && BK == 0 && Gam == 0 && Pois == 0 && SC == 0 && Crew <= Virus) //嗜血骑士胜利
            {
                reason = GameOverReason.ImpostorByKill;
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Virus);
            }
            else if (Imp == 0 && Jackal == 0 && PP == 0 && Traitor == 0 && Med == 0 && Pel == 0 && Vamp == 0 && Virus == 0 && SC == 0 && Rit == 0 && Rogue == 0 && CM == 0 && Jinx == 0 && Juggy == 0 && Arso == 0 && Glitch == 0 && Hex == 0 && SK == 0 && Wraith == 0 && Pestilence == 0 && PB == 0 && BK == 0 && Gam == 0 && Pois == 0 && Crew <= DH) //嗜血骑士胜利
            {
                reason = GameOverReason.ImpostorByKill;
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.DarkHide);
            }
            else if (Imp == 0 && Jackal == 0 && Pel == 0 && Traitor == 0 && PP == 0 && Vamp == 0 && DH == 0 && Rit == 0 && Rogue == 0 && CM == 0 && Jinx == 0 && Juggy == 0 && Arso == 0 && Glitch == 0 && Hex == 0 && SK == 0 && Wraith == 0 && Pestilence == 0 && PB == 0 && BK == 0 && Gam == 0 && Pois == 0 && Virus == 0 && Med == 0 && Crew <= SC) //嗜血骑士胜利
            {
                reason = GameOverReason.ImpostorByKill;
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Spiritcaller);
            }
            else if (Jackal == 0 && Pel == 0 && PP == 0 && Traitor == 0 && Med == 0 && Vamp == 0 && Juggy == 0 && Arso == 0 && Glitch == 0 && DH == 0 && SK == 0 && Rit == 0 && Hex == 0 && Jinx == 0 && Wraith == 0 && Pestilence == 0 && PB == 0 && Pois == 0 && Virus == 0 && SC == 0 && BK == 0 && Gam == 0 && CM == 0 && Imp == 0 && Crew <= Rogue) //嗜血骑士胜利
            {
                reason = GameOverReason.ImpostorByKill;
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Rogue);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Rogue);
            }
            else if (Imp == 0 && Jackal == 0 && PP == 0 && Traitor == 0 && Med == 0 && Pel == 0 && Vamp == 0 && Rit == 0 && DH == 0 && Rogue == 0 && Pois == 0 && Juggy == 0 && Arso == 0 && Glitch == 0 && Wraith == 0 && Pestilence == 0 && PB == 0 && SK == 0 && Hex == 0 && BK == 0 && Gam == 0 && Virus == 0 && SC == 0 && CM == 0 && Crew <= Jinx) //嗜血骑士胜利
            {
                reason = GameOverReason.ImpostorByKill;
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Jinx);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Jinx);
            }
            else if (Imp == 0 && Jackal == 0 && PP == 0 && Traitor == 0 && Med == 0 && Pel == 0 && Vamp == 0 && Jinx == 0 && DH == 0 && Rogue == 0 && Pois == 0 && Juggy == 0 && Arso == 0 && Glitch == 0 && Wraith == 0 && Pestilence == 0 && PB == 0 && SK == 0 && Hex == 0 && BK == 0 && Gam == 0 && Virus == 0 && SC == 0 && CM == 0 && Crew <= Rit) //嗜血骑士胜利
            {
                reason = GameOverReason.ImpostorByKill;
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Ritualist);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Ritualist);
            }
            else if (Imp == 0 && Jackal == 0 && Rit == 0 && Traitor == 0 && Med == 0 && Pel == 0 && Vamp == 0 && Jinx == 0 && DH == 0 && Rogue == 0 && Pois == 0 && Juggy == 0 && Arso == 0 && Glitch == 0 && Wraith == 0 && Pestilence == 0 && PB == 0 && SK == 0 && Hex == 0 && BK == 0 && Gam == 0 && Virus == 0 && SC == 0 && CM == 0 && Crew <= PP) //嗜血骑士胜利
            {
                reason = GameOverReason.ImpostorByKill;
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Pickpocket);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Pickpocket);
            }
            else if (Imp == 0 && Jackal == 0 && Rit == 0 && PP == 0 && Med == 0 && Pel == 0 && Vamp == 0 && Jinx == 0 && DH == 0 && Rogue == 0 && Pois == 0 && Juggy == 0 && Arso == 0 && Glitch == 0 && Wraith == 0 && Pestilence == 0 && PB == 0 && SK == 0 && Hex == 0 && BK == 0 && Gam == 0 && Virus == 0 && SC == 0 && CM == 0 && Crew <= Traitor) //嗜血骑士胜利
            {
                reason = GameOverReason.ImpostorByKill;
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Traitor);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Traitor);
            }
            else if (Imp == 0 && Jackal == 0 && Rit == 0 && PP == 0 && Traitor == 0 && Pel == 0 && Vamp == 0 && Jinx == 0 && DH == 0 && Rogue == 0 && Pois == 0 && Juggy == 0 && Arso == 0 && Glitch == 0 && Wraith == 0 && Pestilence == 0 && PB == 0 && SK == 0 && Hex == 0 && BK == 0 && Gam == 0 && Virus == 0 && SC == 0 && CM == 0 && Crew <= Traitor) //嗜血骑士胜利
            {
                reason = GameOverReason.ImpostorByKill;
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Medusa);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Medusa);
            }
            else if (Imp == 0 && Jackal == 0 && Rit == 0 && PP == 0 && Traitor == 0 && Pel == 0 && Vamp == 0 && Jinx == 0 && DH == 0 && Rogue == 0 && Pois == 0 && Juggy == 0 && Arso == 0 && Med == 0 && Wraith == 0 && Pestilence == 0 && PB == 0 && SK == 0 && Hex == 0 && BK == 0 && Gam == 0 && Virus == 0 && SC == 0 && CM == 0 && Crew == 0) //嗜血骑士胜利
            {
                reason = GameOverReason.ImpostorByKill;
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Glitch);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Glitch);
            }
            else if (Jackal == 0 && Pel == 0 && PP == 0 && Traitor == 0 && Med == 0 && Imp == 0 && Vamp == 0 && DH == 0 && Rit == 0 && Rogue == 0 && Juggy == 0 && Arso == 0 && Glitch == 0 && Jinx == 0 && Hex == 0 && SK == 0 && Wraith == 0 && Pestilence == 0 && PB == 0 && Pois == 0 && BK == 0 && Gam == 0 && CM == 0 && Virus == 0 && SC == 0) //船员胜利
            {
                reason = GameOverReason.HumansByVote;
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Crewmate);
             //   CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Admired);
            }
            
            else return false; //胜利条件未达成

            return true;
        }
    }

    // 个人竞技模式用
    class SoloKombatGameEndPredicate : GameEndPredicate
    {
        public override bool CheckForEndGame(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;
            if (CustomWinnerHolder.WinnerIds.Count > 0) return false;
            if (CheckGameEndByLivingPlayers(out reason)) return true;
            return false;
        }

        public bool CheckGameEndByLivingPlayers(out GameOverReason reason)
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