using System;
using System.Collections.Generic;
using System.Linq;
using EHR.Patches;

namespace EHR.Coven;

public class SpellCaster : Coven
{
    public static bool On;

    private static OptionItem AbilityCooldown;
    private static Dictionary<byte, bool> HexedPlayers = [];
    private static HashSet<byte> VisibleHexes = [];
    private static HashSet<byte> PlayerIdList = [];

    protected override NecronomiconReceivePriorities NecronomiconReceivePriority => NecronomiconReceivePriorities.Random;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(650010)
            .AutoSetupOption(ref AbilityCooldown, 30f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds);
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

    protected override void OnReceiveNecronomicon()
    {
        var appearsAsSpelled = Main.AllAlivePlayerControls.Length / 2 < HexedPlayers.Keys.Count ? PlayerIdList : [];
        VisibleHexes = HexedPlayers.Keys.Concat(appearsAsSpelled).ToHashSet();
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
            if (exileIds.Any(PlayerIdList.Contains))
                HexedPlayers.Clear();
            
            PlayerControl spellCaster = Main.AllAlivePlayerControls.First(x => x.Is(CustomRoles.SpellCaster));

            byte[] curseDeathList = Main.PlayerStates.Keys
                .Except(Main.AfterMeetingDeathPlayers.Keys)
                .Where(IsSpelled)
                .ToArray();

            curseDeathList.ToValidPlayers().Do(x => x.SetRealKiller(spellCaster));
            CheckForEndVotingPatch.TryAddAfterMeetingDeathPlayers(PlayerState.DeathReason.Spell, curseDeathList);
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }

    public static bool IsSpelled(byte playerId)
    {
        return HexedPlayers.TryGetValue(playerId, out bool spell) && spell;
    }

    public override void OnReportDeadBody()
    {
        if (!IsWinConditionMet()) return;
        
        LateTask.New(() =>
        {
            var spellCasterStr = CustomRoles.SpellCaster.ToColoredString();

            Utils.SendMessage(string.Format(Translator.GetString("SpellCaster.WinConditionMet"), spellCasterStr),
                byte.MaxValue,
                $"<{Main.CovenColor}>{string.Format(Translator.GetString("SpellCaster.WinConditionMetTitle"), spellCasterStr)}</color>");
        }, 10f, log: false);
    }

    public override void AfterMeetingTasks()
    {
        if (IsWinConditionMet())
        {
            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Coven);
            CustomWinnerHolder.WinnerIds.UnionWith(Main.AllPlayerControls.Where(x => x.Is(Team.Coven)).Select(x => x.PlayerId));
        }
    }

    public static bool IsWinConditionMet()
    {
        return PlayerIdList.ToValidPlayers().Any(x => x.IsAlive()) && Main.AllAlivePlayerControls.All(x => x.Is(Team.Coven) || HexedPlayers.ContainsKey(x.PlayerId));
    }
}