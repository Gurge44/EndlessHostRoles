using AmongUs.GameOptions;
using Hazel;
using System;
using System.Collections.Generic;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Neutral;

public static class Amnesiac
{
    private static readonly int Id = 35000;
    private static List<byte> playerIdList = [];

    public static OptionItem RememberCooldown;
    public static OptionItem RefugeeKillCD;
    public static OptionItem IncompatibleNeutralMode;
    public static readonly string[] amnesiacIncompatibleNeutralMode =
    [
        "Role.Amnesiac", // 0
        "Role.Pursuer",  // 1
        "Role.Follower", // 2
        "Role.Maverick", // 3
    ];

    private static int RememberLimit;

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Amnesiac);
        RememberCooldown = FloatOptionItem.Create(Id + 10, "RememberCooldown", new(0f, 180f, 2.5f), 5f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Amnesiac])
            .SetValueFormat(OptionFormat.Seconds);
        RefugeeKillCD = FloatOptionItem.Create(Id + 11, "RefugeeKillCD", new(0f, 180f, 2.5f), 25f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Amnesiac])
            .SetValueFormat(OptionFormat.Seconds);
        IncompatibleNeutralMode = StringOptionItem.Create(Id + 12, "IncompatibleNeutralMode", amnesiacIncompatibleNeutralMode, 0, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Amnesiac]);
    }
    public static void Init()
    {
        playerIdList = [];
        RememberLimit = new();
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        RememberLimit = 1;

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }
    public static bool IsEnable => playerIdList.Count > 0;

    private static void SendRPC()
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetRememberLimit, SendOption.Reliable);
        writer.Write(RememberLimit);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ReceiveRPC(MessageReader reader) => RememberLimit = reader.ReadInt32();
    public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = RememberLimit >= 1 ? RememberCooldown.GetFloat() : 300f;
    public static bool CanUseKillButton(PlayerControl player) => !player.Data.IsDead && RememberLimit >= 1;
    public static void OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (RememberLimit < 1) return;

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
            string roleString = amnesiacIncompatibleNeutralMode[IncompatibleNeutralMode.GetValue()][6..];
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

        RememberLimit--;
        SendRPC();

        killer.RpcSetCustomRole(role);

        killer.Notify(killerNotifyString);
        target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("AmnesiacRemembered")));

        Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
        Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer);

        Utils.AddRoles(killer.PlayerId, role);

        killer.ResetKillCooldown();
        killer.SetKillCooldown();

        target.RpcGuardAndKill(killer);
        target.RpcGuardAndKill(target);
    }
    public static bool KnowRole(PlayerControl player, PlayerControl target)
    {
        if (player.IsNeutralKiller() && target.IsNeutralKiller() && player.GetCustomRole() == target.GetCustomRole()) return true;
        if (player.Is(CustomRoles.Refugee) && target.Is(CustomRoleTypes.Impostor)) return true;
        if (player.Is(CustomRoleTypes.Impostor) && target.Is(CustomRoles.Refugee)) return true;
        return false;
    }
}
