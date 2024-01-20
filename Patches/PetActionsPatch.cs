using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TOHE.Modules;
using TOHE.Roles.Crewmate;
using TOHE.Roles.Impostor;
using TOHE.Roles.Neutral;
using UnityEngine;
using static TOHE.Translator;

namespace TOHE;

/*
 * HUGE THANKS TO
 * ImaMapleTree / 단풍잎 / Tealeaf
 * FOR THE CODE
 */

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.TryPet))]
class LocalPetPatch
{
    private static readonly Dictionary<byte, long> LastProcess = [];
    public static bool Prefix(PlayerControl __instance)
    {
        if (!Options.UsePets.GetBool()) return true;
        if (!(AmongUsClient.Instance.AmHost && AmongUsClient.Instance.AmClient)) return true;
        if (GameStates.IsLobby) return true;

        if (__instance.petting) return true;
        __instance.petting = true;

        if (!LastProcess.ContainsKey(__instance.PlayerId)) LastProcess.TryAdd(__instance.PlayerId, Utils.GetTimeStamp() - 2);
        if (LastProcess[__instance.PlayerId] + 1 >= Utils.GetTimeStamp()) return true;

        ExternalRpcPetPatch.Prefix(__instance.MyPhysics, (byte)RpcCalls.Pet);

        LastProcess[__instance.PlayerId] = Utils.GetTimeStamp();
        return !__instance.GetCustomRole().PetActivatedAbility();
    }

    public static void Postfix(PlayerControl __instance)
    {
        if (!Options.UsePets.GetBool()) return;
        if (!(AmongUsClient.Instance.AmHost && AmongUsClient.Instance.AmClient)) return;
        __instance.petting = false;
    }
}

[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.HandleRpc))]
class ExternalRpcPetPatch
{
    private static readonly Dictionary<byte, long> LastProcess = [];
    public static void Prefix(PlayerPhysics __instance, [HarmonyArgument(0)] byte callID)
    {
        if (!Options.UsePets.GetBool() || !AmongUsClient.Instance.AmHost || (RpcCalls)callID != RpcCalls.Pet) return;

        var pc = __instance.myPlayer;
        var physics = __instance;

        if (pc == null || physics == null) return;

        if (pc != null
            && !pc.inVent
            && !pc.inMovingPlat
            && !pc.walkingToVent
            && !pc.onLadder
            && !physics.Animations.IsPlayingEnterVentAnimation()
            && !physics.Animations.IsPlayingClimbAnimation()
            && !physics.Animations.IsPlayingAnyLadderAnimation()
            && !Pelican.IsEaten(pc.PlayerId)
            && GameStates.IsInTask
            && pc.GetCustomRole().PetActivatedAbility())
            physics.CancelPet();

        if (!LastProcess.ContainsKey(pc.PlayerId)) LastProcess.TryAdd(pc.PlayerId, Utils.GetTimeStamp() - 2);
        if (LastProcess[pc.PlayerId] + 1 >= Utils.GetTimeStamp()) return;
        LastProcess[pc.PlayerId] = Utils.GetTimeStamp();

        Logger.Info($"Player {pc.GetNameWithRole().RemoveHtmlTags()} petted their pet", "PetActionTrigger");

        _ = new LateTask(() => { OnPetUse(pc); }, 0.2f, $"OnPetUse: {pc.GetNameWithRole().RemoveHtmlTags()}", false);
    }
    public static void OnPetUse(PlayerControl pc)
    {
        if (pc == null ||
            pc.inVent ||
            pc.inMovingPlat ||
            pc.onLadder ||
            pc.walkingToVent ||
            pc.MyPhysics.Animations.IsPlayingEnterVentAnimation() ||
            pc.MyPhysics.Animations.IsPlayingClimbAnimation() ||
            pc.MyPhysics.Animations.IsPlayingAnyLadderAnimation() ||
            Pelican.IsEaten(pc.PlayerId))
            return;

        if (Mastermind.ManipulatedPlayers.ContainsKey(pc.PlayerId))
        {
            var killTarget = SelectKillButtonTarget(pc);
            if (killTarget != null) Mastermind.ForceKillForManipulatedPlayer(pc, killTarget);
        }

        if (pc.HasAbilityCD()) return;

        PlayerControl[] AllAlivePlayers = Main.AllAlivePlayerControls;

        bool hasKillTarget = false;
        PlayerControl target = SelectKillButtonTarget(pc);
        if (target != null) hasKillTarget = true;
        if (!pc.CanUseKillButton()) hasKillTarget = false;

        switch (pc.GetCustomRole())
        {
            // Crewmates

            case CustomRoles.Doormaster:
                Doormaster.OnEnterVent(pc);
                break;
            case CustomRoles.Sapper:
                Sapper.OnShapeshift(pc, true);
                break;
            case CustomRoles.Tether:
                Tether.OnEnterVent(pc, 0, true);
                break;
            case CustomRoles.CameraMan:
                CameraMan.OnEnterVent(pc);
                break;
            case CustomRoles.Mayor:
                if (Main.MayorUsedButtonCount.TryGetValue(pc.PlayerId, out var count) && count < Options.MayorNumOfUseButton.GetInt())
                    pc?.ReportDeadBody(null);
                break;
            case CustomRoles.Paranoia:
                if (Main.ParaUsedButtonCount.TryGetValue(pc.PlayerId, out var count2) && count2 < Options.ParanoiaNumOfUseButton.GetInt())
                {
                    Main.ParaUsedButtonCount[pc.PlayerId] += 1;
                    if (AmongUsClient.Instance.AmHost)
                    {
                        _ = new LateTask(() =>
                        {
                            Utils.SendMessage(GetString("SkillUsedLeft") + (Options.ParanoiaNumOfUseButton.GetInt() - Main.ParaUsedButtonCount[pc.PlayerId]).ToString(), pc.PlayerId);
                        }, 4.0f, "Skill Remain Message");
                    }

                    pc?.NoCheckStartMeeting(pc?.Data);
                }
                break;
            case CustomRoles.Veteran:
                if (Main.VeteranInProtect.ContainsKey(pc.PlayerId)) break;
                if (Main.VeteranNumOfUsed[pc.PlayerId] >= 1)
                {
                    Main.VeteranInProtect.Remove(pc.PlayerId);
                    Main.VeteranInProtect.Add(pc.PlayerId, Utils.GetTimeStamp(DateTime.Now));
                    Main.VeteranNumOfUsed[pc.PlayerId] -= 1;
                    pc.RPCPlayCustomSound("Gunload");
                    pc.Notify(GetString("VeteranOnGuard"), Options.VeteranSkillDuration.GetFloat());
                    pc.MarkDirtySettings();
                }
                else
                {
                    if (!NameNotifyManager.Notice.ContainsKey(pc.PlayerId)) pc.Notify(GetString("OutOfAbilityUsesDoMoreTasks"));
                }
                break;
            case CustomRoles.Grenadier:
                if (Main.GrenadierBlinding.ContainsKey(pc.PlayerId) || Main.MadGrenadierBlinding.ContainsKey(pc.PlayerId)) break;
                if (Main.GrenadierNumOfUsed[pc.PlayerId] >= 1)
                {
                    if (pc.Is(CustomRoles.Madmate))
                    {
                        Main.MadGrenadierBlinding.Remove(pc.PlayerId);
                        Main.MadGrenadierBlinding.Add(pc.PlayerId, Utils.GetTimeStamp());
                        Main.AllPlayerControls.Where(x => x.IsModClient()).Where(x => !x.GetCustomRole().IsImpostorTeam() && !x.Is(CustomRoles.Madmate)).Do(x => x.RPCPlayCustomSound("FlashBang"));
                    }
                    else
                    {
                        Main.GrenadierBlinding.Remove(pc.PlayerId);
                        Main.GrenadierBlinding.Add(pc.PlayerId, Utils.GetTimeStamp());
                        Main.AllPlayerControls.Where(x => x.IsModClient()).Where(x => x.GetCustomRole().IsImpostor() || (x.GetCustomRole().IsNeutral() && Options.GrenadierCanAffectNeutral.GetBool())).Do(x => x.RPCPlayCustomSound("FlashBang"));
                    }
                    pc.RPCPlayCustomSound("FlashBang");
                    pc.Notify(GetString("GrenadierSkillInUse"), Options.GrenadierSkillDuration.GetFloat());
                    Main.GrenadierNumOfUsed[pc.PlayerId] -= 1;
                    Utils.MarkEveryoneDirtySettingsV3();
                }
                else
                {
                    if (!NameNotifyManager.Notice.ContainsKey(pc.PlayerId)) pc.Notify(GetString("OutOfAbilityUsesDoMoreTasks"));
                }
                break;
            case CustomRoles.Lighter:
                if (Main.Lighter.ContainsKey(pc.PlayerId)) break;
                if (Main.LighterNumOfUsed[pc.PlayerId] >= 1)
                {
                    Main.Lighter.Remove(pc.PlayerId);
                    Main.Lighter.Add(pc.PlayerId, Utils.GetTimeStamp());
                    pc.Notify(GetString("LighterSkillInUse"), Options.LighterSkillDuration.GetFloat());
                    Main.LighterNumOfUsed[pc.PlayerId] -= 1;
                    pc.MarkDirtySettings();
                }
                else
                {
                    if (!NameNotifyManager.Notice.ContainsKey(pc.PlayerId))
                        pc.Notify(GetString("OutOfAbilityUsesDoMoreTasks"));
                }
                break;
            case CustomRoles.SecurityGuard:
                if (Main.BlockSabo.ContainsKey(pc.PlayerId)) break;
                if (Main.SecurityGuardNumOfUsed[pc.PlayerId] >= 1)
                {
                    Main.BlockSabo.Remove(pc.PlayerId);
                    Main.BlockSabo.Add(pc.PlayerId, Utils.GetTimeStamp());
                    pc.Notify(GetString("SecurityGuardSkillInUse"), Options.SecurityGuardSkillDuration.GetFloat());
                    Main.SecurityGuardNumOfUsed[pc.PlayerId] -= 1;
                }
                else
                {
                    if (!NameNotifyManager.Notice.ContainsKey(pc.PlayerId))
                        pc.Notify(GetString("OutOfAbilityUsesDoMoreTasks"));
                }
                break;
            case CustomRoles.DovesOfNeace:
                if (Main.DovesOfNeaceNumOfUsed[pc.PlayerId] < 1)
                {
                    if (!NameNotifyManager.Notice.ContainsKey(pc.PlayerId))
                        pc.Notify(GetString("OutOfAbilityUsesDoMoreTasks"));
                    break;
                }
                else
                {
                    Main.DovesOfNeaceNumOfUsed[pc.PlayerId] -= 1;
                    //pc.RpcGuardAndKill(pc);
                    AllAlivePlayers.Where(x =>
                    pc.Is(CustomRoles.Madmate) ?
                    (x.CanUseKillButton() && x.GetCustomRole().IsCrewmate()) :
                    x.CanUseKillButton()
                    ).Do(x =>
                    {
                        x.RPCPlayCustomSound("Dove");
                        x.ResetKillCooldown();
                        x.SetKillCooldown();
                        if (x.Is(CustomRoles.SerialKiller))
                        { SerialKiller.OnReportDeadBody(); }
                        x.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.DovesOfNeace), GetString("DovesOfNeaceSkillNotify")));
                    });
                    pc.RPCPlayCustomSound("Dove");
                    pc.Notify(string.Format(GetString("DovesOfNeaceOnGuard"), Main.DovesOfNeaceNumOfUsed[pc.PlayerId]));
                }
                break;
            case CustomRoles.Alchemist:
                Alchemist.OnEnterVent(pc, 0, true);
                break;
            case CustomRoles.TimeMaster:
                if (Main.TimeMasterInProtect.ContainsKey(pc.PlayerId)) break;
                if (Main.TimeMasterNumOfUsed[pc.PlayerId] >= 1)
                {
                    Main.TimeMasterNumOfUsed[pc.PlayerId] -= 1;
                    Main.TimeMasterInProtect.Remove(pc.PlayerId);
                    Main.TimeMasterInProtect.Add(pc.PlayerId, Utils.GetTimeStamp());
                    pc.Notify(GetString("TimeMasterOnGuard"), Options.TimeMasterSkillDuration.GetFloat());
                    foreach (PlayerControl player in Main.AllPlayerControls)
                    {
                        if (Main.TimeMasterBackTrack.ContainsKey(player.PlayerId))
                        {
                            var position = Main.TimeMasterBackTrack[player.PlayerId];
                            player.TP(position);
                            if (pc != player)
                                player?.MyPhysics?.RpcBootFromVent(player.PlayerId);
                            Main.TimeMasterBackTrack.Remove(player.PlayerId);
                        }
                        else
                        {
                            Main.TimeMasterBackTrack.Add(player.PlayerId, player.Pos());
                        }
                    }
                }
                else
                {
                    if (!NameNotifyManager.Notice.ContainsKey(pc.PlayerId))
                        pc.Notify(GetString("OutOfAbilityUsesDoMoreTasks"));
                }
                break;
            case CustomRoles.NiceHacker:
                NiceHacker.OnEnterVent(pc);
                break;
            case CustomRoles.Druid:
                Druid.OnEnterVent(pc, isPet: true);
                break;
            case CustomRoles.Tunneler:
                if (Main.TunnelerPositions.TryGetValue(pc.PlayerId, out var ps))
                {
                    pc.TP(ps);
                    Main.TunnelerPositions.Remove(pc.PlayerId);
                }
                else Main.TunnelerPositions[pc.PlayerId] = pc.Pos();
                break;
            case CustomRoles.Tornado:
                Tornado.SpawnTornado(pc);
                break;
            case CustomRoles.Sentinel:
                Sentinel.StartPatrolling(pc);
                break;
            case CustomRoles.Lookout:
                var sb = new StringBuilder();
                for (int i = 0; i < AllAlivePlayers.Length; i++) if (i % 3 == 0) sb.AppendLine();
                for (int i = 0; i < AllAlivePlayers.Length; i++)
                {
                    PlayerControl player = AllAlivePlayers[i];
                    if (player == null) continue;
                    if (i != 0) sb.Append("; ");
                    string name = player.GetRealName();
                    byte id = player.PlayerId;
                    if (Main.PlayerColors.TryGetValue(id, out var color)) name = Utils.ColorString(color, name);
                    sb.Append($"{name} {id}");
                    if (i % 3 == 0 && i != AllAlivePlayers.Length - 1) sb.AppendLine();
                }
                pc.Notify(sb.ToString());
                break;

            case CustomRoles.Gaulois when hasKillTarget:
                Gaulois.OnCheckMurder(pc, target);
                pc.AddKCDAsAbilityCD();
                break;
            case CustomRoles.Aid when hasKillTarget:
                Aid.OnCheckMurder(pc, target);
                pc.AddKCDAsAbilityCD();
                break;
            case CustomRoles.Escort when hasKillTarget:
                Escort.OnCheckMurder(pc, target);
                pc.AddKCDAsAbilityCD();
                break;
            case CustomRoles.DonutDelivery when hasKillTarget:
                DonutDelivery.OnCheckMurder(pc, target);
                pc.AddKCDAsAbilityCD();
                break;
            case CustomRoles.Analyzer when hasKillTarget:
                Analyzer.OnCheckMurder(pc, target);
                pc.AddKCDAsAbilityCD();
                break;
            case CustomRoles.Jailor when hasKillTarget:
                Jailor.OnCheckMurder(pc, target);
                pc.AddKCDAsAbilityCD();
                break;
            case CustomRoles.Sheriff when hasKillTarget:
                if (Sheriff.OnCheckMurder(pc, target)) pc.RpcCheckAndMurder(target);
                else pc.AddKCDAsAbilityCD();
                break;
            case CustomRoles.SwordsMan when hasKillTarget:
                if (SwordsMan.OnCheckMurder(pc)) if (!pc.RpcCheckAndMurder(target)) pc.AddKCDAsAbilityCD();
                    else pc.AddKCDAsAbilityCD();
                break;
            case CustomRoles.Witness when hasKillTarget:
                if (Main.AllKillers.ContainsKey(target.PlayerId))
                    pc.Notify(GetString("WitnessFoundKiller"));
                else pc.Notify(GetString("WitnessFoundInnocent"));
                pc.AddKCDAsAbilityCD();
                break;
            case CustomRoles.Medic when hasKillTarget:
                Medic.OnCheckMurderFormedicaler(pc, target);
                pc.AddKCDAsAbilityCD();
                break;
            case CustomRoles.Monarch when hasKillTarget:
                Monarch.OnCheckMurder(pc, target);
                pc.AddKCDAsAbilityCD();
                break;
            case CustomRoles.CopyCat when hasKillTarget:
                if (CopyCat.OnCheckMurder(pc, target)) if (!pc.RpcCheckAndMurder(target)) pc.AddKCDAsAbilityCD();
                pc.AddKCDAsAbilityCD();
                break;
            case CustomRoles.Farseer when hasKillTarget:
                pc.AddAbilityCD(Farseer.FarseerRevealTime.GetInt());
                if (!Main.isRevealed[(pc.PlayerId, target.PlayerId)] && !Main.FarseerTimer.ContainsKey(pc.PlayerId))
                {
                    Main.FarseerTimer.TryAdd(pc.PlayerId, (target, 0f));
                    Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: target, ForceLoop: true);
                    RPC.SetCurrentRevealTarget(pc.PlayerId, target.PlayerId);
                }
                break;
            case CustomRoles.Deputy when hasKillTarget:
                Deputy.OnCheckMurder(pc, target);
                break;
            case CustomRoles.Admirer when hasKillTarget:
                Admirer.OnCheckMurder(pc, target);
                break;
            case CustomRoles.Crusader when hasKillTarget:
                Crusader.OnCheckMurder(pc, target);
                break;

            // Impostors

            case CustomRoles.Sniper:
                Sniper.OnShapeshift(pc, !Sniper.IsAim[pc.PlayerId]);
                break;
            case CustomRoles.Warlock:
                if (!Main.isCurseAndKill.ContainsKey(pc.PlayerId)) Main.isCurseAndKill[pc.PlayerId] = false;
                if (Main.CursedPlayers[pc.PlayerId] != null)
                {
                    if (!Main.CursedPlayers[pc.PlayerId].Data.IsDead)
                    {
                        var cp = Main.CursedPlayers[pc.PlayerId];
                        UnityEngine.Vector2 cppos = cp.Pos();
                        Dictionary<PlayerControl, float> cpdistance = [];
                        float dis;
                        foreach (PlayerControl p in AllAlivePlayers)
                        {
                            if (p.PlayerId == cp.PlayerId) continue;
                            if (!Options.WarlockCanKillSelf.GetBool() && p.PlayerId == pc.PlayerId) continue;
                            if (!Options.WarlockCanKillAllies.GetBool() && p.GetCustomRole().IsImpostor()) continue;
                            if (p.Is(CustomRoles.Pestilence)) continue;
                            if (Pelican.IsEaten(p.PlayerId) || Medic.ProtectList.Contains(p.PlayerId)) continue;
                            dis = Vector2.Distance(cppos, p.Pos());
                            cpdistance.Add(p, dis);
                            Logger.Info($"{p?.Data?.PlayerName}'s distance: {dis}", "Warlock");
                        }
                        if (cpdistance.Count > 0)
                        {
                            var min = cpdistance.OrderBy(c => c.Value).FirstOrDefault();
                            PlayerControl targetw = min.Key;
                            if (cp.RpcCheckAndMurder(targetw, true))
                            {
                                targetw.SetRealKiller(pc);
                                Logger.Info($"{targetw.GetNameWithRole().RemoveHtmlTags()} was killed", "Warlock");
                                cp.Kill(targetw);
                                pc.SetKillCooldown();
                                pc.Notify(GetString("WarlockControlKill"));
                            }
                            _ = new LateTask(() => { pc.CmdCheckRevertShapeshift(false); }, 1.5f, "Warlock RpcRevertShapeshift");
                        }
                        else
                        {
                            pc.Notify(GetString("WarlockNoTarget"));
                        }
                        Main.isCurseAndKill[pc.PlayerId] = false;
                    }
                    Main.CursedPlayers[pc.PlayerId] = null;
                }
                break;
            case CustomRoles.Assassin:
                Assassin.OnShapeshift(pc, true);
                break;
            case CustomRoles.Undertaker:
                Undertaker.OnShapeshift(pc, true);
                break;
            case CustomRoles.Miner:
                if (Main.LastEnteredVent.ContainsKey(pc.PlayerId))
                {
                    var position = Main.LastEnteredVentLocation[pc.PlayerId];
                    Logger.Msg($"{pc.GetNameWithRole().RemoveHtmlTags()}:{position}", "MinerTeleport");
                    pc.TP(new UnityEngine.Vector2(position.x, position.y));
                }
                break;
            case CustomRoles.Escapee:
                if (Main.EscapeeLocation.ContainsKey(pc.PlayerId))
                {
                    var position = Main.EscapeeLocation[pc.PlayerId];
                    Main.EscapeeLocation.Remove(pc.PlayerId);
                    Logger.Msg($"{pc.GetNameWithRole().RemoveHtmlTags()}:{position}", "EscapeeTeleport");
                    pc.TP(position);
                    pc.RPCPlayCustomSound("Teleport");
                }
                else
                {
                    Main.EscapeeLocation.Add(pc.PlayerId, pc.Pos());
                }
                break;
            case CustomRoles.RiftMaker:
                RiftMaker.OnShapeshift(pc, true);
                break;
            case CustomRoles.Bomber:
                Logger.Info("Bomber explosion", "Boom");
                CustomSoundsManager.RPCPlayCustomSoundAll("Boom");
                foreach (PlayerControl tg in Main.AllPlayerControls)
                {
                    if (!tg.IsModClient()) tg.KillFlash();
                    var pos = pc.Pos();
                    var dis = Vector2.Distance(pos, tg.Pos());

                    if (!tg.IsAlive() || Pelican.IsEaten(tg.PlayerId) || Medic.ProtectList.Contains(tg.PlayerId) || (tg.Is(CustomRoleTypes.Impostor) && Options.ImpostorsSurviveBombs.GetBool()) || tg.inVent || tg.Is(CustomRoles.Pestilence)) continue;
                    if (dis > Options.BomberRadius.GetFloat()) continue;
                    if (tg.PlayerId == pc.PlayerId) continue;

                    tg.Suicide(PlayerState.DeathReason.Bombed, pc);
                }
                _ = new LateTask(() =>
                {
                    var totalAlive = AllAlivePlayers.Length;
                    if (Options.BomberDiesInExplosion.GetBool() && totalAlive > 1 && !GameStates.IsEnded)
                    {
                        pc.Suicide(PlayerState.DeathReason.Bombed);
                    }
                    Utils.NotifyRoles(ForceLoop: true);
                }, 1.5f, "Bomber Suiscide");
                break;
            case CustomRoles.Nuker:
                Logger.Info("Nuker explosion", "Boom");
                CustomSoundsManager.RPCPlayCustomSoundAll("Boom");
                foreach (PlayerControl tg in Main.AllPlayerControls)
                {
                    if (!tg.IsModClient()) tg.KillFlash();
                    var pos = pc.Pos();
                    var dis = Vector2.Distance(pos, tg.Pos());

                    if (!tg.IsAlive() || Pelican.IsEaten(tg.PlayerId) || Medic.ProtectList.Contains(tg.PlayerId) || tg.inVent || tg.Is(CustomRoles.Pestilence)) continue;
                    if (dis > Options.NukeRadius.GetFloat()) continue;
                    if (tg.PlayerId == pc.PlayerId) continue;

                    tg.Suicide(PlayerState.DeathReason.Bombed, pc);
                }
                _ = new LateTask(() =>
                {
                    var totalAlive = AllAlivePlayers.Length;
                    if (totalAlive > 1 && !GameStates.IsEnded)
                    {
                        pc.Suicide(PlayerState.DeathReason.Bombed);
                    }
                    Utils.NotifyRoles(ForceLoop: true);
                }, 1.5f, "Nuke");
                break;
            case CustomRoles.QuickShooter:
                QuickShooter.OnShapeshift(pc, true);
                break;
            case CustomRoles.Disperser:
                Disperser.DispersePlayers(pc);
                break;
            case CustomRoles.Twister:
                Twister.TwistPlayers(pc, true);
                break;

            // Neutrals

            case CustomRoles.Glitch:
                Glitch.Mimic(pc);
                break;
            case CustomRoles.Magician:
                Magician.UseCard(pc);
                break;
            case CustomRoles.WeaponMaster:
                WeaponMaster.SwitchMode();
                break;
            case CustomRoles.Enderman:
                Enderman.MarkPosition();
                break;
            case CustomRoles.Mycologist when Mycologist.SpreadAction.GetValue() == 2:
                Mycologist.SpreadSpores();
                break;
            case CustomRoles.Hookshot:
                Hookshot.ExecuteAction();
                break;
            case CustomRoles.Sprayer:
                Sprayer.PlaceTrap();
                break;
            case CustomRoles.Arsonist when pc.CanUseImpostorVentButton():
                if (pc.IsDouseDone())
                {
                    CustomSoundsManager.RPCPlayCustomSoundAll("Boom");
                    foreach (PlayerControl player in Main.AllAlivePlayerControls)
                    {
                        if (player != pc)
                        {
                            player.Suicide(PlayerState.DeathReason.Torched, pc);
                        }
                    }
                    foreach (PlayerControl player in Main.AllPlayerControls)
                    {
                        player.KillFlash();
                    }

                    CustomWinnerHolder.ShiftWinnerAndSetWinner(CustomWinner.Arsonist);
                    CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                    break;
                }
                else if (Options.ArsonistCanIgniteAnytime.GetBool())
                {
                    var douseCount = Utils.GetDousedPlayerCount(pc.PlayerId).Item1;
                    if (douseCount >= Options.ArsonistMinPlayersToIgnite.GetInt()) // Don't check for max, since the player would not be able to ignite at all if they somehow get more players doused than the max
                    {
                        if (douseCount > Options.ArsonistMaxPlayersToIgnite.GetInt()) Logger.Warn("Arsonist Ignited with more players doused than the maximum amount in the settings", "Arsonist Ignite");
                        foreach (PlayerControl player in Main.AllAlivePlayerControls)
                        {
                            if (!pc.IsDousedPlayer(player))
                                continue;
                            player.KillFlash();
                            player.Suicide(PlayerState.DeathReason.Torched, pc);
                        }
                        var apc = Main.AllAlivePlayerControls.Length;
                        if (apc == 1)
                        {
                            CustomWinnerHolder.ShiftWinnerAndSetWinner(CustomWinner.Arsonist);
                            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                        }
                        if (apc == 2)
                        {
                            foreach (var player in Main.AllAlivePlayerControls.Where(p => p.PlayerId != pc.PlayerId).ToArray())
                            {
                                if (!player.GetCustomRole().IsImpostor() && !player.GetCustomRole().IsNeutralKilling())
                                {
                                    CustomWinnerHolder.ShiftWinnerAndSetWinner(CustomWinner.Arsonist);
                                    CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
                                }
                            }
                        }
                        break;
                    }
                }
                break;

            // Message when no ability is triggered

            default:
                int x = IRandom.Instance.Next(1, 16);
                string suffix;
                if (x >= 14)
                {
                    x -= 13;
                    suffix = pc.GetCustomRole().GetCustomRoleTypes() switch
                    {
                        CustomRoleTypes.Impostor => $"Imp{x}",
                        CustomRoleTypes.Neutral => $"Neutral{x}",
                        CustomRoleTypes.Crewmate => x == 1 ? "Crew" : pc.GetPlayerTaskState().IsTaskFinished ? "CrewTaskDone" : "CrewWithTasksLeft",
                        _ => x.ToString(),
                    };
                }
                else suffix = x.ToString();
                pc.Notify(GetString($"NoPetActionMsg{suffix}"));
                break;
        }

        if (pc.HasAbilityCD() || (pc.Is(CustomRoles.Sniper) && Sniper.IsAim[pc.PlayerId])) return;

        pc.AddAbilityCD();
    }

    public static PlayerControl SelectKillButtonTarget(PlayerControl pc)
    {
        var players = pc.GetPlayersInAbilityRangeSorted();
        var target = players.Count == 0 ? null : players[0];
        return target;
    }
}