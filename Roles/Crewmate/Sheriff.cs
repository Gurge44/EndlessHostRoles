using System;
using System.Collections.Generic;
using AmongUs.GameOptions;

namespace EHR.Crewmate;

public class Sheriff : RoleBase
{
    private const int Id = 8800;
    public static List<byte> playerIdList = [];

    public static OptionItem KillCooldown;
    private static OptionItem MisfireKillsTarget;
    public static OptionItem ShotLimitOpt;
    public static OptionItem ShowShotLimit;
    private static OptionItem CanKillAllAlive;
    public static OptionItem CanKillNeutrals;
    public static OptionItem CanKillNeutralsMode;
    public static OptionItem CanKillMadmate;
    public static OptionItem CanKillCharmed;
    public static OptionItem CanKillLovers;
    public static OptionItem CanKillSidekicks;
    public static OptionItem CanKillEgoists;
    public static OptionItem CanKillContagious;
    public static OptionItem SidekickSheriffCanGoBerserk;
    public static OptionItem SetNonCrewCanKill;
    public static OptionItem NonCrewCanKillCrew;
    public static OptionItem NonCrewCanKillImp;
    public static OptionItem NonCrewCanKillNeutral;
    public static OptionItem KeepsGameGoing;
    public static OptionItem UsePet;
    public static Dictionary<CustomRoles, OptionItem> KillTargetOptions = [];

    public static readonly string[] KillOption =
    [
        "SheriffCanKillAll",
        "SheriffCanKillSeparately"
    ];

    public override bool IsEnable => playerIdList.Count > 0;

    public static void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Sheriff);
        KillCooldown = new FloatOptionItem(Id + 10, "KillCooldown", new(0f, 60f, 2.5f), 22.5f, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sheriff])
            .SetValueFormat(OptionFormat.Seconds);
        MisfireKillsTarget = new BooleanOptionItem(Id + 11, "SheriffMisfireKillsTarget", false, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sheriff]);
        ShotLimitOpt = new IntegerOptionItem(Id + 12, "SheriffShotLimit", new(1, 15, 1), 5, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sheriff])
            .SetValueFormat(OptionFormat.Times);
        ShowShotLimit = new BooleanOptionItem(Id + 13, "SheriffShowShotLimit", false, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sheriff]);
        CanKillAllAlive = new BooleanOptionItem(Id + 15, "SheriffCanKillAllAlive", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sheriff]);
        CanKillMadmate = new BooleanOptionItem(Id + 17, "SheriffCanKillMadmate", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sheriff]);
        CanKillCharmed = new BooleanOptionItem(Id + 22, "SheriffCanKillCharmed", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sheriff]);
        CanKillLovers = new BooleanOptionItem(Id + 24, "SheriffCanKillLovers", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sheriff]);
        CanKillSidekicks = new BooleanOptionItem(Id + 23, "SheriffCanKillSidekick", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sheriff]);
        CanKillEgoists = new BooleanOptionItem(Id + 25, "SheriffCanKillEgoist", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sheriff]);
        CanKillContagious = new BooleanOptionItem(Id + 27, "SheriffCanKillContagious", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sheriff]);
        CanKillNeutrals = new BooleanOptionItem(Id + 16, "SheriffCanKillNeutrals", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sheriff]);
        CanKillNeutralsMode = new StringOptionItem(Id + 14, "SheriffCanKillNeutralsMode", KillOption, 0, TabGroup.CrewmateRoles).SetParent(CanKillNeutrals);
        SetUpNeutralOptions(Id + 30);
        SidekickSheriffCanGoBerserk = new BooleanOptionItem(Id + 28, "SidekickSheriffCanGoBerserk", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sheriff]);
        SetNonCrewCanKill = new BooleanOptionItem(Id + 18, "SheriffSetMadCanKill", false, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sheriff]);
        NonCrewCanKillImp = new BooleanOptionItem(Id + 19, "SheriffMadCanKillImp", true, TabGroup.CrewmateRoles).SetParent(SetNonCrewCanKill);
        NonCrewCanKillCrew = new BooleanOptionItem(Id + 21, "SheriffMadCanKillCrew", true, TabGroup.CrewmateRoles).SetParent(SetNonCrewCanKill);
        NonCrewCanKillNeutral = new BooleanOptionItem(Id + 20, "SheriffMadCanKillNeutral", true, TabGroup.CrewmateRoles).SetParent(SetNonCrewCanKill);
        KeepsGameGoing = new BooleanOptionItem(Id + 26, "SheriffKeepsGameGoing", false, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sheriff]);
        UsePet = Options.CreatePetUseSetting(Id + 29, CustomRoles.Sheriff);
    }

    private static void SetUpNeutralOptions(int id)
    {
        foreach (var neutral in Enum.GetValues<CustomRoles>())
        {
            if (neutral.IsNeutral() && neutral is not CustomRoles.Konan and not CustomRoles.Pestilence and not CustomRoles.GM and not CustomRoles.Convict && !neutral.IsForOtherGameMode())
            {
                SetUpKillTargetOption(neutral, id, true, CanKillNeutralsMode);
                id++;
            }
        }
    }

    public static void SetUpKillTargetOption(CustomRoles role, int id, bool defaultValue = true, OptionItem parent = null)
    {
        parent ??= Options.CustomRoleSpawnChances[CustomRoles.Sheriff];
        var roleName = Utils.GetRoleName(role);
        Dictionary<string, string> replacementDic = new() { { "%role%", Utils.ColorString(Utils.GetRoleColor(role), roleName) } };
        KillTargetOptions[role] = new BooleanOptionItem(id, "SheriffCanKill%role%", defaultValue, TabGroup.CrewmateRoles).SetParent(parent);
        KillTargetOptions[role].ReplacementDictionary = replacementDic;
    }

    public override void Init()
    {
        playerIdList = [];
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        playerId.SetAbilityUseLimit(ShotLimitOpt.GetInt());

        Logger.Info($"{Utils.GetPlayerById(playerId)?.GetNameWithRole().RemoveHtmlTags()} : Shot Limit - {playerId.GetAbilityUseLimit()}", "Sheriff");
    }

    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = CanUseKillButton(Utils.GetPlayerById(id)) ? KillCooldown.GetFloat() : 15f;
    public override void ApplyGameOptions(IGameOptions opt, byte playerId) => opt.SetVision(false);

    public override bool CanUseImpostorVentButton(PlayerControl pc) => false;

    public override bool CanUseKillButton(PlayerControl pc)
        => !Main.PlayerStates[pc.PlayerId].IsDead
           && (CanKillAllAlive.GetBool() || GameStates.AlreadyDied)
           && (pc.GetAbilityUseLimit() is float.NaN or > 0);

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (CanBeKilledBySheriff(target)
            || (killer.Is(CustomRoles.Recruit) && SidekickSheriffCanGoBerserk.GetBool())
            || (SetNonCrewCanKill.GetBool() &&
                (
                    killer.Is(CustomRoles.Madmate)
                    || killer.Is(CustomRoles.Charmed)
                    || killer.Is(CustomRoles.Contagious)
                    || killer.Is(CustomRoles.Undead)
                )
                && ((target.IsImpostor() && NonCrewCanKillImp.GetBool()) || (target.IsCrewmate() && NonCrewCanKillCrew.GetBool()) || (target.GetCustomRole().IsNeutral() && NonCrewCanKillNeutral.GetBool()))
            ))
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
    }

    private static bool CanBeKilledBySheriff(PlayerControl player)
    {
        var cRole = player.GetCustomRole();
        var subRole = player.GetCustomSubRoles();
        if (subRole.Contains(CustomRoles.Rascal)) return true;
        bool CanKill = false;
        foreach (CustomRoles SubRoleTarget in subRole)
        {
            CanKill = SubRoleTarget switch
            {
                CustomRoles.Madmate => CanKillMadmate.GetBool(),
                CustomRoles.Charmed => CanKillCharmed.GetBool(),
                CustomRoles.Lovers => CanKillLovers.GetBool(),
                CustomRoles.Recruit => CanKillSidekicks.GetBool(),
                CustomRoles.Egoist => CanKillEgoists.GetBool(),
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
                CustomRoleTypes.Neutral => CanKillNeutrals.GetBool() && (CanKillNeutralsMode.GetValue() == 0 || !KillTargetOptions.TryGetValue(cRole, out var option) || option.GetBool()),
                _ => CanKill
            }
        };
    }
}