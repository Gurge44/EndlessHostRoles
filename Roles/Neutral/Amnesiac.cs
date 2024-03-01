using AmongUs.GameOptions;
using System;
using System.Collections.Generic;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Neutral;

public class Amnesiac : RoleBase
{
    private const int Id = 35000;
    private static List<byte> playerIdList = [];

    public static OptionItem RememberCooldown;
    public static OptionItem RefugeeKillCD;
    public static OptionItem IncompatibleNeutralMode;
    private static OptionItem CanVent;
    public static OptionItem RememberMode;

    public static readonly string[] AmnesiacIncompatibleNeutralMode =
    [
        "Role.Amnesiac", // 0
        "Role.Pursuer", // 1
        "Role.Follower", // 2
        "Role.Maverick", // 3
    ];

    private static readonly string[] RememberModes =
    [
        "AmnesiacRM.ByKillButton",
        "AmnesiacRM.ByReportingBody"
    ];

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Amnesiac);
        RememberMode = StringOptionItem.Create(Id + 9, "RememberMode", RememberModes, 0, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Amnesiac]);
        RememberCooldown = FloatOptionItem.Create(Id + 10, "RememberCooldown", new(0f, 180f, 2.5f), 5f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Amnesiac])
            .SetValueFormat(OptionFormat.Seconds);
        RefugeeKillCD = FloatOptionItem.Create(Id + 11, "RefugeeKillCD", new(0f, 180f, 2.5f), 25f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Amnesiac])
            .SetValueFormat(OptionFormat.Seconds);
        IncompatibleNeutralMode = StringOptionItem.Create(Id + 12, "IncompatibleNeutralMode", AmnesiacIncompatibleNeutralMode, 0, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Amnesiac]);
        CanVent = BooleanOptionItem.Create(Id + 13, "CanVent", false, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Amnesiac]);
    }

    public override void Init()
    {
        playerIdList = [];
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        playerId.SetAbilityUseLimit(1);

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }

    public override bool IsEnable => playerIdList.Count > 0;

    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = id.GetAbilityUseLimit() >= 1 ? RememberCooldown.GetFloat() : 300f;
    public override bool CanUseKillButton(PlayerControl player) => !player.Data.IsDead && player.GetAbilityUseLimit() >= 1;
    public override bool CanUseImpostorVentButton(PlayerControl pc) => CanVent.GetBool();
    public override void ApplyGameOptions(IGameOptions opt, byte playerId) => opt.SetVision(false);

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (RememberMode.GetValue() == 0)
        {
            RememberRole(killer, target);
        }

        return false;
    }

    public override bool CheckReportDeadBody(PlayerControl reporter, GameData.PlayerInfo target, PlayerControl killer)
    {
        if (RememberMode.GetValue() == 1)
        {
            RememberRole(reporter, target.Object);
            return false;
        }

        return true;
    }

    public static void RememberRole(PlayerControl killer, PlayerControl target)
    {
        if (killer.GetAbilityUseLimit() < 1) return;

        CustomRoles? RememberedRole = null;
        string killerNotifyString = string.Empty;

        CustomRoles targetRole = target.GetCustomRole();

        if (targetRole == CustomRoles.Jackal)
        {
            RememberedRole = CustomRoles.Sidekick;
            killerNotifyString = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("RememberedNeutralKiller"));
        }

        else if (targetRole.IsNK())
        {
            RememberedRole = targetRole;
            killerNotifyString = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("RememberedNeutralKiller"));
        }

        else if (targetRole.IsNonNK())
        {
            string roleString = AmnesiacIncompatibleNeutralMode[IncompatibleNeutralMode.GetValue()][6..];
            RememberedRole = (CustomRoles)Enum.Parse(typeof(CustomRoles), roleString);
            killerNotifyString = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString($"Remembered{roleString}"));
        }

        else if (target.Is(Team.Impostor))
        {
            RememberedRole = CustomRoles.Refugee;
            killerNotifyString = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("RememberedImpostor"));
        }

        else if (target.Is(Team.Crewmate))
        {
            RememberedRole = targetRole.GetDYRole() == RoleTypes.Impostor ? targetRole : CustomRoles.Sheriff;
            killerNotifyString = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("RememberedCrewmate"));
        }


        if (RememberedRole == null)
        {
            killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("AmnesiacInvalidTarget")));
            return;
        }

        var role = (CustomRoles)RememberedRole;

        killer.RpcRemoveAbilityUse();

        killer.RpcSetCustomRole(role);

        killer.Notify(killerNotifyString);
        target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("AmnesiacRemembered")));

        Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
        Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer);

        killer.ResetKillCooldown();
        killer.SetKillCooldown();

        target.RpcGuardAndKill(killer);
        target.RpcGuardAndKill(target);
    }

    public static bool KnowRole(PlayerControl player, PlayerControl target)
    {
        if (player.IsNeutralKiller() && target.IsNeutralKiller() && player.GetCustomRole() == target.GetCustomRole()) return true;
        if (player.Is(CustomRoles.Refugee) && target.Is(CustomRoleTypes.Impostor)) return true;
        return player.Is(CustomRoleTypes.Impostor) && target.Is(CustomRoles.Refugee);
    }
}