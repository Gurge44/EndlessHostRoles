using Hazel;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Impostor;

public static class Hacker
{
    private static readonly int Id = 2200;
    private static List<byte> playerIdList = new();

    private static OptionItem HackLimitOpt;
    private static OptionItem KillCooldown;
    public static OptionItem HackerAbilityUseGainWithEachKill;

    public static Dictionary<byte, float> HackLimit = new();
    private static List<byte> DeadBodyList = new();

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Hacker);
        KillCooldown = FloatOptionItem.Create(Id + 2, "KillCooldown", new(0f, 180f, 2.5f), 30f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Hacker])
            .SetValueFormat(OptionFormat.Seconds);
        HackLimitOpt = IntegerOptionItem.Create(Id + 3, "HackLimit", new(1, 5, 1), 0, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Hacker])
            .SetValueFormat(OptionFormat.Times);
        HackerAbilityUseGainWithEachKill = FloatOptionItem.Create(Id + 4, "AbilityUseGainWithEachKill", new(0f, 5f, 0.1f), 0.2f, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Hacker])
            .SetValueFormat(OptionFormat.Times);
    }
    public static void Init()
    {
        playerIdList = new();
        HackLimit = new();
        DeadBodyList = new();
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        HackLimit.TryAdd(playerId, HackLimitOpt.GetInt());
    }
    public static bool IsEnable => playerIdList.Any();
    private static void SendRPC(byte playerId)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetHackerHackLimit, SendOption.Reliable, -1);
        writer.Write(playerId);
        writer.Write(HackLimit[playerId]);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ReceiveRPC(MessageReader reader)
    {
        byte PlayerId = reader.ReadByte();
        int Limit = reader.ReadInt32();
        HackLimit.TryAdd(PlayerId, HackLimitOpt.GetInt());
        HackLimit[PlayerId] = Limit;
    }
    public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    public static void ApplyGameOptions()
    {
        AURoleOptions.ShapeshifterCooldown = 15f;
        AURoleOptions.ShapeshifterDuration = 1f;
    }
    public static string GetHackLimit(byte playerId) => Utils.ColorString((HackLimit.TryGetValue(playerId, out var x) && x >= 1) ? Utils.GetRoleColor(CustomRoles.Hacker).ShadeColor(0.25f) : Color.red, HackLimit.TryGetValue(playerId, out var hackLimit) ? $"<color=#777777>-</color> {Math.Round(hackLimit, 1)})" : "Invalid");
    public static void GetAbilityButtonText(HudManager __instance, byte playerId)
    {
        if (HackLimit.TryGetValue(playerId, out var x) && x >= 1)
        {
            __instance.AbilityButton.OverrideText(GetString("HackerShapeshiftText"));
            __instance.AbilityButton.SetUsesRemaining((int)x);
        }
    }
    public static void OnReportDeadBody() => DeadBodyList = new();
    public static void AddDeadBody(PlayerControl target)
    {
        if (target != null && !DeadBodyList.Contains(target.PlayerId))
            DeadBodyList.Add(target.PlayerId);
    }
    public static void OnShapeshift(PlayerControl pc, bool shapeshifting, PlayerControl ssTarget)
    {
        if (!shapeshifting || !HackLimit.TryGetValue(pc.PlayerId, out var x) || x < 1 || ssTarget == null || ssTarget.Is(CustomRoles.Needy) || ssTarget.Is(CustomRoles.Lazy)) return;
        HackLimit[pc.PlayerId] -= 1;
        SendRPC(pc.PlayerId);

        var targetId = byte.MaxValue;

        // 寻找骇客击杀的尸体
        for (int i = 0; i < DeadBodyList.Count; i++)
        {
            byte db = DeadBodyList[i];
            var dp = Utils.GetPlayerById(db);
            if (dp == null || dp.GetRealKiller() == null) continue;
            if (dp.GetRealKiller().PlayerId == pc.PlayerId) targetId = db;
        }

        // 未找到骇客击杀的尸体，寻找其他尸体
        if (targetId == byte.MaxValue && DeadBodyList.Any())
            targetId = DeadBodyList[IRandom.Instance.Next(0, DeadBodyList.Count)];

        if (targetId == byte.MaxValue)
            new LateTask(() => ssTarget?.NoCheckStartMeeting(ssTarget?.Data), 0.15f, "Hacker Hacking Report Self");
        else
            new LateTask(() => ssTarget?.NoCheckStartMeeting(Utils.GetPlayerById(targetId)?.Data), 0.15f, "Hacker Hacking Report");
    }
}