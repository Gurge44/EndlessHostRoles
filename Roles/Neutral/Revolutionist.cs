using AmongUs.GameOptions;
using System.Linq;
using TOHE.Modules;
using UnityEngine;
using static TOHE.Options;

namespace TOHE.Roles.Neutral
{
    internal class Revolutionist : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(18400, TabGroup.NeutralRoles, CustomRoles.Revolutionist);
            RevolutionistDrawTime = FloatOptionItem.Create(18410, "RevolutionistDrawTime", new(0f, 90f, 1f), 3f, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Revolutionist])
                .SetValueFormat(OptionFormat.Seconds);
            RevolutionistCooldown = FloatOptionItem.Create(18411, "RevolutionistCooldown", new(0f, 100f, 1f), 10f, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Revolutionist])
                .SetValueFormat(OptionFormat.Seconds);
            RevolutionistDrawCount = IntegerOptionItem.Create(18412, "RevolutionistDrawCount", new(0, 14, 1), 6, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Revolutionist])
                .SetValueFormat(OptionFormat.Players);
            RevolutionistKillProbability = IntegerOptionItem.Create(18413, "RevolutionistKillProbability", new(0, 100, 5), 15, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Revolutionist])
                .SetValueFormat(OptionFormat.Percent);
            RevolutionistVentCountDown = FloatOptionItem.Create(18414, "RevolutionistVentCountDown", new(0f, 180f, 1f), 15f, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Revolutionist])
                .SetValueFormat(OptionFormat.Seconds);
        }


        public override void Add(byte playerId)
        {
            On = true;
            foreach (PlayerControl ar in Main.AllPlayerControls)
            {
                Main.isDraw.Add((playerId, ar.PlayerId), false);
            }
        }

        public override void Init()
        {
            On = false;
        }

        public override bool CanUseKillButton(PlayerControl pc)
        {
            return !pc.IsDrawDone();
        }

        public override bool CanUseImpostorVentButton(PlayerControl pc)
        {
            return pc.IsDrawDone();
        }

        public override bool CanUseSabotage(PlayerControl pc)
        {
            return false;
        }

        public override void SetKillCooldown(byte id)
        {
            Main.AllPlayerKillCooldown[id] = RevolutionistCooldown.GetFloat();
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            opt.SetVision(false);
        }

        public override string GetProgressText(byte playerId, bool comms)
        {
            var draw = Utils.GetDrawPlayerCount(playerId, out _);
            return Utils.ColorString(Utils.GetRoleColor(CustomRoles.Revolutionist).ShadeColor(0.25f), $"<color=#777777>-</color> {draw.Item1}/{draw.Item2}");
        }

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            hud.KillButton?.OverrideText(Translator.GetString("RevolutionistDrawButtonText"));
            hud.ImpostorVentButton.buttonLabelText.text = Translator.GetString("RevolutionistVentButtonText");

            hud.SabotageButton?.ToggleVisible(false);
            hud.AbilityButton?.ToggleVisible(false);
            hud.ImpostorVentButton?.ToggleVisible(false);
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            killer.SetKillCooldown(RevolutionistDrawTime.GetFloat());
            if (!Main.isDraw[(killer.PlayerId, target.PlayerId)] && !Main.RevolutionistTimer.ContainsKey(killer.PlayerId))
            {
                Main.RevolutionistTimer.TryAdd(killer.PlayerId, (target, 0f));
                Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target, ForceLoop: true);
                RPC.SetCurrentDrawTarget(killer.PlayerId, target.PlayerId);
            }

            return false;
        }

        public override void OnReportDeadBody()
        {
            foreach (var x in Main.RevolutionistStart)
            {
                var tar = Utils.GetPlayerById(x.Key);
                if (tar == null) continue;
                tar.Data.IsDead = true;
                Main.PlayerStates[tar.PlayerId].deathReason = PlayerState.DeathReason.Sacrifice;
                tar.RpcExileV2();
                Main.PlayerStates[tar.PlayerId].SetDead();
            }

            Main.RevolutionistTimer.Clear();
            Main.RevolutionistStart.Clear();
            Main.RevolutionistLastTime.Clear();
        }

        public override void OnFixedUpdate(PlayerControl player)
        {
            var playerId = player.PlayerId;

            if (GameStates.IsInTask && Main.RevolutionistTimer.ContainsKey(playerId))
            {
                var rvTarget = Main.RevolutionistTimer[playerId].PLAYER;
                if (!player.IsAlive() || Pelican.IsEaten(playerId))
                {
                    Main.RevolutionistTimer.Remove(playerId);
                    Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: rvTarget, ForceLoop: true);
                    RPC.ResetCurrentDrawTarget(playerId);
                }
                else
                {
                    var rv_target = Main.RevolutionistTimer[playerId].PLAYER;
                    var rv_time = Main.RevolutionistTimer[playerId].TIMER;
                    if (!rv_target.IsAlive())
                    {
                        Main.RevolutionistTimer.Remove(playerId);
                    }
                    else if (rv_time >= RevolutionistDrawTime.GetFloat())
                    {
                        player.SetKillCooldown();
                        Main.RevolutionistTimer.Remove(playerId);
                        Main.isDraw[(playerId, rv_target.PlayerId)] = true;
                        player.RpcSetDrawPlayer(rv_target, true);
                        Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: rv_target, ForceLoop: true);
                        RPC.ResetCurrentDrawTarget(playerId);
                        if (IRandom.Instance.Next(1, 100) <= RevolutionistKillProbability.GetInt())
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
                        float dis = Vector2.Distance(player.transform.position, rv_target.transform.position);
                        if (dis <= range)
                        {
                            Main.RevolutionistTimer[playerId] = (rv_target, rv_time + Time.fixedDeltaTime);
                        }
                        else
                        {
                            Main.RevolutionistTimer.Remove(playerId);
                            Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: rv_target);
                            RPC.ResetCurrentDrawTarget(playerId);

                            Logger.Info($"Canceled: {player.GetNameWithRole().RemoveHtmlTags()}", "Revolutionist");
                        }
                    }
                }
            }

            if (GameStates.IsInTask && player.IsDrawDone() && player.IsAlive())
            {
                if (Main.RevolutionistStart.ContainsKey(playerId))
                {
                    if (Main.RevolutionistLastTime.ContainsKey(playerId))
                    {
                        long nowtime = Utils.TimeStamp;
                        Main.RevolutionistLastTime[playerId] = nowtime;
                        int time = (int)(Main.RevolutionistLastTime[playerId] - Main.RevolutionistStart[playerId]);
                        int countdown = RevolutionistVentCountDown.GetInt() - time;
                        Main.RevolutionistCountdown.Clear();
                        if (countdown <= 0)
                        {
                            Utils.GetDrawPlayerCount(playerId, out var y);
                            foreach (var pc in y.Where(x => x != null && x.IsAlive()))
                            {
                                pc.Suicide(PlayerState.DeathReason.Sacrifice);
                                Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
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
                else
                {
                    Main.RevolutionistStart.TryAdd(playerId, Utils.TimeStamp);
                }
            }
        }

        public override void OnCoEnterVent(PlayerPhysics physics, int ventId)
        {
            if (AmongUsClient.Instance.IsGameStarted && physics.myPlayer.IsDrawDone())
            {
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Revolutionist);
                Utils.GetDrawPlayerCount(physics.myPlayer.PlayerId, out var x);
                CustomWinnerHolder.WinnerIds.Add(physics.myPlayer.PlayerId);
                foreach (PlayerControl apc in x)
                {
                    CustomWinnerHolder.WinnerIds.Add(apc.PlayerId);
                }
            }
        }
    }
}