using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmongUs.GameOptions;
using HarmonyLib;
using Hazel;
using InnerNet;
using TMPro;
using TOHE.Modules;
using TOHE.Roles.AddOns.Common;
using TOHE.Roles.AddOns.Crewmate;
using TOHE.Roles.AddOns.Impostor;
using TOHE.Roles.Crewmate;
using TOHE.Roles.Impostor;
using TOHE.Roles.Neutral;
using UnityEngine;
using static TOHE.Translator;
using static TOHE.Utils;
using Object = UnityEngine.Object;

namespace TOHE;

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckProtect))]
class CheckProtectPatch
{
    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
    {
        if (!AmongUsClient.Instance.AmHost) return false;
        Logger.Info("CheckProtect: " + __instance.GetNameWithRole().RemoveHtmlTags() + " => " + target.GetNameWithRole().RemoveHtmlTags(), "CheckProtect");

        if (__instance.Is(CustomRoles.EvilSpirit))
        {
            if (target.Is(CustomRoles.Spiritcaller))
            {
                Spiritcaller.ProtectSpiritcaller();
            }
            else
            {
                Spiritcaller.HauntPlayer(target);
            }

            __instance.RpcResetAbilityCooldown();
            return true;
        }

        if (__instance.Is(CustomRoles.Sheriff))
        {
            if (__instance.Data.IsDead)
            {
                Logger.Info("Blocked", "CheckProtect");
                return false;
            }
        }

        return true;
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CmdCheckMurder))] // Modded
class CmdCheckMurderPatch
{
    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
    {
        if (!AmongUsClient.Instance.AmHost) return true;
        return CheckMurderPatch.Prefix(__instance, target);
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckMurder))] // Vanilla
class CheckMurderPatch
{
    public static Dictionary<byte, float> TimeSinceLastKill = [];

    public static void Update()
    {
        for (byte i = 0; i < 15; i++)
        {
            if (TimeSinceLastKill.ContainsKey(i))
            {
                TimeSinceLastKill[i] += Time.deltaTime;
                if (15f < TimeSinceLastKill[i]) TimeSinceLastKill.Remove(i);
            }
        }
    }

    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
    {
        if (!AmongUsClient.Instance.AmHost) return false;

        var killer = __instance; // alternative variable

        Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} => {target.GetNameWithRole().RemoveHtmlTags()}", "CheckMurder");

        if (killer.Data.IsDead)
        {
            Logger.Info($"Killer {killer.GetNameWithRole().RemoveHtmlTags()} is dead, kill canceled", "CheckMurder");
            return false;
        }

        if (target.Is(CustomRoles.Detour))
        {
            var tempTarget = target;
            target = Main.AllAlivePlayerControls.Where(x => x.PlayerId != target.PlayerId && x.PlayerId != killer.PlayerId).MinBy(x => Vector2.Distance(x.Pos(), target.Pos()));
            Logger.Info($"Target was {tempTarget.GetNameWithRole()}, new target is {target.GetNameWithRole()}", "Detour");
        }

        if (target.Data == null
            || target.inVent
            || target.inMovingPlat
            || target.MyPhysics.Animations.IsPlayingEnterVentAnimation()
            || target.MyPhysics.Animations.IsPlayingAnyLadderAnimation()
            || target.onLadder
           )
        {
            Logger.Info("The target is in a state where they cannot be killed, kill canceled.", "CheckMurder");
            return false;
        }

        if (target.Data.IsDead)
        {
            Logger.Info("Target is already dead, kill canceled", "CheckMurder");
            return false;
        }

        if (MeetingHud.Instance != null)
        {
            Logger.Info("Kill during meeting, canceled", "CheckMurder");
            return false;
        }

        var divice = Options.CurrentGameMode is CustomGameMode.SoloKombat or CustomGameMode.FFA or CustomGameMode.MoveAndStop or CustomGameMode.HotPotato ? 3000f : 2000f;
        float minTime = Mathf.Max(0.02f, AmongUsClient.Instance.Ping / divice * 6f); // The value of AmongUsClient.Instance.Ping is in milliseconds (ms), so ÷1000
        // No value is stored in TimeSinceLastKill || Stored time is greater than or equal to minTime => Allow kill
        // ↓ If not allowed
        if (TimeSinceLastKill.TryGetValue(killer.PlayerId, out var time) && time < minTime)
        {
            Logger.Info("Last kill was too shortly before, canceled", "CheckMurder");
            return false;
        }

        TimeSinceLastKill[killer.PlayerId] = 0f;
        if (target.Is(CustomRoles.Diseased))
        {
            if (!Main.KilledDiseased.TryAdd(killer.PlayerId, 1))
            {
                // Key already exists, update the value
                Main.KilledDiseased[killer.PlayerId] += 1;
            }
            // Key doesn't exist, add the key-value pair
        }

        if (target.Is(CustomRoles.Antidote))
        {
            if (!Main.KilledAntidote.TryAdd(killer.PlayerId, 1))
            {
                // Key already exists, update the value
                Main.KilledAntidote[killer.PlayerId] += 1; // Main.AllPlayerKillCooldown.TryGetValue(killer.PlayerId, out float kcd) ? (kcd - Options.AntidoteCDOpt.GetFloat() > 0 ? kcd - Options.AntidoteCDOpt.GetFloat() : 0f) : 0f;
            }
            // Key doesn't exist, add the key-value pair
            // Main.AllPlayerKillCooldown.TryGetValue(killer.PlayerId, out float kcd) ? (kcd - Options.AntidoteCDOpt.GetFloat() > 0 ? kcd - Options.AntidoteCDOpt.GetFloat() : 0f) : 0f);
        }

        killer.ResetKillCooldown();

        //キル可能判定
        if (killer.PlayerId != target.PlayerId && !killer.CanUseKillButton())
        {
            Logger.Info(killer.GetNameWithRole().RemoveHtmlTags() + " cannot use their kill button, the kill was blocked", "CheckMurder");
            return false;
        }

        switch (Options.CurrentGameMode)
        {
            case CustomGameMode.SoloKombat:
                SoloKombatManager.OnPlayerAttack(killer, target);
                return false;
            case CustomGameMode.FFA:
                FFAManager.OnPlayerAttack(killer, target);
                return true;
            case CustomGameMode.MoveAndStop:
            case CustomGameMode.HotPotato:
                return false;
        }

        if (Mastermind.ManipulatedPlayers.ContainsKey(killer.PlayerId))
        {
            return Mastermind.ForceKillForManipulatedPlayer(killer, target);
        }

        if (target.Is(CustomRoles.Spy)) Spy.OnKillAttempt(killer, target);

        if (Penguin.IsVictim(target)) return false;

        //実際のキラーとkillerが違う場合の入れ替え処理
        if (Sniper.IsEnable) Sniper.TryGetSniper(target.PlayerId, ref killer);
        if (killer != __instance) Logger.Info($"Real Killer: {killer.GetNameWithRole().RemoveHtmlTags()}", "CheckMurder");

        //鹈鹕肚子里的人无法击杀
        if (Pelican.IsEaten(target.PlayerId))
            return false;

        //阻止对活死人的操作

        // 赝品检查
        //if (Counterfeiter.OnClientMurder(killer)) return false;
        if (Glitch.hackedIdList.ContainsKey(killer.PlayerId))
        {
            killer.Notify(string.Format(GetString("HackedByGlitch"), "Kill"));
            return false;
        }

        if (Pursuer.IsEnable && Pursuer.OnClientMurder(killer)) return false;

        if (killer.PlayerId != target.PlayerId)
        {
            switch (killer.GetCustomRole())
            {
                case CustomRoles.BountyHunter:
                    BountyHunter.OnCheckMurder(killer, target);
                    break;
                case CustomRoles.Kamikaze:
                    if (!Kamikaze.OnCheckMurder(killer, target)) return false;
                    break;
                case CustomRoles.Reckless:
                    Reckless.OnCheckMurder(killer);
                    break;
                case CustomRoles.YinYanger:
                    if (!YinYanger.OnCheckMurder(killer, target)) return false;
                    break;
                case CustomRoles.Magician:
                    Magician.OnCheckMurder(killer);
                    break;
                case CustomRoles.Analyzer:
                    Analyzer.OnCheckMurder(killer, target);
                    return false;
                case CustomRoles.DonutDelivery:
                    DonutDelivery.OnCheckMurder(killer, target);
                    return false;
                case CustomRoles.Mycologist:
                    if (!Mycologist.OnCheckMurder(target)) return false;
                    break;
                case CustomRoles.Bubble:
                    Bubble.OnCheckMurder(target);
                    return false;
                case CustomRoles.Hookshot:
                    if (!Hookshot.OnCheckMurder(target)) return false;
                    break;
                case CustomRoles.Gaulois:
                    Gaulois.OnCheckMurder(killer, target);
                    return false;
                case CustomRoles.Escort:
                    Escort.OnCheckMurder(killer, target);
                    return false;
                case CustomRoles.WeaponMaster:
                    if (!WeaponMaster.OnCheckMurder(killer, target)) return false;
                    break;
                case CustomRoles.Cantankerous:
                    if (!Cantankerous.OnCheckMurder(killer)) return false;
                    break;
                case CustomRoles.Postman:
                    Postman.OnCheckMurder(killer, target);
                    return false;
                case CustomRoles.SoulHunter:
                    if (!SoulHunter.OnCheckMurder(target)) return false;
                    break;
                case CustomRoles.Vengeance:
                    if (!Vengeance.OnCheckMurder(killer, target)) return false;
                    break;
                case CustomRoles.Stealth:
                    Stealth.OnCheckMurder(killer, target);
                    break;
                case CustomRoles.Consort:
                    if (!Consort.OnCheckMurder(killer, target)) return false;
                    break;
                case CustomRoles.Mafioso:
                    if (!Mafioso.OnCheckMurder(killer, target)) return false;
                    break;
                case CustomRoles.Nullifier:
                    if (Nullifier.OnCheckMurder(killer, target)) return false;
                    break;
                case CustomRoles.Chronomancer:
                    Chronomancer.OnCheckMurder(killer, target);
                    break;
                case CustomRoles.Penguin:
                    if (!Penguin.OnCheckMurderAsKiller(target)) return false;
                    break;
                case CustomRoles.Sapper:
                    return false;
                case CustomRoles.Saboteur:
                    if (Math.Abs(Main.AllPlayerKillCooldown[killer.PlayerId] - Options.SaboteurCD.GetFloat()) > 0.5f)
                    {
                        Main.AllPlayerKillCooldown[killer.PlayerId] = Options.SaboteurCD.GetFloat();
                        killer.SyncSettings();
                    }

                    break;
                case CustomRoles.Gambler:
                    if (!Gambler.OnCheckMurder(killer, target)) return false;
                    break;
                case CustomRoles.Mastermind:
                    if (!Mastermind.OnCheckMurder(killer, target)) return false;
                    break;
                case CustomRoles.Hitman:
                    if (!Hitman.OnCheckMurder(killer, target)) return false;
                    break;
                case CustomRoles.Aid:
                    if (!Aid.OnCheckMurder(killer, target)) return false;
                    break;
                case CustomRoles.HeadHunter:
                    HeadHunter.OnCheckMurder(killer, target);
                    break;
                case CustomRoles.SerialKiller:
                    SerialKiller.OnCheckMurder(killer);
                    break;
                case CustomRoles.Glitch:
                    if (!Glitch.OnCheckMurder(killer, target)) return false;
                    break;
                case CustomRoles.Eraser:
                    if (!Eraser.OnCheckMurder(killer, target)) return false;
                    break;
                case CustomRoles.Bandit:
                    if (!Bandit.OnCheckMurder(killer, target)) return false;
                    break;
                case CustomRoles.Vampire:
                    if (!Vampire.OnCheckMurder(killer, target)) return false;
                    break;
                case CustomRoles.Pyromaniac:
                    if (!Pyromaniac.OnCheckMurder(killer, target)) return false;
                    break;
                case CustomRoles.Agitater:
                    if (!Agitater.OnCheckMurder(killer, target))
                        return false;
                    break;
                case CustomRoles.Eclipse:
                    Eclipse.OnCheckMurder(killer);
                    break;
                case CustomRoles.Jailor:
                    if (!Jailor.OnCheckMurder(killer, target))
                        return false;
                    break;
                case CustomRoles.Poisoner:
                    if (!Poisoner.OnCheckMurder(killer, target)) return false;
                    break;
                case CustomRoles.Assassin:
                    if (!Assassin.OnCheckMurder(killer, target)) return false;
                    break;
                case CustomRoles.Undertaker:
                    if (!Undertaker.OnCheckMurder(killer, target)) return false;
                    break;
                //case CustomRoles.Famine:
                //    Baker.FamineKilledTasks(target.PlayerId);
                //    break;

                case CustomRoles.Witch:
                    if (!Witch.OnCheckMurder(killer, target)) return false;
                    break;
                case CustomRoles.HexMaster:
                    if (!HexMaster.OnCheckMurder(killer, target)) return false;
                    break;
                case CustomRoles.PlagueDoctor:
                    if (!PlagueDoctor.OnPDinfect(killer, target)) return false;
                    break;
                case CustomRoles.Gangster:
                    if (Gangster.OnCheckMurder(killer, target))
                        return false;
                    break;
                case CustomRoles.BallLightning:
                    if (BallLightning.CheckBallLightningMurder(killer, target))
                        return false;
                    break;
                case CustomRoles.Greedier:
                    Greedier.OnCheckMurder(killer);
                    break;
                case CustomRoles.Imitator:
                    Imitator.OnCheckMurder(killer);
                    break;
                case CustomRoles.QuickShooter:
                    QuickShooter.QuickShooterKill(killer);
                    break;
                case CustomRoles.Sans:
                    Sans.OnCheckMurder(killer);
                    break;
                case CustomRoles.Juggernaut:
                    Juggernaut.OnCheckMurder(killer);
                    break;
                //case CustomRoles.Reverie:
                //    Reverie.OnCheckMurder(killer);
                //    break;
                case CustomRoles.Hangman:
                    if (!Hangman.OnCheckMurder(killer, target)) return false;
                    break;
                case CustomRoles.Swooper:
                    if (!Swooper.OnCheckMurder(killer, target)) return false;
                    break;
                case CustomRoles.Wraith:
                    if (!Wraith.OnCheckMurder(killer, target)) return false;
                    break;
                case CustomRoles.Werewolf:
                    Werewolf.OnCheckMurder(killer, target);
                    break;
                case CustomRoles.Lurker:
                    Lurker.OnCheckMurder(killer);
                    break;
                case CustomRoles.Crusader:
                    Crusader.OnCheckMurder(killer, target);
                    return false;

                //==========中立阵营==========//
                case CustomRoles.PlagueBearer:
                    if (!PlagueBearer.OnCheckMurder(killer, target))
                        return false;
                    break;
                //case CustomRoles.Pirate:
                //    if (!Pirate.OnCheckMurder(killer, target))
                //        return false;
                //    break;

                case CustomRoles.Arsonist:
                    killer.SetKillCooldown(Options.ArsonistDouseTime.GetFloat());
                    if (!Main.isDoused[(killer.PlayerId, target.PlayerId)] && !Main.ArsonistTimer.ContainsKey(killer.PlayerId))
                    {
                        Main.ArsonistTimer.Add(killer.PlayerId, (target, 0f));
                        NotifyRoles(SpecifySeer: __instance, SpecifyTarget: target, ForceLoop: true);
                        RPC.SetCurrentDousingTarget(killer.PlayerId, target.PlayerId);
                    }

                    return false;
                case CustomRoles.Revolutionist:
                    killer.SetKillCooldown(Options.RevolutionistDrawTime.GetFloat());
                    if (!Main.isDraw[(killer.PlayerId, target.PlayerId)] && !Main.RevolutionistTimer.ContainsKey(killer.PlayerId))
                    {
                        Main.RevolutionistTimer.TryAdd(killer.PlayerId, (target, 0f));
                        NotifyRoles(SpecifySeer: __instance, SpecifyTarget: target, ForceLoop: true);
                        RPC.SetCurrentDrawTarget(killer.PlayerId, target.PlayerId);
                    }

                    return false;
                case CustomRoles.Pelican:
                    if (Pelican.CanEat(killer, target.PlayerId))
                    {
                        Pelican.EatPlayer(killer, target);
                        //killer.RpcGuardAndKill(killer);
                        killer.SetKillCooldown();
                        killer.RPCPlayCustomSound("Eat");
                        target.RPCPlayCustomSound("Eat");
                    }

                    return false;
                case CustomRoles.FFF:
                    if (!FFF.OnCheckMurder(killer, target)) return false;
                    break;
                case CustomRoles.Gamer:
                    Gamer.CheckGamerMurder(killer, target);
                    return false;
                case CustomRoles.DarkHide:
                    DarkHide.OnCheckMurder(killer, target);
                    break;
                case CustomRoles.Provocateur:
                    Main.PlayerStates[target.PlayerId].deathReason = PlayerState.DeathReason.PissedOff;
                    killer.Kill(target);
                    //killer.Kill(killer);
                    //killer.SetRealKiller(target);
                    Main.Provoked.TryAdd(killer.PlayerId, target.PlayerId);
                    return false;
                case CustomRoles.Totocalcio:
                    Totocalcio.OnCheckMurder(killer, target);
                    return false;
                case CustomRoles.Romantic:
                    if (!Romantic.OnCheckMurder(killer, target)) return false;
                    break;
                case CustomRoles.VengefulRomantic:
                    if (!VengefulRomantic.OnCheckMurder(killer, target)) return false;
                    break;
                case CustomRoles.Succubus:
                    Succubus.OnCheckMurder(killer, target);
                    return false;
                case CustomRoles.Necromancer:
                    Necromancer.OnCheckMurder(killer, target);
                    return false;
                case CustomRoles.Deathknight:
                    Deathknight.OnCheckMurder(killer, target);
                    return false;
                //case CustomRoles.CursedSoul:
                //    CursedSoul.OnCheckMurder(killer, target);
                //    return false;
                case CustomRoles.Amnesiac:
                    Amnesiac.OnCheckMurder(killer, target);
                    return false;
                case CustomRoles.Monarch:
                    Monarch.OnCheckMurder(killer, target);
                    return false;
                case CustomRoles.Deputy:
                    Deputy.OnCheckMurder(killer, target);
                    return false;
                case CustomRoles.Jackal:
                    if (Jackal.OnCheckMurder(killer, target))
                        return false;
                    break;

                //==========船员职业==========//
                case CustomRoles.Sheriff:
                    if (!Sheriff.OnCheckMurder(killer, target))
                        return false;
                    break;
                case CustomRoles.CopyCat:
                    if (!CopyCat.OnCheckMurder(killer, target))
                        return false;
                    break;

                case CustomRoles.SwordsMan:
                    if (!SwordsMan.OnCheckMurder(killer))
                        return false;
                    break;
                case CustomRoles.Medic:
                    Medic.OnCheckMurderFormedicaler(killer, target);
                    return false;
                //case CustomRoles.Counterfeiter:
                //    if (Counterfeiter.CanBeClient(target) && Counterfeiter.CanSeel(killer.PlayerId))
                //        Counterfeiter.SeelToClient(killer, target);
                //    return false;
                case CustomRoles.Pursuer:
                    if (Pursuer.CanBeClient(target) && Pursuer.CanSeel(killer.PlayerId))
                        Pursuer.SeelToClient(killer, target);
                    return false;
                case CustomRoles.EvilDiviner:
                    if (!EvilDiviner.OnCheckMurder(killer, target)) return false;
                    break;
                case CustomRoles.Ritualist:
                    if (!Ritualist.OnCheckMurder(killer, target)) return false;
                    break;
            }
        }

        if (!killer.RpcCheckAndMurder(target, true))
            return false;

        if (killer.Is(CustomRoles.Virus)) Virus.OnCheckMurder( /*killer,*/ target);
        else if (killer.Is(CustomRoles.Spiritcaller)) Spiritcaller.OnCheckMurder(target);

        if (killer.Is(CustomRoles.Unlucky))
        {
            var Ue = IRandom.Instance;
            if (Ue.Next(0, 100) < Options.UnluckyKillSuicideChance.GetInt())
            {
                killer.Suicide();
                return false;
            }
        }

        if (killer.Is(CustomRoles.Mare))
        {
            killer.ResetKillCooldown();
        }

        if (!DoubleTrigger.FirstTriggerTimer.ContainsKey(killer.PlayerId) && killer.Is(CustomRoles.Swift) && !target.Is(CustomRoles.Pestilence))
        {
            if (killer.RpcCheckAndMurder(target, true))
            {
                target.Suicide(PlayerState.DeathReason.Kill, killer);
                killer.SetKillCooldown();
            }

            RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
            return false;
        }

        if (killer.Is(CustomRoles.Magnet) && !target.Is(CustomRoles.Pestilence))
        {
            target.TP(killer);
            _ = new LateTask(() => { killer.RpcCheckAndMurder(target); }, 0.1f, log: false);
            return false;
        }

        //==Kill processing==
        __instance.Kill(target);
        //===================

        return false;
    }

    public static bool RpcCheckAndMurder(PlayerControl killer, PlayerControl target, bool check = false)
    {
        if (!AmongUsClient.Instance.AmHost) return false;
        if (target == null) target = killer;

        //Jackal can kill Sidekick
        if (killer.Is(CustomRoles.Jackal) && target.Is(CustomRoles.Sidekick) && !Options.JackalCanKillSidekick.GetBool())
            return false;
        //Sidekick can kill Jackal
        if (killer.Is(CustomRoles.Sidekick) && target.Is(CustomRoles.Jackal) && !Options.SidekickCanKillJackal.GetBool())
            return false;
        if (killer.Is(CustomRoles.Jackal) && target.Is(CustomRoles.Recruit) && !Options.JackalCanKillSidekick.GetBool())
            return false;
        //Sidekick can kill Jackal
        if (killer.Is(CustomRoles.Recruit) && target.Is(CustomRoles.Jackal) && !Options.SidekickCanKillJackal.GetBool())
            return false;
        //禁止内鬼刀叛徒
        if (killer.Is(CustomRoleTypes.Impostor) && target.Is(CustomRoles.Madmate) && !Options.ImpCanKillMadmate.GetBool())
            return false;

        //if ((Romantic.BetPlayer.TryGetValue(target.PlayerId, out var RomanticPartner) && target.PlayerId == RomanticPartner && Romantic.isPartnerProtected))
        //    return false;

        // Romantic partner is protected
        if (Romantic.PartnerId == target.PlayerId && Romantic.IsPartnerProtected) return false;

        if (Options.OppoImmuneToAttacksWhenTasksDone.GetBool())
        {
            if (target.Is(CustomRoles.Opportunist) && target.AllTasksCompleted())
                return false;
        }

        if (Merchant.OnClientMurder(killer, target)) return false;

        if (Medic.OnAnyoneCheckMurder(killer, target))
            return false;

        if (Mathematician.State.ProtectedPlayerId == target.PlayerId) return false;

        if (Randomizer.IsShielded(target)) return false;

        if (Penguin.IsVictim(target)) return false;

        if (killer.Is(CustomRoles.Refugee) && target.Is(CustomRoleTypes.Impostor)) return false;

        if (SoulHunter.IsTargetBlocked && SoulHunter.CurrentTarget.ID == killer.PlayerId && target.Is(CustomRoles.SoulHunter))
        {
            killer.Notify(GetString("SoulHunterTargetNotifyNoKill"));
            _ = new LateTask(() =>
            {
                if (SoulHunter.CurrentTarget.ID == killer.PlayerId) killer.Notify(string.Format(GetString("SoulHunterTargetNotify"), SoulHunter.SoulHunter_.GetRealName()), 300f);
            }, 4f, log: false);
            return false;
        }

        // Traitor can't kill Impostors but Impostors can kill it
        if (killer.Is(CustomRoles.Traitor) && target.Is(CustomRoleTypes.Impostor))
            return false;

        //禁止叛徒刀内鬼
        if (killer.Is(CustomRoles.Madmate) && target.Is(CustomRoleTypes.Impostor) && !Options.MadmateCanKillImp.GetBool())
            return false;
        //Sidekick can kill Sidekick
        if (killer.Is(CustomRoles.Sidekick) && target.Is(CustomRoles.Sidekick) && !Options.SidekickCanKillSidekick.GetBool())
            return false;
        //Recruit can kill Recruit
        if (killer.Is(CustomRoles.Recruit) && target.Is(CustomRoles.Recruit) && !Options.SidekickCanKillSidekick.GetBool())
            return false;
        //Sidekick can kill Sidekick
        if (killer.Is(CustomRoles.Recruit) && target.Is(CustomRoles.Sidekick) && !Options.SidekickCanKillSidekick.GetBool())
            return false;
        //Recruit can kill Recruit
        if (killer.Is(CustomRoles.Sidekick) && target.Is(CustomRoles.Recruit) && !Options.SidekickCanKillSidekick.GetBool())
            return false;

        if (PlagueBearer.OnCheckMurderPestilence(killer, target))
            return false;

        if (Jackal.ResetKillCooldownWhenSbGetKilled.GetBool() && !killer.Is(CustomRoles.Sidekick) && !target.Is(CustomRoles.Sidekick) && !killer.Is(CustomRoles.Jackal) && !target.Is(CustomRoles.Jackal) && !GameStates.IsMeeting)
            Jackal.AfterPlayerDiedTask(killer);

        if (target.Is(CustomRoles.Lucky))
        {
            var rd = IRandom.Instance;
            if (rd.Next(0, 100) < Options.LuckyProbability.GetInt())
            {
                killer.SetKillCooldown(15f);
                return false;
            }
        }

        if (Main.ForCrusade.Contains(target.PlayerId))
        {
            foreach (PlayerControl player in Main.AllPlayerControls)
            {
                if (player.Is(CustomRoles.Crusader) && player.IsAlive() && !killer.Is(CustomRoles.Pestilence) && !killer.Is(CustomRoles.Minimalism))
                {
                    player.Kill(killer);
                    Main.ForCrusade.Remove(target.PlayerId);
                    killer.RpcGuardAndKill(target);
                    return false;
                }

                if (player.Is(CustomRoles.Crusader) && player.IsAlive() && killer.Is(CustomRoles.Pestilence))
                {
                    killer.Kill(player);
                    Main.ForCrusade.Remove(target.PlayerId);
                    target.RpcGuardAndKill(killer);
                    Main.PlayerStates[player.PlayerId].deathReason = PlayerState.DeathReason.PissedOff;
                    return false;
                }
            }
        }

        if (Aid.ShieldedPlayers.ContainsKey(target.PlayerId)) return false;

        switch (target.GetCustomRole())
        {
            case CustomRoles.Medic:
                Medic.IsDead(target);
                break;
            case CustomRoles.Monarch when killer.Is(CustomRoles.Knighted):
                return false;
            case CustomRoles.WeaponMaster when WeaponMaster.OnAttack():
            case CustomRoles.Gambler when Gambler.isShielded.ContainsKey(target.PlayerId):
            case CustomRoles.Alchemist when Alchemist.IsProtected:
            case CustomRoles.Nightmare when !Nightmare.CanBeKilled:
                killer.SetKillCooldown(time: 5f);
                return false;
            case CustomRoles.Vengeance when !Vengeance.OnKillAttempt(killer, target):
                return false;
            case CustomRoles.Ricochet when !Ricochet.OnKillAttempt(killer, target):
                return false;
            case CustomRoles.Jinx:
                if (Main.JinxSpellCount[target.PlayerId] <= 0) break;
                if (killer.Is(CustomRoles.Pestilence)) break;
                if (killer == target) break;
                var kcd2 = Main.KillTimers[target.PlayerId] + Main.AllPlayerKillCooldown[target.PlayerId];
                killer.RpcGuardAndKill(target);
                Main.JinxSpellCount[target.PlayerId] -= 1;
                RPC.SendRPCJinxSpellCount(target.PlayerId);
                if (Jinx.killAttacker.GetBool())
                {
                    Logger.Info($"{target.GetNameWithRole().RemoveHtmlTags()} : {Main.JinxSpellCount[target.PlayerId]} jixes remain", "Jinx");
                    Main.PlayerStates[killer.PlayerId].deathReason = PlayerState.DeathReason.Jinx;
                    killer.SetRealKiller(target);
                    target.Kill(killer);
                }

                target.SetKillCooldown(time: kcd2);
                return false;
            case CustomRoles.Gamer:
                if (!Gamer.CheckMurder(killer, target))
                    return false;
                break;
            case CustomRoles.BloodKnight:
                if (BloodKnight.InProtect(target.PlayerId))
                {
                    killer.RpcGuardAndKill(target);
                    target.Notify(GetString("BKOffsetKill"));
                    return false;
                }

                break;
            case CustomRoles.Spiritcaller:
                if (Spiritcaller.InProtect(target))
                {
                    killer.RpcGuardAndKill(target);
                    return false;
                }

                break;
        }

        // Bodyguard
        if (killer.PlayerId != target.PlayerId && (from pc1 in Main.AllAlivePlayerControls.Where(x => x.PlayerId != target.PlayerId) let pos1 = target.Pos() let dis = Vector2.Distance(pos, pc.Pos()) where !(dis > Options.BodyguardProtectRadius.GetFloat()) select pc).Any(pc => pc.Is(CustomRoles.Bodyguard) && !Bodyguard.OnCheckMurderAsTarget(pc, killer)))
        {
            return false;
        }

        if (Main.ShieldPlayer != byte.MaxValue && Main.ShieldPlayer == target.PlayerId && IsAllAlive)
        {
            Main.ShieldPlayer = byte.MaxValue;
            killer.SetKillCooldown(15f);
            killer.Notify(GetString("TriedToKillLastGameFirstKill"), 10f);
            return false;
        }

        if (Options.MadmateSpawnMode.GetInt() == 1 && Main.MadmateNum < CustomRoles.Madmate.GetCount() && target.CanBeMadmate())
        {
            Main.MadmateNum++;
            target.RpcSetCustomRole(CustomRoles.Madmate);
            ExtendedPlayerControl.RpcSetCustomRole(target.PlayerId, CustomRoles.Madmate);
            target.Notify(ColorString(GetRoleColor(CustomRoles.Madmate), GetString("BecomeMadmateCuzMadmateMode")));
            killer.SetKillCooldown();
            killer.RpcGuardAndKill(target);
            target.RpcGuardAndKill(killer);
            target.RpcGuardAndKill(target);
            Logger.Info("Add-on assigned:" + target?.Data?.PlayerName + " = " + target.GetCustomRole() + " + " + CustomRoles.Madmate, "Assign " + CustomRoles.Madmate);
            return false;
        }

        if (Sentinel.IsEnable)
        {
            if (!Sentinel.OnAnyoneCheckMurder(killer))
                return false;
        }

        if (!check) killer.Kill(target);
        if (killer.Is(CustomRoles.Doppelganger)) Doppelganger.OnCheckMurder(killer, target);
        return true;
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
class MurderPlayerPatch
{
    public static void Prefix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target /*, [HarmonyArgument(1)] MurderResultFlags resultFlags*/)
    {
        Logger.Info($"{__instance.GetNameWithRole().RemoveHtmlTags()} => {target.GetNameWithRole().RemoveHtmlTags()}{(target.IsProtected() ? " (Protected)" : string.Empty)}", "MurderPlayer");

        if (RandomSpawn.CustomNetworkTransformPatch.NumOfTP.TryGetValue(__instance.PlayerId, out var num) && num > 2) RandomSpawn.CustomNetworkTransformPatch.NumOfTP[__instance.PlayerId] = 3;

        if (!target.IsProtected() && !Doppelganger.DoppelVictim.ContainsKey(target.PlayerId) && !Camouflage.ResetSkinAfterDeathPlayers.Contains(target.PlayerId))
        {
            Camouflage.ResetSkinAfterDeathPlayers.Add(target.PlayerId);
            Camouflage.RpcSetSkin(target, ForceRevert: true);
        }
    }

    public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
    {
        if (target.AmOwner) RemoveDisableDevicesPatch.UpdateDisableDevices();
        if (!target.Data.IsDead || !AmongUsClient.Instance.AmHost) return;

        if (Main.OverDeadPlayerList.Contains(target.PlayerId)) return;

        PlayerControl killer = __instance; // Alternative variable
        if (target.PlayerId == Godfather.GodfatherTarget) killer.RpcSetCustomRole(CustomRoles.Refugee);

        PlagueDoctor.OnAnyMurder();

        // Replacement process when the actual killer and killer are different
        if (Sniper.IsEnable)
        {
            if (Sniper.TryGetSniper(target.PlayerId, ref killer))
            {
                Main.PlayerStates[target.PlayerId].deathReason = PlayerState.DeathReason.Sniped;
            }
        }

        if (killer.Is(CustomRoles.Sniper))
            if (!Options.UsePets.GetBool()) killer.RpcResetAbilityCooldown();
            else Main.AbilityCD[killer.PlayerId] = (TimeStamp, Options.DefaultShapeshiftCooldown.GetInt());

        if (killer != __instance)
        {
            Logger.Info($"Real Killer = {killer.GetNameWithRole().RemoveHtmlTags()}", "MurderPlayer");
        }

        if (Main.PlayerStates[target.PlayerId].deathReason == PlayerState.DeathReason.etc)
        {
            //If the cause of death is not specified, it is determined as a normal kill.
            Main.PlayerStates[target.PlayerId].deathReason = PlayerState.DeathReason.Kill;
        }

        //Let’s see if Youtuber was stabbed first
        if (Main.FirstDied == byte.MaxValue && target.Is(CustomRoles.Youtuber))
        {
            CustomSoundsManager.RPCPlayCustomSoundAll("Congrats");
            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Youtuber);
            CustomWinnerHolder.WinnerIds.Add(target.PlayerId);
        }

        //Record the first blow
        if (Main.FirstDied == byte.MaxValue)
            Main.FirstDied = target.PlayerId;

        if (Postman.Target == target.PlayerId)
        {
            Postman.OnTargetDeath();
        }

        if (target.Is(CustomRoles.Trapper) && killer != target)
            killer.TrapperKilled(target);

        //if ((Romantic.BetPlayer.TryGetValue(target.PlayerId, out var RomanticPartner) && target.PlayerId == RomanticPartner) && target.PlayerId != killer.PlayerId)
        //    VengefulRomantic.PartnerKiller.Add(killer.PlayerId, 1);

        Witness.AllKillers.Remove(killer.PlayerId);
        Witness.AllKillers.Add(killer.PlayerId, TimeStamp);

        killer.AddKillTimerToDict();

        switch (target.GetCustomRole())
        {
            case CustomRoles.BallLightning:
                if (killer != target) BallLightning.MurderPlayer(killer, target);
                break;
            case CustomRoles.Altruist:
                if (killer != target) Altruist.OnKilled(killer);
                break;
        }

        switch (killer.GetCustomRole())
        {
            case CustomRoles.SwordsMan:
                if (killer != target)
                    SwordsMan.OnMurder(killer);
                break;
            case CustomRoles.BloodKnight:
                BloodKnight.OnMurderPlayer(killer, target);
                break;
            case CustomRoles.Maverick:
                Maverick.NumOfKills++;
                break;
            case CustomRoles.Wildling:
                Wildling.OnMurderPlayer(killer, target);
                break;
        }

        if (killer.Is(CustomRoles.TicketsStealer) && killer.PlayerId != target.PlayerId)
            killer.Notify(string.Format(GetString("TicketsStealerGetTicket"), ((Main.AllPlayerControls.Count(x => x.GetRealKiller()?.PlayerId == killer.PlayerId) + 1) * Options.TicketsPerKill.GetFloat()).ToString("0.0#####")));

        if (killer.Is(CustomRoles.Pickpocket) && killer.PlayerId != target.PlayerId)
            killer.Notify(string.Format(GetString("PickpocketGetVote"), ((Main.AllPlayerControls.Count(x => x.GetRealKiller()?.PlayerId == killer.PlayerId) + 1) * Pickpocket.VotesPerKill.GetFloat()).ToString("0.0#####")));

        if (target.Is(CustomRoles.Avanger))
        {
            var pcList = Main.AllAlivePlayerControls.Where(x => x.PlayerId != target.PlayerId).ToArray();
            var rp = pcList[IRandom.Instance.Next(0, pcList.Length)];
            if (!rp.Is(CustomRoles.Pestilence))
            {
                rp.Suicide(PlayerState.DeathReason.Revenge, target);
            }
        }

        if (target.Is(CustomRoles.Bait))
        {
            if (killer.PlayerId != target.PlayerId || (target.GetRealKiller()?.GetCustomRole() is CustomRoles.Swooper or CustomRoles.Wraith) || !killer.Is(CustomRoles.Oblivious) || (killer.Is(CustomRoles.Oblivious) && !Options.ObliviousBaitImmune.GetBool()))
            {
                killer.RPCPlayCustomSound("Congrats");
                target.RPCPlayCustomSound("Congrats");
                float delay;
                if (Options.BaitDelayMax.GetFloat() < Options.BaitDelayMin.GetFloat()) delay = 0f;
                else delay = IRandom.Instance.Next((int)Options.BaitDelayMin.GetFloat(), (int)Options.BaitDelayMax.GetFloat() + 1);
                delay = Math.Max(delay, 0.15f);
                if (delay > 0.15f && Options.BaitDelayNotify.GetBool()) killer.Notify(ColorString(GetRoleColor(CustomRoles.Bait), string.Format(GetString("KillBaitNotify"), (int)delay)), delay);
                Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} 击杀诱饵 => {target.GetNameWithRole().RemoveHtmlTags()}", "MurderPlayer");
                _ = new LateTask(() =>
                {
                    if (GameStates.IsInTask) killer.CmdReportDeadBody(target.Data);
                }, delay, "Bait Self Report");
            }
        }

        if (Mediumshiper.IsEnable)
            foreach (var pc in Main.AllAlivePlayerControls.Where(x => x.Is(CustomRoles.Mediumshiper)).ToArray())
                pc.Notify(ColorString(GetRoleColor(CustomRoles.Mediumshiper), GetString("MediumshiperKnowPlayerDead")));

        AfterPlayerDeathTasks(target);

        Main.PlayerStates[target.PlayerId].SetDead();
        target.SetRealKiller(killer, true); //既に追加されてたらスキップ
        CountAlivePlayers(true);

        Camouflager.IsDead(target);
        TargetDies(__instance, target);

        if (Options.LowLoadMode.GetBool())
        {
            __instance.MarkDirtySettings();
            target.MarkDirtySettings();
            NotifyRoles(SpecifySeer: killer, SpecifyTarget: killer);
            NotifyRoles(SpecifySeer: target);
        }
        else
        {
            SyncAllSettings();
            NotifyRoles(ForceLoop: true);
        }
    }
}

// Triggered when the shapeshifter selects a target
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckShapeshift))]
class CheckShapeshiftPatch
{
    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target /*, [HarmonyArgument(1)] bool shouldAnimate*/)
    {
        return ShapeshiftPatch.ProcessShapeshift(__instance, target); // return false to cancel the shapeshift
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CmdCheckShapeshift))]
class CmdCheckShapeshiftPatch
{
    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target /*, [HarmonyArgument(1)] bool shouldAnimate*/)
    {
        return CheckShapeshiftPatch.Prefix(__instance, target /*, shouldAnimate*/);
    }
}

// Triggered when the egg animation starts playing
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Shapeshift))]
class ShapeshiftPatch
{
    public static bool ProcessShapeshift(PlayerControl shapeshifter, PlayerControl target)
    {
        if (!Main.ProcessShapeshifts) return true;

        Logger.Info($"{shapeshifter?.GetNameWithRole()} => {target?.GetNameWithRole()}", "Shapeshift");

        var shapeshifting = shapeshifter.PlayerId != target.PlayerId;

        if (Main.CheckShapeshift.TryGetValue(shapeshifter.PlayerId, out var last) && last == shapeshifting)
        {
            // Dunno how you would get here but ok
            return true;
        }

        Main.CheckShapeshift[shapeshifter.PlayerId] = shapeshifting;
        Main.ShapeshiftTarget[shapeshifter.PlayerId] = target.PlayerId;

        if (Sniper.IsEnable) Sniper.OnShapeshift(shapeshifter, shapeshifting);

        if (!AmongUsClient.Instance.AmHost) return true;
        if (!shapeshifting) Camouflage.RpcSetSkin(shapeshifter);

        bool isSSneeded = true;

        if (!Pelican.IsEaten(shapeshifter.PlayerId) && !GameStates.IsVoting)
        {
            switch (shapeshifter.GetCustomRole())
            {
                case CustomRoles.EvilTracker:
                    EvilTracker.OnShapeshift(shapeshifter, target, shapeshifting);
                    isSSneeded = false;
                    break;
                case CustomRoles.Kidnapper:
                    Kidnapper.OnShapeshift(shapeshifter, target);
                    isSSneeded = false;
                    break;
                case CustomRoles.Swapster:
                    Swapster.OnShapeshift(shapeshifter, target);
                    isSSneeded = false;
                    break;
                case CustomRoles.RiftMaker:
                    RiftMaker.OnShapeshift(shapeshifter, shapeshifting);
                    isSSneeded = false;
                    break;
                case CustomRoles.Hitman:
                    Hitman.OnShapeshift(shapeshifter, target, shapeshifting);
                    isSSneeded = false;
                    break;
                case CustomRoles.Duellist:
                    Duellist.OnShapeshift(shapeshifter, target);
                    isSSneeded = false;
                    break;
                case CustomRoles.FireWorks:
                    FireWorks.ShapeShiftState(shapeshifter, shapeshifting);
                    isSSneeded = false;
                    break;
                case CustomRoles.Penguin:
                    return false;
                case CustomRoles.Sapper:
                    Sapper.OnShapeshift(shapeshifter);
                    isSSneeded = false;
                    break;
                case CustomRoles.Blackmailer:
                    Blackmailer.ForBlackmailer.Add(target.PlayerId);
                    isSSneeded = false;
                    break;
                case CustomRoles.Librarian:
                    isSSneeded = Librarian.OnShapeshift(shapeshifter, shapeshifting);
                    break;
                case CustomRoles.Bomber:
                    if (shapeshifting)
                    {
                        Logger.Info("Bomber explosion", "Boom");
                        CustomSoundsManager.RPCPlayCustomSoundAll("Boom");
                        foreach (PlayerControl tg in Main.AllPlayerControls)
                        {
                            if (!tg.IsModClient())
                                tg.KillFlash();
                            var pos = shapeshifter.transform.position;
                            var dis = Vector2.Distance(pos, tg.Pos());
                            if (!tg.IsAlive() || Pelican.IsEaten(tg.PlayerId) || Medic.ProtectList.Contains(tg.PlayerId) || (tg.Is(CustomRoleTypes.Impostor) && Options.ImpostorsSurviveBombs.GetBool()) || tg.inVent || tg.Is(CustomRoles.Pestilence)) continue;
                            if (dis > Options.BomberRadius.GetFloat()) continue;
                            if (tg.PlayerId == shapeshifter.PlayerId) continue;
                            tg.Suicide(PlayerState.DeathReason.Bombed, shapeshifter);
                        }

                        _ = new LateTask(() =>
                        {
                            var totalAlive = Main.AllAlivePlayerControls.Length;
                            if (Options.BomberDiesInExplosion.GetBool() && totalAlive > 1 && !GameStates.IsEnded)
                            {
                                shapeshifter.Suicide(PlayerState.DeathReason.Bombed);
                            }

                            //else
                            //{
                            //    shapeshifter.CmdCheckRevertShapeshift(false);
                            //}
                            NotifyRoles(ForceLoop: true);
                        }, 1.5f, "Bomber Suiscide");
                    }

                    isSSneeded = false;
                    //if (Options.BomberDiesInExplosion.GetBool()) isSSneeded = false;
                    break;
                case CustomRoles.Nuker:
                    if (shapeshifting)
                    {
                        Logger.Info("Nuker explosion", "Boom");
                        CustomSoundsManager.RPCPlayCustomSoundAll("Boom");
                        foreach (PlayerControl tg in Main.AllPlayerControls)
                        {
                            if (!tg.IsModClient())
                                tg.KillFlash();
                            var pos = shapeshifter.transform.position;
                            var dis = Vector2.Distance(pos, tg.Pos());
                            if (!tg.IsAlive() || Pelican.IsEaten(tg.PlayerId) || Medic.ProtectList.Contains(tg.PlayerId) || tg.inVent || tg.Is(CustomRoles.Pestilence)) continue;
                            if (dis > Options.NukeRadius.GetFloat()) continue;
                            if (tg.PlayerId == shapeshifter.PlayerId) continue;
                            tg.Suicide(PlayerState.DeathReason.Bombed, shapeshifter);
                        }

                        _ = new LateTask(() =>
                        {
                            var totalAlive = Main.AllAlivePlayerControls.Length;
                            if (totalAlive > 1 && !GameStates.IsEnded)
                            {
                                shapeshifter.Suicide(PlayerState.DeathReason.Bombed);
                            }

                            //else if (!shapeshifter.IsModClient())
                            //{
                            //    shapeshifter.CmdCheckRevertShapeshift(false);
                            //}
                            NotifyRoles(ForceLoop: true);
                        }, 1.5f, "Nuke");
                    }

                    isSSneeded = false;
                    break;
                case CustomRoles.Assassin:
                    Assassin.OnShapeshift(shapeshifter, shapeshifting);
                    isSSneeded = false;
                    break;
                case CustomRoles.Undertaker:
                    Undertaker.OnShapeshift(shapeshifter, shapeshifting);
                    isSSneeded = false;
                    break;
                case CustomRoles.QuickShooter:
                    QuickShooter.OnShapeshift(shapeshifter, shapeshifting);
                    isSSneeded = false;
                    break;
                case CustomRoles.Camouflager:
                    if (shapeshifting)
                        Camouflager.OnShapeshift(shapeshifter, shapeshifting);
                    if (!shapeshifting)
                        Camouflager.OnReportDeadBody();
                    break;
                case CustomRoles.Hangman:
                    if (shapeshifter.GetAbilityUseLimit() < 1 && shapeshifting) shapeshifter.SetKillCooldown(Hangman.ShapeshiftDuration.GetFloat() + 1f);
                    break;
                case CustomRoles.Hacker:
                    Hacker.OnShapeshift(shapeshifter, shapeshifting, target);
                    isSSneeded = false;
                    break;
                case CustomRoles.Disperser:
                    if (shapeshifting)
                        Disperser.DispersePlayers(shapeshifter);
                    isSSneeded = false;
                    break;
                case CustomRoles.Dazzler:
                    if (shapeshifting)
                        Dazzler.OnShapeshift(shapeshifter, target);
                    isSSneeded = false;
                    break;
                case CustomRoles.Deathpact:
                    if (shapeshifting)
                        Deathpact.OnShapeshift(shapeshifter, target);
                    isSSneeded = false;
                    break;
                case CustomRoles.Devourer:
                    if (shapeshifting)
                        Devourer.OnShapeshift(shapeshifter, target);
                    isSSneeded = false;
                    break;
                case CustomRoles.Twister:
                    Twister.TwistPlayers(shapeshifter, shapeshifting);
                    isSSneeded = false;
                    break;
            }
        }

        // Forced rewriting in case the name cannot be corrected due to the timing of canceling the transformation being off.
        if (!shapeshifting && !shapeshifter.Is(CustomRoles.Glitch))
        {
            _ = new LateTask(() => { NotifyRoles(NoCache: true); },
                1.2f, "ShapeShiftNotify");
        }

        if ((!shapeshifting || !isSSneeded) && !Swapster.FirstSwapTarget.ContainsKey(shapeshifter.PlayerId))
        {
            _ = new LateTask(shapeshifter.RpcResetAbilityCooldown, 0.01f, log: false);
        }

        if (!isSSneeded)
        {
            Main.CheckShapeshift[shapeshifter.PlayerId] = false;
            shapeshifter.RpcRejectShapeshift();
            NotifyRoles(SpecifySeer: shapeshifter, SpecifyTarget: shapeshifter);
        }

        return isSSneeded || !Options.DisableShapeshiftAnimations.GetBool() || !shapeshifting;
    }

    // Tasks that should run when someone performs a shapeshift (with the egg animation) should be here.
    public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
    {
        if (!Main.ProcessShapeshifts || !GameStates.IsInTask || __instance == null || target == null) return;

        bool shapeshifting = __instance.PlayerId != target.PlayerId;

        foreach (var pc in Main.AllAlivePlayerControls)
        {
            if (pc.Is(CustomRoles.Shiftguard))
            {
                pc.Notify(shapeshifting ? GetString("ShiftguardNotifySS") : "ShiftguardNotifyUnshift");
            }
        }
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.ReportDeadBody))]
class ReportDeadBodyPatch
{
    public static Dictionary<byte, bool> CanReport;
    public static Dictionary<byte, List<GameData.PlayerInfo>> WaitReport = [];

    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] GameData.PlayerInfo target)
    {
        if (GameStates.IsMeeting) return false;
        if (Options.DisableMeeting.GetBool()) return false;
        if (Options.CurrentGameMode is CustomGameMode.SoloKombat or CustomGameMode.FFA or CustomGameMode.MoveAndStop or CustomGameMode.HotPotato) return false;
        if (!CanReport[__instance.PlayerId])
        {
            WaitReport[__instance.PlayerId].Add(target);
            Logger.Warn($"{__instance.GetNameWithRole().RemoveHtmlTags()}: Reporting is currently prohibited, so we will wait until it becomes possible.", "ReportDeadBody");
            return false;
        }

        Logger.Info($"{__instance.GetNameWithRole().RemoveHtmlTags()} => {target?.Object?.GetNameWithRole() ?? "null"}", "ReportDeadBody");

        foreach (var kvp in Main.PlayerStates)
        {
            kvp.Value.LastRoom = GetPlayerById(kvp.Key).GetPlainShipRoom();
        }

        if (!AmongUsClient.Instance.AmHost) return true;

        try
        {
            // If the caller is dead, this process will cancel the meeting, so stop here.
            if (__instance.Data.IsDead) return false;

            //=============================================
            // Next, check whether this meeting is allowed
            //=============================================

            var killer = target?.Object?.GetRealKiller();
            var killerRole = killer?.GetCustomRole();

            if (((IsActive(SystemTypes.Comms) && Options.CommsCamouflage.GetBool() && (Main.NormalOptions.MapId != 5 || !Options.CommsCamouflageDisableOnFungle.GetBool())) || Camouflager.IsActive) && Options.DisableReportWhenCC.GetBool()) return false;

            if (target == null)
            {
                if (__instance.Is(CustomRoles.Jester) && !Options.JesterCanUseButton.GetBool()) return false;
                if (__instance.Is(CustomRoles.NiceSwapper) && !NiceSwapper.CanStartMeeting.GetBool()) return false;
                if (SoulHunter.IsTargetBlocked && __instance.PlayerId == SoulHunter.CurrentTarget.ID)
                {
                    __instance.Notify(GetString("SoulHunterTargetNotifyNoMeeting"));
                    _ = new LateTask(() =>
                    {
                        if (SoulHunter.CurrentTarget.ID == __instance.PlayerId) __instance.Notify(string.Format(GetString("SoulHunterTargetNotify"), SoulHunter.SoulHunter_.GetRealName()), 300f);
                    }, 4f, log: false);
                    return false;
                }
            }

            if (target != null)
            {
                if (Bloodhound.UnreportablePlayers.Contains(target.PlayerId)
                    || Vulture.UnreportablePlayers.Contains(target.PlayerId)) return false;

                if (!Librarian.OnAnyoneReport(__instance)) return false;

                if (!Main.PlayerStates[__instance.PlayerId].Role.CheckReportDeadBody(__instance, target, killer)) return false;

                switch (__instance.GetCustomRole())
                {
                    case CustomRoles.Vulture:
                        long now = TimeStamp;
                        if ((Vulture.AbilityLeftInRound[__instance.PlayerId] > 0) && (now - Vulture.LastReport[__instance.PlayerId] > (long)Vulture.VultureReportCD.GetFloat()))
                        {
                            Vulture.LastReport[__instance.PlayerId] = now;
                            Vulture.OnReportDeadBody(__instance, target);
                            __instance.Notify(GetString("VultureReportBody"));
                            if (Vulture.AbilityLeftInRound[__instance.PlayerId] > 0)
                            {
                                _ = new LateTask(() =>
                                {
                                    if (GameStates.IsInTask)
                                    {
                                        __instance.Notify(GetString("VultureCooldownUp"));
                                    }
                                }, Vulture.VultureReportCD.GetFloat(), "Vulture CD");
                            }

                            Logger.Info($"{__instance.GetRealName()} ate {target.PlayerName} corpse", "Vulture");
                            return false;
                        }

                        break;
                    case CustomRoles.Medusa when Main.KillTimers[__instance.PlayerId] > 0:
                        Main.MedusaBodies.Remove(target.PlayerId);
                        Main.MedusaBodies.Add(target.PlayerId);
                        __instance.Notify(GetString("MedusaStoneBody"));
                        __instance.SetKillCooldown(Medusa.KillCooldownAfterStoneGazing.GetFloat());
                        Logger.Info($"{__instance.GetRealName()} stoned {target.PlayerName}'s body", "Medusa");
                        return false;
                }

                if (Main.PlayerStates[target.PlayerId].deathReason == PlayerState.DeathReason.Gambled
                    || killerRole == CustomRoles.Scavenger
                    || Main.CleanerBodies.Contains(target.PlayerId)
                    || Main.MedusaBodies.Contains(target.PlayerId)) return false;

                var tpc = GetPlayerById(target.PlayerId);

                if (__instance.Is(CustomRoles.Oblivious))
                {
                    if (!tpc.Is(CustomRoles.Bait) || (tpc.Is(CustomRoles.Bait) && Options.ObliviousBaitImmune.GetBool())) /* && (target?.Object != null)*/
                    {
                        return false;
                    }
                }

                if (__instance.Is(CustomRoles.Unlucky) && (target?.Object == null || !target.Object.Is(CustomRoles.Bait)))
                {
                    var Ue = IRandom.Instance;
                    if (Ue.Next(0, 100) < Options.UnluckyReportSuicideChance.GetInt())
                    {
                        __instance.Suicide();
                        return false;
                    }
                }

                if (Main.BoobyTrapBody.Contains(target.PlayerId) && __instance.IsAlive())
                {
                    if (!Options.TrapOnlyWorksOnTheBodyBoobyTrap.GetBool())
                    {
                        var killerID = BoobyTrap.KillerOfBoobyTrapBody[target.PlayerId];

                        __instance.Suicide(PlayerState.DeathReason.Bombed, GetPlayerById(killerID));
                        RPC.PlaySoundRPC(killerID, Sounds.KillSound);

                        if (!Main.BoobyTrapBody.Contains(__instance.PlayerId)) Main.BoobyTrapBody.Add(__instance.PlayerId);
                        BoobyTrap.KillerOfBoobyTrapBody.TryAdd(__instance.PlayerId, killerID);
                        return false;
                    }

                    var killerID2 = target.PlayerId;

                    __instance.Suicide(PlayerState.DeathReason.Bombed, GetPlayerById(killerID2));
                    RPC.PlaySoundRPC(killerID2, Sounds.KillSound);
                    return false;
                }

                if (tpc.Is(CustomRoles.Unreportable)) return false;
            }

            if (Options.SyncButtonMode.GetBool() && target == null)
            {
                Logger.Info("最大:" + Options.SyncedButtonCount.GetInt() + ", 現在:" + Options.UsedButtonCount, "ReportDeadBody");
                if (Options.SyncedButtonCount.GetFloat() <= Options.UsedButtonCount)
                {
                    Logger.Info("使用可能ボタン回数が最大数を超えているため、ボタンはキャンセルされました。", "ReportDeadBody");
                    return false;
                }

                Options.UsedButtonCount++;
                if (Math.Abs(Options.SyncedButtonCount.GetFloat() - Options.UsedButtonCount) < 0.5f)
                {
                    Logger.Info("使用可能ボタン回数が最大数に達しました。", "ReportDeadBody");
                }
            }

            AfterReportTasks(__instance, target);
        }
        catch (Exception e)
        {
            Logger.Exception(e, "ReportDeadBodyPatch");
            Logger.SendInGame("Error: " + e);
        }

        return true;
    }

    public static void AfterReportTasks(PlayerControl player, GameData.PlayerInfo target)
    {
        //====================================================================================
        //    Hereinafter, it is assumed that it is confirmed that the button is pressed.
        //====================================================================================

        Damocles.countRepairSabotage = false;
        Stressed.countRepairSabotage = false;

        if (target == null)
        {
            if (player.Is(CustomRoles.Mayor))
            {
                Main.MayorUsedButtonCount[player.PlayerId] += 1;
            }
        }
        else
        {
            var tpc = GetPlayerById(target.PlayerId);
            if (tpc != null && !tpc.IsAlive())
            {
                if (player.Is(CustomRoles.Detective) && player.PlayerId != target.PlayerId)
                {
                    Detective.OnReportDeadBody(player, target.Object);
                }
                else if (player.Is(CustomRoles.Sleuth) && player.PlayerId != target.PlayerId)
                {
                    string msg = string.Format(GetString("SleuthMsg"), tpc.GetRealName(), tpc.GetDisplayRoleName());
                    Main.SleuthMsgs[player.PlayerId] = msg;
                }
            }

            if (Main.InfectedBodies.Contains(target.PlayerId)) Virus.OnKilledBodyReport(player);
        }

        Enigma.OnReportDeadBody(player, target);
        Mediumshiper.OnReportDeadBody(target);
        Mortician.OnReportDeadBody(player, target);
        Spiritualist.OnReportDeadBody(target);

        Main.LastVotedPlayerInfo = null;
        Witness.AllKillers.Clear();
        Main.ArsonistTimer.Clear();
        if (Farseer.isEnable) Farseer.FarseerTimer.Clear();
        Puppeteer.PuppeteerList.Clear();
        Puppeteer.PuppeteerDelayList.Clear();
        Main.TaglockedList.Clear();
        Main.GuesserGuessed.Clear();
        Main.VeteranInProtect.Clear();
        Main.GrenadierBlinding.Clear();
        Main.Lighter.Clear();
        Main.BlockSabo.Clear();
        Main.BlockedVents.Clear();
        Grenadier.MadGrenadierBlinding.Clear();
        if (Divinator.IsEnable) Divinator.didVote.Clear();
        if (Oracle.IsEnable) Oracle.didVote.Clear();
        if (Bloodhound.IsEnable) Bloodhound.Clear();
        if (Vulture.IsEnable) Vulture.Clear();

        foreach (var state in Main.PlayerStates.Values)
        {
            state.Role.OnReportDeadBody();
        }

        Main.AbilityCD.Clear();

        if (player.Is(CustomRoles.Damocles))
        {
            Damocles.OnReport(player.PlayerId);
        }

        Damocles.OnMeetingStart();

        if (player.Is(CustomRoles.Stressed))
        {
            Stressed.OnReport(player);
        }

        Stressed.OnMeetingStart();

        if (Math.Abs(Options.InhibitorCDAfterMeetings.GetFloat() - Options.InhibitorCD.GetFloat()) > 0.5f || Math.Abs(Options.SaboteurCD.GetFloat() - Options.SaboteurCDAfterMeetings.GetFloat()) > 0.5f)
        {
            foreach (PlayerControl x in Main.AllAlivePlayerControls)
            {
                if (x.Is(CustomRoles.Saboteur))
                {
                    Main.AllPlayerKillCooldown[x.PlayerId] = Options.SaboteurCDAfterMeetings.GetFloat();
                }
            }
        }

        foreach (var x in Main.RevolutionistStart)
        {
            var tar = GetPlayerById(x.Key);
            if (tar == null) continue;
            tar.Data.IsDead = true;
            Main.PlayerStates[tar.PlayerId].deathReason = PlayerState.DeathReason.Sacrifice;
            tar.RpcExileV2();
            Main.PlayerStates[tar.PlayerId].SetDead();
            Logger.Info($"{tar.GetRealName()} 因会议革命失败", "Revolutionist");
        }

        Main.RevolutionistTimer.Clear();
        Main.RevolutionistStart.Clear();
        Main.RevolutionistLastTime.Clear();

        foreach (var pc in Main.AllPlayerControls)
        {
            if (Main.CheckShapeshift.ContainsKey(pc.PlayerId) && !Doppelganger.DoppelVictim.ContainsKey(pc.PlayerId))
            {
                Camouflage.RpcSetSkin(pc, RevertToDefault: true);
            }
        }

        MeetingTimeManager.OnReportDeadBody();

        NameNotifyManager.Reset();
        NotifyRoles(isForMeeting: true, NoCache: true, CamouflageIsForMeeting: true, GuesserIsForMeeting: true);

        _ = new LateTask(SyncAllSettings, 3f, "SyncAllSettings on meeting start");
    }

    public static async void ChangeLocalNameAndRevert(string name, int time)
    {
        //It can't be helped because async Task gives a warning.
        var revertName = PlayerControl.LocalPlayer.name;
        PlayerControl.LocalPlayer.RpcSetNameEx(name);
        await Task.Delay(time);
        PlayerControl.LocalPlayer.RpcSetNameEx(revertName);
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
class FixedUpdatePatch
{
    private static readonly StringBuilder Mark = new(20);
    private static StringBuilder Suffix = new(120);
    private static int LevelKickBufferTime = 10;
    private static readonly Dictionary<byte, int> BufferTime = [];
    private static readonly Dictionary<byte, int> DeadBufferTime = [];
    private static readonly Dictionary<byte, long> LastUpdate = [];
    private static long LastAddAbilityTime;

    public static async void Postfix(PlayerControl __instance)
    {
        if (__instance == null) return;
        if (!GameStates.IsModHost) return;

        byte id = __instance.PlayerId;
        if (AmongUsClient.Instance.AmHost && GameStates.IsInTask && ReportDeadBodyPatch.CanReport[id] && ReportDeadBodyPatch.WaitReport[id].Count > 0)
        {
            if (Glitch.hackedIdList.ContainsKey(id))
            {
                __instance.Notify(string.Format(GetString("HackedByGlitch"), "Report"));
                Logger.Info("Dead Body Report Blocked (player is hacked by Glitch)", "FixedUpdate.ReportDeadBody");
                ReportDeadBodyPatch.WaitReport[id].Clear();
            }
            else
            {
                var info = ReportDeadBodyPatch.WaitReport[id][0];
                ReportDeadBodyPatch.WaitReport[id].Clear();
                Logger.Info($"{__instance.GetNameWithRole().RemoveHtmlTags()}:通報可能になったため通報処理を行います", "ReportDeadbody");
                __instance.ReportDeadBody(info);
            }
        }

        if (Options.DontUpdateDeadPlayers.GetBool() && !__instance.IsAlive())
        {
            DeadBufferTime.TryAdd(id, IRandom.Instance.Next(50, 70));
            DeadBufferTime[id]--;
            if (DeadBufferTime[id] > 0) return;
            DeadBufferTime[id] = IRandom.Instance.Next(50, 70);
        }

        if (Options.LowLoadMode.GetBool())
        {
            BufferTime.TryAdd(id, 10);
            BufferTime[id]--;
            if (BufferTime[id] % 2 == 1 && Options.DeepLowLoad.GetBool()) return;
        }

        //if (Options.DeepLowLoad.GetBool()) await Task.Run(() => { DoPostfix(__instance); });
        /*else */
        try
        {
            await DoPostfix(__instance);
        }
        catch (Exception ex)
        {
            Logger.Error($"Error for {__instance.GetNameWithRole().RemoveHtmlTags()}:  {ex}", "FixedUpdatePatch");
        }
    }

    public static Task DoPostfix(PlayerControl __instance)
    {
        var player = __instance;
        var playerId = player.PlayerId;

        bool lowLoad = false;
        if (Options.LowLoadMode.GetBool())
        {
            if (BufferTime[playerId] > 0) lowLoad = true;
            else BufferTime[playerId] = 10;
        }

        if (Sniper.IsEnable) Sniper.OnFixedUpdate(player);
        if (!lowLoad)
        {
            Zoom.OnFixedUpdate();
            NameNotifyManager.OnFixedUpdate(player);
            TargetArrow.OnFixedUpdate(player);
            LocateArrow.OnFixedUpdate(player);

            if (RPCHandlerPatch.ReportDeadBodyRPCs.Remove(playerId))
                Logger.Info($"Cleared ReportDeadBodyRPC Count for {player.GetRealName().RemoveHtmlTags()}", "FixedUpdatePatch");
        }

        if (AmongUsClient.Instance.AmHost)
        {
            if (GameStates.IsLobby && ((ModUpdater.hasUpdate && ModUpdater.forceUpdate) || ModUpdater.isBroken || !Main.AllowPublicRoom) && AmongUsClient.Instance.IsGamePublic)
                AmongUsClient.Instance.ChangeGamePublic(false);

            // Kick low level people
            if (!lowLoad && GameStates.IsLobby && !player.AmOwner && Options.KickLowLevelPlayer.GetInt() != 0 && (
                    (player.Data.PlayerLevel != 0 && player.Data.PlayerLevel < Options.KickLowLevelPlayer.GetInt()) ||
                    player.Data.FriendCode == string.Empty
                ))
            {
                LevelKickBufferTime--;
                if (LevelKickBufferTime <= 0)
                {
                    LevelKickBufferTime = 20;
                    if (player.GetClient().ProductUserId != "")
                    {
                        if (!BanManager.TempBanWhiteList.Contains(player.GetClient().GetHashedPuid()))
                            BanManager.TempBanWhiteList.Add(player.GetClient().GetHashedPuid());
                    }

                    string msg = string.Format(GetString("KickBecauseLowLevel"), player.GetRealName().RemoveHtmlTags());
                    Logger.SendInGame(msg);
                    AmongUsClient.Instance.KickPlayer(player.GetClientId(), true);
                    Logger.Info(msg, "Low Level Temp Ban");
                }
            }

            if (!Main.KillTimers.TryAdd(playerId, 10f) && ((!player.inVent && !player.MyPhysics.Animations.IsPlayingEnterVentAnimation()) || player.Is(CustomRoles.Haste)) && Main.KillTimers[playerId] > 0)
            {
                Main.KillTimers[playerId] -= Time.fixedDeltaTime;
            }

            if (!lowLoad && player.IsModClient() && player.Is(CustomRoles.Haste)) player.ForceKillTimerContinue = true;

            if (DoubleTrigger.FirstTriggerTimer.Count > 0) DoubleTrigger.OnFixedUpdate(player);

            if (Main.PlayerStates.TryGetValue(playerId, out var s) && s.Role.IsEnable)
            {
                s.Role.OnFixedUpdate(player);
            }

            switch (player.GetCustomRole())
            {
                case CustomRoles.Vampire:
                    Vampire.OnFixedUpdate(player);
                    break;
                case CustomRoles.Poisoner:
                    Poisoner.OnFixedUpdate(player);
                    break;
                case CustomRoles.BountyHunter:
                    BountyHunter.FixedUpdate(player);
                    break;
                case CustomRoles.SoulHunter when !lowLoad:
                    SoulHunter.OnFixedUpdate();
                    break;
                case CustomRoles.Glitch when !lowLoad:
                    Glitch.UpdateHackCooldown(player);
                    break;
                case CustomRoles.Aid when !lowLoad:
                    Aid.OnFixedUpdate(player);
                    break;
                case CustomRoles.Spy when !lowLoad:
                    Spy.OnFixedUpdate(player);
                    break;
                case CustomRoles.RiftMaker when !lowLoad:
                    RiftMaker.OnFixedUpdate(player);
                    break;
                case CustomRoles.Mastermind when !lowLoad:
                    Mastermind.OnFixedUpdate();
                    break;
                case CustomRoles.Magician when !lowLoad:
                    Magician.OnFixedUpdate(player);
                    break;
                case CustomRoles.Mafioso when !lowLoad:
                    Mafioso.OnFixedUpdate(player);
                    break;
                case CustomRoles.Librarian when !lowLoad:
                    Librarian.OnFixedUpdate();
                    break;
                case CustomRoles.NiceHacker when !lowLoad:
                    NiceHacker.OnFixedUpdate(player);
                    break;
                case CustomRoles.Enderman when !lowLoad:
                    Enderman.OnFixedUpdate();
                    break;
                case CustomRoles.Bubble when !lowLoad:
                    Bubble.OnFixedUpdate();
                    break;
                case CustomRoles.Sentinel when !lowLoad:
                    Sentinel.OnFixedUpdate();
                    break;
                case CustomRoles.Analyzer:
                    Analyzer.OnFixedUpdate(player);
                    break;
                case CustomRoles.SerialKiller:
                    SerialKiller.FixedUpdate(player);
                    break;
                case CustomRoles.Penguin:
                    Penguin.OnFixedUpdate();
                    break;
                case CustomRoles.Benefactor when !lowLoad:
                    Benefactor.OnFixedUpdate(player);
                    break;
                case CustomRoles.Chronomancer when !lowLoad:
                    Chronomancer.OnFixedUpdate(player);
                    break;
                case CustomRoles.Druid when !lowLoad:
                    Druid.OnFixedUpdate();
                    break;
                case CustomRoles.Stealth when !lowLoad:
                    Stealth.OnFixedUpdate(player);
                    break;
                case CustomRoles.Gambler when !lowLoad:
                    Gambler.OnFixedUpdate(player);
                    break;
                case CustomRoles.Swiftclaw when !lowLoad:
                    Swiftclaw.OnFixedUpdate(player);
                    break;
                case CustomRoles.Randomizer:
                    Randomizer.OnFixedUpdateForRandomizer(player, lowLoad);
                    break;
            }

            if (GameStates.IsInTask && player.Is(CustomRoles.PlagueBearer) && PlagueBearer.IsPlaguedAll(player))
            {
                player.RpcSetCustomRole(CustomRoles.Pestilence);
                player.Notify(GetString("PlagueBearerToPestilence"));
                player.RpcGuardAndKill(player);
                if (!PlagueBearer.PestilenceList.Contains(playerId))
                    PlagueBearer.PestilenceList.Add(playerId);
                PlagueBearer.SetKillCooldownPestilence(playerId);
                PlagueBearer.playerIdList.Remove(playerId);
            }

            if (GameStates.IsInTask && player != null && player.IsAlive())
            {
                foreach (var state in Main.PlayerStates.Values)
                {
                    if (state.Role.IsEnable)
                    {
                        state.Role.OnCheckPlayerPosition(player);
                    }
                }

                Druid.OnCheckPlayerPosition(player);
                Sentinel.OnCheckPlayerPosition(player);
                Tornado.OnCheckPlayerPosition(player);
                BallLightning.OnCheckPlayerPosition(player);
                Sprayer.OnCheckPlayerPosition(player);
                Asthmatic.OnCheckPlayerPosition(player);
                PlagueDoctor.OnCheckPlayerPosition(player);
                Beacon.OnCheckPlayerPosition(player);
                Randomizer.OnCheckPlayerPosition(player);
            }

            if (!lowLoad && Main.PlayerStates.TryGetValue(playerId, out var playerState) && GameStates.IsInTask)
            {
                var subRoles = playerState.SubRoles;
                if (subRoles.Contains(CustomRoles.Damocles)) Damocles.Update(player);
                if (subRoles.Contains(CustomRoles.Stressed)) Stressed.Update(player);
                if (subRoles.Contains(CustomRoles.Asthmatic)) Asthmatic.OnFixedUpdate();
                if (subRoles.Contains(CustomRoles.Disco)) Disco.OnFixedUpdate(player);
            }

            long now = TimeStamp;
            if (!lowLoad && Options.UsePets.GetBool() && GameStates.IsInTask && (!LastUpdate.TryGetValue(playerId, out var lastPetNotify) || lastPetNotify < now))
            {
                if (Main.AbilityCD.TryGetValue(playerId, out var timer))
                {
                    if (timer.START_TIMESTAMP + timer.TOTALCD < TimeStamp || !player.IsAlive())
                    {
                        Main.AbilityCD.Remove(playerId);
                    }

                    if (!player.IsModClient()) NotifyRoles(SpecifySeer: player, SpecifyTarget: player);
                    LastUpdate[playerId] = now;
                }
            }

            if (!lowLoad)
            {
                foreach (var state in Main.PlayerStates.Values)
                {
                    if (state.Role.IsEnable)
                    {
                        state.Role.OnGlobalFixedUpdate();
                    }
                }

                YinYanger.OnFixedUpdate();
                Duellist.OnFixedUpdate();
                Kamikaze.OnFixedUpdate();
                Succubus.OnFixedUpdate();
                Necromancer.OnFixedUpdate();

                if (Randomizer.IsEnable)
                {
                    Sentinel.OnFixedUpdate();
                    Randomizer.OnFixedUpdateForPlayers(player);
                    Randomizer.GlobalFixedUpdate();
                }
            }
        }


        #region 女巫处理

        if (GameStates.IsInTask && Main.WarlockTimer.ContainsKey(playerId)) //処理を1秒遅らせる
        {
            if (player.IsAlive())
            {
                if (Main.WarlockTimer[playerId] >= 1f)
                {
                    player.RpcResetAbilityCooldown();
                    Main.isCursed = false; //変身クールを１秒に変更
                    player.MarkDirtySettings();
                    Main.WarlockTimer.Remove(playerId);
                }
                else Main.WarlockTimer[playerId] += Time.fixedDeltaTime; //時間をカウント
            }
            else
            {
                Main.WarlockTimer.Remove(playerId);
            }
        }

        //ターゲットのリセット

        #endregion

        #region 纵火犯浇油处理

        if (GameStates.IsInTask && Main.ArsonistTimer.ContainsKey(playerId)) //アーソニストが誰かを塗っているとき
        {
            var arTarget = Main.ArsonistTimer[playerId].PLAYER;
            if (!player.IsAlive() || Pelican.IsEaten(playerId))
            {
                Main.ArsonistTimer.Remove(playerId);
                NotifyRoles(SpecifySeer: __instance, SpecifyTarget: arTarget, ForceLoop: true);
                RPC.ResetCurrentDousingTarget(playerId);
            }
            else
            {
                var ar_target = Main.ArsonistTimer[playerId].PLAYER; //塗られる人
                var ar_time = Main.ArsonistTimer[playerId].TIMER; //塗った時間
                if (!ar_target.IsAlive())
                {
                    Main.ArsonistTimer.Remove(playerId);
                }
                else if (ar_time >= Options.ArsonistDouseTime.GetFloat()) //時間以上一緒にいて塗れた時
                {
                    player.SetKillCooldown();
                    Main.ArsonistTimer.Remove(playerId); //塗が完了したのでDictionaryから削除
                    Main.isDoused[(playerId, ar_target.PlayerId)] = true; //塗り完了
                    player.RpcSetDousedPlayer(ar_target, true);
                    NotifyRoles(SpecifySeer: player, SpecifyTarget: arTarget, ForceLoop: true); //名前変更
                    RPC.ResetCurrentDousingTarget(playerId);
                }
                else
                {
                    float range = NormalGameOptionsV07.KillDistances[Mathf.Clamp(player.Is(CustomRoles.Reach) ? 2 : Main.NormalOptions.KillDistance, 0, 2)] + 0.5f;
                    float dis = Vector2.Distance(player.transform.position, ar_target.transform.position); //距離を出す
                    if (dis <= range) //一定の距離にターゲットがいるならば時間をカウント
                    {
                        Main.ArsonistTimer[playerId] = (ar_target, ar_time + Time.fixedDeltaTime);
                    }
                    else //それ以外は削除
                    {
                        Main.ArsonistTimer.Remove(playerId);
                        NotifyRoles(SpecifySeer: player, SpecifyTarget: arTarget, ForceLoop: true);
                        RPC.ResetCurrentDousingTarget(playerId);

                        Logger.Info($"Canceled: {player.GetNameWithRole().RemoveHtmlTags()}", "Arsonist");
                    }
                }
            }
        }

        #endregion

        #region 革命家拉人处理

        if (GameStates.IsInTask && Main.RevolutionistTimer.ContainsKey(playerId)) //当革命家拉拢一个玩家时
        {
            var rvTarget = Main.RevolutionistTimer[playerId].PLAYER;
            if (!player.IsAlive() || Pelican.IsEaten(playerId))
            {
                Main.RevolutionistTimer.Remove(playerId);
                NotifyRoles(SpecifySeer: player, SpecifyTarget: rvTarget, ForceLoop: true);
                RPC.ResetCurrentDrawTarget(playerId);
            }
            else
            {
                var rv_target = Main.RevolutionistTimer[playerId].PLAYER; //拉拢的人
                var rv_time = Main.RevolutionistTimer[playerId].TIMER; //拉拢时间
                if (!rv_target.IsAlive())
                {
                    Main.RevolutionistTimer.Remove(playerId);
                }
                else if (rv_time >= Options.RevolutionistDrawTime.GetFloat()) //在一起时间超过多久
                {
                    player.SetKillCooldown();
                    Main.RevolutionistTimer.Remove(playerId); //拉拢完成从字典中删除
                    Main.isDraw[(playerId, rv_target.PlayerId)] = true; //完成拉拢
                    player.RpcSetDrawPlayer(rv_target, true);
                    NotifyRoles(SpecifySeer: player, SpecifyTarget: rv_target, ForceLoop: true);
                    RPC.ResetCurrentDrawTarget(playerId);
                    if (IRandom.Instance.Next(1, 100) <= Options.RevolutionistKillProbability.GetInt())
                    {
                        rv_target.SetRealKiller(player);
                        Main.PlayerStates[rv_target.PlayerId].deathReason = PlayerState.DeathReason.Sacrifice;
                        player.Kill(rv_target);
                        Main.PlayerStates[rv_target.PlayerId].SetDead();
                        Logger.Info($"Revolutionist: {player.GetNameWithRole().RemoveHtmlTags()} killed {rv_target.GetNameWithRole().RemoveHtmlTags()}", "Revolutionist");
                    }
                }
                else
                {
                    float range = NormalGameOptionsV07.KillDistances[Mathf.Clamp(player.Is(CustomRoles.Reach) ? 2 : Main.NormalOptions.KillDistance, 0, 2)] + 0.5f;
                    float dis = Vector2.Distance(player.transform.position, rv_target.transform.position); //超出距离
                    if (dis <= range) //在一定距离内则计算时间
                    {
                        Main.RevolutionistTimer[playerId] = (rv_target, rv_time + Time.fixedDeltaTime);
                    }
                    else //否则删除
                    {
                        Main.RevolutionistTimer.Remove(playerId);
                        NotifyRoles(SpecifySeer: __instance, SpecifyTarget: rv_target);
                        RPC.ResetCurrentDrawTarget(playerId);

                        Logger.Info($"Canceled: {__instance.GetNameWithRole().RemoveHtmlTags()}", "Revolutionist");
                    }
                }
            }
        }

        if (GameStates.IsInTask && player.IsDrawDone() && player.IsAlive())
        {
            if (Main.RevolutionistStart.ContainsKey(playerId)) //如果存在字典
            {
                if (Main.RevolutionistLastTime.ContainsKey(playerId))
                {
                    long nowtime = TimeStamp;
                    if (Main.RevolutionistLastTime[playerId] != nowtime) Main.RevolutionistLastTime[playerId] = nowtime;
                    int time = (int)(Main.RevolutionistLastTime[playerId] - Main.RevolutionistStart[playerId]);
                    int countdown = Options.RevolutionistVentCountDown.GetInt() - time;
                    Main.RevolutionistCountdown.Clear();
                    if (countdown <= 0) //倒计时结束
                    {
                        GetDrawPlayerCount(playerId, out var y);
                        foreach (var pc in y.Where(x => x != null && x.IsAlive()))
                        {
                            pc.Suicide(PlayerState.DeathReason.Sacrifice);
                            NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
                        }

                        player.Suicide(PlayerState.DeathReason.Sacrifice);
                    }
                    else
                    {
                        Main.RevolutionistCountdown.Add(playerId, countdown);
                    }
                }
                else
                {
                    Main.RevolutionistLastTime.TryAdd(playerId, Main.RevolutionistStart[playerId]);
                }
            }
            else //如果不存在字典
            {
                Main.RevolutionistStart.TryAdd(playerId, TimeStamp);
            }
        }

        #endregion

        if (Farseer.isEnable) Farseer.OnPostFix(player);
        if (Addict.IsEnable) Addict.FixedUpdate(player);
        if (Deathpact.IsEnable) Deathpact.OnFixedUpdate(player);

        if (!lowLoad)
        {
            long now = TimeStamp;

            // Ability Use Gain every 5 seconds

            if (GameStates.IsInTask && player.IsAlive() && Main.PlayerStates.TryGetValue(playerId, out var state) && state.TaskState.IsTaskFinished && LastAddAbilityTime + 5 < now)
            {
                LastAddAbilityTime = now;

                if (player.Is(CustomRoles.SabotageMaster)) SabotageMaster.UsedSkillCount -= SabotageMaster.AbilityChargesWhenFinishedTasks.GetFloat();
                else
                {
                    float add = player.GetCustomRole() switch
                    {
                        CustomRoles.Ventguard => Options.VentguardAbilityChargesWhenFinishedTasks.GetFloat(),
                        CustomRoles.Grenadier => Options.GrenadierAbilityChargesWhenFinishedTasks.GetFloat(),
                        CustomRoles.Lighter => Options.LighterAbilityChargesWhenFinishedTasks.GetFloat(),
                        CustomRoles.SecurityGuard => Options.SecurityGuardAbilityChargesWhenFinishedTasks.GetFloat(),
                        CustomRoles.DovesOfNeace => Options.DovesOfNeaceAbilityChargesWhenFinishedTasks.GetFloat(),
                        CustomRoles.TimeMaster => Options.TimeMasterAbilityChargesWhenFinishedTasks.GetFloat(),
                        CustomRoles.Veteran => Options.VeteranAbilityChargesWhenFinishedTasks.GetFloat(),
                        CustomRoles.Bloodhound => Bloodhound.AbilityChargesWhenFinishedTasks.GetFloat(),
                        CustomRoles.CameraMan => CameraMan.AbilityChargesWhenFinishedTasks.GetFloat(),
                        CustomRoles.Chameleon => Chameleon.AbilityChargesWhenFinishedTasks.GetFloat(),
                        CustomRoles.Convener => Convener.AbilityChargesWhenFinishedTasks.GetFloat(),
                        CustomRoles.Divinator => Divinator.AbilityChargesWhenFinishedTasks.GetFloat(),
                        CustomRoles.Doormaster => Doormaster.AbilityChargesWhenFinishedTasks.GetFloat(),
                        CustomRoles.Drainer => Drainer.AbilityChargesWhenFinishedTasks.GetFloat(),
                        CustomRoles.Druid => Druid.AbilityChargesWhenFinishedTasks.GetFloat(),
                        CustomRoles.Judge => Judge.AbilityChargesWhenFinishedTasks.GetFloat(),
                        CustomRoles.Mediumshiper => Mediumshiper.AbilityChargesWhenFinishedTasks.GetFloat(),
                        CustomRoles.NiceSwapper => NiceSwapper.AbilityChargesWhenFinishedTasks.GetFloat(),
                        CustomRoles.Oracle => Oracle.AbilityChargesWhenFinishedTasks.GetFloat(),
                        CustomRoles.ParityCop => ParityCop.AbilityChargesWhenFinishedTasks.GetFloat(),
                        CustomRoles.Perceiver => Perceiver.AbilityChargesWhenFinishedTasks.GetFloat(),
                        CustomRoles.Ricochet => Ricochet.AbilityChargesWhenFinishedTasks.GetFloat(),
                        CustomRoles.Spy => Spy.AbilityChargesWhenFinishedTasks.GetFloat(),
                        CustomRoles.Tether => Tether.AbilityChargesWhenFinishedTasks.GetFloat(),
                        CustomRoles.Tracker => Tracker.AbilityChargesWhenFinishedTasks.GetFloat(),
                        _ => float.MaxValue,
                    };
                    if (add != float.MaxValue && add > 0) player.RpcIncreaseAbilityUseLimitBy(add);
                }
            }

            switch (player.GetCustomRole())
            {
                case CustomRoles.Express when GameStates.IsInTask:
                    Express.OnFixedUpdate(player);
                    break;

                case CustomRoles.Vulture when Vulture.BodyReportCount[playerId] >= Vulture.NumberOfReportsToWin.GetInt() && GameStates.IsInTask:
                    Vulture.BodyReportCount[playerId] = Vulture.NumberOfReportsToWin.GetInt();
                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Vulture);
                    CustomWinnerHolder.WinnerIds.Add(playerId);
                    break;

                case CustomRoles.Pelican:
                    Pelican.OnFixedUpdate();
                    break;

                case CustomRoles.Sapper:
                    Sapper.OnFixedUpdate(player);
                    break;

                case CustomRoles.Ignitor:
                    Ignitor.OnFixedUpdate(player);
                    break;

                case CustomRoles.Swooper:
                    Swooper.OnFixedUpdate(player);
                    break;

                case CustomRoles.Wraith:
                    Wraith.OnFixedUpdate(player);
                    break;

                case CustomRoles.Chameleon:
                    Chameleon.OnFixedUpdate(player);
                    break;

                case CustomRoles.Werewolf:
                    Werewolf.OnFixedUpdate(player);
                    break;

                case CustomRoles.Alchemist:
                    Alchemist.OnFixedUpdate( /*player*/);
                    break;

                case CustomRoles.BloodKnight:
                    BloodKnight.OnFixedUpdate(player);
                    break;

                case CustomRoles.Spiritcaller:
                    Spiritcaller.OnFixedUpdate(player);
                    break;
            }

            if (Witness.AllKillers.TryGetValue(playerId, out var ktime) && ktime + Options.WitnessTime.GetInt() < now) Witness.AllKillers.Remove(playerId);
            if (GameStates.IsInTask && player.IsAlive() && Options.LadderDeath.GetBool()) FallFromLadder.FixedUpdate(player);
            if (GameStates.IsInGame) LoversSuicide();

            #region 傀儡师处理

            Puppeteer.OnGlobalFixedUpdate(player);

            #endregion

            if (GameStates.IsInTask && player == PlayerControl.LocalPlayer)
            {
                if (Options.DisableDevices.GetBool()) DisableDevice.FixedUpdate();
                if (player.Is(CustomRoles.AntiAdminer)) AntiAdminer.FixedUpdate();
                if (player.Is(CustomRoles.Monitor)) Monitor.FixedUpdate();
            }

            if (GameStates.IsInGame && Main.RefixCooldownDelay <= 0)
            {
                foreach (PlayerControl pc in Main.AllPlayerControls)
                {
                    if (pc.Is(CustomRoles.Vampire) || pc.Is(CustomRoles.Warlock) || pc.Is(CustomRoles.Assassin) || pc.Is(CustomRoles.Undertaker) || pc.Is(CustomRoles.Poisoner))
                        Main.AllPlayerKillCooldown[pc.PlayerId] = Options.DefaultKillCooldown * 2;
                }
            }

            if (!Main.DoBlockNameChange && AmongUsClient.Instance.AmHost)
                ApplySuffix(__instance);
        }


        //LocalPlayer専用
        if (__instance.AmOwner)
        {
            //キルターゲットの上書き処理
            if ((Main.ChangedRole && __instance == PlayerControl.LocalPlayer && AmongUsClient.Instance.AmHost) || (GameStates.IsInTask && !__instance.Is(CustomRoleTypes.Impostor) && __instance.CanUseKillButton() && !__instance.Data.IsDead))
            {
                var players = __instance.GetPlayersInAbilityRangeSorted();
                PlayerControl closest = players.Count == 0 ? null : players[0];
                HudManager.Instance.KillButton.SetTarget(closest);
            }
        }

        //役職テキストの表示
        var RoleTextTransform = __instance.cosmetics.nameText.transform.Find("RoleText");
        var RoleText = RoleTextTransform.GetComponent<TextMeshPro>();

        if (RoleText != null && __instance != null && !lowLoad)
        {
            if (GameStates.IsLobby)
            {
                if (Main.playerVersion.TryGetValue(playerId, out var ver))
                {
                    if (Main.ForkId != ver.forkId) // フォークIDが違う場合
                        __instance.cosmetics.nameText.text = $"<color=#ff0000><size=1.2>{ver.forkId}</size>\n{__instance?.name}</color>";
                    else if (Main.version.CompareTo(ver.version) == 0)
                        __instance.cosmetics.nameText.text = ver.tag == $"{ThisAssembly.Git.Commit}({ThisAssembly.Git.Branch})" ? $"<color=#87cefa>{__instance.name}</color>" : $"<color=#ffff00><size=1.2>{ver.tag}</size>\n{__instance?.name}</color>";
                    else __instance.cosmetics.nameText.text = $"<color=#ff0000><size=1.2>v{ver.version}</size>\n{__instance?.name}</color>";
                }
                else __instance.cosmetics.nameText.text = __instance?.Data?.PlayerName;
            }

            if (GameStates.IsInGame)
            {
                var RoleTextData = GetRoleText(PlayerControl.LocalPlayer.PlayerId, playerId);

                //if (Options.CurrentGameMode == CustomGameMode.HideAndSeek)
                //{
                //    var hasRole = main.AllPlayerCustomRoles.TryGetValue(playerId, out var role);
                //    if (hasRole) RoleTextData = Utils.GetRoleTextHideAndSeek(__instance.Data.Role.Role, role);
                //}
                RoleText.text = RoleTextData.Item1;
                RoleText.color = RoleTextData.Item2;

                if (Options.CurrentGameMode is CustomGameMode.FFA or CustomGameMode.SoloKombat or CustomGameMode.MoveAndStop or CustomGameMode.HotPotato) RoleText.text = string.Empty;

                RoleText.enabled = IsRoleTextEnabled(__instance);

                if (!PlayerControl.LocalPlayer.Data.IsDead && PlayerControl.LocalPlayer.IsRevealedPlayer(__instance) && __instance.Is(CustomRoles.Trickster))
                {
                    RoleText.text = Farseer.RandomRole[PlayerControl.LocalPlayer.PlayerId];
                    RoleText.text += Farseer.GetTaskState();
                }

                if (!AmongUsClient.Instance.IsGameStarted && AmongUsClient.Instance.NetworkMode != NetworkModes.FreePlay)
                {
                    RoleText.enabled = false; //ゲームが始まっておらずフリープレイでなければロールを非表示
                    if (!__instance.AmOwner) __instance.cosmetics.nameText.text = __instance?.Data?.PlayerName;
                }

                bool isProgressTextLong = false;
                var progressText = GetProgressText(__instance);

                if (progressText.RemoveHtmlTags().Length > 25 && Main.VisibleTasksCount)
                {
                    isProgressTextLong = true;
                    progressText = $"\n{progressText}";
                }

                if (Main.VisibleTasksCount) //他プレイヤーでVisibleTasksCountは有効なら
                    RoleText.text += progressText; //ロールの横にタスクなど進行状況表示


                //変数定義
                var seer = PlayerControl.LocalPlayer;
                var target = __instance;

                string RealName;
                //    string SeerRealName;
                Mark.Clear();
                Suffix.Clear();

                //名前変更
                RealName = target.GetRealName();
                //   SeerRealName = seer.GetRealName();

                //名前色変更処理
                //自分自身の名前の色を変更
                if (target.AmOwner && GameStates.IsInTask)
                {
                    //targetが自分自身
                    if (target.Is(CustomRoles.Arsonist) && target.IsDouseDone())
                        RealName = ColorString(GetRoleColor(CustomRoles.Arsonist), GetString("EnterVentToWin"));
                    else if (target.Is(CustomRoles.Revolutionist) && target.IsDrawDone())
                        RealName = ColorString(GetRoleColor(CustomRoles.Revolutionist), string.Format(GetString("EnterVentWinCountDown"), Main.RevolutionistCountdown.GetValueOrDefault(seer.PlayerId, 10)));
                    if (Pelican.IsEaten(seer.PlayerId))
                        RealName = ColorString(GetRoleColor(CustomRoles.Pelican), GetString("EatenByPelican"));

                    switch (Options.CurrentGameMode)
                    {
                        case CustomGameMode.SoloKombat:
                            SoloKombatManager.GetNameNotify(target, ref RealName);
                            break;
                        case CustomGameMode.FFA:
                            FFAManager.GetNameNotify(target, ref RealName);
                            break;
                    }

                    if (Deathpact.IsInActiveDeathpact(seer))
                        RealName = Deathpact.GetDeathpactString(seer);
                    if (NameNotifyManager.GetNameNotify(target, out var name))
                        RealName = name;
                }

                // Name Color Manager
                RealName = RealName.ApplyNameColorData(seer, target, false);

                if (Marshall.IsEnable && seer.GetCustomRole().IsCrewmate() && seer.Is(CustomRoles.Madmate) && Marshall.MadmateCanFindMarshall) //seerがインポスター
                {
                    if (target.Is(CustomRoles.Marshall) && target.GetTaskState().IsTaskFinished) //targetがタスクを終わらせたマッドスニッチ
                        Mark.Append(ColorString(GetRoleColor(CustomRoles.Marshall), "★")); //targetにマーク付与
                }

                switch (target.GetCustomRole()) //seerがインポスター
                {
                    case CustomRoles.Snitch when seer.GetCustomRole().IsImpostor() && target.Is(CustomRoles.Madmate) && target.GetTaskState().IsTaskFinished:
                        Mark.Append(ColorString(GetRoleColor(CustomRoles.Impostor), "★")); //targetにマーク付与
                        break;
                    case CustomRoles.Marshall when seer.GetCustomRole().IsCrewmate() && target.GetTaskState().IsTaskFinished:
                        Mark.Append(ColorString(GetRoleColor(CustomRoles.Marshall), "★")); //targetにマーク付与
                        break;
                    case CustomRoles.SuperStar when Options.EveryOneKnowSuperStar.GetBool():
                        Mark.Append(ColorString(GetRoleColor(CustomRoles.SuperStar), "★"));
                        break;
                    case CustomRoles.PlagueDoctor:
                        Mark.Append(PlagueDoctor.GetMarkOthers(seer, target));
                        Suffix.Append(PlagueDoctor.GetLowerTextOthers(seer, target));
                        break;
                    case CustomRoles.EvilTracker:
                        Suffix.Append(EvilTracker.GetTargetArrow(seer, target));
                        break;
                }

                switch (seer.GetCustomRole())
                {
                    case CustomRoles.Lookout:
                        if (seer.IsAlive() && target.IsAlive())
                        {
                            Mark.Append(ColorString(GetRoleColor(CustomRoles.Lookout), " " + target.PlayerId) + " ");
                        }

                        break;
                    case CustomRoles.PlagueBearer when PlagueBearer.IsPlagued(seer.PlayerId, target.PlayerId):
                        Mark.Append($"<color={GetRoleColorCode(CustomRoles.PlagueBearer)}>●</color>");
                        //   PlagueBearer.SendRPC(seer, target);
                        break;
                    case CustomRoles.Arsonist:
                        if (seer.IsDousedPlayer(target))
                        {
                            Mark.Append($"<color={GetRoleColorCode(CustomRoles.Arsonist)}>▲</color>");
                        }
                        else if (
                            Main.currentDousingTarget != byte.MaxValue &&
                            Main.currentDousingTarget == target.PlayerId
                        )
                        {
                            Mark.Append($"<color={GetRoleColorCode(CustomRoles.Arsonist)}>△</color>");
                        }

                        break;
                    case CustomRoles.Revolutionist:
                        if (seer.IsDrawPlayer(target))
                        {
                            Mark.Append($"<color={GetRoleColorCode(CustomRoles.Revolutionist)}>●</color>");
                        }
                        else if (
                            Main.currentDrawTarget != byte.MaxValue &&
                            Main.currentDrawTarget == target.PlayerId
                        )
                        {
                            Mark.Append($"<color={GetRoleColorCode(CustomRoles.Revolutionist)}>○</color>");
                        }

                        break;
                    case CustomRoles.Farseer:
                        if (Main.currentDrawTarget != byte.MaxValue &&
                            Main.currentDrawTarget == target.PlayerId)
                        {
                            Mark.Append($"<color={GetRoleColorCode(CustomRoles.Farseer)}>○</color>");
                        }

                        break;
                    case CustomRoles.Analyzer:
                        if (Analyzer.CurrentTarget.ID == target.PlayerId)
                        {
                            Mark.Append($"<color={GetRoleColorCode(CustomRoles.Analyzer)}>○</color>");
                        }

                        break;
                    case CustomRoles.Puppeteer:
                        if (Main.PuppeteerList.ContainsValue(seer.PlayerId) && Main.PuppeteerList.ContainsKey(target.PlayerId))
                        {
                            Mark.Append($"<color={GetRoleColorCode(CustomRoles.Impostor)}>◆</color>");
                        }

                        break;
                    case CustomRoles.EvilTracker:
                        Mark.Append(EvilTracker.GetTargetMark(seer, target));
                        break;
                    case CustomRoles.Tracker:
                        Mark.Append(Tracker.GetTargetMark(seer, target));
                        Suffix.Append(Tracker.GetTrackerArrow(seer, target));
                        break;
                    case CustomRoles.AntiAdminer when GameStates.IsInTask:
                        AntiAdminer.FixedUpdate();
                        if (target.AmOwner)
                        {
                            if (AntiAdminer.IsAdminWatch) Suffix.Append(GetString("AntiAdminerAD"));
                            if (AntiAdminer.IsVitalWatch) Suffix.Append(GetString("AntiAdminerVI"));
                            if (AntiAdminer.IsDoorLogWatch) Suffix.Append(GetString("AntiAdminerDL"));
                            if (AntiAdminer.IsCameraWatch) Suffix.Append(GetString("AntiAdminerCA"));
                        }

                        break;
                    case CustomRoles.Monitor when GameStates.IsInTask:
                        Monitor.FixedUpdate();
                        if (target.AmOwner)
                        {
                            if (Monitor.IsAdminWatch) Suffix.Append(ColorString(GetRoleColor(CustomRoles.Monitor), GetString("AdminWarning")));
                            if (Monitor.IsVitalWatch) Suffix.Append(ColorString(GetRoleColor(CustomRoles.Monitor), GetString("VitalsWarning")));
                            if (Monitor.IsDoorLogWatch) Suffix.Append(ColorString(GetRoleColor(CustomRoles.Monitor), GetString("DoorlogWarning")));
                            if (Monitor.IsCameraWatch) Suffix.Append(ColorString(GetRoleColor(CustomRoles.Monitor), GetString("CameraWarning")));
                        }

                        break;
                    case CustomRoles.Executioner:
                        Mark.Append(Executioner.TargetMark(seer, target));
                        break;
                    case CustomRoles.Gamer:
                        Mark.Append(Gamer.TargetMark(seer, target));
                        break;
                    case CustomRoles.BountyHunter:
                        Suffix.Append(BountyHunter.GetTargetArrow(seer, target));
                        break;
                    case CustomRoles.Mortician:
                        Suffix.Append(Mortician.GetTargetArrow(seer, target));
                        break;
                    case CustomRoles.Bloodhound:
                        Suffix.Append(Bloodhound.GetTargetArrow(seer, target));
                        break;
                    case CustomRoles.Stealth:
                        Suffix.Append(Stealth.GetSuffix(seer, target));
                        break;
                    case CustomRoles.Snitch:
                        Suffix.Append(Snitch.GetSnitchArrow(seer, target));
                        break;
                }

                if (Totocalcio.IsEnable) Mark.Append(Totocalcio.TargetMark(seer, target));
                if (Romantic.IsEnable || VengefulRomantic.IsEnable || RuthlessRomantic.IsEnable) Mark.Append(Romantic.TargetMark(seer, target));
                if (Lawyer.IsEnable()) Mark.Append(Lawyer.LawyerMark(seer, target));
                if (Randomizer.IsEnable && Randomizer.IsShielded(target)) Mark.Append(ColorString(GetRoleColor(CustomRoles.Randomizer), "✚"));
                if (Sniper.IsEnable && target.AmOwner) Mark.Append(Sniper.GetShotNotify(target.PlayerId));
                if (BallLightning.IsGhost(target) && BallLightning.IsEnable) Mark.Append(ColorString(GetRoleColor(CustomRoles.BallLightning), "■"));
                if (Marshall.IsEnable) Mark.Append(Marshall.GetWarningMark(seer, target));

                if (Medic.IsEnable)
                {
                    var shieldMark = $"<color={GetRoleColorCode(CustomRoles.Medic)}> ●</color>";

                    bool seerIsMedic = seer.Is(CustomRoles.Medic);
                    bool targetProtected = Medic.InProtect(target.PlayerId);

                    if (seer.PlayerId == target.PlayerId && (Medic.InProtect(seer.PlayerId) || Medic.TempMarkProtected == seer.PlayerId) && (Medic.WhoCanSeeProtect.GetInt() is 0 or 2))
                        Mark.Append(shieldMark);

                    else if (seerIsMedic && (targetProtected || Medic.TempMarkProtected == target.PlayerId) && (Medic.WhoCanSeeProtect.GetInt() is 0 or 1))
                        Mark.Append(shieldMark);

                    else if (seer.Data.IsDead && targetProtected && !seerIsMedic)
                        Mark.Append(shieldMark);
                }

                if (Snitch.IsEnable)
                {
                    Mark.Append(Snitch.GetWarningArrow(seer, target));
                    Mark.Append(Snitch.GetWarningMark(seer, target));
                }

                if (Main.LoversPlayers.Count > 0)
                {
                    if (__instance.Is(CustomRoles.Lovers))
                    {
                        if (PlayerControl.LocalPlayer.Is(CustomRoles.Lovers))
                        {
                            Mark.Append($"<color={GetRoleColorCode(CustomRoles.Lovers)}>♥</color>");
                        }
                        else if (PlayerControl.LocalPlayer.Data.IsDead)
                        {
                            Mark.Append($"<color={GetRoleColorCode(CustomRoles.Lovers)}>♥</color>");
                        }
                    }
                }

                if (Bubble.IsEnable) Suffix.Append(Bubble.GetEncasedPlayerSuffix(target));
                if (Randomizer.IsEnable) Suffix.Append(Randomizer.GetSuffixText(seer, target));

                if (Deathpact.IsEnable)
                {
                    Suffix.Append(Deathpact.GetDeathpactPlayerArrow(seer, target));
                    Suffix.Append(Deathpact.GetDeathpactMark(seer, target));
                }

                if (seer.PlayerId == target.PlayerId)
                {
                    if (!seer.IsModClient()) GetPetCDSuffix(seer, ref Suffix);
                    if (seer.Is(CustomRoles.Asthmatic)) Suffix.Append(Asthmatic.GetSuffixText(seer.PlayerId));
                    switch (seer.GetCustomRole())
                    {
                        case CustomRoles.VengefulRomantic:
                            Suffix.Append(VengefulRomantic.GetTargetText(seer.PlayerId));
                            break;
                        case CustomRoles.Romantic:
                            Suffix.Append(Romantic.GetTargetText(seer.PlayerId));
                            break;
                        case CustomRoles.Ricochet:
                            Suffix.Append(Ricochet.TargetText(seer.PlayerId));
                            break;
                        case CustomRoles.Hitman:
                            Suffix.Append(Hitman.GetTargetText());
                            break;
                    }
                }

                if (target.Is(CustomRoles.Librarian)) Suffix.Append(Librarian.GetNameTextForSuffix(target.PlayerId));

                if (Spiritualist.IsEnable) Suffix.Append(Spiritualist.GetSpiritualistArrow(seer, target));

                switch (Options.CurrentGameMode)
                {
                    case CustomGameMode.FFA:
                        Suffix.Append(FFAManager.GetPlayerArrow(seer, target));
                        if (seer.PlayerId == target.PlayerId) Suffix.Append((Suffix.Length > 0 && FFAManager.LatestChatMessage != string.Empty ? "\n" : string.Empty) + FFAManager.LatestChatMessage);
                        break;
                    case CustomGameMode.MoveAndStop when seer.PlayerId == target.PlayerId:
                        Suffix.Append(MoveAndStopManager.GetSuffixText(seer));
                        break;
                    case CustomGameMode.HotPotato when !seer.IsModClient() && seer.PlayerId == target.PlayerId && seer.IsAlive():
                        Suffix.Append(HotPotatoManager.GetSuffixText(seer.PlayerId));
                        break;
                }

                if (Vulture.ArrowsPointingToDeadBody.GetBool() && Vulture.IsEnable)
                    Suffix.Append(Vulture.GetTargetArrow(seer, target));

                if (Tracefinder.IsEnable) Suffix.Append(Tracefinder.GetTargetArrow(seer, target));

                if (Options.CurrentGameMode == CustomGameMode.SoloKombat)
                    Suffix.Append(SoloKombatManager.GetDisplayHealth(target));

                /*if(main.AmDebugger.Value && main.BlockKilling.TryGetValue(target.PlayerId, out var isBlocked)) {
                    Mark = isBlocked ? "(true)" : "(false)";
                }*/

                //Devourer
                if (Devourer.HideNameOfConsumedPlayer.GetBool() && Devourer.PlayerSkinsCosumed.Any(a => a.Value.Contains(target.PlayerId)))
                    RealName = GetString("DevouredName");

                // Camouflage
                if ((IsActive(SystemTypes.Comms) && Options.CommsCamouflage.GetBool() && (Main.NormalOptions.MapId != 5 || !Options.CommsCamouflageDisableOnFungle.GetBool())) || Camouflager.IsActive)
                    RealName = $"<size=0>{RealName}</size> ";

                string DeathReason = seer.Data.IsDead && seer.KnowDeathReason(target) ? $"\n<size=1.7>({ColorString(GetRoleColor(CustomRoles.Doctor), GetVitalText(target.PlayerId))})</size>" : string.Empty;
                //Mark・Suffixの適用

                var currentText = target.cosmetics.nameText.text;
                var changeTo = $"{RealName}{DeathReason}{Mark}";
                var needUpdate = false;
                if (currentText != changeTo) needUpdate = true;

                if (needUpdate)
                {
                    target.cosmetics.nameText.text = changeTo;

                    float offset = 0.2f;
                    if (NameNotifyManager.Notice.TryGetValue(seer.PlayerId, out var notify) && notify.TEXT.Contains('\n'))
                    {
                        offset += 0.1f;
                    }

                    if (Suffix.ToString() != string.Empty)
                    {
                        // If the name is on two lines, the job title text needs to be moved up.
                        //RoleText.transform.SetLocalY(0.35f);
                        offset += 0.15f;
                        target.cosmetics.nameText.text += "\r\n" + Suffix;
                    }

                    if (!seer.IsAlive())
                    {
                        offset += 0.1f;
                    }

                    if (isProgressTextLong)
                    {
                        offset += 0.3f;
                    }

                    if (Options.CurrentGameMode == CustomGameMode.MoveAndStop)
                    {
                        offset += 0.2f;
                    }

                    RoleText.transform.SetLocalY(offset);
                }
            }
            else
            {
                // Restoring the position text coordinates to their initial values
                RoleText.transform.SetLocalY(0.2f);
            }
        }

        return Task.CompletedTask;
    }

    public static void LoversSuicide(byte deathId = 0x7f, bool isExiled = false)
    {
        if (Main.LoversPlayers.Count == 0) return;
        if (Options.LoverSuicide.GetBool() && Main.isLoversDead == false)
        {
            foreach (PlayerControl loversPlayer in Main.LoversPlayers.ToArray())
            {
                if (!loversPlayer.Data.IsDead && loversPlayer.PlayerId != deathId) continue;
                Main.isLoversDead = true;
                for (int i1 = 0; i1 < Main.LoversPlayers.Count; i1++)
                {
                    PlayerControl partnerPlayer = Main.LoversPlayers[i1];
                    if (loversPlayer.PlayerId == partnerPlayer.PlayerId) continue;
                    if (partnerPlayer.PlayerId != deathId && !partnerPlayer.Data.IsDead)
                    {
                        if (partnerPlayer.Is(CustomRoles.Lovers))
                        {
                            Main.PlayerStates[partnerPlayer.PlayerId].deathReason = PlayerState.DeathReason.FollowingSuicide;
                            if (isExiled)
                                CheckForEndVotingPatch.TryAddAfterMeetingDeathPlayers(PlayerState.DeathReason.FollowingSuicide, partnerPlayer.PlayerId);
                            else
                                partnerPlayer.Kill(partnerPlayer);
                        }
                    }
                }
            }
        }
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Start))]
class PlayerStartPatch
{
    public static void Postfix(PlayerControl __instance)
    {
        var roleText = Object.Instantiate(__instance.cosmetics.nameText);
        roleText.transform.SetParent(__instance.cosmetics.nameText.transform);
        roleText.transform.localPosition = new Vector3(0f, 0.2f, 0f);
        roleText.fontSize -= 1.2f;
        roleText.text = "RoleText";
        roleText.gameObject.name = "RoleText";
        roleText.enabled = false;
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.SetColor))]
class SetColorPatch
{
    public static bool IsAntiGlitchDisabled;

    public static bool Prefix(PlayerControl __instance, int bodyColor)
    {
        //色変更バグ対策
        if (!AmongUsClient.Instance.AmHost || __instance.CurrentOutfit.ColorId == bodyColor || IsAntiGlitchDisabled) return true;
        return true;
    }
}

[HarmonyPatch(typeof(Vent), nameof(Vent.ExitVent))]
class ExitVentPatch
{
    public static void Postfix(Vent __instance, [HarmonyArgument(0)] PlayerControl pc)
    {
        Logger.Info($" {pc.GetNameWithRole()}, Vent ID: {__instance.Id} ({__instance.name})", "ExitVent");

        if (!AmongUsClient.Instance.AmHost) return;

        Drainer.OnAnyoneExitVent(pc);

        if (Options.WhackAMole.GetBool())
        {
            _ = new LateTask(() => { pc.TPtoRndVent(); }, 0.5f, "Whack-A-Mole TP");
        }

        if (!pc.IsModClient() && pc.Is(CustomRoles.Haste))
        {
            pc.SetKillCooldown(Math.Max(Main.KillTimers[pc.PlayerId], 0.1f));
        }
    }
}

[HarmonyPatch(typeof(Vent), nameof(Vent.EnterVent))]
class EnterVentPatch
{
    public static void Postfix(Vent __instance, [HarmonyArgument(0)] PlayerControl pc)
    {
        Logger.Info($" {pc.GetNameWithRole()}, Vent ID: {__instance.Id} ({__instance.name})", "EnterVent");

        if (pc.GetCustomRole().GetRoleTypes() != RoleTypes.Engineer && !Main.PlayerStates[pc.PlayerId].Role.CanUseImpostorVentButton(pc))
        {
            pc.MyPhysics?.RpcBootFromVent(__instance.Id);
            return;
        }

        Drainer.OnAnyoneEnterVent(pc, __instance);
        Analyzer.OnAnyoneEnterVent(pc);

        switch (pc.GetCustomRole())
        {
            case CustomRoles.Mayor when !Options.UsePets.GetBool() && Main.MayorUsedButtonCount.TryGetValue(pc.PlayerId, out var count2) && count2 < Options.MayorNumOfUseButton.GetInt():
                pc.MyPhysics?.RpcBootFromVent(__instance.Id);
                pc.ReportDeadBody(null);
                break;
        }

        if (!AmongUsClient.Instance.AmHost) return;

        Main.LastEnteredVent.Remove(pc.PlayerId);
        Main.LastEnteredVent.Add(pc.PlayerId, __instance);
        Main.LastEnteredVentLocation.Remove(pc.PlayerId);
        Main.LastEnteredVentLocation.Add(pc.PlayerId, pc.Pos());

        if (pc.Is(CustomRoles.Unlucky))
        {
            var Ue = IRandom.Instance;
            if (Ue.Next(0, 100) < Options.UnluckyVentSuicideChance.GetInt())
            {
                pc.Suicide();
            }
        }

        if (pc.Is(CustomRoles.Damocles))
        {
            Damocles.OnEnterVent(pc.PlayerId, __instance.Id);
        }

        switch (pc.GetCustomRole())
        {
            case CustomRoles.Swooper:
                Swooper.OnEnterVent(pc, __instance);
                break;
            case CustomRoles.Wraith:
                Wraith.OnEnterVent(pc, __instance);
                break;
            case CustomRoles.Addict:
                Addict.OnEnterVent(pc /*, __instance*/);
                break;
            case CustomRoles.CameraMan when !Options.UsePets.GetBool():
                CameraMan.OnEnterVent(pc);
                break;
            case CustomRoles.Alchemist:
                Alchemist.OnEnterVent(pc, __instance.Id);
                break;
            case CustomRoles.Chameleon:
                Chameleon.OnEnterVent(pc, __instance);
                break;
            case CustomRoles.Tether when !Options.UsePets.GetBool():
                Tether.OnEnterVent(pc, __instance.Id);
                break;
            case CustomRoles.Werewolf:
                Werewolf.OnEnterVent(pc);
                break;
            case CustomRoles.Mafioso:
                Mafioso.OnEnterVent(__instance.Id);
                break;
            case CustomRoles.Lurker:
                Lurker.OnEnterVent(pc);
                break;
            case CustomRoles.Druid when !Options.UsePets.GetBool():
                Druid.OnEnterVent(pc);
                break;
            case CustomRoles.Doormaster when !Options.UsePets.GetBool():
                Doormaster.OnEnterVent(pc);
                break;
            case CustomRoles.Hookshot:
                Hookshot.SwitchActionMode();
                break;
            case CustomRoles.Sentinel when !Options.UsePets.GetBool():
                Sentinel.StartPatrolling(pc);
                break;
            case CustomRoles.Veteran when !Options.UsePets.GetBool():
                if (pc.GetAbilityUseLimit() >= 1)
                {
                    Main.VeteranInProtect.Remove(pc.PlayerId);
                    Main.VeteranInProtect.Add(pc.PlayerId, GetTimeStamp(DateTime.Now));
                    pc.RpcRemoveAbilityUse();
                    //pc.RpcGuardAndKill(pc);
                    pc.RPCPlayCustomSound("Gunload");
                    pc.Notify(GetString("VeteranOnGuard"), Options.VeteranSkillDuration.GetFloat());
                    pc.AddAbilityCD();
                    pc.MarkDirtySettings();
                }
                else
                {
                    pc.Notify(GetString("OutOfAbilityUsesDoMoreTasks"));
                }

                break;
            case CustomRoles.DovesOfNeace when !Options.UsePets.GetBool():
                if (pc.GetAbilityUseLimit() < 1)
                {
                    //pc?.MyPhysics?.RpcBootFromVent(__instance.Id);
                    pc.Notify(GetString("OutOfAbilityUsesDoMoreTasks"));
                }
                else
                {
                    pc.RpcRemoveAbilityUse();
                    //pc.RpcGuardAndKill(pc);
                    Main.AllAlivePlayerControls.Where(x =>
                        pc.Is(CustomRoles.Madmate) ? x.CanUseKillButton() && x.GetCustomRole().IsCrewmate() : x.CanUseKillButton()
                    ).Do(x =>
                    {
                        x.RPCPlayCustomSound("Dove");
                        x.ResetKillCooldown();
                        x.SetKillCooldown();
                        if (x.Is(CustomRoles.SerialKiller))
                        {
                            SerialKiller.OnReportDeadBody();
                        }

                        x.Notify(ColorString(GetRoleColor(CustomRoles.DovesOfNeace), GetString("DovesOfNeaceSkillNotify")));
                    });
                    pc.RPCPlayCustomSound("Dove");
                    pc.Notify(string.Format(GetString("DovesOfNeaceOnGuard"), pc.GetAbilityUseLimit()));
                    pc.AddAbilityCD();
                }

                break;

            case CustomRoles.Perceiver when !Options.UsePets.GetBool():
                Perceiver.UseAbility(pc);
                break;
        }
    }
}

[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.CoEnterVent))]
class CoEnterVentPatch
{
    public static bool Prefix(PlayerPhysics __instance, [HarmonyArgument(0)] int id)
    {
        if (!AmongUsClient.Instance.AmHost) return true;

        Logger.Info($" {__instance.myPlayer.GetNameWithRole()}, Vent ID: {id}", "CoEnterVent");

        if (Main.KillTimers.TryGetValue(__instance.myPlayer.PlayerId, out var timer))
        {
            Main.KillTimers[__instance.myPlayer.PlayerId] = timer + 0.5f;
        }

        switch (Options.CurrentGameMode)
        {
            case CustomGameMode.FFA when FFAManager.FFA_DisableVentingWhenTwoPlayersAlive.GetBool() && Main.AllAlivePlayerControls.Length <= 2:
                var pc = __instance?.myPlayer;
                _ = new LateTask(() =>
                {
                    pc?.Notify(GetString("FFA-NoVentingBecauseTwoPlayers"), 7f);
                    pc?.MyPhysics?.RpcBootFromVent(id);
                }, 0.5f);
                return true;
            case CustomGameMode.FFA when FFAManager.FFA_DisableVentingWhenKCDIsUp.GetBool() && Main.KillTimers[__instance.myPlayer.PlayerId] <= 0:
                _ = new LateTask(() =>
                {
                    __instance.myPlayer?.Notify(GetString("FFA-NoVentingBecauseKCDIsUP"), 7f);
                    __instance.myPlayer?.MyPhysics?.RpcBootFromVent(id);
                }, 0.5f);
                return true;
            case CustomGameMode.MoveAndStop:
            case CustomGameMode.HotPotato:
                _ = new LateTask(() => { __instance.myPlayer?.MyPhysics?.RpcBootFromVent(id); }, 0.5f);
                return true;
        }

        if (Glitch.hackedIdList.ContainsKey(__instance.myPlayer.PlayerId))
        {
            _ = new LateTask(() =>
            {
                __instance.myPlayer?.Notify(string.Format(GetString("HackedByGlitch"), "Vent"));
                __instance.myPlayer?.MyPhysics?.RpcBootFromVent(id);
            }, 0.5f);
            return true;
        }

        if (Penguin.IsVictim(__instance.myPlayer))
        {
            _ = new LateTask(() => { __instance.myPlayer?.MyPhysics?.RpcBootFromVent(id); }, 0.5f);
            return true;
        }

        if (SoulHunter.IsTargetBlocked && SoulHunter.CurrentTarget.ID == __instance.myPlayer.PlayerId)
        {
            _ = new LateTask(() =>
            {
                __instance.myPlayer?.Notify(GetString("SoulHunterTargetNotifyNoVent"));
                __instance.myPlayer?.MyPhysics?.RpcBootFromVent(id);
                _ = new LateTask(() =>
                {
                    if (SoulHunter.CurrentTarget.ID == __instance.myPlayer.PlayerId) __instance.myPlayer.Notify(string.Format(GetString("SoulHunterTargetNotify"), SoulHunter.SoulHunter_.GetRealName()), 300f);
                }, 4f, log: false);
            }, 0.5f);
            return true;
        }

        if (Main.BlockedVents.Contains(id))
        {
            var pc = __instance.myPlayer;
            if (Options.VentguardBlockDoesNotAffectCrew.GetBool() && pc.GetCustomRole().IsCrewmate())
            {
            }
            else
            {
                _ = new LateTask(() =>
                {
                    pc?.Notify(GetString("EnteredBlockedVent"));
                    pc?.MyPhysics?.RpcBootFromVent(id);
                }, 0.5f);
                foreach (var ventguard in Main.AllAlivePlayerControls.Where(x => x.Is(CustomRoles.Ventguard)).ToArray())
                {
                    ventguard.Notify(GetString("VentguardNotify"));
                }

                return true;
            }
        }

        if (AmongUsClient.Instance.IsGameStarted)
        {
            if (__instance.myPlayer.IsDouseDone())
            {
                CustomSoundsManager.RPCPlayCustomSoundAll("Boom");
                foreach (PlayerControl pc in Main.AllAlivePlayerControls)
                {
                    if (pc != __instance.myPlayer)
                    {
                        pc.Suicide(PlayerState.DeathReason.Torched, __instance.myPlayer);
                    }
                }

                foreach (PlayerControl pc in Main.AllPlayerControls)
                {
                    pc.KillFlash();
                }

                CustomWinnerHolder.ShiftWinnerAndSetWinner(CustomWinner.Arsonist); //焼殺で勝利した人も勝利させる
                CustomWinnerHolder.WinnerIds.Add(__instance.myPlayer.PlayerId);
                return true;
            }

            if (Options.ArsonistCanIgniteAnytime.GetBool())
            {
                var douseCount = GetDousedPlayerCount(__instance.myPlayer.PlayerId).Item1;
                if (douseCount >= Options.ArsonistMinPlayersToIgnite.GetInt()) // Don't check for max, since the player would not be able to ignite at all if they somehow get more players doused than the max
                {
                    if (douseCount > Options.ArsonistMaxPlayersToIgnite.GetInt()) Logger.Warn("Arsonist Ignited with more players doused than the maximum amount in the settings", "Arsonist Ignite");
                    foreach (PlayerControl pc in Main.AllAlivePlayerControls)
                    {
                        if (!__instance.myPlayer.IsDousedPlayer(pc))
                            continue;
                        pc.KillFlash();
                        pc.Suicide(PlayerState.DeathReason.Torched, __instance.myPlayer);
                    }

                    var apc = Main.AllAlivePlayerControls.Length;
                    switch (apc)
                    {
                        case 1:
                            CustomWinnerHolder.ShiftWinnerAndSetWinner(CustomWinner.Arsonist);
                            CustomWinnerHolder.WinnerIds.Add(__instance.myPlayer.PlayerId);
                            break;
                        case 2:
                        {
                            foreach (var x in Main.AllAlivePlayerControls.Where(p => p.PlayerId != __instance.myPlayer.PlayerId).ToArray())
                            {
                                if (!x.GetCustomRole().IsImpostor() && !x.GetCustomRole().IsNeutralKilling())
                                {
                                    CustomWinnerHolder.ShiftWinnerAndSetWinner(CustomWinner.Arsonist);
                                    CustomWinnerHolder.WinnerIds.Add(__instance.myPlayer.PlayerId);
                                }
                            }

                            break;
                        }
                    }

                    return true;
                }
            }
        }

        if (AmongUsClient.Instance.IsGameStarted && __instance.myPlayer.IsDrawDone()) //完成拉拢任务的玩家跳管后
        {
            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Revolutionist); //革命者胜利
            GetDrawPlayerCount(__instance.myPlayer.PlayerId, out var x);
            CustomWinnerHolder.WinnerIds.Add(__instance.myPlayer.PlayerId);
            foreach (PlayerControl apc in x.ToArray())
            {
                CustomWinnerHolder.WinnerIds.Add(apc.PlayerId);
            }

            return true;
        }

        if (__instance.myPlayer.GetCustomRole().GetDYRole() == RoleTypes.Impostor && !Main.PlayerStates[__instance.myPlayer.PlayerId].Role.CanUseImpostorVentButton(__instance.myPlayer))
        {
            _ = new LateTask(() => { __instance.RpcBootFromVent(id); }, 0.5f);
        }

        //处理弹出管道的阻塞
        if (Main.NormalOptions.MapId == (int)MapNames.Dleks || ((__instance.myPlayer.Data.Role.Role != RoleTypes.Engineer && //不是工程师
                                                                 !__instance.myPlayer.CanUseImpostorVentButton()) || //不能使用内鬼的跳管按钮
                                                                (__instance.myPlayer.Is(CustomRoles.Mayor) && Main.MayorUsedButtonCount.TryGetValue(__instance.myPlayer.PlayerId, out var count) && count >= Options.MayorNumOfUseButton.GetInt()) ||
                                                                (__instance.myPlayer.Is(CustomRoles.Paranoia) && Main.ParaUsedButtonCount.TryGetValue(__instance.myPlayer.PlayerId, out var count2) && count2 >= Options.ParanoiaNumOfUseButton.GetInt()))
            && !__instance.myPlayer.Is(CustomRoles.Nimble)
            //(__instance.myPlayer.Is(CustomRoles.Veteran) && Main.VeteranNumOfUsed.TryGetValue(__instance.myPlayer.PlayerId, out var count3) && count3 < 1) ||
            //(__instance.myPlayer.Is(CustomRoles.Grenadier) && Main.GrenadierNumOfUsed.TryGetValue(__instance.myPlayer.PlayerId, out var count4) && count4 < 1) ||
            //(__instance.myPlayer.Is(CustomRoles.DovesOfNeace) && Main.DovesOfNeaceNumOfUsed.TryGetValue(__instance.myPlayer.PlayerId, out var count5) && count5 < 1)
           )
        {
            try
            {
                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(__instance.NetId, (byte)RpcCalls.BootFromVent, SendOption.Reliable);
                writer.WritePacked(127);
                AmongUsClient.Instance.FinishRpcImmediately(writer);
                _ = new LateTask(() =>
                {
                    try
                    {
                        int clientId = __instance.myPlayer.GetClientId();
                        MessageWriter writer2 = AmongUsClient.Instance.StartRpcImmediately(__instance.NetId, (byte)RpcCalls.BootFromVent, SendOption.Reliable, clientId);
                        writer2.Write(id);
                        AmongUsClient.Instance.FinishRpcImmediately(writer2);
                    }
                    catch
                    {
                    }
                }, 0.5f, "Fix DesyncImpostor Stuck");
            }
            catch
            {
            }

            return true;
        }

        switch (__instance.myPlayer.GetCustomRole())
        {
            case CustomRoles.Swooper:
                Swooper.OnCoEnterVent(__instance, id);
                break;
            case CustomRoles.Wraith:
                Wraith.OnCoEnterVent(__instance, id);
                break;
            case CustomRoles.Chameleon:
                Chameleon.OnCoEnterVent(__instance, id);
                break;
            case CustomRoles.Mole:
                Mole.OnCoEnterVent(__instance);
                break;
            case CustomRoles.Alchemist when Alchemist.PotionID == 6:
                Alchemist.OnCoEnterVent(__instance, id);
                break;
            case CustomRoles.RiftMaker:
                RiftMaker.OnEnterVent(__instance.myPlayer, id);
                break;
            case CustomRoles.WeaponMaster:
                WeaponMaster.OnEnterVent(__instance.myPlayer, id);
                break;
            case CustomRoles.Convener:
                Convener.UseAbility(__instance.myPlayer, id);
                break;
        }

        //if (__instance.myPlayer.Is(CustomRoles.DovesOfNeace)) __instance.myPlayer.Notify(GetString("DovesOfNeaceMaxUsage"));
        //if (__instance.myPlayer.Is(CustomRoles.Veteran)) __instance.myPlayer.Notify(GetString("OutOfAbilityUsesDoMoreTasks"));
        //if (__instance.myPlayer.Is(CustomRoles.Grenadier)) __instance.myPlayer.Notify(GetString("OutOfAbilityUsesDoMoreTasks"));

        return true;
    }
}

//[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.SetName))]
//class SetNamePatch
//{
//    public static void Postfix(/*PlayerControl __instance, [HarmonyArgument(0)] string name*/)
//    {
//    }
//}
[HarmonyPatch(typeof(GameData), nameof(GameData.CompleteTask))]
class GameDataCompleteTaskPatch
{
    public static void Postfix(PlayerControl pc /*, uint taskId*/)
    {
        Logger.Info($"TaskComplete: {pc.GetNameWithRole().RemoveHtmlTags()}", "CompleteTask");
        Main.PlayerStates[pc.PlayerId].UpdateTask(pc);
        NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CompleteTask))]
class PlayerControlCompleteTaskPatch
{
    public static bool Prefix(PlayerControl __instance)
    {
        return !Workhorse.OnCompleteTask(__instance) && Capitalism.AddTaskForPlayer(__instance); // Cancel task win
    }

    public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] uint idx)
    {
        if (__instance != null && __instance.IsAlive()) Benefactor.OnTaskComplete(__instance, __instance.myTasks[(Index)Convert.ToInt32(idx)] as PlayerTask);

        Snitch.OnCompleteTask(__instance);

        var isTaskFinish = __instance.GetTaskState().IsTaskFinished;
        if (isTaskFinish && __instance.Is(CustomRoles.Snitch) && __instance.Is(CustomRoles.Madmate))
        {
            foreach (var impostor in Main.AllAlivePlayerControls.Where(pc => pc.Is(CustomRoleTypes.Impostor)).ToArray())
                NameColorManager.Add(impostor.PlayerId, __instance.PlayerId, "#ff1919");
            NotifyRoles(SpecifySeer: __instance, ForceLoop: true);
        }

        if (isTaskFinish &&
            __instance.GetCustomRole() is CustomRoles.Doctor or CustomRoles.Sunnyboy or CustomRoles.SpeedBooster)
        {
            // Execute CustomSyncAllSettings at the end of the task only for matches with sunnyboy, speed booster, or doctor.
            MarkEveryoneDirtySettings();
        }
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Die))]
public static class PlayerControlDiePatch
{
    public static void Postfix(PlayerControl __instance)
    {
        if (AmongUsClient.Instance.AmHost) PetsPatch.RpcRemovePet(__instance);
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckSporeTrigger))]
public static class PlayerControlCheckSporeTriggerPatch
{
    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] Mushroom mushroom)
    {
        Logger.Info($"{__instance.GetNameWithRole()}, mushroom: {mushroom.name} / {mushroom.Id}, at {mushroom.origPosition}", "Spore Trigger");
        return !AmongUsClient.Instance.AmHost || !Options.DisableSporeTriggerOnFungle.GetBool();
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckUseZipline))]
public static class PlayerControlCheckUseZiplinePatch
{
    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target, [HarmonyArgument(1)] ZiplineBehaviour ziplineBehaviour, [HarmonyArgument(2)] bool fromTop)
    {
        ziplineBehaviour.downTravelTime = Options.ZiplineTravelTimeFromTop.GetFloat();
        ziplineBehaviour.upTravelTime = Options.ZiplineTravelTimeFromBottom.GetFloat();

        Logger.Info($"{__instance.GetNameWithRole()}, target: {target.GetNameWithRole()}, {(fromTop ? $"from Top, travel time: {ziplineBehaviour.downTravelTime}s" : $"from Bottom, travel time: {ziplineBehaviour.upTravelTime}s")}", "Zipline Use");

        if (AmongUsClient.Instance.AmHost)
        {
            if (Options.DisableZiplineFromTop.GetBool() && fromTop) return false;
            if (Options.DisableZiplineFromUnder.GetBool() && !fromTop) return false;

            if (__instance.GetCustomRole().IsImpostor() && Options.DisableZiplineForImps.GetBool()) return false;
            if (__instance.GetCustomRole().IsNeutral() && Options.DisableZiplineForNeutrals.GetBool()) return false;
            if (__instance.GetCustomRole().IsCrewmate() && Options.DisableZiplineForCrew.GetBool()) return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.ProtectPlayer))]
class PlayerControlProtectPlayerPatch
{
    public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] PlayerControl target)
    {
        Logger.Info($"{__instance.GetNameWithRole().RemoveHtmlTags()} => {target.GetNameWithRole().RemoveHtmlTags()}", "ProtectPlayer");
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RemoveProtection))]
class PlayerControlRemoveProtectionPatch
{
    public static void Postfix(PlayerControl __instance)
    {
        Logger.Info($"{__instance.GetNameWithRole().RemoveHtmlTags()}", "RemoveProtection");
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSetRole))]
class PlayerControlSetRolePatch
{
    public static bool Prefix(PlayerControl __instance, ref RoleTypes roleType)
    {
        var target = __instance;
        var targetName = __instance.GetNameWithRole();
        Logger.Info($"{targetName} => {roleType}", "PlayerControl.RpcSetRole");
        if (!ShipStatus.Instance.enabled) return true;
        if (roleType is RoleTypes.CrewmateGhost or RoleTypes.ImpostorGhost)
        {
            var targetIsKiller = target.Is(CustomRoleTypes.Impostor) || Main.ResetCamPlayerList.Contains(target.PlayerId);
            var ghostRoles = new Dictionary<PlayerControl, RoleTypes>();
            foreach (PlayerControl seer in Main.AllPlayerControls)
            {
                var self = seer.PlayerId == target.PlayerId;
                var seerIsKiller = seer.Is(CustomRoleTypes.Impostor) || Main.ResetCamPlayerList.Contains(seer.PlayerId);
                if (target.Is(CustomRoles.EvilSpirit))
                {
                    ghostRoles[seer] = RoleTypes.GuardianAngel;
                }
                else if ((self && targetIsKiller) || (!seerIsKiller && target.Is(CustomRoleTypes.Impostor)))
                {
                    ghostRoles[seer] = RoleTypes.ImpostorGhost;
                }
                else
                {
                    ghostRoles[seer] = RoleTypes.CrewmateGhost;
                }
            }

            if (target.Is(CustomRoles.EvilSpirit))
            {
                roleType = RoleTypes.GuardianAngel;
            }
            else if (ghostRoles.All(kvp => kvp.Value == RoleTypes.CrewmateGhost))
            {
                roleType = RoleTypes.CrewmateGhost;
            }
            else if (ghostRoles.All(kvp => kvp.Value == RoleTypes.ImpostorGhost))
            {
                roleType = RoleTypes.ImpostorGhost;
            }
            else
            {
                foreach ((var seer, var role) in ghostRoles)
                {
                    Logger.Info($"Desync {targetName} => {role} for {seer.GetNameWithRole().RemoveHtmlTags()}", "PlayerControl.RpcSetRole");
                    target.RpcSetRoleDesync(role, seer.GetClientId());
                }

                return false;
            }
        }

        return true;
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.SetRole))]
class PlayerControlLocalSetRolePatch
{
    public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] RoleTypes role)
    {
        if (!AmongUsClient.Instance.AmHost && !GameStates.IsModHost)
        {
            var moddedRole = role switch
            {
                RoleTypes.Impostor => CustomRoles.ImpostorTOHE,
                RoleTypes.Shapeshifter => CustomRoles.ShapeshifterTOHE,
                RoleTypes.Crewmate => CustomRoles.CrewmateTOHE,
                RoleTypes.Engineer => CustomRoles.EngineerTOHE,
                RoleTypes.Scientist => CustomRoles.ScientistTOHE,
                _ => CustomRoles.NotAssigned,
            };
            if (moddedRole != CustomRoles.NotAssigned)
            {
                Main.PlayerStates[__instance.PlayerId].SetMainRole(moddedRole);
            }
        }
    }
}