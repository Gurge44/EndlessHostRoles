using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Neutral;

public class Amnesiac : RoleBase
{
    private const int Id = 35000;
    private static List<Amnesiac> Instances = [];

    private static OptionItem RememberCooldown;
    private static OptionItem CanRememberCrewPower;
    private static OptionItem IncompatibleNeutralMode;
    public static OptionItem RememberMode;
    public static OptionItem CanVent;
    private static OptionItem VentCooldown;
    private static OptionItem VentDuration;
    private static OptionItem ReportBodyAfterRemember;
    private static OptionItem HasArrowsToDeadBodies;
    private static OptionItem ArrowMinDelay;
    private static OptionItem ArrowMaxDelay;
    private static OptionItem RememberAddons;

    private static readonly CustomRoles[] AmnesiacIncompatibleNeutralMode =
    [
        CustomRoles.Amnesiac,
        CustomRoles.Pursuer,
        CustomRoles.Follower,
        CustomRoles.Maverick
    ];

    private static readonly string[] RememberModes =
    [
        "AmnesiacRM.ByReportingBody",
        "AmnesiacRM.ByKillButton"
    ];

    private byte AmnesiacId;

    public override bool IsEnable => Instances.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Amnesiac);

        RememberMode = new StringOptionItem(Id + 9, "RememberMode", RememberModes, 0, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Amnesiac]);

        RememberCooldown = new FloatOptionItem(Id + 10, "RememberCooldown", new(0f, 180f, 0.5f), 5f, TabGroup.NeutralRoles)
            .SetParent(RememberMode)
            .SetValueFormat(OptionFormat.Seconds);

        CanRememberCrewPower = new BooleanOptionItem(Id + 11, "CanRememberCrewPower", false, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Amnesiac]);

        IncompatibleNeutralMode = new StringOptionItem(Id + 12, "IncompatibleNeutralMode", AmnesiacIncompatibleNeutralMode.Select(x => x.ToColoredString()).ToArray(), 0, TabGroup.NeutralRoles, noTranslation: true)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Amnesiac]);

        CanVent = new BooleanOptionItem(Id + 13, "CanVent", false, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Amnesiac]);

        VentCooldown = new FloatOptionItem(Id + 14, "VentCooldown", new(0f, 180f, 0.5f), 5f, TabGroup.NeutralRoles)
            .SetParent(CanVent)
            .SetValueFormat(OptionFormat.Seconds);

        VentDuration = new FloatOptionItem(Id + 15, "MaxInVentTime", new(0f, 180f, 0.5f), 5f, TabGroup.NeutralRoles)
            .SetParent(CanVent)
            .SetValueFormat(OptionFormat.Seconds);

        ReportBodyAfterRemember = new BooleanOptionItem(Id + 16, "ReportBodyAfterRemember", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Amnesiac]);
        
        HasArrowsToDeadBodies = new BooleanOptionItem(Id + 17, "HasArrowsToDeadBodies", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Amnesiac]);
        
        ArrowMinDelay = new FloatOptionItem(Id + 18, "ArrowDelayMin", new(0f, 60f, 0.5f), 2f, TabGroup.NeutralRoles)
            .SetParent(HasArrowsToDeadBodies)
            .SetValueFormat(OptionFormat.Seconds);
        
        ArrowMaxDelay = new FloatOptionItem(Id + 19, "ArrowDelayMax", new(0f, 60f, 0.5f), 5f, TabGroup.NeutralRoles)
            .SetParent(HasArrowsToDeadBodies)
            .SetValueFormat(OptionFormat.Seconds);
        
        RememberAddons = new BooleanOptionItem(Id + 20, "RememberAddons", false, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Amnesiac]);
    }

    public override void Init()
    {
        Instances = [];
    }

    public override void Add(byte playerId)
    {
        Instances.Add(this);
        AmnesiacId = playerId;
    }

    public override void Remove(byte playerId)
    {
        Instances.Remove(this);
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = RememberCooldown.GetFloat();
    }

    public override bool CanUseKillButton(PlayerControl player)
    {
        return player.IsAlive() && RememberMode.GetValue() == 1;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        opt.SetVision(false);

        if (CanVent.GetBool())
        {
            AURoleOptions.EngineerCooldown = VentCooldown.GetFloat();
            AURoleOptions.EngineerInVentMaxTime = VentDuration.GetFloat();
        }
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (RememberMode.GetValue() == 1)
            RememberRole(killer, target);

        return false;
    }

    public override bool CheckReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target, PlayerControl killer)
    {
        if (target.Object.Is(CustomRoles.Disregarded)) return true;

        if (RememberMode.GetValue() == 0)
        {
            RememberRole(reporter, target.Object);
            return ReportBodyAfterRemember.GetBool();
        }

        return true;
    }

    private static void RememberRole(PlayerControl amnesiac, PlayerControl target)
    {
        CustomRoles? RememberedRole = null;

        var amneNotifyString = string.Empty;
        CustomRoles targetRole = target.GetCustomRole();
        int loversAlive = Main.LoversPlayers.Count(x => x.IsAlive());

        switch (targetRole)
        {
            case CustomRoles.Jackal:
                RememberedRole = CustomRoles.Sidekick;
                amneNotifyString = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("RememberedNeutralKiller"));
                break;
            case CustomRoles.Necromancer:
                RememberedRole = CustomRoles.Deathknight;
                amneNotifyString = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("RememberedNeutralKiller"));
                break;
            case CustomRoles.LovingCrewmate when loversAlive > 0:
                target.RpcSetCustomRole(CustomRoles.CrewmateEHR);
                RememberedRole = CustomRoles.LovingCrewmate;
                Main.LoversPlayers.RemoveAll(x => x.PlayerId == target.PlayerId);
                Main.LoversPlayers.Add(amnesiac);
                amneNotifyString = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("RememberedLover"));
                break;
            case CustomRoles.LovingImpostor when loversAlive > 0:
                target.RpcSetCustomRole(CustomRoles.ImpostorEHR);
                RememberedRole = CustomRoles.LovingImpostor;
                Main.LoversPlayers.RemoveAll(x => x.PlayerId == target.PlayerId);
                Main.LoversPlayers.Add(amnesiac);
                amneNotifyString = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("RememberedLover"));
                break;
            case CustomRoles.LovingCrewmate:
                RememberedRole = CustomRoles.Sheriff;
                amneNotifyString = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("RememberedCrewmate"));
                break;
            case CustomRoles.LovingImpostor:
                RememberedRole = CustomRoles.Renegade;
                amneNotifyString = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("RememberedImpostor"));
                break;
            default:
                switch (target.GetTeam())
                {
                    case Team.Impostor:
                        RememberedRole = SingleRoles.Contains(targetRole) ? CustomRoles.Renegade : targetRole;
                        amneNotifyString = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("RememberedImpostor"));
                        break;
                    case Team.Crewmate:
                        RememberedRole = CanRememberCrewPower.GetBool() || targetRole.GetCrewmateRoleCategory() != RoleOptionType.Crewmate_Power ? targetRole : CustomRoles.Sheriff;
                        amneNotifyString = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("RememberedCrewmate"));
                        break;
                    case Team.Neutral when !SingleRoles.Contains(targetRole):
                        RememberedRole = targetRole;
                        amneNotifyString = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("RememberedNeutralKiller"));
                        break;
                    case Team.Neutral:
                        RememberedRole = AmnesiacIncompatibleNeutralMode[IncompatibleNeutralMode.GetValue()];
                        amneNotifyString = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString($"Remembered{RememberedRole}"));
                        break;
                    case Team.Coven:
                        RememberedRole = targetRole == CustomRoles.CovenLeader ? Enum.GetValues<CustomRoles>().FindFirst(x => x.IsCoven() && !x.RoleExist(true), out CustomRoles unusedCovenRole) ? unusedCovenRole : null : targetRole;
                        if (RememberedRole.HasValue) amneNotifyString = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("RememberedCoven"));
                        break;
                }

                break;
        }


        if (!RememberedRole.HasValue)
        {
            amnesiac.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("AmnesiacInvalidTarget")));
            return;
        }

        var sender = CustomRpcSender.Create("Amnesiac.RememberRole", SendOption.Reliable);
        var hasValue = false;

        CustomRoles role = RememberedRole.Value;

        amnesiac.RpcSetCustomRole(role);
        amnesiac.RpcChangeRoleBasis(role);
        
        if (RememberAddons.GetBool()) target.GetCustomSubRoles().ForEach(x => amnesiac.RpcSetCustomRole(x));

        hasValue |= sender.Notify(amnesiac, amneNotifyString);
        hasValue |= sender.Notify(target, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("AmnesiacRemembered")));

        hasValue |= sender.RpcGuardAndKill(target, amnesiac);
        hasValue |= sender.RpcGuardAndKill(target, target);

        sender.SendMessage(!hasValue);

        LateTask.New(() => amnesiac.SetKillCooldown(3f), 0.2f, log: false);
        if (role.IsRecruitingRole()) amnesiac.SetAbilityUseLimit(0);

        if (Utils.HasTasks(amnesiac.Data, false)) amnesiac.GetTaskState().HasTasks = true;

        if (!amnesiac.AmOwner) return;

        switch (role)
        {
            case CustomRoles.Virus:
            case CustomRoles.Cultist:
                Achievements.Type.UnderNewManagement.Complete();
                break;
            case CustomRoles.Deathknight:
            case CustomRoles.Sidekick:
                Achievements.Type.FirstDayOnTheJob.Complete();
                break;
        }
    }

    public override void OnReportDeadBody()
    {
        LocateArrow.RemoveAllTarget(AmnesiacId);
    }

    public override void AfterMeetingTasks()
    {
        LocateArrow.RemoveAllTarget(AmnesiacId);
    }

    public static void OnAnyoneDead(PlayerControl target)
    {
        if (HasArrowsToDeadBodies.GetBool())
        {
            var pos = target.Pos();
            Instances.ForEach(x => LateTask.New(() => LocateArrow.Add(x.AmnesiacId, pos), IRandom.Instance.Next(ArrowMinDelay.GetInt(), ArrowMaxDelay.GetInt() + 1), "Amnesiac Arrow"));
        }
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        ActionButton amneButton = RememberMode.GetValue() == 1 ? hud.KillButton : hud.ReportButton;
        amneButton?.OverrideText(GetString("RememberButtonText"));
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != target.PlayerId || seer.PlayerId != AmnesiacId || meeting || hud) return string.Empty;
        return LocateArrow.GetArrows(seer);
    }
}