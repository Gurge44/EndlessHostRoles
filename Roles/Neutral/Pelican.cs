﻿using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Crewmate;
using EHR.Modules;
using Hazel;
using UnityEngine;

namespace EHR.Neutral;

public class Pelican : RoleBase
{
    private const int Id = 12500;
    private static List<byte> PlayerIdList = [];
    private static Dictionary<byte, List<byte>> EatenList = [];
    private static readonly Dictionary<byte, float> OriginalSpeed = [];
    private static OptionItem KillCooldown;
    private static OptionItem CanVent;
    private static OptionItem ImpostorVision;

    private int Count;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Pelican);

        KillCooldown = new FloatOptionItem(Id + 10, "PelicanKillCooldown", new(0f, 180f, 0.5f), 30f, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Pelican])
            .SetValueFormat(OptionFormat.Seconds);

        CanVent = new BooleanOptionItem(Id + 11, "CanVent", true, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Pelican]);

        ImpostorVision = new BooleanOptionItem(Id + 12, "ImpostorVision", false, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Pelican]);
    }

    public override void Init()
    {
        PlayerIdList = [];
        EatenList = [];
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return CanVent.GetBool();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        opt.SetVision(ImpostorVision.GetBool());
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    private static void SyncEatenList()
    {
        SendRPC(byte.MaxValue);

        foreach (KeyValuePair<byte, List<byte>> el in EatenList)
            SendRPC(el.Key);
    }

    private static void SendRPC(byte playerId)
    {
        if (!Utils.DoRPC) return;

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetPelicanEtenNum, SendOption.Reliable);
        writer.Write(playerId);

        if (playerId != byte.MaxValue)
        {
            writer.Write(EatenList[playerId].Count);
            foreach (byte el in EatenList[playerId].ToArray()) writer.Write(el);
        }

        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        byte playerId = reader.ReadByte();

        if (playerId == byte.MaxValue)
            EatenList.Clear();
        else
        {
            int eatenNum = reader.ReadInt32();
            List<byte> list = [];
            for (var i = 0; i < eatenNum; i++) list.Add(reader.ReadByte());

            EatenList[playerId] = list;
        }
    }

    public static bool IsEaten(PlayerControl pc, byte id)
    {
        return EatenList.TryGetValue(pc.PlayerId, out List<byte> list) && list.Contains(id);
    }

    public static bool IsEaten(byte id)
    {
        foreach (KeyValuePair<byte, List<byte>> el in EatenList)
        {
            if (el.Value.Contains(id))
                return true;
        }

        return false;
    }

    public static bool CanEat(PlayerControl pc, byte id)
    {
        if (!pc.Is(CustomRoles.Pelican) || GameStates.IsMeeting) return false;

        PlayerControl target = Utils.GetPlayerById(id);
        return target != null && target.IsAlive() && !target.inVent && !Medic.ProtectList.Contains(target.PlayerId) && !target.Is(CustomRoles.GM) && !IsEaten(pc, id) && !IsEaten(id);
    }

    public static Vector2 GetBlackRoomPS()
    {
        return Main.NormalOptions.MapId switch
        {
            0 => new(-27f, 3.3f), // The Skeld
            1 => new(-11.4f, 8.2f), // MIRA HQ
            2 => new(42.6f, -19.9f), // Polus
            3 => new(27f, 3.3f), // dlekS ehT
            4 => new(-16.8f, -6.2f), // Airship
            5 => new(9.6f, 23.2f), // The Fungle
            _ => new(50f, 50f) // Default position if the map is not recognized
        };
    }

    public override string GetProgressText(byte playerId, bool comms)
    {
        PlayerControl player = Utils.GetPlayerById(playerId);
        if (player == null) return "Invalid";

        var eatenNum = 0;
        if (EatenList.TryGetValue(playerId, out List<byte> value)) eatenNum = value.Count;

        return Utils.ColorString(eatenNum < 1 ? Color.gray : Utils.GetRoleColor(CustomRoles.Pelican), $"({eatenNum})");
    }

    public static void EatPlayer(PlayerControl pc, PlayerControl target)
    {
        if (pc == null || target == null || !CanEat(pc, target.PlayerId)) return;

        if (!EatenList.ContainsKey(pc.PlayerId)) EatenList.Add(pc.PlayerId, []);

        EatenList[pc.PlayerId].Add(target.PlayerId);

        SyncEatenList( /*pc.PlayerId*/);

        OriginalSpeed[target.PlayerId] = Main.AllPlayerSpeed[target.PlayerId];

        target.TP(GetBlackRoomPS());
        Main.AllPlayerSpeed[target.PlayerId] = 0.5f;
        ReportDeadBodyPatch.CanReport[target.PlayerId] = false;
        target.MarkDirtySettings();

        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: target);
        Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: pc);
        Logger.Info($"{pc.GetRealName()} ate {target.GetRealName()}", "Pelican");
    }

    public override void OnReportDeadBody()
    {
        foreach (KeyValuePair<byte, List<byte>> pc in EatenList)
        {
            foreach (byte tar in pc.Value)
            {
                PlayerControl target = Utils.GetPlayerById(tar);
                PlayerControl killer = Utils.GetPlayerById(pc.Key);
                if (killer == null || target == null) continue;

                Main.AllPlayerSpeed[tar] = Main.AllPlayerSpeed[tar] - 0.5f + OriginalSpeed[tar];
                ReportDeadBodyPatch.CanReport[tar] = true;
                target.RpcExileV2();
                target.SetRealKiller(killer);
                Main.PlayerStates[tar].deathReason = PlayerState.DeathReason.Eaten;
                Main.PlayerStates[tar].SetDead();
                Utils.AfterPlayerDeathTasks(target, true);
                Logger.Info($"{killer.GetRealName()} killed {target.GetRealName()}", "Pelican");
            }
        }

        EatenList.Clear();
        SyncEatenList( /*byte.MaxValue*/);
    }

    public static void OnPelicanDied(byte pc)
    {
        if (!EatenList.TryGetValue(pc, out List<byte> value)) return;

        foreach (byte tar in value)
        {
            PlayerControl target = Utils.GetPlayerById(tar);
            PlayerControl player = Utils.GetPlayerById(pc);
            if (player == null || target == null) continue;

            target.TP(player);
            Main.AllPlayerSpeed[tar] = Main.AllPlayerSpeed[tar] - 0.5f + OriginalSpeed[tar];
            ReportDeadBodyPatch.CanReport[tar] = true;
            target.MarkDirtySettings();
            RPC.PlaySoundRPC(tar, Sounds.TaskComplete);
            Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: player);
            Logger.Info($"{Utils.GetPlayerById(pc).GetRealName()} died, {target.GetRealName()} is back in-game", "Pelican");
        }

        EatenList.Remove(pc);
        SyncEatenList( /*pc*/);
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!GameStates.IsInTask)
        {
            if (EatenList.Count > 0)
            {
                EatenList.Clear();
                SyncEatenList( /*byte.MaxValue*/);
            }

            return;
        }

        if (!IsEnable) return;

        Count--;
        if (Count > 0) return;

        Count = 20;

        if (!EatenList.TryGetValue(pc.PlayerId, out List<byte> list)) return;

        foreach (byte tar in list)
        {
            PlayerControl target = Utils.GetPlayerById(tar);
            if (target == null) continue;

            Vector2 pos = GetBlackRoomPS();
            float dis = Vector2.Distance(pos, target.Pos());
            if (dis < 2f) continue;

            target.TP(pos, log: false);
            Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: pc);
        }
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (CanEat(killer, target.PlayerId))
        {
            EatPlayer(killer, target);
            killer.SetKillCooldown();
            killer.RPCPlayCustomSound("Eat");
            target.RPCPlayCustomSound("Eat");
        }

        return false;
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        hud.KillButton?.OverrideText(Translator.GetString("PelicanButtonText"));
    }
}