using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;

namespace EHR.Roles.Crewmate;

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
        KillCooldown = FloatOptionItem.Create(Id + 10, "KillCooldown", new(0f, 60f, 2.5f), 22.5f, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sheriff])
            .SetValueFormat(OptionFormat.Seconds);
        MisfireKillsTarget = BooleanOptionItem.Create(Id + 11, "SheriffMisfireKillsTarget", false, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sheriff]);
        ShotLimitOpt = IntegerOptionItem.Create(Id + 12, "SheriffShotLimit", new(1, 15, 1), 5, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sheriff])
            .SetValueFormat(OptionFormat.Times);
        ShowShotLimit = BooleanOptionItem.Create(Id + 13, "SheriffShowShotLimit", false, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sheriff]);
        CanKillAllAlive = BooleanOptionItem.Create(Id + 15, "SheriffCanKillAllAlive", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sheriff]);
        CanKillMadmate = BooleanOptionItem.Create(Id + 17, "SheriffCanKillMadmate", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sheriff]);
        CanKillCharmed = BooleanOptionItem.Create(Id + 22, "SheriffCanKillCharmed", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sheriff]);
        CanKillLovers = BooleanOptionItem.Create(Id + 24, "SheriffCanKillLovers", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sheriff]);
        CanKillSidekicks = BooleanOptionItem.Create(Id + 23, "SheriffCanKillSidekick", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sheriff]);
        CanKillEgoists = BooleanOptionItem.Create(Id + 25, "SheriffCanKillEgoist", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sheriff]);
        CanKillContagious = BooleanOptionItem.Create(Id + 27, "SheriffCanKillContagious", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sheriff]);
        CanKillNeutrals = BooleanOptionItem.Create(Id + 16, "SheriffCanKillNeutrals", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sheriff]);
        CanKillNeutralsMode = StringOptionItem.Create(Id + 14, "SheriffCanKillNeutralsMode", KillOption, 0, TabGroup.CrewmateRoles).SetParent(CanKillNeutrals);
        SetUpNeutralOptions(Id + 30);
        SidekickSheriffCanGoBerserk = BooleanOptionItem.Create(Id + 28, "SidekickSheriffCanGoBerserk", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sheriff]);
        SetNonCrewCanKill = BooleanOptionItem.Create(Id + 18, "SheriffSetMadCanKill", false, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sheriff]);
        NonCrewCanKillImp = BooleanOptionItem.Create(Id + 19, "SheriffMadCanKillImp", true, TabGroup.CrewmateRoles).SetParent(SetNonCrewCanKill);
        NonCrewCanKillCrew = BooleanOptionItem.Create(Id + 21, "SheriffMadCanKillCrew", true, TabGroup.CrewmateRoles).SetParent(SetNonCrewCanKill);
        NonCrewCanKillNeutral = BooleanOptionItem.Create(Id + 20, "SheriffMadCanKillNeutral", true, TabGroup.CrewmateRoles).SetParent(SetNonCrewCanKill);
        UsePet = Options.CreatePetUseSetting(Id + 29, CustomRoles.Sheriff);
    }

    public static void SetUpNeutralOptions(int id)
    {
        foreach (var neutral in Enum.GetValues(typeof(CustomRoles)).Cast<CustomRoles>().Where(x => x.IsNeutral() && x is not CustomRoles.KB_Normal and not CustomRoles.Konan and not CustomRoles.Pestilence and not CustomRoles.Killer and not CustomRoles.Tasker and not CustomRoles.Potato and not CustomRoles.Hider and not CustomRoles.Seeker and not CustomRoles.Fox and not CustomRoles.Troll and not CustomRoles.Jumper and not CustomRoles.Detector and not CustomRoles.Jet and not CustomRoles.Dasher and not CustomRoles.Locator and not CustomRoles.Venter and not CustomRoles.Agent and not CustomRoles.Taskinator and not CustomRoles.GM and not CustomRoles.Convict))
        {
            SetUpKillTargetOption(neutral, id, true, CanKillNeutralsMode);
            id++;
        }
    }

    public static void SetUpKillTargetOption(CustomRoles role, int id, bool defaultValue = true, OptionItem parent = null)
    {
        parent ??= Options.CustomRoleSpawnChances[CustomRoles.Sheriff];
        var roleName = Utils.GetRoleName(role);
        Dictionary<string, string> replacementDic = new() { { "%role%", Utils.ColorString(Utils.GetRoleColor(role), roleName) } };
        KillTargetOptions[role] = BooleanOptionItem.Create(id, "SheriffCanKill%role%", defaultValue, TabGroup.CrewmateRoles).SetParent(parent);
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

        if (!AmongUsClient.Instance.AmHost || (Options.UsePets.GetBool() && UsePet.GetBool())) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
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
        killer.RpcRemoveAbilityUse();
        Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} : Number of kills left: {killer.GetAbilityUseLimit()}", "Sheriff");
        if (CanBeKilledBySheriff(target)
            || (killer.Is(CustomRoles.Recruit) && SidekickSheriffCanGoBerserk.GetBool())
            || (SetNonCrewCanKill.GetBool() &&
                (
                    killer.Is(CustomRoles.Madmate)
                    || killer.Is(CustomRoles.Charmed)
                    || killer.Is(CustomRoles.Contagious)
                    || killer.Is(CustomRoles.Undead)
                )
                && ((target.GetCustomRole().IsImpostor() && NonCrewCanKillImp.GetBool()) || (target.IsCrewmate() && NonCrewCanKillCrew.GetBool()) || (target.GetCustomRole().IsNeutral() && NonCrewCanKillNeutral.GetBool()))
            ))
        {
            SetKillCooldown(killer.PlayerId);
            return true;
        }

        killer.Suicide(PlayerState.DeathReason.Misfire);
        return MisfireKillsTarget.GetBool();
    }

    public static bool CanBeKilledBySheriff(PlayerControl player)
    {
        var cRole = player.GetCustomRole();
        var subRole = player.GetCustomSubRoles();
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
                _ => false,
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
                _ => CanKill,
            }
        };
    }
}