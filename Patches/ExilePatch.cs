using System;
using System.Linq;
using AmongUs.Data;
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
    public static NetworkedPlayerInfo LastExiled;

    private static void WrapUpPostfix(NetworkedPlayerInfo exiled)
    {
        var decidedWinner = false;
        if (!AmongUsClient.Instance.AmHost) return;

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

                    decidedWinner = true;
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
                if (decidedWinner) CustomWinnerHolder.ShiftWinnerAndSetWinner(CustomWinner.Jester);
                else CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Jester);

                CustomWinnerHolder.WinnerIds.Add(exiled.PlayerId);
                decidedWinner = true;
            }

            if (Executioner.CheckExileTarget(exiled)) decidedWinner = true;

            if (Lawyer.CheckExileTarget(exiled /*, DecidedWinner*/)) decidedWinner = false;

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

        if (Options.RandomSpawn.GetBool() && Main.CurrentMap != MapNames.Airship)
        {
            var map = RandomSpawn.SpawnMap.GetSpawnMap();
            Main.AllAlivePlayerControls.Do(player => map.RandomTeleport(player));
        }

        FallFromLadder.Reset();
        Utils.CountAlivePlayers(true);

        if (exiled == null) return;
        byte id = exiled.PlayerId;

        LateTask.New(() =>
        {
            if (GameStates.IsEnded) return;
            PlayerControl player = id.GetPlayer();
            if (player != null) Utils.AfterPlayerDeathTasks(player, true);
        }, 2.5f, "AfterPlayerDeathTasks For Exiled Player");
    }

    private static void WrapUpFinalizer()
    {
        // Even if an exception occurs in WrapUpPostfix, this part will be executed reliably.

        if (AmongUsClient.Instance.AmHost)
        {
            LateTask.New(() =>
            {
                if (GameStates.IsEnded) return;
                AntiBlackout.RevertToActualRoleTypes();
            }, Math.Max(1f, Utils.CalculatePingDelay() * 2f), "Revert AntiBlackout Measures");
        }

        GameStates.AlreadyDied |= !Utils.IsAllAlive;
        RemoveDisableDevicesPatch.UpdateDisableDevices();
        SoundManager.Instance.ChangeAmbienceVolume(DataManager.Settings.Audio.AmbienceVolume);
        Logger.Info("Start task phase", "Phase");

        if (!AmongUsClient.Instance.AmHost || GameStates.IsEnded) return;

        bool showRemainingKillers = Options.EnableKillerLeftCommand.GetBool() && Options.ShowImpRemainOnEject.GetBool();
        bool ejectionNotify = CheckForEndVotingPatch.EjectionText != string.Empty;
        Logger.Msg($"Ejection Text: {CheckForEndVotingPatch.EjectionText}", "ExilePatch");

        if ((showRemainingKillers || ejectionNotify) && Options.CurrentGameMode == CustomGameMode.Standard)
        {
            string text = showRemainingKillers ? Utils.GetRemainingKillers(true) : string.Empty;
            string finalText = ejectionNotify ? "<#ffffff>" + CheckForEndVotingPatch.EjectionText.Trim() : text;

            if (!string.IsNullOrEmpty(finalText))
            {
                var r = IRandom.Instance;

                foreach (PlayerControl pc in Main.AllAlivePlayerControls)
                    pc.Notify(finalText, r.Next(7, 13));
            }
        }

        LateTask.New(() =>
        {
            if (ChatCommands.HasMessageDuringEjectionScreen)
                ChatManager.ClearChat(Main.AllAlivePlayerControls);
        }, 3f, log: false);
    }

    public static void AfterMeetingTasks()
    {
        if (GameStates.IsEnded) return;

        Main.AfterMeetingDeathPlayers.Keys.ToValidPlayers().Do(x => x.RpcExileV2());

        foreach ((byte id, PlayerState.DeathReason deathReason) in Main.AfterMeetingDeathPlayers)
        {
            var player = id.GetPlayer();
            var state = Main.PlayerStates[id];

            Logger.Info($"{Main.AllPlayerNames[id]} ({state.MainRole}) died with {deathReason}", "AfterMeetingDeath");

            state.deathReason = deathReason;
            state.SetDead();

            if (player == null) continue;

            if (deathReason == PlayerState.DeathReason.Suicide)
                player.SetRealKiller(player, true);

            Utils.AfterPlayerDeathTasks(player);
        }

        Main.AfterMeetingDeathPlayers.Clear();

        Utils.AfterMeetingTasks();
        Utils.SyncAllSettings();
        Utils.CheckAndSetVentInteractions();

        Main.Instance.StartCoroutine(Utils.NotifyEveryoneAsync(speed: 5));
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

    [HarmonyPatch(typeof(AirshipExileController._WrapUpAndSpawn_d__11), nameof(AirshipExileController._WrapUpAndSpawn_d__11.MoveNext))]
    private static class AirshipExileControllerPatch
    {
        public static void Postfix(AirshipExileController._WrapUpAndSpawn_d__11 __instance, ref bool __result)
        {
            if (__result) return;

            try { WrapUpPostfix(__instance.__4__this.initData.networkedPlayer); }
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