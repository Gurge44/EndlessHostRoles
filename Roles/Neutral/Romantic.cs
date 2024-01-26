using Hazel;
using System.Collections.Generic;
using System.Linq;
using TOHE.Modules;
using UnityEngine;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Neutral;

public static class Romantic
{
    private static readonly int Id = 9850;
    public static List<byte> playerIdList = [];

    private static readonly int MaxBetTimes = 1;
    public static bool isProtect;
    public static bool isRomanticAlive = true;
    public static bool isPartnerProtected;

    public static OptionItem BetCooldown;
    private static OptionItem ProtectCooldown;
    private static OptionItem ProtectDuration;
    private static OptionItem KnowTargetRole;
    private static OptionItem BetTargetKnowRomantic;
    public static OptionItem VengefulKCD;
    public static OptionItem VengefulCanVent;
    public static OptionItem RuthlessKCD;
    public static OptionItem RuthlessCanVent;

    public static Dictionary<byte, int> BetTimes = [];
    public static Dictionary<byte, byte> BetPlayer = [];

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
        VengefulKCD = FloatOptionItem.Create(Id + 15, "VengefulKCD", new(0f, 60f, 2.5f), 22.5f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Romantic])
            .SetValueFormat(OptionFormat.Seconds);
        VengefulCanVent = BooleanOptionItem.Create(Id + 16, "VengefulCanVent", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Romantic]);
        RuthlessKCD = FloatOptionItem.Create(Id + 17, "RuthlessKCD", new(0f, 60f, 2.5f), 22.5f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Romantic])
            .SetValueFormat(OptionFormat.Seconds);
        RuthlessCanVent = BooleanOptionItem.Create(Id + 18, "RuthlessCanVent", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Romantic]);
    }
    public static void Init()
    {
        playerIdList = [];
        BetTimes = [];
        BetPlayer = [];
        isProtect = false;
        isPartnerProtected = false;
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
        if (!IsEnable || !Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncRomanticTarget, SendOption.Reliable, -1);
        writer.Write(playerId);
        writer.Write(BetTimes.TryGetValue(playerId, out var times) ? times : MaxBetTimes);
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
    public static void SetKillCooldown(byte id)
    {
        float beforeCD = Main.AllPlayerKillCooldown.TryGetValue(id, out var kcd) ? kcd : 0f;
        if (BetTimes.TryGetValue(id, out var times) && times < 1) Main.AllPlayerKillCooldown[id] = ProtectCooldown.GetFloat();
        else Main.AllPlayerKillCooldown[id] = BetCooldown.GetFloat();
        if (beforeCD != Main.AllPlayerKillCooldown[id]) Utils.GetPlayerById(id)?.SyncSettings();
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
        //if (BetPlayer.TryGetValue(killer.PlayerId, out var tar) && tar == target.PlayerId) return false;
        if (!BetTimes.TryGetValue(killer.PlayerId, out var times) || times < 1) isProtect = true;

        if (!isProtect)
        {
            BetTimes[killer.PlayerId]--;
            
            BetPlayer.Remove(killer.PlayerId);
            BetPlayer.Add(killer.PlayerId, target.PlayerId);
            SendRPC(killer.PlayerId);

            killer.ResetKillCooldown();
            killer.SetKillCooldown();
            killer.RPCPlayCustomSound("Bet");

            killer.Notify(GetString("RomanticBetPlayer"));
            if (BetTargetKnowRomantic.GetBool())
                target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Romantic), GetString("RomanticBetOnYou")));

            Logger.Info($"Partner picked：{killer.GetNameWithRole().RemoveHtmlTags()} => {target.GetNameWithRole().RemoveHtmlTags()}", "Romantic");
        }
        else
        {
            if (!isPartnerProtected && BetPlayer.TryGetValue(killer.PlayerId, out var originalTarget))
            {
                var tpc = Utils.GetPlayerById(originalTarget);
                isPartnerProtected = true;
                killer.ResetKillCooldown();
                killer.SetKillCooldown();
                killer.RPCPlayCustomSound("Shield");
                killer.Notify(GetString("RomanticProtectPartner"));
                tpc.Notify(GetString("RomanticIsProtectingYou"));
                _ = new LateTask(() =>
                {
                    if (!tpc.IsAlive()) return;
                    isPartnerProtected = false;
                    if (!GameStates.IsInTask) return;
                    killer.Notify(GetString("ProtectingOver"));
                    tpc.Notify(GetString("ProtectingOver"));
                    killer.SetKillCooldown();
                }, ProtectDuration.GetFloat());
            }
        }

        return false;
    }
    public static string TargetMark(PlayerControl seer, PlayerControl target)
    {
        if (!seer.Is(CustomRoles.Romantic))
        {
            if (!BetTargetKnowRomantic.GetBool()) return string.Empty;
            return (BetPlayer.TryGetValue(target.PlayerId, out var x) && seer.PlayerId == x) ?
                Utils.ColorString(Utils.GetRoleColor(CustomRoles.Romantic), "♥") : string.Empty;
        }
        var GetValue = BetPlayer.TryGetValue(seer.PlayerId, out var targetId);
        return GetValue && targetId == target.PlayerId ? Utils.ColorString(Utils.GetRoleColor(CustomRoles.Romantic), "♥") : string.Empty;
    }
    public static string GetTargetText(byte playerId)
    {
        var player = Utils.GetPlayerById(playerId);
        if (player == null) return null;
        return Utils.ColorString(BetTimes.TryGetValue(playerId, out var timesV1) && timesV1 >= 1 ? Color.white : Utils.GetRoleColor(CustomRoles.Romantic), $"{(BetTimes.TryGetValue(playerId, out var timesV2) && timesV2 >= 1 && timesV2 >= 1 ? "PICK PARTNER" : "♥")}");
    }
    public static void OnReportDeadBody()
    {
        isPartnerProtected = false;
    }
    public static void ChangeRole(byte playerId)
    {
        var partner = Utils.GetPlayerById(playerId);
        if (partner == null) return;
        var partnerRole = partner.GetCustomRole();

        byte romanticId = BetPlayer.First(x => x.Value == playerId).Key;
        var romantic = Utils.GetPlayerById(romanticId);
        if (romantic == null) return;

        var killer = partner.GetRealKiller();

        if (killer?.PlayerId == romanticId) // If the Romantic killed their partner, they also die
        {
            if (!romantic.IsAlive()) return;
            Logger.Info("Romantic killed their own partner, Romantic suicides", "Romantic");
            romantic.Suicide(PlayerState.DeathReason.FollowingSuicide, partner);
            return;
        }
        else if (partnerRole.IsNonNK() || killer == null) // If partner is NNK or died by themselves, Romantic becomes Ruthless Romantic
        {
            Logger.Info($"NK Romantic Partner Died / Partner killer is null => changing {romantic.GetNameWithRole().RemoveHtmlTags()} to Ruthless Romantic", "Romantic");
            romantic.RpcSetCustomRole(CustomRoles.RuthlessRomantic);
            RuthlessRomantic.Add(romanticId);
        }
        else if (partner.HasKillButton() || partnerRole.IsNK() || partnerRole.IsCK()) // If partner has a kill button (NK or CK), Romantic becomes the role they were
        {
            try
            {
                romantic.RpcSetCustomRole(partnerRole);
                Utils.AddRoles(romantic.PlayerId, partnerRole);
                Logger.Info($"Romantic Partner With Kill Button Died => changing {romantic.GetNameWithRole()} to {partner.GetAllRoleName().RemoveHtmlTags()}", "Romantic");
            }
            catch
            {
                Logger.Error($"Romantic Partner With Kill Button Died => changing {romantic.GetNameWithRole()} to {partner.GetAllRoleName().RemoveHtmlTags()} : FAILED ----> changing to Ruthless Romantic instead", "Romantic");
                romantic.RpcSetCustomRole(CustomRoles.RuthlessRomantic);
                RuthlessRomantic.Add(romanticId);
            }
        }
        else if (partner.Is(Team.Impostor)) // If partner is Imp, Romantic joins imp team as Refugee
        {
            Logger.Info($"Impostor Romantic Partner Died => changing {romantic.GetNameWithRole()} to Refugee", "Romantic");
            romantic.RpcSetCustomRole(CustomRoles.Refugee);
        }
        else // In every other scenario, Romantic becomes Vengeful Romantic and must kill the killer of their partner
        {
            _ = new LateTask(() =>
            {
                Logger.Info($"Crew/NNK Romantic Partner Died => changing {romantic.GetNameWithRole()} to Vengeful Romantic", "Romantic");

                VengefulRomantic.Add(romanticId, killer.PlayerId);
                VengefulRomantic.SendRPC(romanticId);
                romantic.RpcSetCustomRole(CustomRoles.VengefulRomantic);
            }, 0.2f, "Convert to Vengeful romanticId");
        }

        Utils.NotifyRoles(SpecifySeer: romantic);
        Utils.NotifyRoles(SpecifyTarget: romantic);

        romantic.ResetKillCooldown();
        romantic.SetKillCooldown();

        romantic.SyncSettings();
    }
}

public static class VengefulRomantic
{
    public static List<byte> playerIdList = [];

    public static bool hasKilledKiller;
    public static Dictionary<byte, byte> VengefulTarget = [];

    public static void Init()
    {
        playerIdList = [];
        VengefulTarget = [];
        hasKilledKiller = false;
    }
    public static void Add(byte playerId, byte killerId = byte.MaxValue)
    {
        playerIdList.Add(playerId);
        VengefulTarget.Add(playerId, killerId);

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }
    public static bool IsEnable => playerIdList.Count > 0;

    public static bool CanUseKillButton(PlayerControl player) => !player.Data.IsDead && !hasKilledKiller;

    public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer.PlayerId == target.PlayerId) return true;

        if (VengefulTarget.TryGetValue(killer.PlayerId, out var PartnerKiller) && target.PlayerId == PartnerKiller)
        {
            hasKilledKiller = true;
            Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
            return true;
        }
        else
        {
            killer.Suicide(PlayerState.DeathReason.Misfire);
            return false;
        }
    }
    public static string GetTargetText(byte playerId)
    {
        var player = Utils.GetPlayerById(playerId);
        if (player == null) return null;
        return Utils.ColorString(hasKilledKiller ? Color.green : Utils.GetRoleColor(CustomRoles.VengefulRomantic), $"{(hasKilledKiller ? "✓" : "☹️")}");
    }
    public static void SendRPC(byte playerId)
    {
        if (!IsEnable || !Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncVengefulRomanticTarget, SendOption.Reliable, -1);
        writer.Write(playerId);
        //writer.Write(BetTimes.TryGetValue(playerId, out var times) ? times : MaxBetTimes);
        writer.Write(VengefulTarget.TryGetValue(playerId, out var player) ? player : byte.MaxValue);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ReceiveRPC(MessageReader reader)
    {
        byte PlayerId = reader.ReadByte();
        byte Target = reader.ReadByte();
        VengefulTarget.Remove(PlayerId);
        if (Target != byte.MaxValue)
            VengefulTarget.Add(PlayerId, Target);
    }
}

public static class RuthlessRomantic
{
    public static List<byte> playerIdList = [];
    public static void Init()
    {
        playerIdList = [];
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
