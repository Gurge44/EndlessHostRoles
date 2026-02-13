using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules.Extensions;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Roles;

public class Werewolf : RoleBase
{
    private const int Id = 12850;
    private static List<byte> PlayerIdList = [];

    private static OptionItem KillCooldown;
    private static OptionItem HasImpostorVision;
    private static OptionItem RampageCD;
    private static OptionItem RampageDur;
    private static OptionItem RampageCDAfterNoKillRampage;
    private static OptionItem ResetToNormalCooldownAfterMeetings;

    private CountdownTimer CooldownTimer;
    private CountdownTimer RampageTimer;
    private int KillsInLastRampage;
    private byte WWId;

    private float UsedCooldown => KillsInLastRampage == 0 ? RampageCDAfterNoKillRampage.GetFloat() : RampageCD.GetFloat();

    public override bool IsEnable => PlayerIdList.Count > 0;

    private bool CanRampage => GameStates.IsInTask && RampageTimer == null && CooldownTimer == null;
    private bool IsRampaging => RampageTimer != null;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Werewolf);

        KillCooldown = new FloatOptionItem(Id + 10, "KillCooldown", new(0f, 180f, 0.5f), 3f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Werewolf])
            .SetValueFormat(OptionFormat.Seconds);

        HasImpostorVision = new BooleanOptionItem(Id + 11, "ImpostorVision", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Werewolf]);

        RampageCD = new FloatOptionItem(Id + 12, "WWRampageCD", new(0f, 180f, 0.5f), 35f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Werewolf])
            .SetValueFormat(OptionFormat.Seconds);

        RampageDur = new FloatOptionItem(Id + 13, "WWRampageDur", new(0.5f, 180f, 0.5f), 12f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Werewolf])
            .SetValueFormat(OptionFormat.Seconds);

        RampageCDAfterNoKillRampage = new FloatOptionItem(Id + 14, "WWRampageCDAfterNoKillRampage", new(0f, 180f, 0.5f), 15f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Werewolf])
            .SetValueFormat(OptionFormat.Seconds);

        ResetToNormalCooldownAfterMeetings = new BooleanOptionItem(Id + 15, "ResetToNormalCooldownAfterMeetings", false, TabGroup.NeutralRoles)
            .SetParent(RampageCDAfterNoKillRampage);
    }

    public override void Init()
    {
        PlayerIdList = [];
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        WWId = playerId;

        RampageTimer = null;
        KillsInLastRampage = -10;

        if (!AmongUsClient.Instance.AmHost) return;
        
        StartCooldownTimer(playerId.GetPlayer(), 8);
        
        LateTask.New(() =>
        {
            if (UsePhantomBasis.GetBool() && UsePhantomBasisForNKs.GetBool())
                Utils.GetPlayerById(playerId)?.RpcResetAbilityCooldown();
        }, 12f, log: false);
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    void StartCooldownTimer(PlayerControl pc, int add = 0)
    {
        CooldownTimer = new CountdownTimer(UsedCooldown + add, () =>
        {
            CooldownTimer = null;
            bool otherTrigger = UsePhantomBasis.GetBool() && UsePhantomBasisForNKs.GetBool();

            if (!pc.IsModdedClient())
            {
                pc.Notify(GetString(otherTrigger ? "WWCanRampageVanish" : "WWCanRampage"));
                pc.RpcChangeRoleBasis(otherTrigger ? CustomRoles.Werewolf : CustomRoles.EngineerEHR);
            }
        }, onTick: () => pc.Notify(string.Format(GetString("CDPT"), (int)Math.Ceiling(CooldownTimer.Remaining.TotalSeconds)), 3f, true), onCanceled: () => CooldownTimer = null);
    }

    void StartRampageTimer(PlayerControl pc)
    {
        RampageTimer = new CountdownTimer(RampageDur.GetFloat(), () =>
        {
            RampageTimer = null;
            StartCooldownTimer(pc);
            pc.Notify(GetString("WWRampageOut"));
            if (!pc.IsModdedClient()) pc.RpcChangeRoleBasis(CustomRoles.CrewmateEHR);
        }, onTick: pc.IsModdedClient() ? null : () => pc.Notify(string.Format(GetString("WWRampageCountdown"), (int)RampageTimer.Remaining.TotalSeconds), overrideAll: true), onCanceled: () => RampageTimer = null);
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return (CanRampage && (!UsePhantomBasis.GetBool() || !UsePhantomBasisForNKs.GetBool())) || IsRampaging || pc.inVent;
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return IsRampaging;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        opt.SetVision(HasImpostorVision.GetBool());

        if (UsePhantomBasis.GetBool() && UsePhantomBasisForNKs.GetBool())
            AURoleOptions.PhantomCooldown = 1f;

        AURoleOptions.EngineerCooldown = 0f;
        AURoleOptions.EngineerInVentMaxTime = 0f;
    }

    public override void AfterMeetingTasks()
    {
        RampageTimer?.Dispose();
        RampageTimer = null;
        CooldownTimer?.Dispose();
        var pc = WWId.GetPlayer();
        if (pc == null || !pc.IsAlive()) return;
        StartCooldownTimer(pc);
    }

    public override void OnExitVent(PlayerControl pc, Vent vent)
    {
        Rampage(pc);
    }

    public override void OnPet(PlayerControl pc)
    {
        Rampage(pc);
    }

    public override bool OnVanish(PlayerControl pc)
    {
        Rampage(pc);
        return false;
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (!shapeshifting) return true;

        Rampage(shapeshifter);
        return false;
    }

    private void Rampage(PlayerControl pc)
    {
        if (!AmongUsClient.Instance.AmHost || IsRampaging) return;

        LateTask.New(() =>
        {
            if (CanRampage)
            {
                KillsInLastRampage = 0;
                StartRampageTimer(pc);
                pc.Notify(GetString("WWRampaging"), RampageDur.GetFloat());
                if (!pc.IsModdedClient()) pc.RpcChangeRoleBasis(CustomRoles.Werewolf);
            }
        }, 0.5f, "Werewolf Vent");
    }

    public override void OnMurder(PlayerControl killer, PlayerControl target)
    {
        KillsInLastRampage++;
    }

    public override void OnReportDeadBody()
    {
        if (ResetToNormalCooldownAfterMeetings.GetBool())
            KillsInLastRampage = -10;
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (!hud || meeting || seer.PlayerId != WWId) return string.Empty;

        var str = new StringBuilder();

        if (IsRampaging)
        {
            int remainTime = (int)Math.Ceiling(RampageTimer.Remaining.TotalSeconds);
            str.Append(string.Format(GetString("WWRampageCountdown"), remainTime));
        }
        else if (CooldownTimer != null)
        {
            int cooldown = (int)Math.Ceiling(CooldownTimer.Remaining.TotalSeconds);
            str.Append(string.Format(GetString("WWCD"), cooldown));
        }
        else
            str.Append(GetString(UsePhantomBasis.GetBool() && UsePhantomBasisForNKs.GetBool() ? "WWCanRampageVanish" : "WWCanRampage"));

        return str.ToString();
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (Medic.ProtectList.Contains(target.PlayerId) || !IsRampaging) return false;

        if (killer.RpcCheckAndMurder(target, true))
            Main.PlayerStates[target.PlayerId].deathReason = PlayerState.DeathReason.Mauled;

        return true;
    }
}
