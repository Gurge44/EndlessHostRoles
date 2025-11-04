using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using UnityEngine;
using static EHR.Options;

namespace EHR.Neutral;

public class Demon : RoleBase
{
    private const int Id = 10600;
    public static List<byte> PlayerIdList = [];

    private static Dictionary<byte, int> PlayerHealth = [];
    private static Dictionary<byte, int> DemonHealth = [];

    private static OptionItem KillCooldown;
    private static OptionItem CanVent;
    private static OptionItem HasImpostorVision;
    private static OptionItem HealthMax;
    private static OptionItem Damage;
    private static OptionItem SelfHealthMax;
    private static OptionItem SelfDamage;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Demon);

        KillCooldown = new FloatOptionItem(Id + 10, "DemonKillCooldown", new(1f, 180f, 1f), 2f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Demon])
            .SetValueFormat(OptionFormat.Seconds);

        CanVent = new BooleanOptionItem(Id + 11, "CanVent", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Demon]);

        HasImpostorVision = new BooleanOptionItem(Id + 13, "ImpostorVision", false, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Demon]);

        HealthMax = new IntegerOptionItem(Id + 15, "DemonHealthMax", new(5, 300, 5), 100, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Demon])
            .SetValueFormat(OptionFormat.Health);

        Damage = new IntegerOptionItem(Id + 16, "DemonDamage", new(1, 100, 1), 15, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Demon])
            .SetValueFormat(OptionFormat.Health);

        SelfHealthMax = new IntegerOptionItem(Id + 17, "DemonSelfHealthMax", new(100, 100, 5), 100, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Demon])
            .SetValueFormat(OptionFormat.Health);

        SelfDamage = new IntegerOptionItem(Id + 18, "DemonSelfDamage", new(1, 100, 1), 35, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Demon])
            .SetValueFormat(OptionFormat.Health);
    }

    public override void Init()
    {
        PlayerIdList = [];
        DemonHealth = [];
        PlayerHealth = [];
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        DemonHealth[playerId] = SelfHealthMax.GetInt();

        foreach (PlayerControl pc in Main.AllAlivePlayerControls)
            PlayerHealth[pc.PlayerId] = HealthMax.GetInt();
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        opt.SetVision(HasImpostorVision.GetBool());
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return CanVent.GetBool();
    }

    private void SendRPC(byte playerId)
    {
        if (!IsEnable || !Utils.DoRPC) return;

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetDemonHealth, SendOption.Reliable);
        writer.Write(playerId);
        writer.Write(DemonHealth.TryGetValue(playerId, out int value) ? value : PlayerHealth[playerId]);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        byte playerId = reader.ReadByte();
        int health = reader.ReadInt32();

        if (DemonHealth.ContainsKey(playerId))
            DemonHealth[playerId] = health;
        else
            PlayerHealth[playerId] = health;
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer == null || target == null || target.Is(CustomRoles.Demon) || !PlayerHealth.TryGetValue(target.PlayerId, out var targetHealth)) return false;

        killer.SetKillCooldown();

        if (targetHealth - Damage.GetInt() < 1)
        {
            if (target.Is(CustomRoles.Pestilence))
            {
                target.Kill(killer);

                if (target.AmOwner)
                    Achievements.Type.YoureTooLate.Complete();

                return false;
            }

            PlayerHealth.Remove(target.PlayerId);
            killer.RpcCheckAndMurder(target);
            Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
            return false;
        }

        PlayerHealth[target.PlayerId] -= Damage.GetInt();
        SendRPC(target.PlayerId);
        RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
        Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);

        Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} attacked {target.GetNameWithRole().RemoveHtmlTags()}, did {Damage.GetInt()} damage", "Demon");
        return false;
    }

    public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
    {
        if (killer == null || target == null || killer.Is(CustomRoles.Demon)) return true;

        if (DemonHealth[target.PlayerId] - SelfDamage.GetInt() < 1)
        {
            DemonHealth.Remove(target.PlayerId);
            Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer);
            return true;
        }

        killer.SetKillCooldown();

        DemonHealth[target.PlayerId] -= SelfDamage.GetInt();
        SendRPC(target.PlayerId);
        RPC.PlaySoundRPC(target.PlayerId, Sounds.KillSound);
        Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer);

        Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} attacked {target.GetNameWithRole().RemoveHtmlTags()}, did {SelfDamage.GetInt()} damage", "Demon");
        return false;
    }

    public static string TargetMark(PlayerControl seer, PlayerControl target)
    {
        if (!seer.IsAlive() || !PlayerIdList.Contains(seer.PlayerId)) return string.Empty;

        if (seer.PlayerId == target.PlayerId)
        {
            return DemonHealth.TryGetValue(target.PlayerId, out int value) && value > 0
                ? Utils.ColorString(GetColor(value, true), $"【{value}/{SelfHealthMax.GetInt()}】")
                : string.Empty;
        }
        else
        {
            return PlayerHealth.TryGetValue(target.PlayerId, out int value) && value > 0
                ? Utils.ColorString(GetColor(value), $"【{value}/{HealthMax.GetInt()}】")
                : string.Empty;
        }
    }

    private static Color32 GetColor(float health, bool self = false)
    {
        var x = (int)(health / (self ? SelfHealthMax.GetInt() : HealthMax.GetInt()) * 10 * 50);
        var r = 255;
        var g = 255;

        if (x > 255)
            r -= x - 255;
        else
            g = x;

        return new((byte)r, (byte)g, 0, byte.MaxValue);
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        hud.KillButton?.OverrideText(Translator.GetString("DemonButtonText"));
    }
}