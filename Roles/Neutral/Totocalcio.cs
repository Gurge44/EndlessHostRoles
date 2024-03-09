using System.Collections.Generic;
using System.Linq;
using Hazel;
using Il2CppSystem;
using TOHE.Modules;
using UnityEngine;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Neutral;

public class Totocalcio : RoleBase
{
    private const int Id = 9800;
    public static List<byte> playerIdList = [];

    private static OptionItem MaxBetTimes;
    public static OptionItem BetCooldown;
    private static OptionItem BetCooldownIncrese;
    private static OptionItem MaxBetCooldown;
    private static OptionItem KnowTargetRole;
    private static OptionItem BetTargetKnowTotocalcio;

    private int BetTimes;
    public byte BetPlayer;

    public static void SetupCustomOption()
    {
        SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Totocalcio, 1, zeroOne: false);
        MaxBetTimes = IntegerOptionItem.Create(Id + 10, "TotocalcioMaxBetTimes", new(1, 5, 1), 3, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Totocalcio])
            .SetValueFormat(OptionFormat.Times);
        BetCooldown = FloatOptionItem.Create(Id + 12, "TotocalcioBetCooldown", new(0f, 60f, 2.5f), 10f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Totocalcio])
            .SetValueFormat(OptionFormat.Seconds);
        BetCooldownIncrese = FloatOptionItem.Create(Id + 14, "TotocalcioBetCooldownIncrese", new(0f, 60f, 1f), 10f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Totocalcio])
            .SetValueFormat(OptionFormat.Seconds);
        MaxBetCooldown = FloatOptionItem.Create(Id + 16, "TotocalcioMaxBetCooldown", new(0f, 180f, 2.5f), 70f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Totocalcio])
            .SetValueFormat(OptionFormat.Seconds);
        KnowTargetRole = BooleanOptionItem.Create(Id + 18, "TotocalcioKnowTargetRole", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Totocalcio]);
        BetTargetKnowTotocalcio = BooleanOptionItem.Create(Id + 20, "TotocalcioBetTargetKnowTotocalcio", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Totocalcio]);
    }

    public override void Init()
    {
        playerIdList = [];
        BetTimes = MaxBetTimes.GetInt();
        BetPlayer = byte.MaxValue;
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        BetTimes = MaxBetTimes.GetInt();
        BetPlayer = byte.MaxValue;

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }

    public override bool IsEnable => playerIdList.Count > 0;

    void SendRPC(byte playerId)
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
        byte PlayerId = reader.ReadByte();
        if (Main.PlayerStates[PlayerId].Role is not Totocalcio tc) return;
        int Times = reader.ReadInt32();
        byte Target = reader.ReadByte();
        tc.BetTimes = Times;
        if (Target != byte.MaxValue)
            tc.BetPlayer = Target;
    }

    public override bool CanUseKillButton(PlayerControl player) => !player.Data.IsDead && (BetTimes >= 1);
    public override bool CanUseImpostorVentButton(PlayerControl pc) => false;

    public override void SetKillCooldown(byte id)
    {
        if (BetTimes < 1)
        {
            Main.AllPlayerKillCooldown[id] = 300f;
            return;
        }
        float cd = BetCooldown.GetFloat();
        cd += Main.AllPlayerControls.Count(x => !x.IsAlive()) * BetCooldownIncrese.GetFloat();
        cd = Math.Min(cd, MaxBetCooldown.GetFloat());
        Main.AllPlayerKillCooldown[id] = cd;
    }
    public static bool KnowRole(PlayerControl player, PlayerControl target)
    {
        if (!KnowTargetRole.GetBool()) return false;
        return Main.PlayerStates[player.PlayerId].Role is Totocalcio { IsEnable: true } tc && tc.BetPlayer == target.PlayerId;
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer.PlayerId == target.PlayerId) return true;
        if (BetPlayer == target.PlayerId) return false;
        if (BetTimes < 1) return false;

        BetTimes--;
        var betPlayer = Utils.GetPlayerById(BetPlayer);
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
        if (BetTargetKnowTotocalcio.GetBool())
            target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Totocalcio), GetString("TotocalcioBetOnYou")));

        Logger.Info($"Target selected：{killer.GetNameWithRole().RemoveHtmlTags()} => {target.GetNameWithRole().RemoveHtmlTags()}", "Totocalcio");
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
        var player = Utils.GetPlayerById(playerId);
        if (Main.PlayerStates[playerId].Role is not Totocalcio tc) return string.Empty;
        return player == null ? string.Empty : Utils.ColorString(tc.CanUseKillButton(player) ? Utils.GetRoleColor(CustomRoles.Totocalcio) : Color.gray, $"({tc.BetTimes})");
    }
}