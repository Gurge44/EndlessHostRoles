using System.Linq;
using AmongUs.Data;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Roles.AddOns.Crewmate;
using EHR.Roles.AddOns.Impostor;
using EHR.Roles.Crewmate;
using EHR.Roles.Impostor;
using EHR.Roles.Neutral;
using HarmonyLib;

namespace EHR.Patches;

class ExileControllerWrapUpPatch
{
    private static GameData.PlayerInfo antiBlackout_LastExiled;

    public static GameData.PlayerInfo AntiBlackout_LastExiled
    {
        get => antiBlackout_LastExiled;
        set => antiBlackout_LastExiled = value;
    }

    static void WrapUpPostfix(GameData.PlayerInfo exiled)
    {
        if (AntiBlackout.OverrideExiledPlayer)
        {
            exiled = AntiBlackout_LastExiled;
        }

        bool DecidedWinner = false;
        if (!AmongUsClient.Instance.AmHost) return;
        AntiBlackout.RestoreIsDead(doSend: false);
        if (!Collector.CollectorWin(false) && exiled != null)
        {
            if (!AntiBlackout.OverrideExiledPlayer && Main.ResetCamPlayerList.Contains(exiled.PlayerId))
                exiled.Object?.ResetPlayerCam(1f);

            exiled.IsDead = true;
            Main.PlayerStates[exiled.PlayerId].deathReason = PlayerState.DeathReason.Vote;
            var role = exiled.GetCustomRole();

            if (Main.AllPlayerControls.Any(x => x.Is(CustomRoles.Innocent) && !x.IsAlive() && x.GetRealKiller()?.PlayerId == exiled.PlayerId))
            {
                if (!Options.InnocentCanWinByImp.GetBool() && role.IsImpostor())
                {
                    Logger.Info("冤罪的目标是内鬼，非常可惜啊", "Exeiled Winner Check");
                }
                else
                {
                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Innocent);
                    Main.AllPlayerControls.Where(x => x.Is(CustomRoles.Innocent) && !x.IsAlive() && x.GetRealKiller()?.PlayerId == exiled.PlayerId)
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
                //RPC.RpcSyncCurseAndKill();
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
                _ => null,
            };
            if (map != null) Main.AllAlivePlayerControls.Do(map.RandomTeleport);
        }

        if (exiled != null && Options.SpawnAdditionalRefugeeOnImpsDead.GetBool() && Main.AllAlivePlayerControls.Length >= Options.SpawnAdditionalRefugeeMinAlivePlayers.GetInt() && !CustomRoles.Refugee.RoleExist(countDead: true) && !Main.AllAlivePlayerControls.Any(x => x.PlayerId != exiled.PlayerId && (x.Is(CustomRoleTypes.Impostor) || (x.IsNeutralKiller() && Options.SpawnAdditionalRefugeeWhenNKAlive.GetBool()))))
        {
            PlayerControl[] ListToChooseFrom = Options.UsePets.GetBool() ? Main.AllAlivePlayerControls.Where(x => x.PlayerId != exiled.PlayerId && x.Is(CustomRoleTypes.Crewmate)).ToArray() : Main.AllAlivePlayerControls.Where(x => x.PlayerId != exiled.PlayerId && x.Is(CustomRoleTypes.Crewmate) && x.GetCustomRole().GetRoleTypes() == RoleTypes.Impostor).ToArray();

            if (ListToChooseFrom.Length > 0)
            {
                var index = IRandom.Instance.Next(0, ListToChooseFrom.Length);
                var pc = ListToChooseFrom[index];
                pc.RpcSetCustomRole(CustomRoles.Refugee);
                pc.SetKillCooldown();
                Logger.Warn($"{pc.GetRealName()} is now a Refugee since all Impostors are dead", "Add Refugee");

                pc.ChangeBasisToImpostor();
            }
            else Logger.Msg("No Player to change to Refugee.", "Add Refugee");
        }

        FallFromLadder.Reset();
        Utils.CountAlivePlayers(true);
        Utils.AfterMeetingTasks();
        Utils.SyncAllSettings();
        Utils.NotifyRoles(ForceLoop: true);
    }

    static void WrapUpFinalizer(GameData.PlayerInfo exiled)
    {
        // Even if an exception occurs in WrapUpPostfix, this part will be executed reliably.
        if (AmongUsClient.Instance.AmHost)
        {
            _ = new LateTask(() =>
            {
                exiled = AntiBlackout_LastExiled;
                AntiBlackout.SendGameData();
                if (AntiBlackout.OverrideExiledPlayer && // State where the exile target is overwritten (no need to execute if it is not overwritten)
                    exiled != null &&
                    exiled.Object != null)
                {
                    exiled.Object.RpcExileV2();
                }
            }, 0.8f, "Restore IsDead Task");
            _ = new LateTask(() =>
            {
                Main.AfterMeetingDeathPlayers.Do(x =>
                {
                    var player = Utils.GetPlayerById(x.Key);
                    var state = Main.PlayerStates[x.Key];
                    Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags()} died with {x.Value}", "AfterMeetingDeath");
                    state.deathReason = x.Value;
                    state.SetDead();
                    player?.RpcExileV2();
                    if (x.Value == PlayerState.DeathReason.Suicide)
                        player?.SetRealKiller(player, true);
                    if (Main.ResetCamPlayerList.Contains(x.Key))
                        player?.ResetPlayerCam(1f);
                    Utils.AfterPlayerDeathTasks(player);
                });
                Main.AfterMeetingDeathPlayers.Clear();
            }, 0.9f, "AfterMeetingDeathPlayers Task");
        }

        GameStates.AlreadyDied |= !Utils.IsAllAlive;
        RemoveDisableDevicesPatch.UpdateDisableDevices();
        SoundManager.Instance.ChangeAmbienceVolume(DataManager.Settings.Audio.AmbienceVolume);
        Logger.Info("Start task phase", "Phase");

        bool showRemainingKillers = Options.EnableKillerLeftCommand.GetBool() && Options.ShowImpRemainOnEject.GetBool();
        bool appendEjectionNotify = CheckForEndVotingPatch.EjectionText != string.Empty;
        Logger.Warn($"Ejection Text: {CheckForEndVotingPatch.EjectionText}", "debug");
        if ((showRemainingKillers || appendEjectionNotify) && Options.CurrentGameMode == CustomGameMode.Standard)
        {
            _ = new LateTask(() =>
            {
                var text = showRemainingKillers ? Utils.GetRemainingKillers(notify: true) : string.Empty;
                text = $"<#ffffff>{text}</color>";
                var r = IRandom.Instance;
                foreach (var pc in Main.AllAlivePlayerControls)
                {
                    string finalText = text;
                    if (NameNotifyManager.Notice.TryGetValue(pc.PlayerId, out var notify))
                    {
                        finalText = $"\n{notify.TEXT}\n{finalText}";
                    }

                    if (appendEjectionNotify)
                    {
                        finalText = $"\n<#ffffff>{CheckForEndVotingPatch.EjectionText}</color>\n{finalText}";
                    }

                    if (!showRemainingKillers) finalText = finalText.TrimStart();

                    pc.Notify(finalText, r.Next(7, 13));
                }
            }, 0.5f, log: false);
        }

        _ = new LateTask(() => { ChatManager.SendPreviousMessagesToAll(); }, 3f, log: false);
    }

    [HarmonyPatch(typeof(ExileController), nameof(ExileController.WrapUp))]
    class BaseExileControllerPatch
    {
        public static void Postfix(ExileController __instance)
        {
            try
            {
                WrapUpPostfix(__instance.exiled);
            }
            finally
            {
                WrapUpFinalizer(__instance.exiled);
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
                WrapUpPostfix(__instance.exiled);
            }
            finally
            {
                WrapUpFinalizer(__instance.exiled);
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