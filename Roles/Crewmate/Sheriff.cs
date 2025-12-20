using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;

namespace EHR.Crewmate;

public class Sheriff : RoleBase
{
    private const int Id = 653000;
    private static List<byte> PlayerIdList = [];

    private static OptionItem KillCooldown;
    private static OptionItem MisfireKillsTarget;
    private static OptionItem ShotLimitOpt;
    private static OptionItem ShowShotLimit;
    private static OptionItem CanKillAllAlive;
    private static OptionItem CanKillNeutrals;
    private static OptionItem CanKillNeutralsMode;
    private static OptionItem CanKillCoven;
    private static OptionItem CanKillMadmate;
    private static OptionItem CanKillCharmed;
    private static OptionItem CanKillLovers;
    private static OptionItem CanKillSidekicks;
    private static OptionItem CanKillContagious;
    private static OptionItem SidekickSheriffCanGoBerserk;
    private static OptionItem SetNonCrewCanKill;
    private static OptionItem NonCrewCanKillCrew;
    private static OptionItem NonCrewCanKillImp;
    private static OptionItem NonCrewCanKillNeutral;
    public static OptionItem UsePet;
    private static readonly Dictionary<CustomRoles, OptionItem> KillTargetOptions = [];

    public static readonly string[] KillOption =
    [
        "SheriffCanKillAll",
        "SheriffCanKillSeparately"
    ];

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Sheriff);
        KillCooldown = new FloatOptionItem(Id + 10, "KillCooldown", new(0f, 60f, 0.5f), 22.5f, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sheriff]).SetValueFormat(OptionFormat.Seconds);
        MisfireKillsTarget = new BooleanOptionItem(Id + 11, "SheriffMisfireKillsTarget", false, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sheriff]);
        ShotLimitOpt = new IntegerOptionItem(Id + 12, "SheriffShotLimit", new(1, 15, 1), 5, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sheriff]).SetValueFormat(OptionFormat.Times);
        ShowShotLimit = new BooleanOptionItem(Id + 13, "SheriffShowShotLimit", false, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sheriff]);
        CanKillAllAlive = new BooleanOptionItem(Id + 15, "SheriffCanKillAllAlive", false, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sheriff]);
        CanKillCoven = new BooleanOptionItem(Id + 25, "SheriffCanKillCoven", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sheriff]);
        CanKillMadmate = new BooleanOptionItem(Id + 17, "SheriffCanKillMadmate", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sheriff]);
        CanKillCharmed = new BooleanOptionItem(Id + 22, "SheriffCanKillCharmed", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sheriff]);
        CanKillLovers = new BooleanOptionItem(Id + 24, "SheriffCanKillLovers", false, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sheriff]);
        CanKillSidekicks = new BooleanOptionItem(Id + 23, "SheriffCanKillSidekick", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sheriff]);
        CanKillContagious = new BooleanOptionItem(Id + 27, "SheriffCanKillContagious", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sheriff]);
        CanKillNeutrals = new BooleanOptionItem(Id + 16, "SheriffCanKillNeutrals", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sheriff]);
        CanKillNeutralsMode = new StringOptionItem(Id + 14, "SheriffCanKillNeutralsMode", KillOption, 0, TabGroup.CrewmateRoles).SetParent(CanKillNeutrals);
        SetUpNeutralOptions(Id + 30);
        SidekickSheriffCanGoBerserk = new BooleanOptionItem(Id + 28, "SidekickSheriffCanGoBerserk", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sheriff]);
        SetNonCrewCanKill = new BooleanOptionItem(Id + 18, "SheriffSetMadCanKill", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sheriff]);
        NonCrewCanKillImp = new BooleanOptionItem(Id + 19, "SheriffMadCanKillImp", true, TabGroup.CrewmateRoles).SetParent(SetNonCrewCanKill);
        NonCrewCanKillCrew = new BooleanOptionItem(Id + 21, "SheriffMadCanKillCrew", true, TabGroup.CrewmateRoles).SetParent(SetNonCrewCanKill);
        NonCrewCanKillNeutral = new BooleanOptionItem(Id + 20, "SheriffMadCanKillNeutral", true, TabGroup.CrewmateRoles).SetParent(SetNonCrewCanKill);
        UsePet = Options.CreatePetUseSetting(Id + 29, CustomRoles.Sheriff);
    }

    private static void SetUpNeutralOptions(int id)
    {
        foreach (CustomRoles neutral in Enum.GetValues<CustomRoles>())
        {
            if (neutral.IsNeutral() && neutral is not CustomRoles.Pestilence and not CustomRoles.GM and not CustomRoles.Convict && !neutral.IsForOtherGameMode())
            {
                SetUpKillTargetOption(neutral, id, true, CanKillNeutralsMode);
                id++;
            }
        }
    }

    public static void SetUpKillTargetOption(CustomRoles role, int id, bool defaultValue = true, OptionItem parent = null)
    {
        parent ??= Options.CustomRoleSpawnChances[CustomRoles.Sheriff];
        Dictionary<string, string> replacementDic = new() { { "%role%", role.ToColoredString() } };
        KillTargetOptions[role] = new BooleanOptionItem(id, "SheriffCanKill%role%", defaultValue, TabGroup.CrewmateRoles).SetParent(parent);
        KillTargetOptions[role].ReplacementDictionary = replacementDic;
    }

    public override void Init()
    {
        PlayerIdList = [];
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        playerId.SetAbilityUseLimit(ShotLimitOpt.GetFloat());

        Logger.Info($"{Utils.GetPlayerById(playerId)?.GetNameWithRole().RemoveHtmlTags()} : Shot Limit - {playerId.GetAbilityUseLimit()}", "Sheriff");
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        opt.SetVision(false);
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return false;
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return !Main.PlayerStates[pc.PlayerId].IsDead
               && (CanKillAllAlive.GetBool() || GameStates.AlreadyDied)
               && pc.GetAbilityUseLimit() is float.NaN or > 0;
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (CanBeKilledBySheriff(target) || (SetNonCrewCanKill.GetBool() && (killer.IsMadmate() || killer.IsConverted()) && ((target.IsImpostor() && NonCrewCanKillImp.GetBool()) || (target.IsCrewmate() && NonCrewCanKillCrew.GetBool()) || (target.GetCustomRole().IsNeutral() && NonCrewCanKillNeutral.GetBool()))))
        {
            SetKillCooldown(killer.PlayerId);
            return true;
        }

        killer.Suicide(PlayerState.DeathReason.Misfire);
        return MisfireKillsTarget.GetBool();
    }

    public override void OnMurder(PlayerControl killer, PlayerControl target)
    {
        killer.RpcRemoveAbilityUse();
        Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} : Number of kills left: {killer.GetAbilityUseLimit()}", "Sheriff");
        
        if (killer.AmOwner && Utils.TimeStamp - IntroCutsceneDestroyPatch.IntroDestroyTS < 25)
            Achievements.Type.ItsGamblingTime.Complete();
    }

    private static bool CanBeKilledBySheriff(PlayerControl player)
    {
        CustomRoles cRole = player.GetCustomRole();
        List<CustomRoles> subRole = player.GetCustomSubRoles();
        if (subRole.Contains(CustomRoles.Rascal)) return true;

        var CanKill = false;

        foreach (CustomRoles SubRoleTarget in subRole)
        {
            CanKill = SubRoleTarget switch
            {
                CustomRoles.Madmate => CanKillMadmate.GetBool(),
                CustomRoles.Charmed => CanKillCharmed.GetBool(),
                CustomRoles.Lovers => CanKillLovers.GetBool(),
                CustomRoles.Contagious => CanKillContagious.GetBool(),
                CustomRoles.Rascal => true,
                _ => false
            };
        }

        return cRole switch
        {
            CustomRoles.Trickster => false,
            CustomRoles.Pestilence => true,
            _ => player.GetCustomRoleTypes() switch
            {
                CustomRoleTypes.Impostor => true,
                CustomRoleTypes.Coven => CanKillCoven.GetBool(),
                CustomRoleTypes.Neutral => CanKillNeutrals.GetBool() && (CanKillNeutralsMode.GetValue() == 0 || !KillTargetOptions.TryGetValue(cRole, out OptionItem option) || option.GetBool()),
                _ => CanKill
            }
        };
    }

    public override string GetProgressText(byte playerId, bool comms)
    {
        return ShowShotLimit.GetBool() ? base.GetProgressText(playerId, comms) : Utils.GetTaskCount(playerId, comms);
    }

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
}