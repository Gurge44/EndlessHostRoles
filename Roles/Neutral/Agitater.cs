﻿using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Crewmate;
using EHR.Modules;
using Hazel;
using UnityEngine;
using static EHR.Translator;

namespace EHR.Neutral;

public class Agitater : RoleBase
{
    private const int Id = 12420;
    private static List<byte> PlayerIdList = [];

    private static OptionItem BombExplodeCooldown;
    private static OptionItem PassCooldown;
    private static OptionItem AgitaterCanGetBombed;
    private static OptionItem AgiTaterBombCooldown;
    private static OptionItem AgitaterAutoReportBait;
    private static OptionItem HasImpostorVision;
    private bool AgitaterHasBombed;
    private byte AgitaterId;

    private byte CurrentBombedPlayer = byte.MaxValue;
    private long CurrentBombedPlayerTime;
    private byte LastBombedPlayer = byte.MaxValue;

    public override bool IsEnable => PlayerIdList.Count > 0 || Randomizer.Exists;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Agitater);
        AgiTaterBombCooldown = new FloatOptionItem(Id + 10, "AgitaterBombCooldown", new(10f, 180f, 0.5f), 20f, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Agitater])
            .SetValueFormat(OptionFormat.Seconds);
        PassCooldown = new FloatOptionItem(Id + 11, "AgitaterPassCooldown", new(0f, 5f, 0.25f), 2f, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Agitater])
            .SetValueFormat(OptionFormat.Seconds);
        BombExplodeCooldown = new FloatOptionItem(Id + 12, "BombExplodeCooldown", new(1f, 60f, 1f), 15f, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Agitater])
            .SetValueFormat(OptionFormat.Seconds);
        AgitaterCanGetBombed = new BooleanOptionItem(Id + 13, "AgitaterCanGetBombed", false, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Agitater]);
        AgitaterAutoReportBait = new BooleanOptionItem(Id + 14, "AgitaterAutoReportBait", true, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Agitater]);
        HasImpostorVision = new BooleanOptionItem(Id + 15, "ImpostorVision", true, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Agitater]);
    }

    public override void Init()
    {
        PlayerIdList = [];
        CurrentBombedPlayer = byte.MaxValue;
        LastBombedPlayer = byte.MaxValue;
        AgitaterHasBombed = false;
        CurrentBombedPlayerTime = new();
        AgitaterId = byte.MaxValue;
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        AgitaterId = playerId;

        CurrentBombedPlayer = byte.MaxValue;
        LastBombedPlayer = byte.MaxValue;
        AgitaterHasBombed = false;
        CurrentBombedPlayerTime = new();
    }

    void ResetBomb()
    {
        CurrentBombedPlayer = byte.MaxValue;
        CurrentBombedPlayerTime = new();
        LastBombedPlayer = byte.MaxValue;
        AgitaterHasBombed = false;
        SendRPC(CurrentBombedPlayer, LastBombedPlayer);
    }

    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = AgiTaterBombCooldown.GetFloat();
    public override void ApplyGameOptions(IGameOptions opt, byte id) => opt.SetVision(HasImpostorVision.GetBool());

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (!IsEnable) return false;
        if (AgitaterAutoReportBait.GetBool() && target.Is(CustomRoles.Bait)) return true;
        if (target.Is(CustomRoles.Pestilence) || (target.Is(CustomRoles.Veteran) && Veteran.VeteranInProtect.ContainsKey(target.PlayerId)))
        {
            target.Kill(killer);
            ResetBomb();
            return false;
        }

        CurrentBombedPlayer = target.PlayerId;
        LastBombedPlayer = killer.PlayerId;
        CurrentBombedPlayerTime = Utils.TimeStamp;
        killer.RpcGuardAndKill(killer);
        killer.Notify(GetString("AgitaterPassNotify"));
        target.Notify(GetString("AgitaterTargetNotify"));
        AgitaterHasBombed = true;
        killer.ResetKillCooldown();
        killer.SetKillCooldown();
        LateTask.New(() =>
        {
            if (CurrentBombedPlayer != byte.MaxValue && GameStates.IsInTask)
            {
                var pc = Utils.GetPlayerById(CurrentBombedPlayer);
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if (pc != null && pc.IsAlive() && killer != null) // Can be null since it's a late task
                {
                    pc.Suicide(PlayerState.DeathReason.Bombed, Utils.GetPlayerById(PlayerIdList[0]));
                    Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} bombed {pc.GetNameWithRole().RemoveHtmlTags()}, bomb cd complete", "Agitater");
                    ResetBomb();
                }
            }
        }, BombExplodeCooldown.GetFloat(), "AgitaterBombKill");
        return false;
    }

    public override void OnReportDeadBody()
    {
        if (!IsEnable) return;
        if (CurrentBombedPlayer == byte.MaxValue) return;
        var target = Utils.GetPlayerById(CurrentBombedPlayer);
        var killer = Utils.GetPlayerById(PlayerIdList[0]);
        if (target == null || killer == null) return;
        target.RpcExileV2();
        target.SetRealKiller(killer);
        Main.PlayerStates[CurrentBombedPlayer].deathReason = PlayerState.DeathReason.Bombed;
        Main.PlayerStates[CurrentBombedPlayer].SetDead();
        Utils.AfterPlayerDeathTasks(target, true);
        ResetBomb();
        Logger.Info($"{killer.GetRealName()} bombed {target.GetRealName()} on report", "Agitater");
    }

    public override void OnGlobalFixedUpdate(PlayerControl player, bool lowLoad)
    {
        var playerId = player.PlayerId;

        if (!lowLoad && GameStates.IsInTask && IsEnable && AgitaterHasBombed && CurrentBombedPlayer == playerId)
        {
            if (!player.IsAlive())
            {
                ResetBomb();
            }
            else
            {
                Vector2 agitaterPos = player.transform.position;
                Dictionary<byte, float> targetDistance = [];
                foreach (var target in PlayerControl.AllPlayerControls)
                {
                    if (!target.IsAlive()) continue;
                    if (target.PlayerId != playerId && target.PlayerId != LastBombedPlayer && !target.Data.IsDead)
                    {
                        float dis = Vector2.Distance(agitaterPos, target.transform.position);
                        targetDistance.Add(target.PlayerId, dis);
                    }
                }

                if (targetDistance.Count > 0)
                {
                    var min = targetDistance.OrderBy(c => c.Value).FirstOrDefault();
                    PlayerControl target = Utils.GetPlayerById(min.Key);
                    var KillRange = GameOptionsData.KillDistances[Mathf.Clamp(GameOptionsManager.Instance.currentNormalGameOptions.KillDistance, 0, 2)];
                    if (min.Value <= KillRange && player.CanMove && target.CanMove)
                        PassBomb(player, target);
                }
            }
        }
    }

    void PassBomb(PlayerControl player, PlayerControl target /*, bool IsAgitater = false*/)
    {
        if (!IsEnable) return;
        if (!AgitaterHasBombed) return;
        if (target.Data.IsDead) return;

        var now = Utils.TimeStamp;
        if (now - CurrentBombedPlayerTime < PassCooldown.GetFloat()) return;
        if (target.PlayerId == LastBombedPlayer) return;
        if (!AgitaterCanGetBombed.GetBool() && target.Is(CustomRoles.Agitater)) return;


        if (target.Is(CustomRoles.Pestilence) || (target.Is(CustomRoles.Veteran) && Veteran.VeteranInProtect.ContainsKey(target.PlayerId)))
        {
            target.Kill(player);
            ResetBomb();
            return;
        }

        LastBombedPlayer = CurrentBombedPlayer;
        CurrentBombedPlayer = target.PlayerId;
        CurrentBombedPlayerTime = now;

        player.MarkDirtySettings();
        target.MarkDirtySettings();

        player.Notify(GetString("AgitaterPassNotify"));
        target.Notify(GetString("AgitaterTargetNotify"));

        SendRPC(CurrentBombedPlayer, LastBombedPlayer);
        Logger.Msg($"{player.GetNameWithRole().RemoveHtmlTags()} passed bomb to {target.GetNameWithRole().RemoveHtmlTags()}", "Agitater Pass");
    }

    void SendRPC(byte newbomb, byte oldbomb)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.RpcPassBomb, SendOption.Reliable);
        writer.Write(AgitaterId);
        writer.Write(newbomb);
        writer.Write(oldbomb);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        byte agitaterId = reader.ReadByte();
        if (Main.PlayerStates[agitaterId].Role is not Agitater at) return;

        at.CurrentBombedPlayer = reader.ReadByte();
        at.LastBombedPlayer = reader.ReadByte();
    }
}