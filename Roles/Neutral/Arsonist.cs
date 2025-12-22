using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using UnityEngine;
using static EHR.Options;

namespace EHR.Neutral;

internal class Arsonist : RoleBase
{
    public static Dictionary<byte, (PlayerControl Player, float Timer)> ArsonistTimer = [];
    public static Dictionary<(byte, byte), bool> IsDoused = [];
    public static byte CurrentDousingTarget = byte.MaxValue;

    public static OptionItem ArsonistDouseTime;
    public static OptionItem ArsonistCooldown;
    public static OptionItem ArsonistKeepsGameGoing;
    public static OptionItem ArsonistCanIgniteAnytime;
    public static OptionItem ArsonistMinPlayersToIgnite;
    public static OptionItem ArsonistMaxPlayersToIgnite;
    public static OptionItem ArsonistCanVent;
    public static OptionItem ArsonistHasImpostorVision;

    public static bool On;
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(10400, TabGroup.NeutralRoles, CustomRoles.Arsonist);

        ArsonistDouseTime = new FloatOptionItem(10410, "ArsonistDouseTime", new(0f, 90f, 0.5f), 3f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Arsonist])
            .SetValueFormat(OptionFormat.Seconds);

        ArsonistCooldown = new FloatOptionItem(10411, "Cooldown", new(0f, 60f, 0.5f), 10f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Arsonist])
            .SetValueFormat(OptionFormat.Seconds);

        ArsonistCanIgniteAnytime = new BooleanOptionItem(10413, "ArsonistCanIgniteAnytime", false, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Arsonist]);

        ArsonistMinPlayersToIgnite = new IntegerOptionItem(10414, "ArsonistMinPlayersToIgnite", new(1, 14, 1), 1, TabGroup.NeutralRoles)
            .SetParent(ArsonistCanIgniteAnytime);

        ArsonistMaxPlayersToIgnite = new IntegerOptionItem(10415, "ArsonistMaxPlayersToIgnite", new(1, 14, 1), 3, TabGroup.NeutralRoles)
            .SetParent(ArsonistCanIgniteAnytime);

        ArsonistKeepsGameGoing = new BooleanOptionItem(10412, "ArsonistKeepsGameGoing", false, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Arsonist]);
        
        ArsonistCanVent = new BooleanOptionItem(10416, "CanVent", false, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Arsonist]);
        
        ArsonistHasImpostorVision = new BooleanOptionItem(10417, "ImpostorVision", false, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Arsonist]);
    }

    public override void Add(byte playerId)
    {
        On = true;
        foreach (PlayerControl ar in Main.AllPlayerControls) IsDoused.Add((playerId, ar.PlayerId), false);
    }

    public override void Init()
    {
        On = false;
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return ArsonistCanIgniteAnytime.GetBool() || !pc.IsDouseDone();
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return pc.IsDouseDone() || ArsonistCanVent.GetBool() || (ArsonistCanIgniteAnytime.GetBool() && !UsePets.GetBool() && (Utils.GetDousedPlayerCount(pc.PlayerId).Item1 >= ArsonistMinPlayersToIgnite.GetInt() || pc.inVent));
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = ArsonistCooldown.GetFloat();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        opt.SetVision(ArsonistHasImpostorVision.GetBool());
    }

    public override string GetProgressText(byte playerId, bool comms)
    {
        (int, int) doused = Utils.GetDousedPlayerCount(playerId);
        return Utils.ColorString(Utils.GetRoleColor(CustomRoles.Arsonist).ShadeColor(0.25f), !ArsonistCanIgniteAnytime.GetBool() ? $"<color=#777777>-</color> {doused.Item1}/{doused.Item2}" : $"<color=#777777>-</color> {doused.Item1}/{ArsonistMaxPlayersToIgnite.GetInt()}");
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        float cooldown = ArsonistCooldown.GetFloat();
        
        if (ArsonistCanIgniteAnytime.GetBool() && IsDoused[(killer.PlayerId, target.PlayerId)] && Utils.GetDousedPlayerCount(killer.PlayerId).Doused >= ArsonistMinPlayersToIgnite.GetInt())
        {
            killer.SetKillCooldown(cooldown);
            Ignite(killer.MyPhysics);
            return false;
        }
        
        float douseTime = ArsonistDouseTime.GetFloat();
        killer.SetKillCooldown(Mathf.Approximately(douseTime, 0f) ? cooldown : douseTime);

        if (!IsDoused[(killer.PlayerId, target.PlayerId)] && !ArsonistTimer.ContainsKey(killer.PlayerId) && killer.RpcCheckAndMurder(target, check: true))
        {
            ArsonistTimer.Add(killer.PlayerId, (target, 0f));
            Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target, ForceLoop: true);
            RPC.SetCurrentDousingTarget(killer.PlayerId, target.PlayerId);
        }

        return false;
    }

    public override void OnPet(PlayerControl pc)
    {
        if (ArsonistCanIgniteAnytime.GetBool()) return;
        Ignite(pc.MyPhysics);
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        if (ArsonistCanIgniteAnytime.GetBool()) return;
        if (UsePets.GetBool()) return;
        if (AmongUsClient.Instance.IsGameStarted)
            Ignite(pc.MyPhysics);
    }

    private static void Ignite(PlayerPhysics physics)
    {
        switch (ArsonistCanIgniteAnytime.GetBool())
        {
            case false when physics.myPlayer.IsDouseDone():
            {
                CustomSoundsManager.RPCPlayCustomSoundAll("Boom");

                foreach (PlayerControl pc in Main.AllAlivePlayerControls)
                {
                    if (pc != physics.myPlayer)
                        pc.Suicide(PlayerState.DeathReason.Torched, physics.myPlayer);
                }

                foreach (PlayerControl pc in Main.AllPlayerControls)
                    pc.KillFlash();

                if (CustomWinnerHolder.WinnerTeam is CustomWinner.Crewmate or CustomWinner.Impostor)
                    CustomWinnerHolder.Reset();

                CustomWinnerHolder.ShiftWinnerAndSetWinner(CustomWinner.Arsonist);
                CustomWinnerHolder.WinnerIds.Add(physics.myPlayer.PlayerId);
                return;
            }
            case true:
            {
                int douseCount = Utils.GetDousedPlayerCount(physics.myPlayer.PlayerId).Doused;

                if (douseCount >= ArsonistMinPlayersToIgnite.GetInt()) // Don't check for max, since the player would not be able to ignite at all if they somehow get more players doused than the max
                {
                    if (douseCount > ArsonistMaxPlayersToIgnite.GetInt()) Logger.Warn("Arsonist Ignited with more players doused than the maximum amount in the settings", "Arsonist Ignite");

                    foreach (PlayerControl pc in Main.AllAlivePlayerControls)
                    {
                        if (!physics.myPlayer.IsDousedPlayer(pc)) continue;
                        pc.Suicide(PlayerState.DeathReason.Torched, physics.myPlayer);
                    }

                    physics.myPlayer.KillFlash();

                    int apc = Main.AllAlivePlayerControls.Length;

                    switch (apc)
                    {
                        case 1:
                            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Arsonist);
                            CustomWinnerHolder.WinnerIds.Add(physics.myPlayer.PlayerId);
                            break;
                        case 2:
                            if (Main.AllAlivePlayerControls.Where(x => x.PlayerId != physics.myPlayer.PlayerId).All(x => x.GetCountTypes() == CountTypes.Crew))
                            {
                                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Arsonist);
                                CustomWinnerHolder.WinnerIds.Add(physics.myPlayer.PlayerId);
                            }

                            break;
                    }
                }

                break;
            }
        }
    }

    public override void OnGlobalFixedUpdate(PlayerControl player, bool lowLoad)
    {
        byte playerId = player.PlayerId;

        if (GameStates.IsInTask && ArsonistTimer.TryGetValue(playerId, out var value))
        {
            PlayerControl arTarget = value.Player;

            if (!player.IsAlive() || Pelican.IsEaten(playerId))
            {
                ArsonistTimer.Remove(playerId);
                Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: arTarget, ForceLoop: true);
                RPC.ResetCurrentDousingTarget(playerId);
            }
            else
            {
                float arTime = value.Timer;

                if (!arTarget.IsAlive())
                    ArsonistTimer.Remove(playerId);
                else if (arTime >= ArsonistDouseTime.GetFloat())
                {
                    player.SetKillCooldown();
                    ArsonistTimer.Remove(playerId);
                    IsDoused[(playerId, arTarget.PlayerId)] = true;
                    player.RpcSetDousedPlayer(arTarget, true);
                    Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: arTarget, ForceLoop: true);
                    RPC.ResetCurrentDousingTarget(playerId);
                }
                else
                {
                    float range = NormalGameOptionsV10.KillDistances[Mathf.Clamp(player.Is(CustomRoles.Reach) ? 2 : Main.NormalOptions.KillDistance, 0, 2)] + 0.5f;
                    float dis = Vector2.Distance(player.Pos(), arTarget.Pos());

                    if (dis <= range)
                        ArsonistTimer[playerId] = (arTarget, arTime + Time.fixedDeltaTime);
                    else
                    {
                        player.SetKillCooldown(0.01f);
                        ArsonistTimer.Remove(playerId);
                        Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: arTarget, ForceLoop: true);
                        RPC.ResetCurrentDousingTarget(playerId);

                        Logger.Info($"Canceled: {player.GetNameWithRole().RemoveHtmlTags()}", "Arsonist");
                    }
                }
            }
        }
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        hud.KillButton?.OverrideText(Translator.GetString("ArsonistDouseButtonText"));
        ActionButton usedButton = UsePhantomBasis.GetBool() ? hud.AbilityButton : UsePets.GetBool() ? hud.PetButton : hud.ImpostorVentButton;
        usedButton?.OverrideText(Translator.GetString("ArsonistVentButtonText"));
    }
}