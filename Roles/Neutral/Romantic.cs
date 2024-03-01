using System;
using System.Collections.Generic;
using Hazel;
using TOHE.Modules;
using UnityEngine;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Neutral;

public class Romantic : RoleBase
{
    private const int Id = 9850;

    public static byte RomanticId = byte.MaxValue;
    public static PlayerControl Romantic_;
    public static bool HasPickedPartner => PartnerId != byte.MaxValue;
    public static byte PartnerId = byte.MaxValue;
    public static PlayerControl Partner;

    public static bool IsPartnerProtected;

    public static OptionItem BetCooldown;
    private static OptionItem ProtectCooldown;
    private static OptionItem ProtectDuration;
    private static OptionItem KnowTargetRole;
    private static OptionItem BetTargetKnowRomantic;
    public static OptionItem VengefulKCD;
    public static OptionItem VengefulCanVent;
    public static OptionItem RuthlessKCD;
    public static OptionItem RuthlessCanVent;

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

    public override void Init()
    {
        RomanticId = byte.MaxValue;
        Romantic_ = null;
        PartnerId = byte.MaxValue;
        Partner = null;
        IsPartnerProtected = false;
    }

    public override void Add(byte playerId)
    {
        RomanticId = playerId;
        Romantic_ = Utils.GetPlayerById(playerId);

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }

    public override bool IsEnable => RomanticId != byte.MaxValue;

    private static void SendRPC()
    {
        if (!Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncRomanticTarget, SendOption.Reliable);
        writer.Write(PartnerId);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        PartnerId = reader.ReadByte();
        Partner = Utils.GetPlayerById(PartnerId);
    }

    public override void SetKillCooldown(byte id)
    {
        float beforeCD = Main.AllPlayerKillCooldown.GetValueOrDefault(RomanticId, 0f);

        if (HasPickedPartner) Main.AllPlayerKillCooldown[RomanticId] = ProtectCooldown.GetFloat();
        else Main.AllPlayerKillCooldown[RomanticId] = BetCooldown.GetFloat();

        if (Math.Abs(beforeCD - Main.AllPlayerKillCooldown[RomanticId]) > 0.5f) Romantic_?.SyncSettings();
    }

    public static bool KnowRole(PlayerControl player, PlayerControl target)
    {
        if (!KnowTargetRole.GetBool()) return false;
        return player.Is(CustomRoles.Romantic) && PartnerId == target.PlayerId;
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer == null || target == null || killer.PlayerId == target.PlayerId || killer.PlayerId != RomanticId) return true;

        if (!HasPickedPartner)
        {
            PartnerId = target.PlayerId;
            Partner = Utils.GetPlayerById(PartnerId);

            SendRPC();

            Romantic_.ResetKillCooldown();
            Romantic_.SetKillCooldown();
            Romantic_.RPCPlayCustomSound("Bet");

            Romantic_.Notify(GetString("RomanticBetPlayer"));
            if (BetTargetKnowRomantic.GetBool()) target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Romantic), GetString("RomanticBetOnYou")));

            Logger.Info($"Partner picked： {Romantic_.GetNameWithRole().RemoveHtmlTags()} => {target.GetNameWithRole().RemoveHtmlTags()}", "Romantic");
        }
        else if (!IsPartnerProtected)
        {
            IsPartnerProtected = true;

            Romantic_.ResetKillCooldown();
            Romantic_.SetKillCooldown();
            Romantic_.RPCPlayCustomSound("Shield");

            Romantic_.Notify(GetString("RomanticProtectPartner"));
            Partner.Notify(GetString("RomanticIsProtectingYou"));

            _ = new LateTask(() =>
            {
                if (!Partner.IsAlive()) return;

                IsPartnerProtected = false;

                if (!GameStates.IsInTask) return;

                Romantic_.Notify(GetString("ProtectingOver"));
                Partner.Notify(GetString("ProtectingOver"));

                Romantic_.SetKillCooldown();
            }, ProtectDuration.GetFloat());
        }

        return false;
    }

    public static string TargetMark(PlayerControl seer, PlayerControl target)
    {
        if (!seer.Is(CustomRoles.Romantic))
        {
            if (!BetTargetKnowRomantic.GetBool()) return string.Empty;

            return RomanticId == target.PlayerId
                ? Utils.ColorString(Utils.GetRoleColor(CustomRoles.Romantic), "♥")
                : string.Empty;
        }

        return PartnerId == target.PlayerId
            ? Utils.ColorString(Utils.GetRoleColor(CustomRoles.Romantic), "♥")
            : string.Empty;
    }

    public static string GetTargetText(byte playerId) => playerId != RomanticId
        ? null
        : Utils.ColorString(!HasPickedPartner ? Color.white : Utils.GetRoleColor(CustomRoles.Romantic), $"{(!HasPickedPartner ? "PICK PARTNER" : "♥")}");

    public override void OnReportDeadBody() => IsPartnerProtected = false;
    public override bool CanUseImpostorVentButton(PlayerControl pc) => false;

    public static void ChangeRole()
    {
        if (Partner == null || Romantic_ == null) return;

        var partnerRole = Partner.GetCustomRole();
        var killer = Partner.GetRealKiller();

        if (killer?.PlayerId == RomanticId) // If the Romantic killed their Partner, they also die
        {
            if (!Romantic_.IsAlive()) return;
            Logger.Info("Romantic killed their own Partner, Romantic suicides", "Romantic");
            Romantic_.Suicide(PlayerState.DeathReason.FollowingSuicide, Partner);
            return;
        }

        if ((partnerRole.IsNonNK() && partnerRole is not CustomRoles.Romantic and not CustomRoles.VengefulRomantic and not CustomRoles.RuthlessRomantic) || killer == null || Main.PlayerStates[PartnerId].IsSuicide) // If Partner is NNK or died by themselves, Romantic becomes Ruthless Romantic
        {
            Logger.Info($"NNK Romantic Partner Died ({partnerRole.IsNonNK()}) / Partner killer is null ({killer == null}) / Partner commited Suicide ({Main.PlayerStates[PartnerId].IsSuicide}) => Changing {Romantic_.GetNameWithRole().RemoveHtmlTags()} to Ruthless Romantic", "Romantic");
            Romantic_.RpcSetCustomRole(CustomRoles.RuthlessRomantic);
        }
        else if (Partner.Is(Team.Impostor)) // If Partner is Imp, Romantic joins imp team as Refugee
        {
            Logger.Info($"Impostor Romantic Partner Died => Changing {Romantic_.GetNameWithRole()} to Refugee", "Romantic");
            Romantic_.RpcSetCustomRole(CustomRoles.Refugee);
        }
        else if (Partner.HasKillButton() || partnerRole.IsNK() || partnerRole.IsTasklessCrewmate()) // If Partner has a kill button (NK or CK), Romantic becomes the role they were
        {
            try
            {
                Romantic_.RpcSetCustomRole(partnerRole);
                HudManager.Instance.SetHudActive(Romantic_, Romantic_.Data.Role, !GameStates.IsMeeting);
                Main.PlayerStates[RomanticId].RemoveSubRole(CustomRoles.NotAssigned);
                Logger.Info($"Romantic Partner With Kill Button Died => Changing {Romantic_.GetNameWithRole()} to {Partner.GetAllRoleName().RemoveHtmlTags()}", "Romantic");
            }
            catch
            {
                Logger.Error($"Romantic Partner With Kill Button Died => Changing {Romantic_.GetNameWithRole()} to {Partner.GetAllRoleName().RemoveHtmlTags()} : FAILED ----> changing to Ruthless Romantic instead", "Romantic");
                Romantic_.RpcSetCustomRole(CustomRoles.RuthlessRomantic);
            }
        }
        else // In every other scenario, Romantic becomes Vengeful Romantic and must kill the killer of their Partner
        {
            Logger.Info($"Non-Killing Crew Romantic Partner Died => Changing {Romantic_.GetNameWithRole()} to Vengeful Romantic", "Romantic");

            Romantic_.RpcSetCustomRole(CustomRoles.VengefulRomantic);
            VengefulRomantic.Target = killer.PlayerId;
            VengefulRomantic.SendRPC();
        }

        Utils.NotifyRoles(SpecifySeer: Romantic_);
        Utils.NotifyRoles(SpecifyTarget: Romantic_);

        Romantic_.ResetKillCooldown();
        Romantic_.SetKillCooldown();

        Romantic_.SyncSettings();
    }
}

public class VengefulRomantic : RoleBase
{
    public static byte VengefulRomanticId = byte.MaxValue;
    public static PlayerControl VengefulRomantic_;

    public static bool HasKilledKiller;
    public static byte Target = byte.MaxValue;

    public override void Init()
    {
        VengefulRomanticId = byte.MaxValue;
        VengefulRomantic_ = null;
        Target = byte.MaxValue;
        HasKilledKiller = false;
    }

    public override void Add(byte playerId)
    {
        VengefulRomanticId = playerId;
        VengefulRomantic_ = Utils.GetPlayerById(playerId);

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }

    public override bool IsEnable => VengefulRomanticId != byte.MaxValue;
    public override bool CanUseKillButton(PlayerControl player) => !player.Data.IsDead && !HasKilledKiller;
    public override bool CanUseImpostorVentButton(PlayerControl pc) => Romantic.VengefulCanVent.GetBool();

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = Romantic.VengefulKCD.GetFloat();
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer.PlayerId == target.PlayerId || killer.PlayerId != VengefulRomanticId) return true;

        if (target.PlayerId == Target)
        {
            HasKilledKiller = true;
            Utils.NotifyRoles(SpecifySeer: VengefulRomantic_, SpecifyTarget: target);
            return true;
        }

        killer.Suicide(PlayerState.DeathReason.Misfire);
        return false;
    }

    public static string GetTargetText(byte playerId)
    {
        var player = Utils.GetPlayerById(playerId);
        return player == null ? null : Utils.ColorString(HasKilledKiller ? Color.green : Utils.GetRoleColor(CustomRoles.VengefulRomantic), $"{(HasKilledKiller ? "✓" : "☹️")}");
    }

    public static void SendRPC()
    {
        if (!Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncVengefulRomanticTarget, SendOption.Reliable);
        writer.Write(Target);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        byte target = reader.ReadByte();
        Target = target;
    }
}

public class RuthlessRomantic : RoleBase
{
    public static List<byte> playerIdList = [];

    public override void Init()
    {
        playerIdList = [];
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }

    public override bool IsEnable => playerIdList.Count > 0;
    public override bool CanUseImpostorVentButton(PlayerControl pc) => Romantic.RuthlessCanVent.GetBool();

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = Romantic.RuthlessKCD.GetFloat();
    }
}