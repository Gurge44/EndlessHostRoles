using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using UnityEngine;

namespace EHR.Neutral;

public class Romantic : RoleBase
{
    private const int Id = 9850;

    public static byte RomanticId = byte.MaxValue;
    public static PlayerControl RomanticPC;
    public static byte PartnerId = byte.MaxValue;
    public static PlayerControl Partner;

    public static bool IsPartnerProtected;

    public static OptionItem BetCooldown;
    private static OptionItem ProtectCooldown;
    private static OptionItem ProtectDuration;
    private static OptionItem KnowTargetRole;
    private static OptionItem BetTargetKnowRomantic;
    private static OptionItem RomanticGetsPartnerConvertedAddons;
    private static OptionItem Arrows;
    private static OptionItem PartnerHasArrows;
    public static OptionItem VengefulKCD;
    public static OptionItem VengefulCanVent;
    public static OptionItem VengefulHasImpVision;
    public static OptionItem RuthlessKCD;
    public static OptionItem RuthlessCanVent;
    public static OptionItem RuthlessHasImpVision;

    private static readonly Dictionary<CustomRoles, CustomRoles> ConvertingRolesAndAddons = new()
    {
        [CustomRoles.Cultist] = CustomRoles.Charmed,
        [CustomRoles.Jackal] = CustomRoles.Sidekick,
        [CustomRoles.Virus] = CustomRoles.Contagious,
        [CustomRoles.Deathknight] = CustomRoles.Undead,
        [CustomRoles.Necromancer] = CustomRoles.Undead
    };

    public static bool HasPickedPartner => PartnerId != byte.MaxValue;

    public override bool IsEnable => RomanticId != byte.MaxValue;

    public override void SetupCustomOption()
    {
        Options.SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Romantic);

        BetCooldown = new FloatOptionItem(Id + 10, "RomanticBetCooldown", new(0f, 60f, 1f), 7f, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Romantic])
            .SetValueFormat(OptionFormat.Seconds);

        ProtectCooldown = new FloatOptionItem(Id + 11, "RomanticProtectCooldown", new(0f, 60f, 0.5f), 25f, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Romantic])
            .SetValueFormat(OptionFormat.Seconds);

        ProtectDuration = new FloatOptionItem(Id + 12, "RomanticProtectDuration", new(0f, 60f, 0.5f), 10f, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Romantic])
            .SetValueFormat(OptionFormat.Seconds);

        KnowTargetRole = new BooleanOptionItem(Id + 13, "RomanticKnowTargetRole", true, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Romantic]);

        BetTargetKnowRomantic = new BooleanOptionItem(Id + 14, "RomanticBetTargetKnowRomantic", true, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Romantic]);

        RomanticGetsPartnerConvertedAddons = new BooleanOptionItem(Id + 15, "RomanticGetsPartnerConvertedAddons", true, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Romantic]);

        Arrows = new BooleanOptionItem(Id + 16, "RomanticArrows", true, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Romantic]);

        PartnerHasArrows = new BooleanOptionItem(Id + 17, "RomanticPartnerHasArrows", true, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Romantic]);

        VengefulKCD = new FloatOptionItem(Id + 18, "VengefulKCD", new(0f, 60f, 0.5f), 22.5f, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Romantic])
            .SetValueFormat(OptionFormat.Seconds);

        VengefulCanVent = new BooleanOptionItem(Id + 19, "VengefulCanVent", true, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Romantic]);

        VengefulHasImpVision = new BooleanOptionItem(Id + 20, "VengefulHasImpVision", true, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Romantic]);

        RuthlessKCD = new FloatOptionItem(Id + 21, "RuthlessKCD", new(0f, 60f, 0.5f), 22.5f, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Romantic])
            .SetValueFormat(OptionFormat.Seconds);

        RuthlessCanVent = new BooleanOptionItem(Id + 22, "RuthlessCanVent", true, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Romantic]);

        RuthlessHasImpVision = new BooleanOptionItem(Id + 23, "RuthlessHasImpVision", true, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Romantic]);
    }

    public override void Init()
    {
        RomanticId = byte.MaxValue;
        RomanticPC = null;
        PartnerId = byte.MaxValue;
        Partner = null;
        IsPartnerProtected = false;
    }

    public override void Add(byte playerId)
    {
        RomanticId = playerId;
        RomanticPC = Utils.GetPlayerById(playerId);
        PartnerId = byte.MaxValue;
        Partner = null;
        IsPartnerProtected = false;
    }

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

        if (HasPickedPartner)
            Main.AllPlayerKillCooldown[RomanticId] = ProtectCooldown.GetFloat();
        else
            Main.AllPlayerKillCooldown[RomanticId] = BetCooldown.GetFloat();

        if (Math.Abs(beforeCD - Main.AllPlayerKillCooldown[RomanticId]) > 0.5f) RomanticPC?.SyncSettings();
    }

    public override bool KnowRole(PlayerControl player, PlayerControl target)
    {
        if (base.KnowRole(player, target)) return true;

        if (!KnowTargetRole.GetBool()) return false;

        return (player.Is(CustomRoles.Romantic) && PartnerId == target.PlayerId) || (BetTargetKnowRomantic.GetBool() && target.Is(CustomRoles.Romantic) && player.PlayerId == PartnerId);
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer == null || target == null || killer.PlayerId == target.PlayerId || killer.PlayerId != RomanticId) return true;

        if (!HasPickedPartner)
        {
            PartnerId = target.PlayerId;
            Partner = target;

            SendRPC();

            RomanticPC.ResetKillCooldown();
            RomanticPC.SetKillCooldown();
            RomanticPC.RPCPlayCustomSound("Bet");

            RomanticPC.Notify(Translator.GetString("RomanticBetPlayer"));
            if (BetTargetKnowRomantic.GetBool()) target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Romantic), Translator.GetString("RomanticBetOnYou")));

            if (RomanticGetsPartnerConvertedAddons.GetBool() && Partner.IsConverted()) Partner.GetCustomSubRoles().DoIf(x => x.IsConverted() && !RomanticPC.Is(x), x => RomanticPC.RpcSetCustomRole(x));

            if (Arrows.GetBool())
            {
                TargetArrow.Add(RomanticId, PartnerId);
                if (PartnerHasArrows.GetBool() && BetTargetKnowRomantic.GetBool()) TargetArrow.Add(PartnerId, RomanticId);
            }

            Logger.Info($"Partner picked: {RomanticPC.GetNameWithRole().RemoveHtmlTags()} => {target.GetNameWithRole().RemoveHtmlTags()}", "Romantic");
        }
        else if (!IsPartnerProtected)
        {
            IsPartnerProtected = true;

            RomanticPC.ResetKillCooldown();
            RomanticPC.SetKillCooldown();
            RomanticPC.RPCPlayCustomSound("Shield");

            RomanticPC.Notify(Translator.GetString("RomanticProtectPartner"));
            Partner.Notify(Translator.GetString("RomanticIsProtectingYou"));

            LateTask.New(() =>
            {
                if (!Partner.IsAlive()) return;

                IsPartnerProtected = false;

                if (!GameStates.IsInTask) return;

                RomanticPC.Notify(Translator.GetString("ProtectingOver"));
                Partner.Notify(Translator.GetString("ProtectingOver"));

                RomanticPC.SetKillCooldown();
            }, ProtectDuration.GetFloat(), "RomanticProtecting");
        }

        return false;
    }

    public override void OnGlobalFixedUpdate(PlayerControl pc, bool lowLoad)
    {
        if (!lowLoad && ((Partner != null && Partner.Data != null && !Partner.Data.Disconnected && !Partner.IsAlive()) || (Main.PlayerStates.TryGetValue(PartnerId, out var state) && state.IsDead)) && RomanticPC.IsAlive() && RomanticPC.Is(CustomRoles.Romantic))
            ChangeRole();
    }

    public static string TargetMark(PlayerControl seer, PlayerControl target)
    {
        if (!seer.Is(CustomRoles.Romantic))
        {
            if (!BetTargetKnowRomantic.GetBool()) return string.Empty;

            return target.Is(CustomRoles.Romantic) && RomanticId == target.PlayerId && PartnerId == seer.PlayerId
                ? Utils.ColorString(Utils.GetRoleColor(CustomRoles.Romantic), "♥")
                : string.Empty;
        }

        return PartnerId == target.PlayerId
            ? Utils.ColorString(Utils.GetRoleColor(CustomRoles.Romantic), "♥")
            : string.Empty;
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != target.PlayerId || hud) return string.Empty;

        if (seer.PlayerId == PartnerId && HasPickedPartner) return TargetArrow.GetArrows(seer, RomanticId);

        if (seer.PlayerId != RomanticId || !seer.Is(CustomRoles.Romantic)) return string.Empty;

        Color color = !HasPickedPartner ? Color.white : Utils.GetRoleColor(CustomRoles.Romantic);
        string text = !HasPickedPartner ? Translator.GetString("Romantic.PickPartnerText") : "♥";
        if (Arrows.GetBool()) text += TargetArrow.GetArrows(seer, PartnerId);

        return Utils.ColorString(color, text);
    }

    public override void OnReportDeadBody()
    {
        IsPartnerProtected = false;
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return false;
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return pc.IsAlive();
    }

    private static void ChangeRole()
    {
        if (RomanticPC == null || !RomanticPC.Is(CustomRoles.Romantic)) return;

        if (Partner == null)
        {
            Logger.Info("Romantic Partner is null => Changing to Ruthless Romantic", "Romantic");
            RomanticPC.RpcSetCustomRole(CustomRoles.RuthlessRomantic);
            RomanticPC.SetKillCooldown();
            return;
        }

        CustomRoles partnerRole = Partner.GetCustomRole();
        PlayerControl killer = Partner.GetRealKiller();

        if (killer?.PlayerId == RomanticId) // If the Romantic killed their Partner, they also die
        {
            if (!RomanticPC.IsAlive()) return;

            Logger.Info("Romantic killed their own Partner, Romantic suicides", "Romantic");
            RomanticPC.Suicide(PlayerState.DeathReason.FollowingSuicide, Partner);
            return;
        }

        if ((partnerRole.IsNonNK() && partnerRole is not CustomRoles.Romantic and not CustomRoles.VengefulRomantic and not CustomRoles.RuthlessRomantic) || killer == null || !killer.IsAlive() || Main.PlayerStates[PartnerId].IsSuicide) // If Partner is NNK or died by themselves, Romantic becomes Ruthless Romantic
        {
            Logger.Info($"NNK Romantic Partner Died ({partnerRole.IsNonNK()}) / Partner killer is null ({killer == null}) / Partner killer is dead ({!killer.IsAlive()}) / Partner commited Suicide ({Main.PlayerStates[PartnerId].IsSuicide}) => Changing {RomanticPC.GetNameWithRole().RemoveHtmlTags()} to Ruthless Romantic", "Romantic");
            RomanticPC.RpcSetCustomRole(CustomRoles.RuthlessRomantic);
        }
        else if (ConvertingRolesAndAddons.TryGetValue(partnerRole, out CustomRoles convertedRole))
        {
            RomanticPC.RpcSetCustomRole(convertedRole);
            if (convertedRole.IsAdditionRole()) RomanticPC.RpcSetCustomRole(CustomRoles.RuthlessRomantic);

            Logger.Info($"Converting Romantic Partner Died ({Partner.GetNameWithRole()}) => Romantic becomes their ally ({RomanticPC.GetNameWithRole()})", "Romantic");
        }
        else if (Partner.Is(Team.Impostor)) // If Partner is Imp, Romantic joins the imp team as Renegade
        {
            Logger.Info($"Impostor Romantic Partner Died => Changing {RomanticPC.GetNameWithRole()} to Renegade", "Romantic");
            RomanticPC.RpcSetCustomRole(CustomRoles.Renegade);
        }
        else if (Partner.HasKillButton() || partnerRole.IsNK() || partnerRole.GetDYRole() is RoleTypes.Impostor or RoleTypes.Shapeshifter or RoleTypes.Phantom) // If the Partner has a kill button (NK or CK), Romantic becomes the role they were
        {
            try
            {
                RomanticPC.RpcSetCustomRole(partnerRole);
                RomanticPC.RpcChangeRoleBasis(partnerRole);
                Main.PlayerStates[RomanticId].RemoveSubRole(CustomRoles.NotAssigned);
                Logger.Info($"Romantic Partner With Kill Button Died => Changing {RomanticPC.GetNameWithRole()} to {Partner.GetAllRoleName().RemoveHtmlTags()}", "Romantic");
            }
            catch
            {
                Logger.Error($"Romantic Partner With Kill Button Died => Changing {RomanticPC.GetNameWithRole()} to {Partner.GetAllRoleName().RemoveHtmlTags()} : FAILED ----> changing to Ruthless Romantic instead", "Romantic");
                RomanticPC.RpcSetCustomRole(CustomRoles.RuthlessRomantic);
            }
        }
        else // In every other scenario, Romantic becomes Vengeful Romantic and must kill the killer of their Partner
        {
            Logger.Info($"Non-Killing Crew Romantic Partner Died => Changing {RomanticPC.GetNameWithRole()} to Vengeful Romantic", "Romantic");

            RomanticPC.RpcSetCustomRole(CustomRoles.VengefulRomantic);
            VengefulRomantic.Target = killer.PlayerId;
            VengefulRomantic.SendRPC();
        }

        if (RomanticGetsPartnerConvertedAddons.GetBool() && Partner.IsConverted()) Partner.GetCustomSubRoles().DoIf(x => x.IsConverted() && !RomanticPC.Is(x), x => RomanticPC.RpcSetCustomRole(x));

        RomanticPC.SetKillCooldown();
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        hud.KillButton?.OverrideText(!HasPickedPartner ? Translator.GetString("RomanticKillButtonText") : Translator.GetString("MedicalerButtonText"));
    }
}

public class VengefulRomantic : RoleBase
{
    private static byte VengefulRomanticId = byte.MaxValue;

    public static bool HasKilledKiller;
    public static byte Target = byte.MaxValue;

    public override bool IsEnable => VengefulRomanticId != byte.MaxValue;

    public override void SetupCustomOption() { }

    public override void Init()
    {
        VengefulRomanticId = byte.MaxValue;
        Target = byte.MaxValue;
        HasKilledKiller = false;
    }

    public override void Add(byte playerId)
    {
        VengefulRomanticId = playerId;
        Utils.GetPlayerById(playerId);
    }

    public override bool CanUseKillButton(PlayerControl player)
    {
        return player.IsAlive() && !HasKilledKiller;
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return Romantic.VengefulCanVent.GetBool();
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = Romantic.VengefulKCD.GetFloat();
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer.PlayerId == target.PlayerId || killer.PlayerId != VengefulRomanticId || target.PlayerId == Target) return true;

        killer.Suicide(PlayerState.DeathReason.Misfire);
        return false;
    }

    public override void OnMurder(PlayerControl killer, PlayerControl target)
    {
        if (target.PlayerId == Target) HasKilledKiller = true;
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != target.PlayerId || seer.PlayerId != VengefulRomanticId) return string.Empty;

        return seer == null ? string.Empty : Utils.ColorString(HasKilledKiller ? Color.green : Utils.GetRoleColor(CustomRoles.VengefulRomantic), $"{(HasKilledKiller ? "✓" : "☹")}");
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

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        hud.KillButton?.OverrideText(Translator.GetString("VengefulRomanticKillButtonText"));
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        opt.SetVision(Romantic.VengefulHasImpVision.GetBool());
    }
}

public class RuthlessRomantic : RoleBase
{
    public static List<byte> PlayerIdList = [];

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption() { }

    public override void Init()
    {
        PlayerIdList = [];
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return Romantic.RuthlessCanVent.GetBool();
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = Romantic.RuthlessKCD.GetFloat();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        opt.SetVision(Romantic.RuthlessHasImpVision.GetBool());
    }
}