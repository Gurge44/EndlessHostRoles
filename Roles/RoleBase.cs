//@formatter:off
extern alias JetBrainsAnnotationsNuget;
global using Annotations = JetBrainsAnnotationsNuget::JetBrains.Annotations;
//@formatter:on
global using Object = UnityEngine.Object;
global using Vector2 = UnityEngine.Vector2;
global using File = System.IO.File;
global using StringBuilder = System.Text.StringBuilder;
global using Logger = EHR.Logger;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using AmongUs.GameOptions;
using EHR.AddOns.Crewmate;
using EHR.AddOns.Impostor;
using EHR.Crewmate;


namespace EHR;

public abstract class RoleBase : IComparable<RoleBase>
{
    public abstract bool IsEnable { get; }

    public virtual bool SeesArrowsToDeadBodies => false;

    public int CompareTo(RoleBase other)
    {
        string thisName = GetType().Name;
        string translatedName = Translator.GetString(thisName);
        if (translatedName != string.Empty && !translatedName.StartsWith("*") && !translatedName.StartsWith("<INVALID")) thisName = translatedName;

        string otherName = other.GetType().Name;
        string translatedOtherName = Translator.GetString(otherName);
        if (translatedOtherName != string.Empty && !translatedOtherName.StartsWith("*") && !translatedOtherName.StartsWith("<INVALID")) otherName = translatedOtherName;

        return string.Compare(thisName, otherName, StringComparison.Ordinal);
    }

    // This is a base class for all roles. It contains some common methods and properties that are used by all roles.
    public abstract void Init();
    public abstract void Add(byte playerId);
    public abstract void SetupCustomOption();

    public virtual void Remove(byte playerId) { }

    // Some virtual methods that trigger actions, like venting, petting, CheckMurder, etc. These are not abstract because they have a default implementation. These should also have the same name as the methods in the derived classes.
    public virtual void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = Options.AdjustedDefaultKillCooldown;
    }

    public virtual bool CanUseKillButton(PlayerControl pc)
    {
        return pc.IsAlive() && (pc.Is(CustomRoleTypes.Impostor) || pc.Is(CustomRoles.Bloodlust) || pc.GetCustomRole().IsNK());
    }

    public virtual bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return pc.IsAlive() && (pc.Is(CustomRoleTypes.Impostor) || (pc.Is(CustomRoles.Bloodlust) && Bloodlust.CanVent.GetBool())) && Circumvent.CanUseImpostorVentButton(pc);
    }

    public virtual bool CanUseVent(PlayerControl pc, int ventId)
    {
        return true;
    }

    public virtual bool CanUseSabotage(PlayerControl pc)
    {
        if (pc.Is(CustomRoles.Aide)) return false;
        if (Options.DisableSabotagingOn1v1.GetBool() && Options.CurrentGameMode == CustomGameMode.Standard && Main.AllAlivePlayerControls.Length == 2) return false;
        return pc.Is(CustomRoleTypes.Impostor) || pc.Is(CustomRoles.Trickster) || pc.Is(CustomRoles.Mischievous) || (pc.Is(CustomRoles.Bloodlust) && Bloodlust.HasImpVision.GetBool() && pc.IsAlive());
    }

    public virtual void ApplyGameOptions(IGameOptions opt, byte playerId) { }

    public virtual void OnFixedUpdate(PlayerControl pc) { }

    public virtual void OnCheckPlayerPosition(PlayerControl pc) { }

    public virtual void OnGlobalFixedUpdate(PlayerControl pc, bool lowLoad) { }

    public virtual void OnTaskComplete(PlayerControl pc, int completedTaskCount, int totalTaskCount) { }

    public virtual void OnEnterVent(PlayerControl pc, Vent vent) { }

    public virtual void OnExitVent(PlayerControl pc, Vent vent) { }

    public virtual void OnPet(PlayerControl pc)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        int x = IRandom.Instance.Next(1, 16);
        string suffix;

        if (x >= 14)
        {
            x -= 13;
            TaskState ts = pc.GetTaskState();

            suffix = pc.GetCustomRoleTypes() switch
            {
                CustomRoleTypes.Impostor => $"Imp{x}",
                CustomRoleTypes.Neutral => $"Neutral{x}",
                CustomRoleTypes.Crewmate => x == 1 ? "Crew" : ts.HasTasks && ts.IsTaskFinished ? "CrewTaskDone" : "CrewWithTasksLeft",
                _ => x.ToString()
            };
        }
        else
            suffix = x.ToString();

        pc.Notify(Translator.GetString($"NoPetActionMsg{suffix}"));
    }

    public virtual bool OnSabotage(PlayerControl pc)
    {
        return CanUseSabotage(pc);
    }

    public virtual bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        return target != null && killer != null;
    }

    public virtual bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
    {
        return target != null && killer != null;
    }

    public virtual void OnMurder(PlayerControl killer, PlayerControl target) { }

    public virtual void OnVoteKick(PlayerControl pc, PlayerControl target)
    {
        if (Imitator.PlayerIdList.Contains(pc.PlayerId))
        {
            string command = $"/imitate {target.PlayerId}";
            ChatCommands.ImitateCommand(pc, "Command.Imitate", command, command.Split(' '));
        }
    }

    public virtual void OnMeetingShapeshift(PlayerControl shapeshifter, PlayerControl target)
    {
        if (Options.UseMeetingShapeshiftForGuessing.GetBool() && GuessManager.StartMeetingPatch.CanGuess(shapeshifter, Options.GuesserNumRestrictions.GetBool()))
            GuessManager.OnMeetingShapeshiftReceived(shapeshifter, target);
    }

    public virtual bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        return true;
    }

    public virtual bool OnVanish(PlayerControl pc)
    {
        return true;
    }

    public virtual bool OnVote(PlayerControl voter, PlayerControl target)
    {
        return false;
    }

    public virtual void OnReportDeadBody() { }

    public virtual bool CheckReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target, PlayerControl killer)
    {
        return true;
    }

    public virtual void AfterMeetingTasks() { }

    public virtual void OnRevived(PlayerControl pc)
    {
        AfterMeetingTasks();
    }

    public virtual string GetProgressText(byte playerId, bool comms)
    {
        StringBuilder sb = new();
        sb.Append(Utils.GetAbilityUseLimitDisplay(playerId));
        sb.Append(Utils.GetTaskCount(playerId, comms));
        return sb.ToString();
    }

    public virtual void SetButtonTexts(HudManager hud, byte id) { }

    public virtual string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        return string.Empty;
    }

    public virtual bool KnowRole(PlayerControl seer, PlayerControl target)
    {
        if (Options.NeutralsKnowEachOther.GetBool() && seer.Is(Team.Neutral) && target.Is(Team.Neutral)) return true;
        if (Options.EveryoneSeesDeadPlayersRoles.GetBool() && !target.IsAlive() && !seer.Is(CustomRoles.NecroGuesser)) return true;

        CustomRoles seerRole = seer.GetCustomRole();
        return seerRole.IsNK() && seerRole == target.GetCustomRole() && seer.GetTeam() == target.GetTeam();
    }

    public virtual void ManipulateGameEndCheckCrew(PlayerState playerState, out bool keepGameGoing, out int countsAs)
    {
        keepGameGoing = false;
        countsAs = 1;
    }

    protected bool IsThisRole(PlayerControl pc)
    {
        return pc.GetCustomRole() == Enum.Parse<CustomRoles>(GetType().Name, true);
    }

    protected bool IsThisRole(byte id)
    {
        CustomRoles role = Main.PlayerStates.TryGetValue(id, out PlayerState state) ? state.MainRole : CustomRoles.NotAssigned;
        return role == Enum.Parse<CustomRoles>(GetType().Name, true);
    }

    // Option setup simplifier
    protected OptionSetupHandler StartSetup(int id, bool single = false)
    {
        var role = Enum.Parse<CustomRoles>(GetType().Name, true);
        var tab = TabGroup.OtherRoles;

        if (role.IsCoven()) tab = TabGroup.CovenRoles;
        else if (role.IsImpostor() || role.IsMadmate()) tab = TabGroup.ImpostorRoles;
        else if (role.IsNeutral(true)) tab = TabGroup.NeutralRoles;
        else if (role.IsCrewmate()) tab = TabGroup.CrewmateRoles;

        if (single) Options.SetupSingleRoleOptions(id++, tab, role, hideMaxSetting: true);
        else Options.SetupRoleOptions(id++, tab, role);

        return new(id, tab, role);
    }
}

public class OptionSetupHandler(int id, TabGroup tab, CustomRoles role)
{
    private readonly OptionItem Parent = Options.CustomRoleSpawnChances[role];
    private int _id = id;

    public OptionSetupHandler AutoSetupOption(ref OptionItem field, object defaultValue, object valueRule = null, OptionFormat format = OptionFormat.None, [CallerArgumentExpression("field")] string fieldName = "", string overrideName = "", OptionItem overrideParent = null, bool noTranslation = false)
    {
        try
        {
            bool generalOption = !Translator.GetString(fieldName).StartsWith('*');
            string name = overrideName == "" ? generalOption ? fieldName : $"{role}.{fieldName}" : overrideName;

            field = (valueRule, defaultValue) switch
            {
                (null, bool bdv) => new BooleanOptionItem(++_id, name, bdv, tab),
                (IntegerValueRule ivr, int idv) => new IntegerOptionItem(++_id, name, ivr, idv, tab),
                (FloatValueRule fvr, float fdv) => new FloatOptionItem(++_id, name, fvr, fdv, tab),
                (IList<string> selections, int index) => new StringOptionItem(++_id, name, selections, index, tab, noTranslation: noTranslation),
                _ => throw new ArgumentException("The valueRule and defaultValue combination is not supported.")
            };

            field?.SetParent(overrideParent ?? Parent);
            if (format != OptionFormat.None) field?.SetValueFormat(format);
        }
        catch (Exception e)
        {
            Logger.Error($"Failed to setup option {fieldName} for {role}", "OptionSetupHandler");
            Utils.ThrowException(e);
        }

        return this;
    }

    public OptionSetupHandler CreateOverrideTasksData()
    {
        Options.OverrideTasksData.Create(++_id, tab, role);
        return this;
    }

    public OptionSetupHandler CreateVoteCancellingUseSetting(ref OptionItem field)
    {
        field = Options.CreateVoteCancellingUseSetting(++_id, role, tab);
        return this;
    }
    
    public OptionSetupHandler CreatePetUseSetting(ref OptionItem field)
    {
        field = Options.CreatePetUseSetting(++_id, role);
        return this;
    }
}