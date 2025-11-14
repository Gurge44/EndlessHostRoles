using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using UnityEngine;
using static EHR.Options;

namespace EHR.Neutral;

internal class Revolutionist : RoleBase
{
    public static Dictionary<(byte, byte), bool> IsDraw = [];
    public static Dictionary<byte, (PlayerControl Player, float Timer)> RevolutionistTimer = [];
    public static Dictionary<byte, long> RevolutionistStart = [];
    public static Dictionary<byte, long> RevolutionistLastTime = [];
    public static Dictionary<byte, int> RevolutionistCountdown = [];
    public static byte CurrentDrawTarget = byte.MaxValue;

    public static bool On;

    public static OptionItem RevolutionistDrawTime;
    public static OptionItem RevolutionistCooldown;
    public static OptionItem RevolutionistDrawCount;
    public static OptionItem RevolutionistKillProbability;
    public static OptionItem RevolutionistVentCountDown;
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(18400, TabGroup.NeutralRoles, CustomRoles.Revolutionist);

        RevolutionistDrawTime = new FloatOptionItem(18410, "RevolutionistDrawTime", new(0f, 90f, 1f), 3f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Revolutionist])
            .SetValueFormat(OptionFormat.Seconds);

        RevolutionistCooldown = new FloatOptionItem(18411, "RevolutionistCooldown", new(0f, 100f, 1f), 10f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Revolutionist])
            .SetValueFormat(OptionFormat.Seconds);

        RevolutionistDrawCount = new IntegerOptionItem(18412, "RevolutionistDrawCount", new(0, 14, 1), 6, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Revolutionist])
            .SetValueFormat(OptionFormat.Players);

        RevolutionistKillProbability = new IntegerOptionItem(18413, "RevolutionistKillProbability", new(0, 100, 5), 15, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Revolutionist])
            .SetValueFormat(OptionFormat.Percent);

        RevolutionistVentCountDown = new FloatOptionItem(18414, "RevolutionistVentCountDown", new(0f, 180f, 1f), 15f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Revolutionist])
            .SetValueFormat(OptionFormat.Seconds);
    }


    public override void Add(byte playerId)
    {
        On = true;
        foreach (PlayerControl ar in Main.AllPlayerControls)
            IsDraw.Add((playerId, ar.PlayerId), false);
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
        return pc.Is(CustomRoles.Mischievous);
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
        (int, int) draw = Utils.GetDrawPlayerCount(playerId, out _);
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

        if (!IsDraw[(killer.PlayerId, target.PlayerId)] && !RevolutionistTimer.ContainsKey(killer.PlayerId))
        {
            RevolutionistTimer.TryAdd(killer.PlayerId, (target, 0f));
            Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target, ForceLoop: true);
            RPC.SetCurrentDrawTarget(killer.PlayerId, target.PlayerId);
        }

        return false;
    }

    public override void OnReportDeadBody()
    {
        foreach (KeyValuePair<byte, long> x in RevolutionistStart)
        {
            PlayerControl tar = Utils.GetPlayerById(x.Key);
            if (tar == null || tar.Is(CustomRoles.Pestilence)) continue;

            tar.Data.IsDead = true;
            Main.PlayerStates[tar.PlayerId].deathReason = PlayerState.DeathReason.Sacrifice;
            tar.RpcExileV2();
            Main.PlayerStates[tar.PlayerId].SetDead();
            Utils.AfterPlayerDeathTasks(tar, true);
        }

        RevolutionistTimer.Clear();
        RevolutionistStart.Clear();
        RevolutionistLastTime.Clear();
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        byte playerId = player.PlayerId;

        if (GameStates.IsInTask && RevolutionistTimer.TryGetValue(playerId, out var value))
        {
            PlayerControl rvTarget = value.Player;

            if (!player.IsAlive() || Pelican.IsEaten(playerId))
            {
                RevolutionistTimer.Remove(playerId);
                Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: rvTarget, ForceLoop: true);
                RPC.ResetCurrentDrawTarget(playerId);
            }
            else
            {
                float rvTime = value.Timer;

                if (!rvTarget.IsAlive())
                    RevolutionistTimer.Remove(playerId);
                else if (rvTime >= RevolutionistDrawTime.GetFloat())
                {
                    player.SetKillCooldown();
                    RevolutionistTimer.Remove(playerId);
                    IsDraw[(playerId, rvTarget.PlayerId)] = true;
                    player.RpcSetDrawPlayer(rvTarget, true);
                    Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: rvTarget, ForceLoop: true);
                    RPC.ResetCurrentDrawTarget(playerId);

                    if (IRandom.Instance.Next(1, 100) <= RevolutionistKillProbability.GetInt())
                    {
                        rvTarget.SetRealKiller(player);
                        Main.PlayerStates[rvTarget.PlayerId].deathReason = PlayerState.DeathReason.Sacrifice;
                        player.Kill(rvTarget);
                        Main.PlayerStates[rvTarget.PlayerId].SetDead();
                        Logger.Info($"Revolutionist: {player.GetNameWithRole().RemoveHtmlTags()} killed {rvTarget.GetNameWithRole().RemoveHtmlTags()}", "Revolutionist");
                    }
                }
                else
                {
                    float range = NormalGameOptionsV10.KillDistances[Mathf.Clamp(player.Is(CustomRoles.Reach) ? 2 : Main.NormalOptions.KillDistance, 0, 2)] + 0.5f;
                    float dis = Vector2.Distance(player.Pos(), rvTarget.Pos());

                    if (dis <= range)
                        RevolutionistTimer[playerId] = (rvTarget, rvTime + Time.fixedDeltaTime);
                    else
                    {
                        RevolutionistTimer.Remove(playerId);
                        Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: rvTarget);
                        RPC.ResetCurrentDrawTarget(playerId);

                        Logger.Info($"Canceled: {player.GetNameWithRole().RemoveHtmlTags()}", "Revolutionist");
                    }
                }
            }
        }

        if (GameStates.IsInTask && player.IsDrawDone() && player.IsAlive() && !player.Data.IsDead)
        {
            if (RevolutionistStart.TryGetValue(playerId, out var start))
            {
                if (RevolutionistLastTime.ContainsKey(playerId))
                {
                    long nowtime = Utils.TimeStamp;
                    RevolutionistLastTime[playerId] = nowtime;
                    var time = (int)(RevolutionistLastTime[playerId] - start);
                    int countdown = RevolutionistVentCountDown.GetInt() - time;
                    RevolutionistCountdown.Clear();

                    if (countdown <= 0)
                    {
                        Utils.GetDrawPlayerCount(playerId, out List<PlayerControl> y);

                        foreach (PlayerControl pc in y)
                        {
                            if (pc != null && pc.IsAlive())
                            {
                                pc.Suicide(PlayerState.DeathReason.Sacrifice);
                                Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
                            }
                        }

                        player.Suicide(PlayerState.DeathReason.Sacrifice);

                        if (player.AmOwner)
                            Achievements.Type.OutOfTime.Complete();
                    }
                    else RevolutionistCountdown[playerId] = countdown;
                }
                else RevolutionistLastTime.TryAdd(playerId, start);
            }
            else RevolutionistStart.TryAdd(playerId, Utils.TimeStamp);
        }
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        if (AmongUsClient.Instance.IsGameStarted && pc.IsDrawDone())
        {
            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Revolutionist);
            Utils.GetDrawPlayerCount(pc.PlayerId, out List<PlayerControl> x);
            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
            foreach (PlayerControl apc in x) CustomWinnerHolder.WinnerIds.Add(apc.PlayerId);
        }
    }

    public override void OnRevived(PlayerControl pc)
    {
        RevolutionistTimer.Remove(pc.PlayerId);
        RevolutionistStart.Remove(pc.PlayerId);
        RevolutionistLastTime.Remove(pc.PlayerId);
        RevolutionistCountdown.Remove(pc.PlayerId);
    }
}