using System;
using System.Linq;
using AmongUs.Data;
using EHR.AddOns.Common;
using EHR.AddOns.Crewmate;
using EHR.AddOns.Impostor;
using EHR.Crewmate;
using EHR.Impostor;
using EHR.Neutral;
using HarmonyLib;

namespace EHR.Patches;

static class ExileControllerWrapUpPatch
{
    public static NetworkedPlayerInfo AntiBlackoutLastExiled { get; set; }

    static void WrapUpPostfix(NetworkedPlayerInfo exiled)
    {
        bool DecidedWinner = false;
        if (!AmongUsClient.Instance.AmHost) return;
        AntiBlackout.RestoreIsDead(doSend: false);
        AntiBlackoutLastExiled = exiled;
        if (!Collector.CollectorWin(false) && exiled != null)
        {
            exiled.IsDead = true;
            Main.PlayerStates[exiled.PlayerId].deathReason = PlayerState.DeathReason.Vote;
            var role = exiled.GetCustomRole();

            if (Main.AllPlayerControls.Any(x => x.Is(CustomRoles.Innocent) && !x.IsAlive() && x.GetRealKiller()?.PlayerId == exiled.PlayerId))
            {
                if (!Options.InnocentCanWinByImp.GetBool() && role.IsImpostor())
                {
                    Logger.Info("The exiled player is an impostor, but the Innocent cannot win due to the settings", "Exeiled Winner Check");
                }
                else
                {
                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Innocent);
                    Main.AllPlayerControls
                        .Where(x => x.Is(CustomRoles.Innocent) && !x.IsAlive() && x.GetRealKiller()?.PlayerId == exiled.PlayerId)
                        .Do(x => CustomWinnerHolder.WinnerIds.Add(x.PlayerId));
                    DecidedWinner = true;
                }
            }

            if (role.Is(Team.Impostor) || role.Is(Team.Neutral))
            {
                Stressed.OnNonCrewmateEjected();
            }
            else
            {
                Stressed.OnCrewmateEjected();
            }

            if (role.Is(Team.Impostor))
            {
                Damocles.OnImpostorEjected();
            }
            else
            {
                Cantankerous.OnCrewmateEjected();
                Mafioso.OnCrewmateEjected();
                Damocles.OnCrewmateEjected();
            }

            switch (role)
            {
                case CustomRoles.Jester:
                    if (DecidedWinner) CustomWinnerHolder.ShiftWinnerAndSetWinner(CustomWinner.Jester);
                    else CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Jester);
                    CustomWinnerHolder.WinnerIds.Add(exiled.PlayerId);
                    DecidedWinner = true;
                    break;
                case CustomRoles.Terrorist:
                    Utils.CheckTerroristWin(exiled);
                    break;
                case CustomRoles.Devourer:
                    Devourer.OnDevourerDied(exiled.PlayerId);
                    break;
                case CustomRoles.Medic:
                    Medic.IsDead(exiled.Object);
                    break;
            }

            if (Executioner.CheckExileTarget(exiled)) DecidedWinner = true;
            if (Lawyer.CheckExileTarget(exiled /*, DecidedWinner*/)) DecidedWinner = false;

            if (CustomWinnerHolder.WinnerTeam != CustomWinner.Terrorist) Main.PlayerStates[exiled.PlayerId].SetDead();
        }

        if (AmongUsClient.Instance.AmHost && Main.IsFixedCooldown)
            Main.RefixCooldownDelay = Options.DefaultKillCooldown - 3f;

        Witch.RemoveSpelledPlayer();

        NiceSwapper.OnExileFinish();

        foreach (PlayerControl pc in Main.AllPlayerControls)
        {
            if (pc.Is(CustomRoles.Warlock))
            {
                Warlock.CursedPlayers[pc.PlayerId] = null;
                Warlock.IsCurseAndKill[pc.PlayerId] = false;
            }

            pc.ResetKillCooldown();
            pc.RpcResetAbilityCooldown();
            PetsPatch.RpcRemovePet(pc);
        }

        if (Options.RandomSpawn.GetBool() || Options.CurrentGameMode != CustomGameMode.Standard)
        {
            RandomSpawn.SpawnMap map = Main.NormalOptions.MapId switch
            {
                0 => new RandomSpawn.SkeldSpawnMap(),
                1 => new RandomSpawn.MiraHQSpawnMap(),
                2 => new RandomSpawn.PolusSpawnMap(),
                3 => new RandomSpawn.DleksSpawnMap(),
                5 => new RandomSpawn.FungleSpawnMap(),
                _ => null
            };
            if (map != null) Main.AllAlivePlayerControls.Do(map.RandomTeleport);
        }

        Utils.CheckAndSpawnAdditionalRefugee(exiled);

        FallFromLadder.Reset();
        Utils.CountAlivePlayers(true);
        Utils.AfterMeetingTasks();
        Utils.SyncAllSettings();
        Utils.NotifyRoles(ForceLoop: true);
        Utils.CheckAndSetVentInteractions();
    }

    static void WrapUpFinalizer()
    {
        // Even if an exception occurs in WrapUpPostfix, this part will be executed reliably.
        if (AmongUsClient.Instance.AmHost)
        {
            LateTask.New(() =>
            {
                AntiBlackout.SendGameData();
                AntiBlackout.SetRealPlayerRoles();
            }, 1.1f, "Restore IsDead Task");
            LateTask.New(() =>
            {
                Main.AfterMeetingDeathPlayers.Do(x =>
                {
                    var player = Utils.GetPlayerById(x.Key);
                    var state = Main.PlayerStates[x.Key];
                    Logger.Info($"{player?.GetNameWithRole().RemoveHtmlTags()} died with {x.Value}", "AfterMeetingDeath");
                    state.deathReason = x.Value;
                    state.SetDead();
                    player?.RpcExileV2();
                    if (x.Value == PlayerState.DeathReason.Suicide)
                        player?.SetRealKiller(player, true);
                    Utils.AfterPlayerDeathTasks(player);
                });
                Main.AfterMeetingDeathPlayers.Clear();
                AntiBlackout.ResetAfterMeeting();
            }, 1.2f, "AfterMeetingDeathPlayers Task");
        }

        GameStates.AlreadyDied |= !Utils.IsAllAlive;
        RemoveDisableDevicesPatch.UpdateDisableDevices();
        SoundManager.Instance.ChangeAmbienceVolume(DataManager.Settings.Audio.AmbienceVolume);
        Logger.Info("Start task phase", "Phase");

        if (Lovers.IsChatActivated && Lovers.PrivateChat.GetBool()) return;

        bool showRemainingKillers = Options.EnableKillerLeftCommand.GetBool() && Options.ShowImpRemainOnEject.GetBool();
        bool appendEjectionNotify = CheckForEndVotingPatch.EjectionText != string.Empty;
        Logger.Msg($"Ejection Text: {CheckForEndVotingPatch.EjectionText}", "ExilePatch");
        if ((showRemainingKillers || appendEjectionNotify) && Options.CurrentGameMode == CustomGameMode.Standard)
        {
            LateTask.New(() =>
            {
                var text = showRemainingKillers ? Utils.GetRemainingKillers(notify: true) : string.Empty;
                text = $"<#ffffff>{text}</color>";
                var r = IRandom.Instance;
                foreach (var pc in Main.AllAlivePlayerControls)
                {
                    string finalText = text;

                    if (appendEjectionNotify && !finalText.Contains(CheckForEndVotingPatch.EjectionText, StringComparison.OrdinalIgnoreCase))
                    {
                        finalText = $"\n<#ffffff>{CheckForEndVotingPatch.EjectionText}</color>\n{finalText}";
                    }

                    if (!showRemainingKillers) finalText = finalText.TrimStart();

                    pc.Notify(finalText, r.Next(7, 13));
                }
            }, 0.5f, log: false);
        }

        LateTask.New(() => ChatManager.SendPreviousMessagesToAll(clear: true), 3f, log: false);
    }

    [HarmonyPatch(typeof(ExileController), nameof(ExileController.WrapUp))]
    class BaseExileControllerPatch
    {
        public static void Postfix(ExileController __instance)
        {
            try
            {
                WrapUpPostfix(__instance.initData.networkedPlayer);
            }
            finally
            {
                WrapUpFinalizer();
            }
        }
    }

    [HarmonyPatch(typeof(AirshipExileController), nameof(AirshipExileController.WrapUpAndSpawn))]
    class AirshipExileControllerPatch
    {
        public static void Postfix(AirshipExileController __instance)
        {
            try
            {
                WrapUpPostfix(__instance.initData.networkedPlayer);
            }
            finally
            {
                WrapUpFinalizer();
            }
        }
    }
}

[HarmonyPatch(typeof(PbExileController), nameof(PbExileController.PlayerSpin))]
class PolusExileHatFixPatch
{
    public static void Prefix(PbExileController __instance)
    {
        __instance.Player.cosmetics.hat.transform.localPosition = new(-0.2f, 0.6f, 1.1f);
    }
}