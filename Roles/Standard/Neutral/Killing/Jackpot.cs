using System.Collections.Generic;
using AmongUs.GameOptions;
using UnityEngine;

namespace EHR.Roles;

using static Translator;

public class Jackpot : RoleBase
{
    private const int Id = 658620;

    public static bool On;
    private static List<byte> PlayerIdList = [];

    private static OptionItem KillCooldown;
    private static OptionItem StartingMoney;
    private static OptionItem MoneyPerKill;
    private static OptionItem MissCost;
    private static OptionItem SpinInterval;
    private static OptionItem BaseJackpotChance;
    private static OptionItem MissChanceIncrease;
    private static OptionItem NearMissChance;
    private static OptionItem NearMissChanceIncrease;
    private static OptionItem NearMissDuration;
    private static OptionItem NearMissSpeed;
    private static OptionItem NearMissVision;
    private static OptionItem JackpotDuration;
    private static OptionItem JackpotSpeed;
    private static OptionItem JackpotVision;

    private byte JackpotId;
    private int Money;
    private int CurrentJackpotChanceValue;
    private float SpinTimer;
    private float NearMissTimer;
    private float JackpotTimer;
    private bool DomainActive;
    private bool LastNearMissState;
    private bool LastJackpotState;

    public override bool IsEnable => On;

    private bool IsNearMissActive => NearMissTimer > 0f;
    private bool IsJackpotActive => JackpotTimer > 0f;

    public override void SetupCustomOption()
    {
        StartSetup(Id)
            .AutoSetupOption(ref KillCooldown, 22.5f, new FloatValueRule(0f, 180f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref StartingMoney, 4, new IntegerValueRule(1, 30, 1), overrideName: "Jackpot.StartingMoney")
            .AutoSetupOption(ref MoneyPerKill, 2, new IntegerValueRule(0, 10, 1), overrideName: "Jackpot.MoneyPerKill")
            .AutoSetupOption(ref MissCost, 1, new IntegerValueRule(1, 10, 1), overrideName: "Jackpot.MissCost")
            .AutoSetupOption(ref SpinInterval, 5f, new FloatValueRule(1f, 30f, 0.5f), OptionFormat.Seconds, overrideName: "Jackpot.SpinInterval")
            .AutoSetupOption(ref BaseJackpotChance, 5, new IntegerValueRule(1, 100, 1), OptionFormat.Percent, overrideName: "Jackpot.BaseChance")
            .AutoSetupOption(ref MissChanceIncrease, 2, new IntegerValueRule(1, 25, 1), OptionFormat.Percent, overrideName: "Jackpot.MissChanceIncrease")
            .AutoSetupOption(ref NearMissChance, 15, new IntegerValueRule(0, 100, 1), OptionFormat.Percent, overrideName: "Jackpot.NearChance")
            .AutoSetupOption(ref NearMissChanceIncrease, 5, new IntegerValueRule(1, 50, 1), OptionFormat.Percent, overrideName: "Jackpot.NearChanceIncrease")
            .AutoSetupOption(ref NearMissDuration, 4f, new FloatValueRule(0.5f, 20f, 0.5f), OptionFormat.Seconds, overrideName: "Jackpot.NearDuration")
            .AutoSetupOption(ref NearMissSpeed, 1.35f, new FloatValueRule(0.25f, 5f, 0.05f), OptionFormat.Multiplier, overrideName: "Jackpot.NearSpeed")
            .AutoSetupOption(ref NearMissVision, 1.25f, new FloatValueRule(0.1f, 5f, 0.05f), OptionFormat.Multiplier, overrideName: "Jackpot.NearVision")
            .AutoSetupOption(ref JackpotDuration, 15f, new FloatValueRule(10f, 20f, 0.5f), OptionFormat.Seconds, overrideName: "Jackpot.Duration")
            .AutoSetupOption(ref JackpotSpeed, 1.8f, new FloatValueRule(0.25f, 5f, 0.05f), OptionFormat.Multiplier, overrideName: "Jackpot.Speed")
            .AutoSetupOption(ref JackpotVision, 2.5f, new FloatValueRule(0.1f, 5f, 0.05f), OptionFormat.Multiplier, overrideName: "Jackpot.Vision");
    }

    public override void Init()
    {
        On = false;
        PlayerIdList = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        PlayerIdList.Add(playerId);
        JackpotId = playerId;
        Money = StartingMoney.GetInt();
        CurrentJackpotChanceValue = BaseJackpotChance.GetInt();
        SpinTimer = 0f;
        NearMissTimer = 0f;
        JackpotTimer = 0f;
        DomainActive = false;
        LastNearMissState = false;
        LastJackpotState = false;
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
        On = PlayerIdList.Count > 0;
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = IsJackpotActive ? 0.1f : KillCooldown.GetFloat();
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return false;
    }

    public override bool CanUseSabotage(PlayerControl pc)
    {
        return false;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        AURoleOptions.ShapeshifterCooldown = 1f;
        AURoleOptions.ShapeshifterDuration = 1f;

        if (!IsNearMissActive && !IsJackpotActive) return;

        opt.SetVision(false);
        float vision = IsJackpotActive ? JackpotVision.GetFloat() : NearMissVision.GetFloat();
        opt.SetFloat(FloatOptionNames.CrewLightMod, vision);
        opt.SetFloat(FloatOptionNames.ImpostorLightMod, vision);
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        hud.AbilityButton?.OverrideText(GetString("JackpotButtonText"));
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (!shapeshifting) return false;

        StartDomain(shapeshifter);
        return false;
    }

    public override void OnMurder(PlayerControl killer, PlayerControl target)
    {
        Money += MoneyPerKill.GetInt();
        killer.Notify(string.Format(GetString("Jackpot.KillReward"), MoneyPerKill.GetInt(), Money));
    }

    public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
    {
        return !IsJackpotActive;
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!GameStates.IsInTask || !pc.IsAlive())
        {
            if (DomainActive || IsNearMissActive || IsJackpotActive)
                StopSequence(pc, resetChance: true, notifyEnd: false, refreshCooldown: false);

            return;
        }

        if (IsNearMissActive)
            NearMissTimer = Mathf.Max(0f, NearMissTimer - Time.fixedDeltaTime);

        if (IsJackpotActive)
            JackpotTimer = Mathf.Max(0f, JackpotTimer - Time.fixedDeltaTime);

        if (LastJackpotState && !IsJackpotActive)
        {
            StopSequence(pc, resetChance: true, notifyEnd: true, refreshCooldown: true);
            return;
        }

        if (DomainActive && !IsJackpotActive)
        {
            SpinTimer -= Time.fixedDeltaTime;

            while (SpinTimer <= 0f && DomainActive && !IsJackpotActive)
            {
                SpinTimer += SpinInterval.GetFloat();
                ResolveSpin(pc);
            }
        }

        RefreshTemporaryBuffs(pc, refreshCooldown: false);
    }

    public override void OnReportDeadBody()
    {
        PlayerControl jackpot = JackpotId.GetPlayer();
        StopSequence(jackpot, resetChance: true, notifyEnd: false, refreshCooldown: false);
    }

    public override void AfterMeetingTasks()
    {
        PlayerControl jackpot = JackpotId.GetPlayer();
        StopSequence(jackpot, resetChance: true, notifyEnd: false, refreshCooldown: true);
    }

    public override void GetProgressText(byte playerId, bool comms, StringBuilder resultText)
    {
        resultText.Append(' ').Append(Utils.ColorPrefix(Utils.GetRoleColor(CustomRoles.Jackpot)));

        if (IsJackpotActive) resultText.AppendFormat(GetString("Jackpot.Progress.Jackpot"), Money, Mathf.CeilToInt(JackpotTimer));
        else if (DomainActive) resultText.AppendFormat(GetString(IsNearMissActive ? "Jackpot.Progress.Near" : "Jackpot.Progress.Spin"), Money, CurrentJackpotChanceValue);
        else resultText.AppendFormat(GetString("Jackpot.Progress.Ready"), Money, CurrentJackpotChanceValue);

        resultText.Append("</color>");
    }

    private void StartDomain(PlayerControl player)
    {
        if (!player || player.PlayerId != JackpotId || !player.IsAlive()) return;

        if (DomainActive || IsJackpotActive)
        {
            player.Notify(GetString("Jackpot.DomainAlreadyActive"));
            player.RpcResetAbilityCooldown();
            return;
        }

        DomainActive = true;
        SpinTimer = SpinInterval.GetFloat();
        player.Notify($"{GetString("Jackpot.GameRulesTransmitted")}\n<size=80%>{string.Format(GetString("Jackpot.DomainStarted"), SpinInterval.GetFloat())}</size>");
        RefreshTemporaryBuffs(player, refreshCooldown: false);
    }

    // Domain phase: rolls keep happening until the player jackpots, dies, or a meeting interrupts the sequence.
    private void ResolveSpin(PlayerControl player)
    {
        if (!player || !player.IsAlive()) return;

        int roll = IRandom.Instance.Next(100);

        if (roll < CurrentJackpotChanceValue)
        {
            DomainActive = false;
            NearMissTimer = 0f;
            JackpotTimer = JackpotDuration.GetFloat();
            CurrentJackpotChanceValue = BaseJackpotChance.GetInt();

            player.Notify($"<size=120%>{GetString("Jackpot.Spin.Jackpot")}</size>\n<size=80%>{string.Format(GetString("Jackpot.JackpotState"), JackpotDuration.GetFloat())}</size>", JackpotDuration.GetFloat());
            RefreshTemporaryBuffs(player, refreshCooldown: true);
            return;
        }

        if (roll < CurrentJackpotChanceValue + NearMissChance.GetInt())
        {
            NearMissTimer = NearMissDuration.GetFloat();
            CurrentJackpotChanceValue = Mathf.Clamp(CurrentJackpotChanceValue + NearMissChanceIncrease.GetInt(), 0, 100);
            player.Notify(string.Format(GetString("Jackpot.Spin.Near"), NearMissDuration.GetFloat(), Money, CurrentJackpotChanceValue), NearMissDuration.GetFloat());
            RefreshTemporaryBuffs(player, refreshCooldown: false);
            return;
        }

        Money -= MissCost.GetInt();
        CurrentJackpotChanceValue = Mathf.Clamp(CurrentJackpotChanceValue + MissChanceIncrease.GetInt(), 0, 100);

        if (Money <= 0)
        {
            Money = 0;
            StopSequence(player, resetChance: true, notifyEnd: false, refreshCooldown: false);
            player.Notify(GetString("Jackpot.OutOfMoney"));
            player.Suicide(PlayerState.DeathReason.Bankrupt);
            return;
        }

        player.Notify(string.Format(GetString("Jackpot.Spin.Miss"), MissCost.GetInt(), Money, CurrentJackpotChanceValue));
    }

    // Near Miss and Jackpot both use the same buff refresh path so we only touch speed/settings when the phase changes.
    private void RefreshTemporaryBuffs(PlayerControl player, bool refreshCooldown)
    {
        bool nearMissActive = IsNearMissActive;
        bool jackpotActive = IsJackpotActive;

        if (LastNearMissState == nearMissActive && LastJackpotState == jackpotActive && !refreshCooldown) return;

        LastNearMissState = nearMissActive;
        LastJackpotState = jackpotActive;

        float speed = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);

        if (jackpotActive) speed = JackpotSpeed.GetFloat();
        else if (nearMissActive) speed = NearMissSpeed.GetFloat();

        Main.AllPlayerSpeed[JackpotId] = speed;

        if (!player || !player.IsAlive()) return;

        if (refreshCooldown) player.ResetKillCooldown();
        else player.MarkDirtySettings();
    }

    // Jackpot is the payoff window: once it ends, the chance resets and the button is ready again immediately.
    private void StopSequence(PlayerControl player, bool resetChance, bool notifyEnd, bool refreshCooldown)
    {
        bool wasJackpotActive = IsJackpotActive;

        DomainActive = false;
        SpinTimer = 0f;
        NearMissTimer = 0f;
        JackpotTimer = 0f;

        if (resetChance)
            CurrentJackpotChanceValue = BaseJackpotChance.GetInt();

        RefreshTemporaryBuffs(player, refreshCooldown);

        if (notifyEnd && wasJackpotActive && player && player.IsAlive())
            player.Notify(GetString("Jackpot.JackpotEnded"));
    }
}
