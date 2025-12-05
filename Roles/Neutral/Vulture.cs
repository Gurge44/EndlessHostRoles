using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using UnityEngine;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Neutral;

public class Vulture : RoleBase
{
    private const int Id = 11600;
    private static List<byte> PlayerIdList = [];

    public static HashSet<byte> UnreportablePlayers = [];

    private static OptionItem ArrowsPointingToDeadBody;
    private static OptionItem NumberOfReportsToWin;
    public static OptionItem CanVent;
    private static OptionItem VentCooldown;
    private static OptionItem MaxInVentTime;
    private static OptionItem VultureReportCD;
    private static OptionItem MaxEaten;
    private static OptionItem HasImpVision;
    private static OptionItem ChangeRoleWhenCantWin;
    private static OptionItem ChangeRole;

    private static readonly CustomRoles[] ChangeRoles =
    [
        CustomRoles.Amnesiac,
        CustomRoles.Pursuer,
        CustomRoles.Maverick,
        CustomRoles.Follower,
        CustomRoles.Opportunist,
        CustomRoles.Crewmate,
        CustomRoles.Jester
    ];

    private long LastNotifyTS;
    private long CooldownFinishTS;
    private int TotalEaten;
    private byte VultureId;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override bool SeesArrowsToDeadBodies => ArrowsPointingToDeadBody.GetBool();

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Vulture);

        ArrowsPointingToDeadBody = new BooleanOptionItem(Id + 10, "VultureArrowsPointingToDeadBody", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Vulture]);

        NumberOfReportsToWin = new IntegerOptionItem(Id + 11, "VultureNumberOfReportsToWin", new(1, 10, 1), 4, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Vulture]);

        CanVent = new BooleanOptionItem(Id + 12, "CanVent", true, TabGroup.NeutralRoles, true)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Vulture]);

        VentCooldown = new FloatOptionItem(Id + 18, "VentCooldown", new(0f, 60f, 0.5f), 15f, TabGroup.NeutralRoles)
            .SetParent(CanVent)
            .SetValueFormat(OptionFormat.Seconds);

        MaxInVentTime = new FloatOptionItem(Id + 19, "MaxInVentTime", new(0f, 300f, 0.5f), 5f, TabGroup.NeutralRoles)
            .SetParent(CanVent)
            .SetValueFormat(OptionFormat.Seconds);

        VultureReportCD = new FloatOptionItem(Id + 13, "VultureReportCooldown", new(0f, 180f, 1f), 20f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Vulture])
            .SetValueFormat(OptionFormat.Seconds);

        MaxEaten = new IntegerOptionItem(Id + 14, "VultureMaxEatenInOneRound", new(1, 10, 1), 2, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Vulture]);

        HasImpVision = new BooleanOptionItem(Id + 15, "ImpostorVision", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Vulture]);

        ChangeRoleWhenCantWin = new BooleanOptionItem(Id + 16, "VultureChangeRoleWhenCantWin", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Vulture]);

        ChangeRole = new StringOptionItem(Id + 17, "VultureChangeRole", ChangeRoles.Select(x => x.ToColoredString()).ToArray(), 0, TabGroup.NeutralRoles, noTranslation: true)
            .SetParent(ChangeRoleWhenCantWin);
    }

    public override void Init()
    {
        PlayerIdList = [];
        UnreportablePlayers = [];
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);

        TotalEaten = 0;
        playerId.SetAbilityUseLimit(MaxEaten.GetFloat());
        CooldownFinishTS = Utils.TimeStamp;
        LastNotifyTS = Utils.TimeStamp;

        VultureId = playerId;
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        opt.SetVision(HasImpVision.GetBool());

        AURoleOptions.EngineerCooldown = VentCooldown.GetFloat();
        AURoleOptions.EngineerInVentMaxTime = MaxInVentTime.GetFloat();
    }

    public override void OnReportDeadBody()
    {
        LocateArrow.RemoveAllTarget(VultureId);
    }

    public override void AfterMeetingTasks()
    {
        LocateArrow.RemoveAllTarget(VultureId);

        foreach (byte id in PlayerIdList)
        {
            PlayerControl pc = Utils.GetPlayerById(id);
            if (pc == null) continue;

            if (pc.IsAlive())
            {
                id.SetAbilityUseLimit(MaxEaten.GetInt());
                CooldownFinishTS = Utils.TimeStamp + VultureReportCD.GetInt();
            }
        }
    }

    public override bool CheckReportDeadBody(PlayerControl pc, NetworkedPlayerInfo target, PlayerControl killer)
    {
        if (pc.GetAbilityUseLimit() < 1f || target.Object == null || target.Object.Is(CustomRoles.Disregarded)) return true;

        if (CooldownFinishTS > Utils.TimeStamp) return true;

        pc.RPCPlayCustomSound("Eat");
        TotalEaten++;

        if (TotalEaten >= NumberOfReportsToWin.GetInt() && GameStates.IsInTask)
        {
            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Vulture);
            CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
            return false;
        }

        pc.RpcRemoveAbilityUse();
        CooldownFinishTS = Utils.TimeStamp + VultureReportCD.GetInt();

        Vector2 bodyPos = Object.FindObjectsOfType<DeadBody>().First(x => x.ParentId == target.PlayerId).TruePosition;
        foreach (byte seerId in Main.PlayerStates.Keys) LocateArrow.Remove(seerId, bodyPos);

        pc.Notify(GetString("VultureBodyReported"));
        UnreportablePlayers.Add(target.PlayerId);

        return false;
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != VultureId || seer.PlayerId != target.PlayerId || hud || meeting) return string.Empty;
        string arrows = Utils.ColorString(Color.white, LocateArrow.GetArrows(seer));
        bool hasCooldown = CooldownFinishTS > Utils.TimeStamp;
        if (arrows.Length > 0 && hasCooldown) arrows += "\n";
        if (hasCooldown) arrows += string.Format(GetString("CDPT"), CooldownFinishTS - Utils.TimeStamp);
        return arrows;
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!pc.IsAlive() || !Main.IntroDestroyed) return;

        if (ChangeRoleWhenCantWin.GetBool() && Main.AllAlivePlayerControls.Length - 1 <= NumberOfReportsToWin.GetInt() - TotalEaten)
        {
            CustomRoles role = ChangeRoles[ChangeRole.GetValue()];
            pc.RpcSetCustomRole(role);
            pc.RpcChangeRoleBasis(role);
            return;
        }

        long now = Utils.TimeStamp;

        if (now != LastNotifyTS)
        {
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
            LastNotifyTS = now;
        }
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        hud.ReportButton?.OverrideText(GetString("VultureEatButtonText"));
    }
}