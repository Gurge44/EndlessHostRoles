using System.Linq;
using AmongUs.GameOptions;
using TOHE.Modules;
using UnityEngine;

namespace TOHE.Roles.Neutral
{
    internal class Arsonist : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        public override void Add(byte playerId)
        {
            On = true;
            foreach (PlayerControl ar in Main.AllPlayerControls)
            {
                Main.isDoused.Add((playerId, ar.PlayerId), false);
            }
        }

        public override void Init()
        {
            On = false;
        }

        public override bool CanUseKillButton(PlayerControl pc)
        {
            return Options.ArsonistCanIgniteAnytime.GetBool() ? Utils.GetDousedPlayerCount(pc.PlayerId).Item1 < Options.ArsonistMaxPlayersToIgnite.GetInt() : !pc.IsDouseDone();
        }

        public override bool CanUseImpostorVentButton(PlayerControl pc)
        {
            return pc.IsDouseDone() || (Options.ArsonistCanIgniteAnytime.GetBool() && (Utils.GetDousedPlayerCount(pc.PlayerId).Item1 >= Options.ArsonistMinPlayersToIgnite.GetInt() || pc.inVent));
        }

        public override void SetKillCooldown(byte id)
        {
            Main.AllPlayerKillCooldown[id] = Options.ArsonistCooldown.GetFloat();
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            opt.SetVision(false);
        }

        public override string GetProgressText(byte playerId, bool comms)
        {
            var doused = Utils.GetDousedPlayerCount(playerId);
            return Utils.ColorString(Utils.GetRoleColor(CustomRoles.Arsonist).ShadeColor(0.25f), !Options.ArsonistCanIgniteAnytime.GetBool() ? $"<color=#777777>-</color> {doused.Item1}/{doused.Item2}" : $"<color=#777777>-</color> {doused.Item1}/{Options.ArsonistMaxPlayersToIgnite.GetInt()}");
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            killer.SetKillCooldown(Options.ArsonistDouseTime.GetFloat());
            if (!Main.isDoused[(killer.PlayerId, target.PlayerId)] && !Main.ArsonistTimer.ContainsKey(killer.PlayerId))
            {
                Main.ArsonistTimer.Add(killer.PlayerId, (target, 0f));
                Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target, ForceLoop: true);
                RPC.SetCurrentDousingTarget(killer.PlayerId, target.PlayerId);
            }

            return false;
        }

        public override void OnPet(PlayerControl pc)
        {
            Ignite(pc.MyPhysics);
        }

        public override void OnCoEnterVent(PlayerPhysics physics, int ventId)
        {
            if (AmongUsClient.Instance.IsGameStarted)
            {
                Ignite(physics);
            }
        }

        private static void Ignite(PlayerPhysics physics)
        {
            if (physics.myPlayer.IsDouseDone())
            {
                CustomSoundsManager.RPCPlayCustomSoundAll("Boom");
                foreach (PlayerControl pc in Main.AllAlivePlayerControls)
                {
                    if (pc != physics.myPlayer)
                    {
                        pc.Suicide(PlayerState.DeathReason.Torched, physics.myPlayer);
                    }
                }

                foreach (PlayerControl pc in Main.AllPlayerControls)
                {
                    pc.KillFlash();
                }

                CustomWinnerHolder.ShiftWinnerAndSetWinner(CustomWinner.Arsonist); //焼殺で勝利した人も勝利させる
                CustomWinnerHolder.WinnerIds.Add(physics.myPlayer.PlayerId);
                return;
            }

            if (Options.ArsonistCanIgniteAnytime.GetBool())
            {
                var douseCount = Utils.GetDousedPlayerCount(physics.myPlayer.PlayerId).Item1;
                if (douseCount >= Options.ArsonistMinPlayersToIgnite.GetInt()) // Don't check for max, since the player would not be able to ignite at all if they somehow get more players doused than the max
                {
                    if (douseCount > Options.ArsonistMaxPlayersToIgnite.GetInt()) Logger.Warn("Arsonist Ignited with more players doused than the maximum amount in the settings", "Arsonist Ignite");
                    foreach (PlayerControl pc in Main.AllAlivePlayerControls)
                    {
                        if (!physics.myPlayer.IsDousedPlayer(pc))
                            continue;
                        pc.KillFlash();
                        pc.Suicide(PlayerState.DeathReason.Torched, physics.myPlayer);
                    }

                    var apc = Main.AllAlivePlayerControls.Length;
                    switch (apc)
                    {
                        case 1:
                            CustomWinnerHolder.ShiftWinnerAndSetWinner(CustomWinner.Arsonist);
                            CustomWinnerHolder.WinnerIds.Add(physics.myPlayer.PlayerId);
                            break;
                        case 2:
                        {
                            foreach (var x in Main.AllAlivePlayerControls.Where(p => p.PlayerId != physics.myPlayer.PlayerId).ToArray())
                            {
                                if (!x.GetCustomRole().IsImpostor() && !x.GetCustomRole().IsNeutralKilling())
                                {
                                    CustomWinnerHolder.ShiftWinnerAndSetWinner(CustomWinner.Arsonist);
                                    CustomWinnerHolder.WinnerIds.Add(physics.myPlayer.PlayerId);
                                }
                            }

                            break;
                        }
                    }
                }
            }
        }

        public override void OnGlobalFixedUpdate(PlayerControl player, bool lowLoad)
        {
            var playerId = player.PlayerId;
            if (GameStates.IsInTask && Main.ArsonistTimer.ContainsKey(playerId))
            {
                var arTarget = Main.ArsonistTimer[playerId].PLAYER;
                if (!player.IsAlive() || Pelican.IsEaten(playerId))
                {
                    Main.ArsonistTimer.Remove(playerId);
                    Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: arTarget, ForceLoop: true);
                    RPC.ResetCurrentDousingTarget(playerId);
                }
                else
                {
                    var ar_target = Main.ArsonistTimer[playerId].PLAYER;
                    var ar_time = Main.ArsonistTimer[playerId].TIMER;
                    if (!ar_target.IsAlive())
                    {
                        Main.ArsonistTimer.Remove(playerId);
                    }
                    else if (ar_time >= Options.ArsonistDouseTime.GetFloat())
                    {
                        player.SetKillCooldown();
                        Main.ArsonistTimer.Remove(playerId);
                        Main.isDoused[(playerId, ar_target.PlayerId)] = true;
                        player.RpcSetDousedPlayer(ar_target, true);
                        Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: arTarget, ForceLoop: true);
                        RPC.ResetCurrentDousingTarget(playerId);
                    }
                    else
                    {
                        float range = NormalGameOptionsV07.KillDistances[Mathf.Clamp(player.Is(CustomRoles.Reach) ? 2 : Main.NormalOptions.KillDistance, 0, 2)] + 0.5f;
                        float dis = Vector2.Distance(player.transform.position, ar_target.transform.position);
                        if (dis <= range)
                        {
                            Main.ArsonistTimer[playerId] = (ar_target, ar_time + Time.fixedDeltaTime);
                        }
                        else
                        {
                            Main.ArsonistTimer.Remove(playerId);
                            Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: arTarget, ForceLoop: true);
                            RPC.ResetCurrentDousingTarget(playerId);

                            Logger.Info($"Canceled: {player.GetNameWithRole().RemoveHtmlTags()}", "Arsonist");
                        }
                    }
                }
            }
        }
    }
}