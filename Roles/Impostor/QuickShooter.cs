﻿using Hazel;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TOHE.Roles.Impostor;

internal static class QuickShooter
{
    private static readonly int Id = 1800;
    public static List<byte> playerIdList = [];
    private static OptionItem KillCooldown;
    private static OptionItem MeetingReserved;
    public static OptionItem ShapeshiftCooldown;

    public static Dictionary<byte, int> ShotLimit = [];

    public static void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.QuickShooter);
        KillCooldown = FloatOptionItem.Create(Id + 10, "KillCooldown", new(0f, 180f, 2.5f), 25f, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.QuickShooter])
            .SetValueFormat(OptionFormat.Seconds);
        ShapeshiftCooldown = FloatOptionItem.Create(Id + 12, "QuickShooterShapeshiftCooldown", new(0f, 180f, 2.5f), 40f, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.QuickShooter])
            .SetValueFormat(OptionFormat.Seconds);
        MeetingReserved = IntegerOptionItem.Create(Id + 14, "MeetingReserved", new(0, 15, 1), 1, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.QuickShooter])
            .SetValueFormat(OptionFormat.Pieces);
    }
    public static void Init()
    {
        playerIdList = [];
        ShotLimit = [];
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        ShotLimit.TryAdd(playerId, 0);
    }
    public static bool IsEnable => playerIdList.Count > 0;
    private static void SendRPC(byte playerId)
    {
        if (!IsEnable || !Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetQuickShooterShotLimit, SendOption.Reliable, -1);
        writer.Write(playerId);
        writer.Write(ShotLimit[playerId]);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ReceiveRPC(MessageReader reader)
    {
        byte QuickShooterId = reader.ReadByte();
        int Limit = reader.ReadInt32();
        ShotLimit.TryAdd(QuickShooterId, Limit);
        ShotLimit[QuickShooterId] = Limit;
    }
    public static void OnShapeshift(PlayerControl pc, bool shapeshifting)
    {
        if (Main.KillTimers[pc.PlayerId] <= 0 && shapeshifting)
        {
            ShotLimit[pc.PlayerId]++;
            SendRPC(pc.PlayerId);
            //Storaging = true;
            pc.ResetKillCooldown();
            pc.SetKillCooldown();
            pc.Notify(Translator.GetString("QuickShooterStoraging"));
            Logger.Info($"{Utils.GetPlayerById(pc.PlayerId)?.GetNameWithRole().RemoveHtmlTags()} : Remaining: {ShotLimit[pc.PlayerId]} bullets", "QuickShooter");
        }
    }
    //private static bool Storaging;
    public static void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
        //Storaging = false;
    }
    public static void OnReportDeadBody()
    {
        Dictionary<byte, int> NewSL = [];
        foreach (var sl in ShotLimit)
            NewSL.Add(sl.Key, Math.Clamp(sl.Value, 0, MeetingReserved.GetInt()));
        foreach (var sl in NewSL)
        {
            ShotLimit[sl.Key] = sl.Value;
            SendRPC(sl.Key);
        }
    }
    public static void QuickShooterKill(PlayerControl killer)
    {
        ShotLimit.TryAdd(killer.PlayerId, 0);
        if (ShotLimit[killer.PlayerId] > 0) _ = new LateTask(() => { killer.SetKillCooldown(0.01f); }, 0.01f, "QuickShooterKill: Set KCD to 0s");
        ShotLimit[killer.PlayerId]--;
        ShotLimit[killer.PlayerId] = Math.Max(ShotLimit[killer.PlayerId], 0);
        SendRPC(killer.PlayerId);
    }
    public static string GetShotLimit(byte playerId) => Utils.ColorString(ShotLimit.ContainsKey(playerId) && ShotLimit[playerId] > 0 ? Utils.GetRoleColor(CustomRoles.QuickShooter).ShadeColor(0.25f) : Color.gray, ShotLimit.TryGetValue(playerId, out var shotLimit) ? $"({shotLimit})" : "Invalid");
}
