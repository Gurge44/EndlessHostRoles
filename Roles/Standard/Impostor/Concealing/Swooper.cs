using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Modules.Extensions;
using Hazel;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Roles;

public class Swooper : RoleBase
{
    private const int Id = 4200;
    private static List<byte> PlayerIdList = [];

    private static OptionItem SwooperCooldown;
    private static OptionItem SwooperDuration;
    private static OptionItem SwooperVentNormallyOnCooldown;
    private static OptionItem SwooperCanVent;
    private static OptionItem SwooperLimitOpt;
    public static OptionItem SwooperAbilityUseGainWithEachKill;

    private CountdownTimer CooldownTimer;
    private CountdownTimer InvisTimer;

    private CustomRoles UsedRole;
    private float Cooldown;
    private float Duration;
    private bool CanVent;
    private bool VentNormallyOnCooldown;
    
    private byte SwooperId;

    public override bool IsEnable => PlayerIdList.Count > 0;

    private bool CanGoInvis => GameStates.IsInTask && !ExileController.Instance && !AntiBlackout.SkipTasks && InvisTimer == null && CooldownTimer == null;
    private bool IsInvis => InvisTimer != null;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Swooper);

        SwooperCooldown = new FloatOptionItem(Id + 2, "SwooperCooldown", new(1f, 60f, 1f), 20f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Swooper])
            .SetValueFormat(OptionFormat.Seconds);

        SwooperDuration = new FloatOptionItem(Id + 3, "SwooperDuration", new(1f, 30f, 1f), 10f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Swooper])
            .SetValueFormat(OptionFormat.Seconds);

        SwooperVentNormallyOnCooldown = new BooleanOptionItem(Id + 4, "SwooperVentNormallyOnCooldown", true, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Swooper]);
        
        SwooperCanVent = new BooleanOptionItem(Id + 7, "CanVent", true, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Swooper]);

        SwooperLimitOpt = new IntegerOptionItem(Id + 5, "AbilityUseLimit", new(0, 20, 1), 1, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Swooper])
            .SetValueFormat(OptionFormat.Times);

        SwooperAbilityUseGainWithEachKill = new FloatOptionItem(Id + 6, "AbilityUseGainWithEachKill", new(0f, 5f, 0.1f), 0.7f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Swooper])
            .SetValueFormat(OptionFormat.Times);
    }

    public override void Init()
    {
        PlayerIdList = [];
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        SwooperId = playerId;

        UsedRole = Main.PlayerStates[playerId].MainRole;

        switch (UsedRole)
        {
            case CustomRoles.Swooper:
                playerId.SetAbilityUseLimit(SwooperLimitOpt.GetFloat());
                Cooldown = SwooperCooldown.GetFloat();
                Duration = SwooperDuration.GetFloat();
                VentNormallyOnCooldown = SwooperVentNormallyOnCooldown.GetBool();
                CanVent = SwooperCanVent.GetBool();
                break;
            case CustomRoles.Chameleon:
                playerId.SetAbilityUseLimit(Chameleon.UseLimitOpt.GetFloat());
                Cooldown = Chameleon.ChameleonCooldown.GetFloat();
                Duration = Chameleon.ChameleonDuration.GetFloat();
                VentNormallyOnCooldown = true;
                CanVent = true;
                break;
            case CustomRoles.Wraith:
                Cooldown = Wraith.WraithCooldown.GetFloat();
                Duration = Wraith.WraithDuration.GetFloat();
                VentNormallyOnCooldown = Wraith.WraithVentNormallyOnCooldown.GetBool();
                CanVent = Wraith.WraithCanVent.GetBool();
                break;
        }

        InvisTimer = null;
        StartCooldownTimer();
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        try
        {
            if (UsedRole == CustomRoles.Chameleon)
                AURoleOptions.EngineerCooldown = Cooldown + 2f;
            else if (UsePhantomBasis.GetBool() && (UsedRole == CustomRoles.Swooper || UsePhantomBasisForNKs.GetBool()))
            {
                AURoleOptions.PhantomCooldown = Cooldown + 2f;
                AURoleOptions.PhantomDuration = 0.1f;
            }
            
            if (UsedRole == CustomRoles.Wraith)
                opt.SetVision(Wraith.ImpostorVision.GetBool());
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }

    public override void SetKillCooldown(byte id)
    {
        if (UsedRole != CustomRoles.Wraith) base.SetKillCooldown(id);
        else Main.AllPlayerKillCooldown[id] = Wraith.KillCooldown.GetFloat();
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return UsedRole != CustomRoles.Chameleon && CanVent;
    }

    public override void AfterMeetingTasks()
    {
        InvisTimer = null;
        StartCooldownTimer();
    }

    private void StartCooldownTimer()
    {
        CooldownTimer = new CountdownTimer(Cooldown, () =>
        {
            CooldownTimer = null;
            if (!AmongUsClient.Instance.AmHost) return;

            var player = SwooperId.GetPlayer();
            if (player == null || !player.IsAlive()) return;
            
            if (!player.IsModdedClient() && !(UsedRole != CustomRoles.Chameleon && UsePhantomBasis.GetBool() && (UsedRole != CustomRoles.Wraith || UsePhantomBasisForNKs.GetBool())))
                player.Notify(GetString("SwooperCanVent"), 10f);
        }, onTick: () =>
        {
            if (!AmongUsClient.Instance.AmHost) return;
            
            var player = SwooperId.GetPlayer();
            if (player == null || !player.IsAlive()) return;
            
            if (!(UsePhantomBasis.GetBool() && (UsedRole != CustomRoles.Wraith || UsePhantomBasisForNKs.GetBool())))
                player.Notify(string.Format(GetString("CDPT"), (int)Math.Ceiling(CooldownTimer.Remaining.TotalSeconds)), 3f, true);
        }, onCanceled: () => CooldownTimer = null);
        
        Utils.SendRPC(CustomRPC.SyncRoleData, SwooperId, true);
    }

    private void StartInvisTimer(PlayerControl player, int ventId = 0)
    {
        InvisTimer = new CountdownTimer(Duration, () =>
        {
            InvisTimer = null;

            if (UsedRole == CustomRoles.Chameleon && !UsePets.GetBool())
                Main.EnumeratePlayerControls().Without(player).Do(x => player.MyPhysics.RpcExitVentDesync(ventId, x));
            else
                player.RpcMakeVisible(phantom: UsedRole == CustomRoles.Swooper);

            player.Notify(GetString("SwooperInvisStateOut"));
            player.RpcResetAbilityCooldown();
        }, onTick: player.IsModdedClient() ? null : () => player.Notify(string.Format(GetString("SwooperInvisStateCountdown"), (int)InvisTimer.Remaining.TotalSeconds), overrideAll: true), onCanceled: () => InvisTimer = null);
        
        Utils.SendRPC(CustomRPC.SyncRoleData, SwooperId, false);
    }

    public void ReceiveRPC(MessageReader reader)
    {
        if (reader.ReadBoolean()) CooldownTimer = new CountdownTimer(Cooldown, () => CooldownTimer = null, onCanceled: () => CooldownTimer = null);
        else InvisTimer = new CountdownTimer(Duration, () => InvisTimer = null, onCanceled: () => InvisTimer = null);
    }

    bool OnCoEnterVent(PlayerPhysics __instance, int ventId)
    {
        if (!AmongUsClient.Instance.AmHost || IsInvis || (UsedRole == CustomRoles.Chameleon && UsePets.GetBool()) || (UsedRole != CustomRoles.Chameleon && UsePhantomBasis.GetBool() && (UsedRole != CustomRoles.Wraith || UsePhantomBasisForNKs.GetBool()))) return false;

        PlayerControl pc = __instance.myPlayer;

        float limit = pc.GetAbilityUseLimit();
        bool wraith = UsedRole == CustomRoles.Wraith;

        if (CanGoInvis && (wraith || limit >= 1))
        {
            LateTask.New(() => __instance.RpcExitVentDesync(ventId, pc), 0.5f);

            StartInvisTimer(pc, ventId);
            
            if (!wraith) pc.RpcRemoveAbilityUse();

            pc.Notify(GetString("SwooperInvisState"), Duration);
            return true;
        }

        if (!VentNormallyOnCooldown)
        {
            LateTask.New(() => __instance.RpcExitVent(ventId), 0.5f);
            pc.Notify(GetString("SwooperInvisInCooldown"));
            return true;
        }

        return false;
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        if (OnCoEnterVent(pc.MyPhysics, vent.Id)) return;

        if (!IsInvis || InvisTimer.Stopwatch.Elapsed.TotalSeconds < 1) return;

        InvisTimer.Dispose();
        InvisTimer = null;
        StartCooldownTimer();

        pc.Notify(GetString("SwooperInvisStateOut"), 10f);
        pc.RpcResetAbilityCooldown();
    }

    public override bool OnVanish(PlayerControl pc)
    {
        if (IsInvis) return false;

        float limit = pc.GetAbilityUseLimit();
        bool wraith = UsedRole == CustomRoles.Wraith;

        if (CanGoInvis && (wraith || limit >= 1))
        {
            pc.RpcMakeInvisible(phantom: UsedRole == CustomRoles.Swooper);
            
            StartInvisTimer(pc);
            
            if (!wraith) pc.RpcRemoveAbilityUse();

            pc.Notify(GetString("SwooperInvisState"), Duration);
        }

        return false;
    }

    public override void OnPet(PlayerControl pc)
    {
        OnVanish(pc);
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (!hud || seer == null || seer.PlayerId != SwooperId || !GameStates.IsInTask || ExileController.Instance || !seer.IsAlive()) return string.Empty;

        var str = new StringBuilder();

        if (IsInvis)
        {
            int remainTime = (int)InvisTimer.Remaining.TotalSeconds;
            str.Append(string.Format(GetString("SwooperInvisStateCountdown"), remainTime + 1));
        }
        else if (!(UsedRole != CustomRoles.Chameleon && UsePhantomBasis.GetBool() && (UsedRole != CustomRoles.Wraith || UsePhantomBasisForNKs.GetBool())))
        {
            if (CooldownTimer != null)
            {
                int cooldown = (int)Math.Ceiling(CooldownTimer.Remaining.TotalSeconds);
                str.Append(string.Format(GetString("SwooperInvisCooldownRemain"), cooldown + 1));
            }
            else
                str.Append(GetString("SwooperCanVent"));
        }

        return str.ToString();
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (target.Is(CustomRoles.Bait)) return true;
        if (!IsInvis) return true;
        if (!killer.RpcCheckAndMurder(target, true)) return false;

        RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
        target.Suicide(PlayerState.DeathReason.Swooped, killer);
        killer.SetKillCooldown();
        return false;
    }

    public override bool CanUseVent(PlayerControl pc, int ventId)
    {
        return !IsThisRole(pc) || UsedRole != CustomRoles.Chameleon || pc.GetClosestVent()?.Id == ventId;
    }
}
