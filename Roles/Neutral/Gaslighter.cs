using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Crewmate;
using EHR.Patches;

namespace EHR.Neutral;

public class Gaslighter : RoleBase
{
    public static bool On;
    private static List<Gaslighter> Instances = [];

    private static OptionItem KillCooldown;
    public static OptionItem WinCondition;
    private static OptionItem CycleRepeats;

    private static readonly string[] WinConditionOptions =
    [
        "GaslighterWinCondition.CrewLoses",
        "GaslighterWinCondition.IfAlive",
        "GaslighterWinCondition.LastStanding"
    ];

    private Round CurrentRound;
    private HashSet<byte> CursedPlayers;
    private bool CycleFinished;

    private byte GaslighterId;
    private HashSet<byte> ShieldedPlayers;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(648350)
            .AutoSetupOption(ref KillCooldown, 22.5f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref WinCondition, 0, WinConditionOptions)
            .AutoSetupOption(ref CycleRepeats, false);
    }

    public override void Init()
    {
        On = false;
        Instances = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        Instances.Add(this);
        GaslighterId = playerId;
        CurrentRound = default;
        CursedPlayers = [];
        ShieldedPlayers = [];
        CycleFinished = false;
    }

    public override void Remove(byte playerId)
    {
        Instances.Remove(this);
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return pc.IsAlive();
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = CurrentRound switch
        {
            Round.Kill => KillCooldown.GetFloat(),
            Round.Knight => Monarch.KnightCooldown.GetFloat(),
            Round.Curse => Main.RealOptionsData.GetFloat(FloatOptionNames.KillCooldown),
            Round.Shield => Medic.CD.GetFloat(),
            _ => Options.DefaultKillCooldown
        };
    }

    public static void OnExile(byte[] exileIds)
    {
        try
        {
            foreach (Gaslighter instance in Instances)
            {
                foreach (byte id in exileIds)
                    if (id == instance.GaslighterId)
                        instance.CursedPlayers.Clear();
            }

            List<byte> curseDeathList = [];

            foreach (PlayerControl pc in Main.AllAlivePlayerControls)
            {
                foreach (Gaslighter instance in Instances)
                {
                    if (Main.AfterMeetingDeathPlayers.ContainsKey(pc.PlayerId)) continue;

                    PlayerControl gaslighter = instance.GaslighterId.GetPlayer();

                    if (instance.CursedPlayers.Contains(pc.PlayerId) && gaslighter != null && gaslighter.IsAlive())
                    {
                        pc.SetRealKiller(gaslighter);
                        curseDeathList.Add(pc.PlayerId);
                    }
                }
            }

            CheckForEndVotingPatch.TryAddAfterMeetingDeathPlayers(PlayerState.DeathReason.Spell, [.. curseDeathList]);
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }

    public override void AfterMeetingTasks()
    {
        ShieldedPlayers.Clear();
        CursedPlayers.Clear();

        if (Main.PlayerStates[GaslighterId].IsDead) return;

        if (CurrentRound == Round.Shield)
        {
            CycleFinished = true;
            CurrentRound = Round.Kill;
        }
        else if (!CycleFinished || CycleRepeats.GetBool()) CurrentRound++;

        float limit = CurrentRound switch
        {
            Round.Knight => Monarch.KnightMax.GetFloat(),
            Round.Shield => Medic.SkillLimit,
            _ => 0
        };

        GaslighterId.SetAbilityUseLimit(limit);

        PlayerControl pc = GaslighterId.GetPlayer();
        pc?.ResetKillCooldown();
        pc?.Notify(Translator.GetString($"Gaslighter.{CurrentRound}"));

        LateTask.New(() => pc?.SetKillCooldown(), 1.5f, log: false);
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        switch (CurrentRound)
        {
            case Round.Kill:
                return true;
            case Round.Knight when killer.GetAbilityUseLimit() > 0 && !target.Is(CustomRoles.Knighted):
                target.RpcSetCustomRole(CustomRoles.Knighted);
                Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
                Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: target);
                killer.RpcRemoveAbilityUse();
                killer.SetKillCooldown();
                return false;
            case Round.Curse:
                CursedPlayers.Add(target.PlayerId);
                Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
                killer.SetKillCooldown();
                return false;
            case Round.Shield when killer.GetAbilityUseLimit() > 0:
                ShieldedPlayers.Add(target.PlayerId);
                Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
                Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: target);
                killer.RpcRemoveAbilityUse();
                killer.SetKillCooldown();
                return false;
        }

        return false;
    }

    public static bool IsShielded(PlayerControl target)
    {
        return On && Instances.Exists(i => i.ShieldedPlayers.Contains(target.PlayerId));
    }

    private static bool IsCursed(PlayerControl target)
    {
        return On && Instances.Exists(i => i.CursedPlayers.Contains(target.PlayerId));
    }

    public override string GetProgressText(byte playerId, bool comms)
    {
        return CurrentRound is Round.Knight or Round.Shield
            ? base.GetProgressText(playerId, comms)
            : Utils.GetTaskCount(playerId, comms);
    }

    public static string GetMark(PlayerControl seer, PlayerControl target, bool meeting = false)
    {
        bool seerIsGaslighter = seer.Is(CustomRoles.Gaslighter);
        var sb = new StringBuilder();
        if (IsShielded(target) && (seerIsGaslighter || seer.PlayerId == target.PlayerId)) sb.Append($"<color={Utils.GetRoleColorCode(CustomRoles.Medic)}> ●</color>");

        if (IsCursed(target) && (meeting || seerIsGaslighter)) sb.Append(Utils.ColorString(Palette.ImpostorRed, "†"));

        return sb.ToString();
    }

    public bool AddAsAdditionalWinner()
    {
        return WinCondition.GetValue() switch
        {
            0 => CustomWinnerHolder.WinnerTeam != CustomWinner.Crewmate,
            1 => GaslighterId.GetPlayer()?.IsAlive() == true,
            _ => false
        };
    }

    private enum Round
    {
        Kill,
        Knight,
        Curse,
        Shield
    }
}