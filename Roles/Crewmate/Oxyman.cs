using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using UnityEngine;

namespace EHR.Crewmate;

public class Oxyman : RoleBase
{
    public static bool On;

    private static readonly Dictionary<Level, OptionItem> LevelSettings = [];
    private static OptionItem IncrementByVenting;
    private static OptionItem DecreasementEachSecond;
    private static OptionItem IncreasedSpeed;
    private static OptionItem DecreasedSpeed;
    private long LastUpdate;
    private int OxygenLevel;
    private byte OxymanId;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        var id = 647700;
        Options.SetupRoleOptions(id++, TabGroup.CrewmateRoles, CustomRoles.Oxyman);
        (List<Level> trueList, List<Level> falseList) = Enum.GetValues<Level>().Without(Level.None).Split(x => x <= Level.Slow);

        trueList.ForEach(x => LevelSettings[x] = new IntegerOptionItem(++id, $"Oxyman.{x}.BelowPercentage", new(0, 100, 1), x == Level.Blind ? 10 : 30, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Oxyman])
            .SetValueFormat(OptionFormat.Percent));

        falseList.ForEach(x => LevelSettings[x] = new IntegerOptionItem(++id, $"Oxyman.{x}.AbovePercentage", new(0, 100, 1), x == Level.Fast ? 60 : 80, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Oxyman])
            .SetValueFormat(OptionFormat.Percent));

        IncrementByVenting = new IntegerOptionItem(++id, "Oxyman.IncrementByVenting", new(0, 100, 1), 7, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Oxyman])
            .SetValueFormat(OptionFormat.Percent);

        DecreasementEachSecond = new IntegerOptionItem(++id, "Oxyman.DecreasementEachSecond", new(0, 10, 1), 1, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Oxyman])
            .SetValueFormat(OptionFormat.Percent);

        IncreasedSpeed = new FloatOptionItem(++id, "IncreasedSpeed", new(0f, 3f, 0.05f), 1.75f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Oxyman])
            .SetValueFormat(OptionFormat.Multiplier);

        DecreasedSpeed = new FloatOptionItem(++id, "DecreasedSpeed", new(0f, 3f, 0.05f), 1f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Oxyman])
            .SetValueFormat(OptionFormat.Multiplier);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        OxymanId = playerId;
        OxygenLevel = 59;
        LastUpdate = Utils.TimeStamp;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        AURoleOptions.EngineerCooldown = 0f;
        AURoleOptions.EngineerInVentMaxTime = 1f;
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        Level previousLevel = GetCurrentLevel();

        OxygenLevel += IncrementByVenting.GetValue();
        if (OxygenLevel > 100) OxygenLevel = 100;

        Level nowLevel = GetCurrentLevel();
        if (nowLevel != previousLevel) ApplyLevelEffect(pc, nowLevel);

        Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, OxygenLevel);
        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!pc.IsAlive() || !GameStates.IsInTask || ExileController.Instance || !Main.IntroDestroyed) return;

        long now = Utils.TimeStamp;
        if (now == LastUpdate) return;
        LastUpdate = now;

        if (OxygenLevel <= 0)
        {
            pc.Suicide();
            return;
        }

        Level previousLevel = GetCurrentLevel();

        OxygenLevel -= DecreasementEachSecond.GetInt();
        Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, OxygenLevel);
        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);

        Level nowLevel = GetCurrentLevel();

        if (nowLevel != previousLevel) ApplyLevelEffect(pc, nowLevel);
    }

    private void ApplyLevelEffect(PlayerControl pc, Level nowLevel)
    {
        switch (nowLevel)
        {
            case Level.Blind:
                Main.PlayerStates[pc.PlayerId].IsBlackOut = true;
                Main.AllPlayerSpeed[pc.PlayerId] = DecreasedSpeed.GetFloat();
                pc.MarkDirtySettings();
                break;
            case Level.Slow:
                Main.PlayerStates[pc.PlayerId].IsBlackOut = false;
                Main.AllPlayerSpeed[pc.PlayerId] = DecreasedSpeed.GetFloat();
                pc.MarkDirtySettings();
                break;
            case Level.None:
                Main.PlayerStates[pc.PlayerId].IsBlackOut = false;
                Main.AllPlayerSpeed[pc.PlayerId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
                pc.MarkDirtySettings();
                break;
            case Level.Invulnerable:
            case Level.Fast:
                Main.PlayerStates[pc.PlayerId].IsBlackOut = false;
                Main.AllPlayerSpeed[pc.PlayerId] = IncreasedSpeed.GetFloat();
                pc.MarkDirtySettings();
                break;
        }
    }

    public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
    {
        return GetCurrentLevel() != Level.Invulnerable;
    }

    private Level GetCurrentLevel()
    {
        // ReSharper disable ConvertIfStatementToReturnStatement
        if (OxygenLevel <= LevelSettings[Level.Blind].GetInt()) return Level.Blind;
        if (OxygenLevel <= LevelSettings[Level.Slow].GetInt()) return Level.Slow;
        if (OxygenLevel >= LevelSettings[Level.Invulnerable].GetInt()) return Level.Invulnerable;
        if (OxygenLevel >= LevelSettings[Level.Fast].GetInt()) return Level.Fast;
        return Level.None;
        // ReSharper restore ConvertIfStatementToReturnStatement
    }

    private Color GetLevelColor()
    {
        return GetCurrentLevel() switch
        {
            Level.Blind => Palette.Orange,
            Level.Slow => Color.yellow,
            Level.Fast => Color.cyan,
            Level.Invulnerable => Color.green,
            _ => Color.white
        };
    }

    public void ReceiveRPC(MessageReader reader)
    {
        OxygenLevel = reader.ReadPackedInt32();
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != target.PlayerId || seer.PlayerId != OxymanId || (seer.IsModdedClient() && !hud)) return string.Empty;

        return $"<#ff0000>O<sub>2</sub>:</color> {Utils.ColorString(GetLevelColor(), OxygenLevel.ToString())}%";
    }

    public override bool CanUseVent(PlayerControl pc, int ventId)
    {
        return !IsThisRole(pc) || pc.Is(CustomRoles.Nimble) || pc.GetClosestVent()?.Id == ventId;
    }

    private enum Level
    {
        Blind,
        Slow,
        None,
        Fast,
        Invulnerable
    }
}