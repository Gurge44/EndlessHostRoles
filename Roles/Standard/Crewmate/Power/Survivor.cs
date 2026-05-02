using System;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Modules.Extensions;
using Hazel;
using UnityEngine;

namespace EHR.Roles;

using static Options;
using static Translator;
using static Utils;

public class Survivor : RoleBase
{
    private const int Id = 7680;
    public static bool On;

    public static OptionItem FirstAbility;
    public static OptionItem SecondAbility;
    public static OptionItem ThirdAbility;
    public static OptionItem LastAbility;
    public static OptionItem ShieldCooldown;
    public static OptionItem ShieldDuration;
    public static OptionItem AdditionalVote;
    public static OptionItem KillCooldown;

    private CountdownTimer ShieldTimer;
    private byte SurvivorId;
    private bool Killing;
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Survivor);

        FirstAbility = new IntegerOptionItem(Id + 11, "SurvivorFirstAbility", new(0, 70, 1), 10, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Survivor]);

        SecondAbility = new IntegerOptionItem(Id + 12, "SurvivorSecondAbility", new(0, 70, 1), 8, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Survivor]);

        ThirdAbility = new IntegerOptionItem(Id + 13, "SurvivorThirdAbility", new(0, 70, 1), 6, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Survivor]);

        LastAbility = new IntegerOptionItem(Id + 14, "SurvivorLastAbility", new(0, 70, 1), 4, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Survivor]);

        ShieldCooldown = new FloatOptionItem(Id + 15, "SurvivorShieldCooldown", new(0f, 70f, 1f), 15f, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Survivor])
            .SetValueFormat(OptionFormat.Seconds);

        ShieldDuration = new FloatOptionItem(Id + 16, "SurvivorShieldDuration", new(5f, 70f, 1f), 20f, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Survivor])
            .SetValueFormat(OptionFormat.Seconds);

        AdditionalVote = new IntegerOptionItem(Id + 17, "SurvivorAdditionalVote", new(0, 90, 1), 2, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Survivor]);

        KillCooldown = new FloatOptionItem(Id + 18, "SurvivorKillCooldown", new(5f, 70f, 1f), 20f, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Survivor])
            .SetValueFormat(OptionFormat.Seconds);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        ShieldTimer = null;
        SurvivorId = playerId;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        AURoleOptions.PhantomCooldown = ShieldCooldown.GetFloat();
        AURoleOptions.EngineerCooldown = ShieldCooldown.GetFloat();
        AURoleOptions.EngineerInVentMaxTime = 1f;
    }

    public void ReceiveRPC(MessageReader reader)
    {
        Killing = reader.ReadBoolean();
    }

    // First ability: Players alive count
    public override string GetProgressText(byte playerId, bool comms)
    {
        var sb = new StringBuilder();
        int CPA = Main.EnumerateAlivePlayerControls().Count();

        sb.Append(GetTaskCount(playerId, comms));

        if (CPA < FirstAbility.GetInt()) sb.Append(ColorString(Color.yellow, $" {GetString("SurvivorCurrentAlive")} {CPA}"));

        return sb.ToString();
    }

    // Second ability: Shield
    public override void AfterMeetingTasks()
    {
        ShieldTimer?.Dispose();
        ShieldTimer = null;
    }

    public override void SetButtonTexts(HudManager hud, byte id) { hud.AbilityButton?.OverrideText(GetString("AbilityButtonText.GuardianAngel")); }

    public override bool OnVanish(PlayerControl pc) 
    {   
        ShieldSelf(pc);
        return false;
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent) { if (!Killing) ShieldSelf(pc); }

    private void ShieldSelf(PlayerControl pc)
    {
        ShieldTimer?.Dispose();
        if (Main.EnumerateAlivePlayerControls().Count() <= SecondAbility.GetInt())
        {
            bool shielded = ShieldTimer != null;
            ShieldTimer = new CountdownTimer(ShieldDuration.GetFloat(), () =>
            {
                ShieldTimer = null;
                Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
            }, onCanceled: () => ShieldTimer = null);
            if (!shielded) Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }
    }

    public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
    {
        return ShieldTimer == null;
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != target.PlayerId || seer.PlayerId != SurvivorId || meeting || (seer.IsModdedClient() && !hud) || ShieldTimer == null) return string.Empty;
        return seer.IsHost() ? string.Format(Translator.GetString("SafeguardSuffixTimer"), (int)Math.Ceiling(ShieldTimer.Remaining.TotalSeconds)) : Translator.GetString("SafeguardSuffix");
    }

    public override bool CanUseVent(PlayerControl pc, int ventId) { return !IsThisRole(pc) || pc.GetClosestVent()?.Id == ventId || pc.Is(CustomRoles.Nimble); }

    public override bool CanUseImpostorVentButton(PlayerControl pc) { return pc.Is(CustomRoles.Nimble); }

    // Third ability: Votes
    public override void ManipulateGameEndCheckCrew(PlayerState playerState, out bool keepGameGoing, out int countsAs)
    {
        if (playerState.IsDead)
        {
            base.ManipulateGameEndCheckCrew(playerState, out keepGameGoing, out countsAs);
            return;
        }

        keepGameGoing = true;
        countsAs = 1;
    }

    // Last ability: Kill
    public override void OnTaskComplete(PlayerControl pc, int completedTaskCount, int totalTaskCount)
    {
        Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, KillCooldown.GetFloat(), Killing);
        
        if (completedTaskCount + 1 >= totalTaskCount)
            ChangeBasisToKill(pc);
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!IsThisRole(pc)) return;
        if (pc.GetTaskState().IsTaskFinished && !Killing) ChangeBasisToKill(pc);
    }

    private void ChangeBasisToKill(PlayerControl pc)
    {
        if (Main.EnumerateAlivePlayerControls().Count() > LastAbility.GetInt() && Killing) return;
        Killing = true;
        pc.RpcChangeRoleBasis(CustomRoles.PhantomEHR);
        LateTask.New(() => pc.SetKillCooldown(KillCooldown.GetFloat()), 0.2f);
        Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, KillCooldown.GetFloat(), Killing);
        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        pc.MarkDirtySettings();
    }

    public override bool CanUseKillButton(PlayerControl pc) { return Killing; }

    public override void SetKillCooldown(byte id) { Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat(); }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target) { return Main.EnumerateAlivePlayerControls().Count() <= LastAbility.GetInt(); }

    public override bool CanUseSabotage(PlayerControl pc) { return false; }
}
