using Hazel;
using HarmonyLib;
using Il2CppSystem;
using System.Collections.Generic;
using System.Linq;
using TOHE.Modules;
using UnityEngine;
using static TOHE.Options;
using static TOHE.Translator;
using static UnityEngine.GraphicsBuffer;
using AmongUs.GameOptions;

namespace TOHE.Roles.Neutral;

public static class Romantic
{
    private static readonly int Id = 9850;
    public static List<byte> playerIdList = new();

    private static readonly int MaxBetTimes = 1;
    public static bool isProtect = false;
    public static bool isRomanticAlive = true;
    public static bool isPartnerProtected = false;
    public static OptionItem BetCooldown;
    private static OptionItem ProtectCooldown;
    private static OptionItem ProtectDuration;
    private static OptionItem KnowTargetRole;
    private static OptionItem BetTargetKnowRomantic;
    public static OptionItem VengefulKCD;
    public static OptionItem VengefulCanVent;
    public static OptionItem RuthlessKCD;
    public static OptionItem RuthlessCanVent;

    private static Dictionary<byte, int> BetTimes = new();
    public static Dictionary<byte, byte> BetPlayer = new();

    public static void SetupCustomOption()
    {
        SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Romantic, 1, zeroOne: false);
        BetCooldown = FloatOptionItem.Create(Id + 10, "RomanticBetCooldown", new(0f, 60f, 1f), 7f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Romantic])
            .SetValueFormat(OptionFormat.Seconds);
        ProtectCooldown = FloatOptionItem.Create(Id + 11, "RomanticProtectCooldown", new(0f, 60f, 2.5f), 25f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Romantic])
            .SetValueFormat(OptionFormat.Seconds);
        ProtectDuration = FloatOptionItem.Create(Id + 12, "RomanticProtectDuration", new(0f, 60f, 2.5f), 10f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Romantic])
            .SetValueFormat(OptionFormat.Seconds);
        KnowTargetRole = BooleanOptionItem.Create(Id + 13, "RomanticKnowTargetRole", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Romantic]);
        BetTargetKnowRomantic = BooleanOptionItem.Create(Id + 14, "RomanticBetTargetKnowRomantic", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Romantic]);
        VengefulKCD = FloatOptionItem.Create(Id + 15, "VengefulKCD", new(0f, 60f, 22.5f), 22.5f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Romantic])
            .SetValueFormat(OptionFormat.Seconds);
        VengefulCanVent = BooleanOptionItem.Create(Id + 16, "VengefulCanVent", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Romantic]);
        RuthlessKCD = FloatOptionItem.Create(Id + 17, "RuthlessKCD", new(0f, 60f, 22.5f), 22.5f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Romantic])
            .SetValueFormat(OptionFormat.Seconds);
        RuthlessCanVent = BooleanOptionItem.Create(Id + 18, "RuthlessCanVent", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Romantic]);
    }
    public static void Init()
    {
        playerIdList = new();
        BetTimes = new();
        BetPlayer = new();
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        BetTimes.Add(playerId, MaxBetTimes);

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }
    public static bool IsEnable => playerIdList.Count > 0;
    private static void SendRPC(byte playerId)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncRomanticTarget, SendOption.Reliable, -1);
        writer.Write(playerId);
        //writer.Write(BetTimes.TryGetValue(playerId, out var times) ? times : MaxBetTimes);
        writer.Write(BetPlayer.TryGetValue(playerId, out var player) ? player : byte.MaxValue);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ReceiveRPC(MessageReader reader)
    {
        byte PlayerId = reader.ReadByte();
        int Times = reader.ReadInt32();
        byte Target = reader.ReadByte();
        BetTimes.Remove(PlayerId);
        BetPlayer.Remove(PlayerId);
        BetTimes.Add(PlayerId, Times);
        if (Target != byte.MaxValue)
            BetPlayer.Add(PlayerId, Target);
    }
    public static bool CanUseKillButton(PlayerControl player) => !player.Data.IsDead;
    public static void SetKillCooldown(byte id)
    {
        if (BetTimes.TryGetValue(id, out var times) && times < 1)
        {
            Main.AllPlayerKillCooldown[id] = ProtectCooldown.GetFloat();
            return;
        }
        else Main.AllPlayerKillCooldown[id] = BetCooldown.GetFloat();
        //float cd = BetCooldown.GetFloat();
        //cd += Main.AllPlayerControls.Count(x => !x.IsAlive()) * BetCooldownIncrese.GetFloat();
        //cd = Math.Min(cd, MaxBetCooldown.GetFloat());
        //Main.AllPlayerKillCooldown[id] = cd;
    }
    public static bool KnowRole(PlayerControl player, PlayerControl target)
    {
        if (!KnowTargetRole.GetBool()) return false;
        return player.Is(CustomRoles.Romantic) && BetPlayer.TryGetValue(player.PlayerId, out var tar) && tar == target.PlayerId;
    }
    public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer.PlayerId == target.PlayerId) return true;
        if (BetPlayer.TryGetValue(killer.PlayerId, out var tar) && tar == target.PlayerId) return false;
        if (!BetTimes.TryGetValue(killer.PlayerId, out var times) || times < 1) isProtect = true;

        if (!isProtect) {
            BetTimes[killer.PlayerId]--;
            if (BetPlayer.TryGetValue(killer.PlayerId, out var originalTarget) && Utils.GetPlayerById(originalTarget) != null)
                Utils.NotifyRoles(SpecifySeer: Utils.GetPlayerById(originalTarget));
            BetPlayer.Remove(killer.PlayerId);
            BetPlayer.Add(killer.PlayerId, target.PlayerId);
            SendRPC(killer.PlayerId);

            killer.ResetKillCooldown();
            killer.SetKillCooldown();
            killer.RPCPlayCustomSound("Bet");

            killer.Notify(GetString("RomanticBetPlayer"));
            if (BetTargetKnowRomantic.GetBool())
                target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Romantic), GetString("RomanticBetOnYou")));

            Logger.Info($"赌徒下注：{killer.GetNameWithRole()} => {target.GetNameWithRole()}", "Romantic");
        }
        else
        {
            isPartnerProtected = true;
            killer.ResetKillCooldown();
            killer.SetKillCooldown();
            killer.RPCPlayCustomSound("Shield");
            killer.Notify(GetString("RomanticProtectPartner"));
            target.Notify(GetString("RomanticIsProtectingYou"));
            new LateTask(() => {
                isPartnerProtected = false;
                killer.Notify("ProtectingOver");
                target.Notify("ProtectingOver");
                killer.SetKillCooldown();
            }, ProtectDuration.GetFloat());
        }
        
        return false;
    }
    public static string TargetMark(PlayerControl seer, PlayerControl target)
    {
        if (!seer.Is(CustomRoles.Romantic))
        {
            if (!BetTargetKnowRomantic.GetBool()) return "";
            return (BetPlayer.TryGetValue(target.PlayerId, out var x) && seer.PlayerId == x) ?
                Utils.ColorString(Utils.GetRoleColor(CustomRoles.Romantic), "♥") : "";
        }
        var GetValue = BetPlayer.TryGetValue(seer.PlayerId, out var targetId);
        return GetValue && targetId == target.PlayerId ? Utils.ColorString(Utils.GetRoleColor(CustomRoles.Romantic), "♥") : "";
    }
    public static string GetProgressText(byte playerId)
    {
        var player = Utils.GetPlayerById(playerId);
        if (player == null) return null;
        return Utils.ColorString(BetTimes.TryGetValue(playerId, out var times) && times >= 1 ? Color.white : Utils.GetRoleColor(CustomRoles.Romantic), $"<color=#777777>-</color> {(CanUseKillButton(player) ? "PICK PARTNER" : "PROTECT PARTNER")}");
    }
    public static void ChangeRole(byte playerId)
    {
        var player = Utils.GetPlayerById(playerId);
        if (player == null) return;
        byte Romantic = 0x73;
        BetPlayer.Do(x =>
        {
            if (x.Value == playerId)
                Romantic = x.Key;
        });

        if (player.IsNeutralKiller()) Utils.GetPlayerById(Romantic).RpcSetCustomRole(player.GetCustomRole());
        if (player.GetCustomRole().IsImpostor()) Utils.GetPlayerById(Romantic).RpcSetCustomRole(CustomRoles.RuthlessRomantic);
        else Utils.GetPlayerById(Romantic).RpcSetCustomRole(CustomRoles.VengefulRomantic);
    }
}

public static class VengefulRomantic
{
    public static List<byte> playerIdList = new();

    public static bool hasKilledKiller = false;
    public static Dictionary<byte, byte> PartnerKiller = new();

    public static void Init()
    {
        playerIdList = new();
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }
    public static bool IsEnable => playerIdList.Count > 0;

    public static bool CanUseKillButton(PlayerControl player) => !player.Data.IsDead && !hasKilledKiller;

    public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer.PlayerId == target.PlayerId) return false;

        if (PartnerKiller.TryGetValue(target.PlayerId, out var RomanticPartner) && target.PlayerId == RomanticPartner)
        {
            hasKilledKiller = true;
            return true;
        }
        else
        {
            killer.RpcMurderPlayerV3(killer);
            return false;
        }
    }
    public static string GetProgressText(byte playerId)
    {
        var player = Utils.GetPlayerById(playerId);
        if (player == null) return null;
        return Utils.ColorString(CanUseKillButton(player) ? Utils.GetRoleColor(CustomRoles.VengefulRomantic) : Color.green, $"<color=#777777>-</color> {((hasKilledKiller) ? "AVEGNGE SUCCESSFUL" : "AVENGE YOUR PARTNER")}");
    }
}

public static class RuthlessRomantic
{
    public static List<byte> playerIdList = new();
    public static void Init()
    {
        playerIdList = new();
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }
    public static bool IsEnable => playerIdList.Count > 0;
}
