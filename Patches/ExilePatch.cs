using AmongUs.Data;
using HarmonyLib;
using System.Linq;
using TOHE.Roles.Crewmate;
using TOHE.Roles.Impostor;
using TOHE.Roles.Neutral;

namespace TOHE;

class ExileControllerWrapUpPatch
{
    private static GameData.PlayerInfo antiBlackout_LastExiled;

    public static GameData.PlayerInfo AntiBlackout_LastExiled { get => antiBlackout_LastExiled; set => antiBlackout_LastExiled = value; }

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
    static void WrapUpPostfix(GameData.PlayerInfo exiled)
    {
        if (AntiBlackout.OverrideExiledPlayer)
        {
            exiled = AntiBlackout_LastExiled;
        }

        bool DecidedWinner = false;
        if (!AmongUsClient.Instance.AmHost) return; //ホスト以外はこれ以降の処理を実行しません
        AntiBlackout.RestoreIsDead(doSend: false);
        if (!Collector.CollectorWin(false) && exiled != null) //判断集票者胜利
        {
            //霊界用暗転バグ対処
            if (!AntiBlackout.OverrideExiledPlayer && Main.ResetCamPlayerList.Contains(exiled.PlayerId))
                exiled.Object?.ResetPlayerCam(1f);

            exiled.IsDead = true;
            Main.PlayerStates[exiled.PlayerId].deathReason = PlayerState.DeathReason.Vote;
            var role = exiled.GetCustomRole();

            //判断冤罪师胜利
            if (Main.AllPlayerControls.Any(x => x.Is(CustomRoles.Innocent) && !x.IsAlive() && x.GetRealKiller()?.PlayerId == exiled.PlayerId))
            {
                if (!Options.InnocentCanWinByImp.GetBool() && role.IsImpostor())
                {
                    Logger.Info("冤罪的目标是内鬼，非常可惜啊", "Exeiled Winner Check");
                }
                else
                {
                    if (DecidedWinner) CustomWinnerHolder.ShiftWinnerAndSetWinner(CustomWinner.Innocent);
                    else CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Innocent);
                    Main.AllPlayerControls.Where(x => x.Is(CustomRoles.Innocent) && !x.IsAlive() && x.GetRealKiller()?.PlayerId == exiled.PlayerId)
                        .Do(x => CustomWinnerHolder.WinnerIds.Add(x.PlayerId));
                    DecidedWinner = true;
                }
            }

            //判断小丑胜利 (EAC封禁名单成为小丑达成胜利条件无法胜利)
            if (role == CustomRoles.Jester)
            {
                if (DecidedWinner) CustomWinnerHolder.ShiftWinnerAndSetWinner(CustomWinner.Jester);
                else CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Jester);
                CustomWinnerHolder.WinnerIds.Add(exiled.PlayerId);
                DecidedWinner = true;
            }

            //判断处刑人胜利
            if (Executioner.CheckExileTarget(exiled, DecidedWinner)) DecidedWinner = true;
            if (Lawyer.CheckExileTarget(exiled/*, DecidedWinner*/)) DecidedWinner = false;

            //判断恐怖分子胜利
            if (role == CustomRoles.Terrorist) Utils.CheckTerroristWin(exiled);

            if (role == CustomRoles.Devourer) Devourer.OnDevourerDied(exiled.PlayerId);

            if (CustomWinnerHolder.WinnerTeam != CustomWinner.Terrorist) Main.PlayerStates[exiled.PlayerId].SetDead();
        }
        if (AmongUsClient.Instance.AmHost && Main.IsFixedCooldown)
            Main.RefixCooldownDelay = Options.DefaultKillCooldown - 3f;

        Witch.RemoveSpelledPlayer();
        HexMaster.RemoveHexedPlayer();


        if (NiceSwapper.Vote.Any() && NiceSwapper.VoteTwo.Any())
        {
            foreach (PlayerControl swapper in Main.AllAlivePlayerControls)
            {
                if (swapper.Is(CustomRoles.NiceSwapper))
                {
                    NiceSwapper.NiceSwappermax[swapper.PlayerId]--;
                    NiceSwapper.Vote.Clear();
                    NiceSwapper.VoteTwo.Clear();
                    Main.NiceSwapSend = false;
                }
            }
        }

        foreach (PlayerControl pc in Main.AllPlayerControls)
        {
            pc.ResetKillCooldown();

            if (pc.Is(CustomRoles.Warlock))
            {
                Main.CursedPlayers[pc.PlayerId] = null;
                Main.isCurseAndKill[pc.PlayerId] = false;
                //RPC.RpcSyncCurseAndKill();
            }

            pc.RpcResetAbilityCooldown();
        }
        if (Options.RandomSpawn.GetBool() || Options.CurrentGameMode == CustomGameMode.SoloKombat || Options.CurrentGameMode == CustomGameMode.FFA)
        {
            RandomSpawn.SpawnMap map;
            switch (Main.NormalOptions.MapId)
            {
                case 0:
                    map = new RandomSpawn.SkeldSpawnMap();
                    Main.AllPlayerControls.Do(map.RandomTeleport);
                    break;
                case 1:
                    map = new RandomSpawn.MiraHQSpawnMap();
                    Main.AllPlayerControls.Do(map.RandomTeleport);
                    break;
                case 2:
                    map = new RandomSpawn.PolusSpawnMap();
                    Main.AllPlayerControls.Do(map.RandomTeleport);
                    break;
                case 5:
                    map = new RandomSpawn.FungleSpawnMap();
                    Main.AllPlayerControls.Do(map.RandomTeleport);
                    break;
            }
        }

        FallFromLadder.Reset();
        Utils.CountAlivePlayers(true);
        Utils.AfterMeetingTasks();
        Utils.SyncAllSettings();
        Utils.NotifyRoles();
    }

    static void WrapUpFinalizer(GameData.PlayerInfo exiled)
    {
        //WrapUpPostfixで例外が発生しても、この部分だけは確実に実行されます。
        if (AmongUsClient.Instance.AmHost)
        {
            _ = new LateTask(() =>
            {
                exiled = AntiBlackout_LastExiled;
                AntiBlackout.SendGameData();
                if (AntiBlackout.OverrideExiledPlayer && // 追放対象が上書きされる状態 (上書きされない状態なら実行不要)
                    exiled != null && //exiledがnullでない
                    exiled.Object != null) //exiled.Objectがnullでない
                {
                    exiled.Object.RpcExileV2();
                }
            }, 0.5f, "Restore IsDead Task");
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
            }, 0.5f, "AfterMeetingDeathPlayers Task");
        }

        GameStates.AlreadyDied |= !Utils.IsAllAlive;
        RemoveDisableDevicesPatch.UpdateDisableDevices();
        SoundManager.Instance.ChangeAmbienceVolume(DataManager.Settings.Audio.AmbienceVolume);
        Logger.Info("Start task phase", "Phase");
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