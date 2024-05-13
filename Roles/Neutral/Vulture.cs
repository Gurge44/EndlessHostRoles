using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using UnityEngine;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Roles.Neutral;

public class Vulture : RoleBase
{
    private const int Id = 11600;
    private static List<byte> playerIdList = [];

    public static List<byte> UnreportablePlayers = [];

    public static OptionItem ArrowsPointingToDeadBody;
    public static OptionItem NumberOfReportsToWin;
    public static OptionItem CanVent;
    public static OptionItem VultureReportCD;
    public static OptionItem MaxEaten;
    public static OptionItem HasImpVision;
    public static OptionItem ChangeRoleWhenCantWin;
    public static OptionItem ChangeRole;

    private static readonly CustomRoles[] ChangeRoles =
    [
        CustomRoles.Amnesiac,
        CustomRoles.Pursuer,
        CustomRoles.Maverick,
        CustomRoles.Totocalcio,
        CustomRoles.Opportunist,
        CustomRoles.Crewmate,
        CustomRoles.Jester
    ];

    private int BodyReportCount;
    private long LastReport;

    public override bool IsEnable => playerIdList.Count > 0;

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Vulture);
        ArrowsPointingToDeadBody = BooleanOptionItem.Create(Id + 10, "VultureArrowsPointingToDeadBody", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Vulture]);
        NumberOfReportsToWin = IntegerOptionItem.Create(Id + 11, "VultureNumberOfReportsToWin", new(1, 10, 1), 4, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Vulture]);
        CanVent = BooleanOptionItem.Create(Id + 12, "CanVent", true, TabGroup.NeutralRoles, true)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Vulture]);
        VultureReportCD = FloatOptionItem.Create(Id + 13, "VultureReportCooldown", new(0f, 180f, 2.5f), 15f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Vulture])
            .SetValueFormat(OptionFormat.Seconds);
        MaxEaten = IntegerOptionItem.Create(Id + 14, "VultureMaxEatenInOneRound", new(1, 10, 1), 2, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Vulture]);
        HasImpVision = BooleanOptionItem.Create(Id + 15, "ImpostorVision", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Vulture]);
        ChangeRoleWhenCantWin = BooleanOptionItem.Create(Id + 16, "VultureChangeRoleWhenCantWin", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Vulture]);
        ChangeRole = StringOptionItem.Create(Id + 17, "VultureChangeRole", ChangeRoles.Select(x => x.ToColoredString()).ToArray(), 0, TabGroup.NeutralRoles, noTranslation: true)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Vulture]);
    }

    public override void Init()
    {
        playerIdList = [];
        UnreportablePlayers = [];
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        BodyReportCount = 0;
        playerId.SetAbilityUseLimit(MaxEaten.GetInt());
        LastReport = Utils.TimeStamp;
        _ = new LateTask(() =>
        {
            if (GameStates.IsInTask)
            {
                Utils.GetPlayerById(playerId).Notify(GetString("VultureCooldownUp"));
            }
        }, VultureReportCD.GetFloat() + 8f, "Vulture CD");
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        opt.SetVision(HasImpVision.GetBool());
        AURoleOptions.EngineerCooldown = 0f;
        AURoleOptions.EngineerInVentMaxTime = 0f;
    }

    public static void Clear()
    {
        foreach (byte apc in playerIdList)
        {
            LocateArrow.RemoveAllTarget(apc);
        }
    }

    public override void AfterMeetingTasks()
    {
        Clear();
        foreach (byte apc in playerIdList)
        {
            var player = Utils.GetPlayerById(apc);
            if (player == null) continue;
            if (player.IsAlive())
            {
                apc.SetAbilityUseLimit(MaxEaten.GetInt());
                LastReport = Utils.TimeStamp;
                _ = new LateTask(() =>
                {
                    if (GameStates.IsInTask)
                    {
                        Utils.GetPlayerById(apc).Notify(GetString("VultureCooldownUp"));
                    }
                }, VultureReportCD.GetFloat(), "Vulture CD");
                Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player);
            }
        }
    }

    public static void OnPlayerDead(PlayerControl target)
    {
        if (!ArrowsPointingToDeadBody.GetBool() || target.Data.Disconnected) return;

        foreach (byte pc in playerIdList)
        {
            var player = Utils.GetPlayerById(pc);
            if (player == null || !player.IsAlive()) continue;
            LocateArrow.Add(pc, target.transform.position);
            Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player);
        }
    }

    public override bool CheckReportDeadBody(PlayerControl pc, GameData.PlayerInfo target, PlayerControl killer)
    {
        if (pc.GetAbilityUseLimit() <= 0) return true;
        if (Utils.TimeStamp - LastReport < VultureReportCD.GetFloat()) return true;

        BodyReportCount++;
        pc.RpcRemoveAbilityUse();
        Logger.Msg($"target.object {target.Object}, is null? {target.Object == null}", "VultureNull");
        if (target.Object != null)
        {
            foreach (byte apc in playerIdList)
            {
                LocateArrow.Remove(apc, target.Object.transform.position);
            }
        }

        pc.Notify(GetString("VultureBodyReported"));
        UnreportablePlayers.Remove(target.PlayerId);
        UnreportablePlayers.Add(target.PlayerId);

        return false;
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool m = false)
    {
        if (!seer.Is(CustomRoles.Vulture)) return string.Empty;
        if (target != null && seer.PlayerId != target.PlayerId) return string.Empty;
        return GameStates.IsMeeting ? string.Empty : Utils.ColorString(Color.white, LocateArrow.GetArrows(seer));
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        var playerId = pc.PlayerId;
        if (BodyReportCount >= NumberOfReportsToWin.GetInt() && GameStates.IsInTask)
        {
            BodyReportCount = NumberOfReportsToWin.GetInt();
            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Vulture);
            CustomWinnerHolder.WinnerIds.Add(playerId);
            return;
        }

        if (ChangeRoleWhenCantWin.GetBool() && Main.AllAlivePlayerControls.Length <= (NumberOfReportsToWin.GetInt() - BodyReportCount))
        {
            var role = ChangeRoles[ChangeRole.GetValue()];
            pc.RpcSetCustomRole(role);
        }
    }
}