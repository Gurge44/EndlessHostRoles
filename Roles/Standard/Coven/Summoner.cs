using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Modules.Extensions;
using Hazel;

namespace EHR.Roles;

public class Summoner : CovenBase
{
    public static bool On;
    public static List<Summoner> Instances = [];
    public static HashSet<byte> AdditionalWinners = [];
    
    private static OptionItem SummonedTimeToKill;
    private static OptionItem AbilityUseLimit;
    private static OptionItem AbilityUseGainWithEachKill;
    private static OptionItem KillCooldown;
    public static OptionItem CanVentBeforeNecronomicon;
    private static OptionItem VentCooldown;
    private static OptionItem MaxInVentTime;
    private static OptionItem CanVentAfterNecronomicon;

    protected override NecronomiconReceivePriorities NecronomiconReceivePriority => NecronomiconReceivePriorities.Random;

    public override bool IsEnable => On;

    public byte SummonerId;
    public byte SummonedPlayerId;
    private bool Changed;
    private CountdownTimer SummonedPlayerTimer;

    public override void SetupCustomOption()
    {
        StartSetup(658000)
            .AutoSetupOption(ref SummonedTimeToKill, 30f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref AbilityUseLimit, 1f, new FloatValueRule(0, 20, 0.05f), OptionFormat.Times)
            .AutoSetupOption(ref AbilityUseGainWithEachKill, 0.3f, new FloatValueRule(0f, 5f, 0.1f), OptionFormat.Times)
            .AutoSetupOption(ref KillCooldown, 30f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref CanVentBeforeNecronomicon, false)
            .AutoSetupOption(ref VentCooldown, 0f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds, overrideParent: CanVentBeforeNecronomicon)
            .AutoSetupOption(ref MaxInVentTime, 0f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds, overrideParent: CanVentBeforeNecronomicon)
            .AutoSetupOption(ref CanVentAfterNecronomicon, true);
    }

    public override void Init()
    {
        On = false;
        Instances = [];
        AdditionalWinners = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        Instances.Add(this);
        SummonerId = playerId;
        Changed = false;
        SummonedPlayerId = byte.MaxValue;
        SummonedPlayerTimer = null;
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetFloat());
    }

    public override void Remove(byte playerId)
    {
        Instances.Remove(this);
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        if (!HasNecronomicon && CanVentBeforeNecronomicon.GetBool())
        {
            AURoleOptions.EngineerCooldown = VentCooldown.GetFloat();
            AURoleOptions.EngineerInVentMaxTime = MaxInVentTime.GetFloat();
        }
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return HasNecronomicon;
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return HasNecronomicon && CanVentAfterNecronomicon.GetBool();
    }

    public override void AfterMeetingTasks()
    {
        if (SummonedPlayerId != byte.MaxValue && Main.PlayerStates.TryGetValue(SummonedPlayerId, out var state))
        {
            var summoned = SummonedPlayerId.GetPlayer();
            
            if (summoned != null && !summoned.IsAlive())
            {
                SummonedPlayerTimer = new(SummonedTimeToKill.GetFloat(), () =>
                {
                    SummonedPlayerTimer = null;
                    SummonedPlayerId = byte.MaxValue;
                    if (summoned == null || !summoned.IsAlive()) return;
                    state.SetDead();
                    summoned.RpcExileV2();
                }, onTick: () => Utils.NotifyRoles(SpecifySeer: summoned, SpecifyTarget: summoned), onCanceled: () => SummonedPlayerTimer = null);

                RPC.PlaySoundRPC(SummonedPlayerId, Sounds.SpawnSound);
                GhostRolesManager.RemoveGhostRole(SummonedPlayerId);
                state.IsDead = false;
                ExtendedPlayerControl.TempExiled.Remove(SummonedPlayerId);
                summoned.RpcSetCustomRole(CustomRoles.SerialKiller);
                summoned.RpcChangeRoleBasis(CustomRoles.SerialKiller);
                summoned.SyncGeneralOptions();
                summoned.SyncSettings();
                summoned.TP(FastVector2.TryGetClosestPlayerTo(summoned, out PlayerControl closest) ? closest : PlayerControl.LocalPlayer);
                LateTask.New(() => summoned.SetKillCooldown(10f), 0.2f);
                
                Utils.SendRPC(CustomRPC.SyncRoleData, SummonerId, 1, SummonedPlayerId);
            }
        }
        
        
        if (!HasNecronomicon || Changed) return;
        
        var summoner = SummonerId.GetPlayer();
        if (summoner == null || !summoner.IsAlive()) return;
            
        summoner.RpcChangeRoleBasis(CustomRoles.SerialKiller);
        summoner.ResetKillCooldown();
        LateTask.New(() => summoner.SetKillCooldown(), 0.2f);
        
        Changed = true;
    }

    public override void OnVoteKick(PlayerControl pc, PlayerControl target)
    {
        string command = $"/summon {target.PlayerId}";
        ChatCommands.SummonCommand(pc, command, command.Split(' '));
    }

    public override bool KnowRole(PlayerControl seer, PlayerControl target)
    {
        if (base.KnowRole(seer, target)) return true;
        return seer.PlayerId == SummonedPlayerId && target.Is(Team.Coven);
    }

    public static bool OnAnyoneCheckMurder(PlayerControl killer, PlayerControl target)
    {
        return !Instances.Exists(x => x.SummonedPlayerId == killer.PlayerId && target.Is(Team.Coven));
    }

    public static void OnAnyoneMurder(PlayerControl killer)
    {
        foreach (Summoner instance in Instances)
        {
            if (instance.SummonedPlayerId == killer.PlayerId)
            {
                AdditionalWinners.Add(instance.SummonedPlayerId);
                instance.SummonedPlayerTimer?.Dispose();
                instance.SummonedPlayerTimer = null;
                killer.RpcExileV2();
                Main.PlayerStates[instance.SummonedPlayerId].SetDead();
                instance.SummonedPlayerId = byte.MaxValue;
                Utils.SendRPC(CustomRPC.SyncRoleData, instance.SummonerId, 2);
                return;
            }
        }
    }

    public static void OnMeetingEnd()
    {
        try
        {
            if (!Instances.Exists(x => x.SummonedPlayerId != byte.MaxValue)) return;
            Utils.SendMessage(Translator.GetString("Summoner.SomeoneWillBeRevivedMessage"), title: CustomRoles.Summoner.ToColoredString(), importance: MessageImportance.High);
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }

    public void ReceiveRPC(MessageReader reader)
    {
        switch (reader.ReadPackedInt32())
        {
            case 1:
                SummonedPlayerId = reader.ReadByte();
                SummonedPlayerTimer = new(SummonedTimeToKill.GetFloat(), () => SummonedPlayerTimer = null, onCanceled: () => SummonedPlayerTimer = null);
                break;
            case 2:
                SummonedPlayerId = byte.MaxValue;
                SummonedPlayerTimer?.Dispose();
                SummonedPlayerTimer = null;
                break;
        }
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != target.PlayerId || (seer.IsModdedClient() && !hud) || meeting || SummonedPlayerId == byte.MaxValue || SummonedPlayerTimer == null) return string.Empty;
        if (seer.PlayerId == SummonerId) return string.Format(Translator.GetString("Summoner.SelfSuffix"), SummonedPlayerId.ColoredPlayerName(), (int)SummonedPlayerTimer.Remaining.TotalSeconds);
        return seer.PlayerId == SummonedPlayerId ? string.Format(Translator.GetString("Summoner.SummonedPlayerSuffix"), SummonerId.ColoredPlayerName(), CustomRoles.Summoner.ToColoredString(), (int)SummonedPlayerTimer.Remaining.TotalSeconds) : string.Empty;
    }
}