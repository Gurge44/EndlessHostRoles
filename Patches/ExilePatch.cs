using System;
using System.Linq;
using AmongUs.Data;
using EHR.AddOns.Common;
using EHR.AddOns.Crewmate;
using EHR.AddOns.Impostor;
using EHR.Crewmate;
using EHR.Impostor;
using EHR.Modules;
using EHR.Neutral;
using HarmonyLib;

namespace EHR.Patches;

internal static class ExileControllerWrapUpPatch
{
    private static void WrapUpPostfix(NetworkedPlayerInfo exiled)
    {
        var DecidedWinner = false;
        if (!AmongUsClient.Instance.AmHost) return;

        AntiBlackout.RestoreIsDead(false);

        if (!Collector.CollectorWin(false) && exiled != null)
        {
            exiled.IsDead = true;
            Main.PlayerStates[exiled.PlayerId].deathReason = PlayerState.DeathReason.Vote;
            CustomRoles role = exiled.GetCustomRole();

            if (Main.AllPlayerControls.Any(x => x.Is(CustomRoles.Innocent) && !x.IsAlive() && x.GetRealKiller()?.PlayerId == exiled.PlayerId))
            {
                if (!Options.InnocentCanWinByImp.GetBool() && role.IsImpostor())
                    Logger.Info("The exiled player is an impostor, but the Innocent cannot win due to the settings", "Exeiled Winner Check");
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
                Stressed.OnNonCrewmateEjected();
            else
                Stressed.OnCrewmateEjected();

            if (role.Is(Team.Impostor))
                Damocles.OnImpostorEjected();
            else
            {
                Cantankerous.OnCrewmateEjected();
                Mafioso.OnCrewmateEjected();
                Damocles.OnCrewmateEjected();
            }

            if (role == CustomRoles.Jester)
            {
                if (DecidedWinner) CustomWinnerHolder.ShiftWinnerAndSetWinner(CustomWinner.Jester);
                else CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Jester);

                CustomWinnerHolder.WinnerIds.Add(exiled.PlayerId);
                DecidedWinner = true;
            }

            if (Executioner.CheckExileTarget(exiled)) DecidedWinner = true;

            if (Lawyer.CheckExileTarget(exiled /*, DecidedWinner*/)) DecidedWinner = false;

            if (CustomWinnerHolder.WinnerTeam != CustomWinner.Terrorist)
                Main.PlayerStates[exiled.PlayerId].SetDead();
        }

        if (AmongUsClient.Instance.AmHost && Main.IsFixedCooldown) Main.RefixCooldownDelay = Options.DefaultKillCooldown - 3f;

        Witch.RemoveSpelledPlayer();

        NiceSwapper.OnExileFinish();

        foreach (PlayerControl pc in Main.AllPlayerControls)
        {
            if (pc.Is(CustomRoles.Warlock))
            {
                Warlock.CursedPlayers[pc.PlayerId] = null;
                Warlock.IsCurseAndKill[pc.PlayerId] = false;
            }

            pc.ResetKillCooldown(false);
            pc.RpcResetAbilityCooldown();
            PetsHelper.RpcRemovePet(pc);
        }

        if (Options.RandomSpawn.GetBool())
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

        FallFromLadder.Reset();
        Utils.CountAlivePlayers(true);

        LateTask.New(() => Utils.AfterPlayerDeathTasks(exiled.Object, onMeeting: true), 2.5f, "AfterPlayerDeathTasks For Exiled Player");
    }

    private static void WrapUpFinalizer()
    {
        // Even if an exception occurs in WrapUpPostfix, this part will be executed reliably.
        if (AmongUsClient.Instance.AmHost)
        {
            Utils.NotifyRoles();

            LateTask.New(() =>
            {
                AntiBlackout.SendGameData();
                AntiBlackout.SetRealPlayerRoles();
            }, 0.7f, "Restore IsDead Task");

            LateTask.New(() =>
            {
                if (!GameStates.IsEnded)
                {
                    AntiBlackout.ResetAfterMeeting();

                    Main.AfterMeetingDeathPlayers.Do(x =>
                    {
                        PlayerControl player = Utils.GetPlayerById(x.Key);
                        PlayerState state = Main.PlayerStates[x.Key];
                        Logger.Info($"{player?.GetNameWithRole().RemoveHtmlTags()} died with {x.Value}", "AfterMeetingDeath");
                        state.deathReason = x.Value;
                        state.SetDead();
                        player?.RpcExileV2();
                        if (x.Value == PlayerState.DeathReason.Suicide) player?.SetRealKiller(player, true);
                        Utils.AfterPlayerDeathTasks(player);
                    });

                    Main.AfterMeetingDeathPlayers.Clear();
                    Utils.AfterMeetingTasks();
                    Utils.SyncAllSettings();
                    Utils.NotifyRoles(NoCache: true);
                    Utils.CheckAndSetVentInteractions();
                }
            }, 2f, "AntiBlackout Reset & AfterMeetingTasks");
        }

        GameStates.AlreadyDied |= !Utils.IsAllAlive;
        RemoveDisableDevicesPatch.UpdateDisableDevices();
        SoundManager.Instance.ChangeAmbienceVolume(DataManager.Settings.Audio.AmbienceVolume);
        Logger.Info("Start task phase", "Phase");

        if (!AmongUsClient.Instance.AmHost || (Lovers.IsChatActivated && Lovers.PrivateChat.GetBool())) return;

        bool showRemainingKillers = Options.EnableKillerLeftCommand.GetBool() && Options.ShowImpRemainOnEject.GetBool();
        bool appendEjectionNotify = CheckForEndVotingPatch.EjectionText != string.Empty;
        Logger.Msg($"Ejection Text: {CheckForEndVotingPatch.EjectionText}", "ExilePatch");

        if ((showRemainingKillers || appendEjectionNotify) && CustomGameMode.Standard.IsActiveOrIntegrated())
        {
            string text = showRemainingKillers ? Utils.GetRemainingKillers(true) : string.Empty;
            text = $"<#ffffff>{text}</color>";
            var r = IRandom.Instance;

            foreach (PlayerControl pc in Main.AllAlivePlayerControls)
            {
                string finalText = text;

                if (appendEjectionNotify && !finalText.Contains(CheckForEndVotingPatch.EjectionText, StringComparison.OrdinalIgnoreCase))
                    finalText = $"\n<#ffffff>{CheckForEndVotingPatch.EjectionText}</color>\n{finalText}";

                if (!showRemainingKillers) finalText = finalText.TrimStart();

                pc.Notify(finalText, r.Next(7, 13));
            }
        }

        LateTask.New(() => ChatManager.SendPreviousMessagesToAll(true), 3f, log: false);
    }

    [HarmonyPatch(typeof(ExileController), nameof(ExileController.WrapUp))]
    private static class BaseExileControllerPatch
    {
        public static void Postfix(ExileController __instance)
        {
            try { WrapUpPostfix(__instance.initData.networkedPlayer); }
            finally { WrapUpFinalizer(); }
        }
    }

    [HarmonyPatch(typeof(AirshipExileController), nameof(AirshipExileController.WrapUpAndSpawn))]
    private static class AirshipExileControllerPatch
    {
        public static void Postfix(AirshipExileController __instance)
        {
            try { WrapUpPostfix(__instance.initData.networkedPlayer); }
            finally { WrapUpFinalizer(); }
        }
    }
}

[HarmonyPatch(typeof(PbExileController), nameof(PbExileController.PlayerSpin))]
internal class PolusExileHatFixPatch
{
    public static void Prefix(PbExileController __instance)
    {
        __instance.Player.cosmetics.hat.transform.localPosition = new(-0.2f, 0.6f, 1.1f);
    }
}