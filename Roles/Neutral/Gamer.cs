﻿using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using UnityEngine;
using static EHR.Options;

namespace EHR.Roles.Neutral;

public class Gamer : RoleBase
{
    private const int Id = 10600;
    public static List<byte> playerIdList = [];

    private static Dictionary<byte, int> PlayerHealth = [];
    private static Dictionary<byte, int> GamerHealth = [];

    private static OptionItem KillCooldown;
    private static OptionItem CanVent;
    private static OptionItem HasImpostorVision;
    private static OptionItem HealthMax;
    private static OptionItem Damage;
    private static OptionItem SelfHealthMax;
    private static OptionItem SelfDamage;

    public override bool IsEnable => playerIdList.Count > 0;

    public static void SetupCustomOption()
    {
        SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Gamer);
        KillCooldown = FloatOptionItem.Create(Id + 10, "GamerKillCooldown", new(1f, 180f, 1f), 2f, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Gamer])
            .SetValueFormat(OptionFormat.Seconds);
        CanVent = BooleanOptionItem.Create(Id + 11, "CanVent", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Gamer]);
        HasImpostorVision = BooleanOptionItem.Create(Id + 13, "ImpostorVision", false, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Gamer]);
        HealthMax = IntegerOptionItem.Create(Id + 15, "GamerHealthMax", new(5, 300, 5), 100, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Gamer])
            .SetValueFormat(OptionFormat.Health);
        Damage = IntegerOptionItem.Create(Id + 16, "GamerDamage", new(1, 100, 1), 15, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Gamer])
            .SetValueFormat(OptionFormat.Health);
        SelfHealthMax = IntegerOptionItem.Create(Id + 17, "GamerSelfHealthMax", new(100, 100, 5), 100, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Gamer])
            .SetValueFormat(OptionFormat.Health);
        SelfDamage = IntegerOptionItem.Create(Id + 18, "GamerSelfDamage", new(1, 100, 1), 35, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Gamer])
            .SetValueFormat(OptionFormat.Health);
    }

    public override void Init()
    {
        playerIdList = [];
        GamerHealth = [];
        PlayerHealth = [];
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        GamerHealth[playerId] = SelfHealthMax.GetInt();
        foreach (PlayerControl pc in Main.AllAlivePlayerControls)
        {
            PlayerHealth[pc.PlayerId] = HealthMax.GetInt();
        }

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }

    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    public override void ApplyGameOptions(IGameOptions opt, byte id) => opt.SetVision(HasImpostorVision.GetBool());
    public override bool CanUseImpostorVentButton(PlayerControl pc) => CanVent.GetBool();

    void SendRPC(byte playerId)
    {
        if (!IsEnable || !Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetGamerHealth, SendOption.Reliable);
        writer.Write(playerId);
        writer.Write(GamerHealth.TryGetValue(playerId, out int value) ? value : PlayerHealth[playerId]);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        byte PlayerId = reader.ReadByte();
        int Health = reader.ReadInt32();
        if (GamerHealth.ContainsKey(PlayerId))
            GamerHealth[PlayerId] = Health;
        else
            PlayerHealth[PlayerId] = Health;
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer == null || target == null || target.Is(CustomRoles.Gamer) || !PlayerHealth.ContainsKey(target.PlayerId)) return false;
        killer.SetKillCooldown();

        if (PlayerHealth[target.PlayerId] - Damage.GetInt() < 1)
        {
            PlayerHealth.Remove(target.PlayerId);
            killer.Kill(target);
            Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
            return false;
        }

        PlayerHealth[target.PlayerId] -= Damage.GetInt();
        SendRPC(target.PlayerId);
        RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
        Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);

        Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} attacked {target.GetNameWithRole().RemoveHtmlTags()}, did {Damage.GetInt()} damage", "Gamer");
        return false;
    }

    public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
    {
        if (killer == null || target == null || killer.Is(CustomRoles.Gamer)) return true;

        if (GamerHealth[target.PlayerId] - SelfDamage.GetInt() < 1)
        {
            GamerHealth.Remove(target.PlayerId);
            Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer);
            return true;
        }

        killer.SetKillCooldown();

        GamerHealth[target.PlayerId] -= SelfDamage.GetInt();
        SendRPC(target.PlayerId);
        RPC.PlaySoundRPC(target.PlayerId, Sounds.KillSound);
        Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer);

        Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} attacked {target.GetNameWithRole().RemoveHtmlTags()}, did {SelfDamage.GetInt()} damage", "Gamer");
        return false;
    }

    public static string TargetMark(PlayerControl seer, PlayerControl target)
    {
        if (!seer.IsAlive()) return string.Empty;
        if (seer.PlayerId == target.PlayerId)
        {
            var GetValue = GamerHealth.TryGetValue(target.PlayerId, out var value);
            return GetValue && value > 0 ? Utils.ColorString(GetColor(value, true), $"【{value}/{SelfHealthMax.GetInt()}】") : string.Empty;
        }
        else
        {
            var GetValue = PlayerHealth.TryGetValue(target.PlayerId, out var value);
            return GetValue && value > 0 ? Utils.ColorString(GetColor(value), $"【{value}/{HealthMax.GetInt()}】") : string.Empty;
        }
    }

    private static Color32 GetColor(float Health, bool self = false)
    {
        var x = (int)(Health / (self ? SelfHealthMax.GetInt() : HealthMax.GetInt()) * 10 * 50);
        int R = 255;
        int G = 255;
        int B = 0;
        if (x > 255) R -= x - 255;
        else G = x;
        return new((byte)R, (byte)G, (byte)B, byte.MaxValue);
    }
}