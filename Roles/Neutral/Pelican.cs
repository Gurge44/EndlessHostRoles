using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Roles.Crewmate;
using Hazel;
using UnityEngine;

namespace EHR.Roles.Neutral;

public class Pelican : RoleBase
{
    private const int Id = 12500;
    private static List<byte> playerIdList = [];
    private static Dictionary<byte, List<byte>> eatenList = [];
    private static readonly Dictionary<byte, float> OriginalSpeed = [];
    public static OptionItem KillCooldown;
    public static OptionItem CanVent;

    private int Count;

    public override bool IsEnable => playerIdList.Count > 0;

    public static void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Pelican);
        KillCooldown = FloatOptionItem.Create(Id + 10, "PelicanKillCooldown", new(0f, 180f, 0.5f), 30f, TabGroup.NeutralRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Pelican])
            .SetValueFormat(OptionFormat.Seconds);
        CanVent = BooleanOptionItem.Create(Id + 11, "CanVent", true, TabGroup.NeutralRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Pelican]);
    }

    public override void Init()
    {
        playerIdList = [];
        eatenList = [];
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc) => CanVent.GetBool();
    public override void ApplyGameOptions(IGameOptions opt, byte playerId) => opt.SetVision(false);

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    private static void SyncEatenList( /*byte playerId*/)
    {
        SendRPC(byte.MaxValue);
        foreach (var el in eatenList)
            SendRPC(el.Key);
    }

    private static void SendRPC(byte playerId)
    {
        if (!Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetPelicanEtenNum, SendOption.Reliable);
        writer.Write(playerId);
        if (playerId != byte.MaxValue)
        {
            writer.Write(eatenList[playerId].Count);
            foreach (byte el in eatenList[playerId].ToArray())
            {
                writer.Write(el);
            }
        }

        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        byte playerId = reader.ReadByte();
        if (playerId == byte.MaxValue)
        {
            eatenList.Clear();
        }
        else
        {
            int eatenNum = reader.ReadInt32();
            eatenList.Remove(playerId);
            List<byte> list = [];
            for (int i = 0; i < eatenNum; i++)
                list.Add(reader.ReadByte());
            eatenList.Add(playerId, list);
        }
    }

    public static bool IsEaten(PlayerControl pc, byte id) => eatenList.ContainsKey(pc.PlayerId) && eatenList[pc.PlayerId].Contains(id);

    public static bool IsEaten(byte id)
    {
        foreach (var el in eatenList)
            if (el.Value.Contains(id))
                return true;
        return false;
    }

    public static bool CanEat(PlayerControl pc, byte id)
    {
        if (!pc.Is(CustomRoles.Pelican) || GameStates.IsMeeting) return false;
        var target = Utils.GetPlayerById(id);
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
            _ => throw new NotImplementedException(),
        };
    }

    public override string GetProgressText(byte playerId, bool comms)
    {
        var player = Utils.GetPlayerById(playerId);
        if (player == null) return "Invalid";
        var eatenNum = 0;
        if (eatenList.TryGetValue(playerId, out List<byte> value))
            eatenNum = value.Count;
        return Utils.ColorString(eatenNum < 1 ? Color.gray : Utils.GetRoleColor(CustomRoles.Pelican), $"({eatenNum})");
    }

    public static void EatPlayer(PlayerControl pc, PlayerControl target)
    {
        if (pc == null || target == null || !CanEat(pc, target.PlayerId)) return;
        if (!eatenList.ContainsKey(pc.PlayerId)) eatenList.Add(pc.PlayerId, []);
        eatenList[pc.PlayerId].Add(target.PlayerId);

        SyncEatenList( /*pc.PlayerId*/);

        OriginalSpeed.Remove(target.PlayerId);
        OriginalSpeed.Add(target.PlayerId, Main.AllPlayerSpeed[target.PlayerId]);

        target.TP(GetBlackRoomPS());
        Main.AllPlayerSpeed[target.PlayerId] = 0.5f;
        ReportDeadBodyPatch.CanReport[target.PlayerId] = false;
        target.MarkDirtySettings();

        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: target);
        Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: pc);
        Logger.Info($"{pc.GetRealName()} 吞掉了 {target.GetRealName()}", "Pelican");
    }

    public override void OnReportDeadBody()
    {
        foreach (var pc in eatenList)
        {
            foreach (byte tar in pc.Value)
            {
                var target = Utils.GetPlayerById(tar);
                var killer = Utils.GetPlayerById(pc.Key);
                if (killer == null || target == null) continue;
                Main.AllPlayerSpeed[tar] = Main.AllPlayerSpeed[tar] - 0.5f + OriginalSpeed[tar];
                ReportDeadBodyPatch.CanReport[tar] = true;
                target.RpcExileV2();
                target.SetRealKiller(killer);
                Main.PlayerStates[tar].deathReason = PlayerState.DeathReason.Eaten;
                Main.PlayerStates[tar].SetDead();
                Utils.AfterPlayerDeathTasks(target, true);
                Logger.Info($"{killer.GetRealName()} 消化了 {target.GetRealName()}", "Pelican");
            }
        }

        eatenList.Clear();
        SyncEatenList( /*byte.MaxValue*/);
    }

    public static void OnPelicanDied(byte pc)
    {
        if (!eatenList.TryGetValue(pc, out List<byte> value)) return;
        foreach (byte tar in value)
        {
            var target = Utils.GetPlayerById(tar);
            var player = Utils.GetPlayerById(pc);
            if (player == null || target == null) continue;
            target.TP(player);
            Main.AllPlayerSpeed[tar] = Main.AllPlayerSpeed[tar] - 0.5f + OriginalSpeed[tar];
            ReportDeadBodyPatch.CanReport[tar] = true;
            target.MarkDirtySettings();
            RPC.PlaySoundRPC(tar, Sounds.TaskComplete);
            Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: player);
            Logger.Info($"{Utils.GetPlayerById(pc).GetRealName()} 吐出了 {target.GetRealName()}", "Pelican");
        }

        eatenList.Remove(pc);
        SyncEatenList( /*pc*/);
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!GameStates.IsInTask)
        {
            if (eatenList.Count > 0)
            {
                eatenList.Clear();
                SyncEatenList( /*byte.MaxValue*/);
            }

            return;
        }

        if (!IsEnable) return;
        Count--;
        if (Count > 0) return;
        Count = 10;

        foreach (byte tar in eatenList[pc.PlayerId])
        {
            var target = Utils.GetPlayerById(tar);
            if (target == null) continue;
            var pos = GetBlackRoomPS();
            var dis = Vector2.Distance(pos, target.Pos());
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
            //killer.RpcGuardAndKill(killer);
            killer.SetKillCooldown();
            killer.RPCPlayCustomSound("Eat");
            target.RPCPlayCustomSound("Eat");
        }

        return false;
    }
}