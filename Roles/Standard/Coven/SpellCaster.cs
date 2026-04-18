using System;
using System.Collections.Generic;
using System.Linq;
using EHR.Patches;

namespace EHR.Roles;

public class SpellCaster : CovenBase
{
    public static bool On;

    private static OptionItem AbilityCooldown;
    private static OptionItem CanVentBeforeNecronomicon;
    private static OptionItem CanVentAfterNecronomicon;

    private static Dictionary<byte, bool> HexedPlayers = [];
    private static HashSet<byte> VisibleHexes = [];
    private static HashSet<byte> PlayerIdList = [];

    protected override NecronomiconReceivePriorities NecronomiconReceivePriority => NecronomiconReceivePriorities.Random;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(650010)
            .AutoSetupOption(ref AbilityCooldown, 30f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref CanVentBeforeNecronomicon, false)
            .AutoSetupOption(ref CanVentAfterNecronomicon, false);
    }

    public override void Init()
    {
        On = false;
        PlayerIdList = [];
        HexedPlayers = [];
        VisibleHexes = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        PlayerIdList.Add(playerId);
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return HasNecronomicon ? CanVentAfterNecronomicon.GetBool() : CanVentBeforeNecronomicon.GetBool();
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return pc.IsAlive();
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        HexedPlayers[target.PlayerId] = HasNecronomicon;
        killer.SetKillCooldown(AbilityCooldown.GetFloat());
        return false;
    }

    public override void OnReceiveNecronomicon()
    {
        VisibleHexes = HexedPlayers.Keys.Concat(PlayerIdList).ToHashSet();

        if (Main.AllAlivePlayerControls.Count / 2 >= HexedPlayers.Keys.Count)
            VisibleHexes.ExceptWith(PlayerIdList);

        HexedPlayers.SetAllValues(true);
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = AbilityCooldown.GetFloat();
    }

    public static void OnExile(byte[] exileIds)
    {
        try
        {
            if (!On) return;
            
            if (exileIds.Any(PlayerIdList.Contains))
                HexedPlayers.Clear();

            PlayerControl spellCaster = Main.EnumerateAlivePlayerControls().First(x => x.Is(CustomRoles.SpellCaster));

            byte[] curseDeathList = Main.PlayerStates.Keys
                .Except(Main.AfterMeetingDeathPlayers.Keys)
                .Where(IsSpelled)
                .ToArray();

            curseDeathList.ToValidPlayers().Do(x => x.SetRealKiller(spellCaster));
            CheckForEndVotingPatch.TryAddAfterMeetingDeathPlayers(PlayerState.DeathReason.Spell, curseDeathList);
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }

    private static bool IsSpelled(byte playerId)
    {
        return HexedPlayers.TryGetValue(playerId, out bool spell) && spell;
    }

    public static bool HasSpelledMark(byte playerId)
    {
        return VisibleHexes.Contains(playerId);
    }

    public override void OnReportDeadBody()
    {
        if (!IsWinConditionMet()) return;

        LateTask.New(() =>
        {
            string spellCasterStr = CustomRoles.SpellCaster.ToColoredString();

            Utils.SendMessage(string.Format(Translator.GetString("SpellCaster.WinConditionMet"), spellCasterStr),
                byte.MaxValue,
                $"<{Main.CovenColor}>{string.Format(Translator.GetString("SpellCaster.WinConditionMetTitle"), spellCasterStr)}</color>",
                importance: MessageImportance.High);
        }, 10f, log: false);
    }

    public override void AfterMeetingTasks()
    {
        if (IsWinConditionMet())
        {
            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Coven);
            CustomWinnerHolder.WinnerIds.UnionWith(Main.EnumeratePlayerControls().Where(x => x.Is(Team.Coven)).Select(x => x.PlayerId));
        }
    }

    private static bool IsWinConditionMet()
    {
        return PlayerIdList.ToValidPlayers().Any(x => x.IsAlive()) && Main.EnumerateAlivePlayerControls().All(x => x.Is(Team.Coven) || HexedPlayers.ContainsKey(x.PlayerId));
    }
}