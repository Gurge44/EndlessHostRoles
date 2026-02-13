using System;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Modules.Extensions;
using Hazel;

namespace EHR.Roles;

internal class Tiger : RoleBase
{
    private const int Id = 643500;

    public static OptionItem Radius;
    public static OptionItem EnrageCooldown;
    public static OptionItem EnrageDuration;
    public static OptionItem KillCooldown;
    public static OptionItem CanVent;
    public static OptionItem ImpostorVision;

    public static bool On;

    private CountdownTimer CooldownTimer;
    private CountdownTimer EnrageTimer;
    private byte TigerId;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Tiger);

        Radius = new FloatOptionItem(Id + 2, "TigerRadius", new(0.5f, 10f, 0.5f), 3f, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Tiger])
            .SetValueFormat(OptionFormat.Multiplier);

        EnrageCooldown = new FloatOptionItem(Id + 3, "EnrageCooldown", new(0f, 60f, 0.5f), 15f, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Tiger])
            .SetValueFormat(OptionFormat.Seconds);

        EnrageDuration = new FloatOptionItem(Id + 4, "EnrageDuration", new(1f, 30f, 1f), 15f, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Tiger])
            .SetValueFormat(OptionFormat.Seconds);

        KillCooldown = new FloatOptionItem(Id + 5, "KillCooldown", new(0f, 60f, 0.5f), 22.5f, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Tiger])
            .SetValueFormat(OptionFormat.Seconds);

        CanVent = new BooleanOptionItem(Id + 6, "CanVent", true, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Tiger]);

        ImpostorVision = new BooleanOptionItem(Id + 7, "ImpostorVision", true, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Tiger]);
    }

    public override void Add(byte playerId)
    {
        On = true;
        EnrageTimer = null;
        CooldownTimer = null;
        TigerId = playerId;
    }

    public override void Init()
    {
        On = false;
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return CanVent.GetBool();
    }

    public override bool CanUseSabotage(PlayerControl pc)
    {
        return base.CanUseSabotage(pc) || (pc.IsAlive() && !(Options.UsePhantomBasis.GetBool() && Options.UsePhantomBasisForNKs.GetBool()));
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        opt.SetVision(ImpostorVision.GetBool());

        if (Options.UsePhantomBasis.GetBool() && Options.UsePhantomBasisForNKs.GetBool())
            AURoleOptions.PhantomCooldown = EnrageCooldown.GetFloat() + EnrageDuration.GetFloat();
    }

    public override bool OnSabotage(PlayerControl pc)
    {
        if (CooldownTimer == null)
        {
            StartEnraging(pc);
            ResetCooldown(pc);
        }

        return pc.Is(CustomRoles.Mischievous);
    }

    public override void OnPet(PlayerControl pc)
    {
        StartEnraging(pc);
    }

    public override bool OnVanish(PlayerControl pc)
    {
        if (CooldownTimer == null)
        {
            StartEnraging(pc);
            ResetCooldown(pc);
        }

        return false;
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (!shapeshifting) return true;

        if (CooldownTimer == null)
        {
            StartEnraging(shapeshifter);
            ResetCooldown(shapeshifter);
        }

        return false;
    }

    private void ResetCooldown(PlayerControl pc, bool addDuration = true)
    {
        CooldownTimer = new CountdownTimer(EnrageCooldown.GetFloat() + (addDuration ? EnrageDuration.GetFloat() : 0), () =>
        {
            CooldownTimer = null;
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }, onTick: () =>
        {
            if (pc == null || !pc.IsAlive())
            {
                CooldownTimer.Dispose();
                EnrageTimer?.Dispose();
                CooldownTimer = null;
                EnrageTimer = null;
                Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, 2);
                return;
            }
            
            if (EnrageTimer.Remaining.TotalSeconds > 5) return;
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }, onCanceled: () => CooldownTimer = null);
        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, 1, addDuration);
    }

    private void StartEnraging(PlayerControl pc)
    {
        EnrageTimer = new CountdownTimer(EnrageDuration.GetFloat(), () =>
        {
            EnrageTimer = null;
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }, onTick: () =>
        {
            if (pc == null || !pc.IsAlive())
            {
                EnrageTimer.Dispose();
                CooldownTimer?.Dispose();
                EnrageTimer = null;
                CooldownTimer = null;
                Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, 4);
                return;
            }
            
            if (EnrageTimer.Remaining.TotalSeconds > 5) return;
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }, onCanceled: () => EnrageTimer = null);
        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, 3);
    }

    public override void OnMurder(PlayerControl killer, PlayerControl target)
    {
        if (EnrageTimer == null || !FastVector2.TryGetClosestPlayerInRangeTo(killer, Radius.GetFloat(), out PlayerControl victim, x => x.PlayerId != target.PlayerId)) return;

        if (killer.RpcCheckAndMurder(victim, true))
            victim.Suicide(realKiller: killer);
    }

    public override void AfterMeetingTasks()
    {
        var pc = TigerId.GetPlayer();
        if (pc == null || !pc.IsAlive()) return;
        ResetCooldown(pc, false);
    }

    public void ReceiveRPC(MessageReader reader)
    {
        switch (reader.ReadPackedInt32())
        {
            case 1:
                CooldownTimer = new CountdownTimer(EnrageCooldown.GetFloat() + (reader.ReadBoolean() ? EnrageDuration.GetFloat() : 0), () => CooldownTimer = null, onCanceled: () => CooldownTimer = null);
                break;
            case 2:
                CooldownTimer?.Dispose();
                CooldownTimer = null;
                break;
            case 3:
                EnrageTimer = new CountdownTimer(EnrageDuration.GetFloat(), () => EnrageTimer = null, onCanceled: () => EnrageTimer = null);
                break;
            case 4:
                EnrageTimer?.Dispose();
                EnrageTimer = null;
                break;
        }
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != target.PlayerId || seer.PlayerId != TigerId || hud || meeting) return string.Empty;
        if (EnrageTimer != null) return EnrageTimer.Remaining.TotalSeconds <= 5 || seer.IsModdedClient() ? $"\u25a9 ({(int)Math.Ceiling(EnrageTimer.Remaining.TotalSeconds)}s)" : "\u25a9";
        if (CooldownTimer != null) return string.Format(Translator.GetString("CDPT"), CooldownTimer.Remaining.TotalSeconds <= 5 || seer.IsModdedClient() ? (int)Math.Ceiling(CooldownTimer.Remaining.TotalSeconds) : "> 5s");
        return string.Empty;
    }
}