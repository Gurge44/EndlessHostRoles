using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AmongUs.GameOptions;
using HarmonyLib;
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

        if (!LastProcess.ContainsKey(__instance.PlayerId)) LastProcess.TryAdd(__instance.PlayerId, Utils.TimeStamp - 2);
        if (LastProcess[__instance.PlayerId] + 1 >= Utils.TimeStamp) return true;

        ExternalRpcPetPatch.Prefix(__instance.MyPhysics, (byte)RpcCalls.Pet);

        LastProcess[__instance.PlayerId] = Utils.TimeStamp;
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

        if (pc == null) return;

        if (!pc.inVent
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

        if (!LastProcess.ContainsKey(pc.PlayerId)) LastProcess.TryAdd(pc.PlayerId, Utils.TimeStamp - 2);
        if (LastProcess[pc.PlayerId] + 1 >= Utils.TimeStamp) return;
        LastProcess[pc.PlayerId] = Utils.TimeStamp;

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
            Pelican.IsEaten(pc.PlayerId) ||
            Penguin.IsVictim(pc))
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
                Sapper.OnShapeshift(pc);
                break;
            case CustomRoles.Tether:
                Tether.OnEnterVent(pc, 0, true);
                break;
            case CustomRoles.CameraMan:
                CameraMan.OnEnterVent(pc);
                break;
            case CustomRoles.Mayor:
                if (Main.MayorUsedButtonCount.TryGetValue(pc.PlayerId, out var count) && count < Options.MayorNumOfUseButton.GetInt())
                    pc.ReportDeadBody(null);
                break;
            case CustomRoles.Paranoia:
                if (Main.ParaUsedButtonCount.TryGetValue(pc.PlayerId, out var count2) && count2 < Options.ParanoiaNumOfUseButton.GetInt())
                {
                    Main.ParaUsedButtonCount[pc.PlayerId] += 1;
                    if (AmongUsClient.Instance.AmHost)
                    {
                        _ = new LateTask(() => { Utils.SendMessage(GetString("SkillUsedLeft") + (Options.ParanoiaNumOfUseButton.GetInt() - Main.ParaUsedButtonCount[pc.PlayerId]), pc.PlayerId); }, 4.0f, "Skill Remain Message");
                    }

                    pc.NoCheckStartMeeting(pc.Data);
                }

                break;
            case CustomRoles.Veteran:
                if (Main.VeteranInProtect.ContainsKey(pc.PlayerId)) break;
                if (pc.GetAbilityUseLimit() >= 1)
                {
                    Main.VeteranInProtect.Remove(pc.PlayerId);
                    Main.VeteranInProtect.Add(pc.PlayerId, Utils.GetTimeStamp(DateTime.Now));
                    pc.RpcRemoveAbilityUse();
                    pc.RPCPlayCustomSound("Gunload");
                    pc.Notify(GetString("VeteranOnGuard"), Options.VeteranSkillDuration.GetFloat());
                    pc.MarkDirtySettings();
                }
                else
                {
                    if (!NameNotifyManager.Notice.ContainsKey(pc.PlayerId)) pc.Notify(GetString("OutOfAbilityUsesDoMoreTasks"));
                }

                break;
            case CustomRoles.SecurityGuard:
                if (Main.BlockSabo.ContainsKey(pc.PlayerId)) break;
                if (pc.GetAbilityUseLimit() >= 1)
                {
                    Main.BlockSabo.Remove(pc.PlayerId);
                    Main.BlockSabo.Add(pc.PlayerId, Utils.TimeStamp);
                    pc.Notify(GetString("SecurityGuardSkillInUse"), Options.SecurityGuardSkillDuration.GetFloat());
                    pc.RpcRemoveAbilityUse();
                }
                else
                {
                    if (!NameNotifyManager.Notice.ContainsKey(pc.PlayerId))
                        pc.Notify(GetString("OutOfAbilityUsesDoMoreTasks"));
                }

                break;
            case CustomRoles.Alchemist:
                Alchemist.OnEnterVent(pc, 0, true);
                break;
            case CustomRoles.TimeMaster:
                if (Main.TimeMasterInProtect.ContainsKey(pc.PlayerId)) break;
                if (pc.GetAbilityUseLimit() >= 1)
                {
                    pc.RpcRemoveAbilityUse();
                    Main.TimeMasterInProtect.Remove(pc.PlayerId);
                    Main.TimeMasterInProtect.Add(pc.PlayerId, Utils.TimeStamp);
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
                for (int i = 0; i < AllAlivePlayers.Length; i++)
                    if (i % 3 == 0)
                        sb.AppendLine();
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
            case CustomRoles.Convener:
                Convener.UseAbility(pc, isPet: true);
                break;
            case CustomRoles.Perceiver:
                Perceiver.UseAbility(pc);
                break;

            case CustomRoles.Gaulois when hasKillTarget:
                pc.AddKCDAsAbilityCD();
                Gaulois.OnCheckMurder(pc, target);
                break;
            case CustomRoles.Aid when hasKillTarget:
                pc.AddKCDAsAbilityCD();
                Aid.OnCheckMurder(pc, target);
                break;
            case CustomRoles.Escort when hasKillTarget:
                pc.AddKCDAsAbilityCD();
                Escort.OnCheckMurder(pc, target);
                break;
            case CustomRoles.DonutDelivery when hasKillTarget:
                pc.AddKCDAsAbilityCD();
                DonutDelivery.OnCheckMurder(pc, target);
                break;
            case CustomRoles.Analyzer when hasKillTarget:
                pc.AddKCDAsAbilityCD();
                Analyzer.OnCheckMurder(pc, target);
                break;
            case CustomRoles.Jailor when hasKillTarget:
                pc.AddKCDAsAbilityCD();
                Jailor.OnCheckMurder(pc, target);
                break;
            case CustomRoles.Sheriff when hasKillTarget:
                pc.AddKCDAsAbilityCD();
                if (Sheriff.OnCheckMurder(pc, target)) pc.RpcCheckAndMurder(target);
                break;
            case CustomRoles.SwordsMan when hasKillTarget:
                pc.AddKCDAsAbilityCD();
                if (SwordsMan.OnCheckMurder(pc)) pc.RpcCheckAndMurder(target);
                break;
            case CustomRoles.Witness when hasKillTarget:
                pc.AddKCDAsAbilityCD();
                if (Main.AllKillers.ContainsKey(target.PlayerId))
                    pc.Notify(GetString("WitnessFoundKiller"));
                else pc.Notify(GetString("WitnessFoundInnocent"));
                break;
            case CustomRoles.Medic when hasKillTarget:
                Medic.OnCheckMurderFormedicaler(pc, target);
                pc.AddKCDAsAbilityCD();
                break;
            case CustomRoles.Monarch when hasKillTarget:
                pc.AddKCDAsAbilityCD();
                Monarch.OnCheckMurder(pc, target);
                break;
            case CustomRoles.CopyCat when hasKillTarget:
                pc.AddKCDAsAbilityCD();
                if (CopyCat.OnCheckMurder(pc, target)) pc.RpcCheckAndMurder(target);
                break;
            case CustomRoles.Farseer when hasKillTarget:
                pc.AddAbilityCD(Farseer.FarseerRevealTime.GetInt());
                if (!Main.isRevealed[(pc.PlayerId, target.PlayerId)] && !Farseer.FarseerTimer.ContainsKey(pc.PlayerId))
                {
                    Farseer.FarseerTimer.TryAdd(pc.PlayerId, (target, 0f));
                    Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: target, ForceLoop: true);
                    RPC.SetCurrentRevealTarget(pc.PlayerId, target.PlayerId);
                }

                break;
            case CustomRoles.Deputy when hasKillTarget:
                pc.AddKCDAsAbilityCD();
                Deputy.OnCheckMurder(pc, target);
                break;
            case CustomRoles.Crusader when hasKillTarget:
                pc.AddKCDAsAbilityCD();
                Crusader.OnCheckMurder(pc, target);
                break;

            // Impostors

            case CustomRoles.Sniper:
                Sniper.OnShapeshift(pc, !Sniper.IsAim[pc.PlayerId]);
                break;
            case CustomRoles.FireWorks:
                FireWorks.ShapeShiftState(pc, true);
                break;
            case CustomRoles.Assassin:
                Assassin.OnShapeshift(pc, true);
                break;
            case CustomRoles.Undertaker:
                Undertaker.OnShapeshift(pc, true);
                break;
            case CustomRoles.RiftMaker:
                RiftMaker.OnShapeshift(pc, true);
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
            case CustomRoles.Swiftclaw:
                Swiftclaw.OnPet(pc);
                break;

            case CustomRoles.Refugee when hasKillTarget:
                pc.AddKCDAsAbilityCD();
                pc.RpcCheckAndMurder(target);
                pc.SetKillCooldown();
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

                if (Options.ArsonistCanIgniteAnytime.GetBool())
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
                    }
                }

                break;

            case CustomRoles.Necromancer when hasKillTarget && Main.KillTimers[pc.PlayerId] <= 0:
                if (pc.Data.RoleType != RoleTypes.Impostor) pc.AddKCDAsAbilityCD();
                Necromancer.OnCheckMurder(pc, target);
                break;
            case CustomRoles.Deathknight when hasKillTarget:
                if (pc.Data.RoleType != RoleTypes.Impostor) pc.AddKCDAsAbilityCD();
                Deathknight.OnCheckMurder(pc, target);
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
                        CustomRoleTypes.Crewmate => x == 1 ? "Crew" : pc.GetTaskState().hasTasks && pc.GetTaskState().IsTaskFinished ? "CrewTaskDone" : "CrewWithTasksLeft",
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