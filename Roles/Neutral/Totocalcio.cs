using System.Collections.Generic;
using EHR.Modules;
using Hazel;
using UnityEngine;
using static EHR.Options;
using static EHR.Translator;
using Math = System.Math;

namespace EHR.Neutral;

public class Totocalcio : RoleBase
{
    private const int Id = 9800;
    public static List<byte> PlayerIdList = [];

    private static OptionItem MaxBetTimes;
    private static OptionItem BetCooldown;
    private static OptionItem BetCooldownIncrese;
    private static OptionItem MaxBetCooldown;
    private static OptionItem KnowTargetRole;
    private static OptionItem BetTargetKnowTotocalcio;

    public byte BetPlayer;
    private int BetTimes;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Totocalcio);

        MaxBetTimes = new IntegerOptionItem(Id + 10, "TotocalcioMaxBetTimes", new(1, 5, 1), 3, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Totocalcio])
            .SetValueFormat(OptionFormat.Times);

        BetCooldown = new FloatOptionItem(Id + 12, "TotocalcioBetCooldown", new(0f, 60f, 0.5f), 10f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Totocalcio])
            .SetValueFormat(OptionFormat.Seconds);

        BetCooldownIncrese = new FloatOptionItem(Id + 14, "TotocalcioBetCooldownIncrese", new(0f, 60f, 1f), 10f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Totocalcio])
            .SetValueFormat(OptionFormat.Seconds);

        MaxBetCooldown = new FloatOptionItem(Id + 16, "TotocalcioMaxBetCooldown", new(0f, 180f, 0.5f), 70f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Totocalcio])
            .SetValueFormat(OptionFormat.Seconds);

        KnowTargetRole = new BooleanOptionItem(Id + 18, "TotocalcioKnowTargetRole", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Totocalcio]);

        BetTargetKnowTotocalcio = new BooleanOptionItem(Id + 20, "TotocalcioBetTargetKnowTotocalcio", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Totocalcio]);
    }

    public override void Init()
    {
        PlayerIdList = [];
        BetTimes = MaxBetTimes.GetInt();
        BetPlayer = byte.MaxValue;
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        BetTimes = MaxBetTimes.GetInt();
        BetPlayer = byte.MaxValue;
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    private void SendRPC(byte playerId)
    {
        if (!IsEnable || !Utils.DoRPC) return;

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncTotocalcioTargetAndTimes, SendOption.Reliable);
        writer.Write(playerId);
        writer.Write(BetTimes);
        writer.Write(BetPlayer);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        byte playerId = reader.ReadByte();
        if (Main.PlayerStates[playerId].Role is not Totocalcio tc) return;

        int times = reader.ReadInt32();
        byte target = reader.ReadByte();
        tc.BetTimes = times;
        if (target != byte.MaxValue) tc.BetPlayer = target;
    }

    public override bool CanUseKillButton(PlayerControl player)
    {
        return player.IsAlive() && BetTimes >= 1;
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return false;
    }

    public override void SetKillCooldown(byte id)
    {
        if (BetTimes < 1)
        {
            Main.AllPlayerKillCooldown[id] = 300f;
            return;
        }

        float cd = BetCooldown.GetFloat();
        cd += (MaxBetTimes.GetInt() - BetTimes) * BetCooldownIncrese.GetFloat();
        cd = Math.Min(cd, MaxBetCooldown.GetFloat());
        Main.AllPlayerKillCooldown[id] = cd;
    }

    public override bool KnowRole(PlayerControl player, PlayerControl target)
    {
        if (base.KnowRole(player, target)) return true;

        if (!KnowTargetRole.GetBool()) return false;

        return player.Is(CustomRoles.Totocalcio) && BetPlayer == target.PlayerId;
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer.PlayerId == target.PlayerId) return true;

        if (BetPlayer == target.PlayerId) return false;
        if (BetTimes < 1) return false;

        BetTimes--;
        PlayerControl betPlayer = Utils.GetPlayerById(BetPlayer);

        if (betPlayer != null)
        {
            Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: betPlayer, ForceLoop: true);
            Utils.NotifyRoles(SpecifySeer: betPlayer, SpecifyTarget: killer, ForceLoop: true);
        }

        BetPlayer = target.PlayerId;
        SendRPC(killer.PlayerId);

        killer.ResetKillCooldown();
        killer.SetKillCooldown();
        killer.RPCPlayCustomSound("Bet");

        killer.Notify(GetString("TotocalcioBetPlayer"));
        if (BetTargetKnowTotocalcio.GetBool()) target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Totocalcio), GetString("TotocalcioBetOnYou")));

        Logger.Info($"Target selected: {killer.GetNameWithRole().RemoveHtmlTags()} => {target.GetNameWithRole().RemoveHtmlTags()}", "Totocalcio");
        return false;
    }

    public static string TargetMark(PlayerControl seer, PlayerControl target)
    {
        if (!seer.Is(CustomRoles.Totocalcio))
        {
            if (!BetTargetKnowTotocalcio.GetBool()) return string.Empty;

            if (Main.PlayerStates[target.PlayerId].Role is not Totocalcio tc) return string.Empty;

            return seer.PlayerId == tc.BetPlayer ? Utils.ColorString(Utils.GetRoleColor(CustomRoles.Totocalcio), "♦") : string.Empty;
        }
        else
        {
            if (Main.PlayerStates[seer.PlayerId].Role is not Totocalcio tc) return string.Empty;

            return tc.BetPlayer == target.PlayerId ? Utils.ColorString(Utils.GetRoleColor(CustomRoles.Totocalcio), "♦") : string.Empty;
        }
    }

    public override string GetProgressText(byte playerId, bool comms)
    {
        PlayerControl player = Utils.GetPlayerById(playerId);
        if (Main.PlayerStates[playerId].Role is not Totocalcio tc) return string.Empty;

        return player == null ? string.Empty : Utils.ColorString(tc.CanUseKillButton(player) ? Utils.GetRoleColor(CustomRoles.Totocalcio) : Color.gray, $"({tc.BetTimes})");
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        hud.KillButton?.OverrideText(GetString("TotocalcioKillButtonText"));
    }
}