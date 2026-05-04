using System.Collections.Generic;

namespace EHR.Roles;

public class Reroll : IAddon
{
    private const int Id = 658400;

    public AddonTypes Type => AddonTypes.Mixed;

    private static OptionItem SameFactionOnly;
    private static OptionItem AnnounceReroll;
    private static OptionItem OnlyHostEnabledRoles;
    private static OptionItem IgnoreVanillaRoles;
    private static OptionItem ConsumeOnSuccess;

    private static readonly HashSet<CustomRoles> AliveRoleDenylist =
    [
        CustomRoles.Sidekick,
        CustomRoles.Deathknight,
        CustomRoles.LovingCrewmate,
        CustomRoles.LovingImpostor,
        CustomRoles.VengefulRomantic,
        CustomRoles.RuthlessRomantic
    ];

    private readonly record struct PendingReroll(Team TeamAtTrigger);

    private static Dictionary<byte, PendingReroll> Pending = [];

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(Id, CustomRoles.Reroll, canSetNum: true, teamSpawnOptions: true);

        SameFactionOnly = new BooleanOptionItem(Id + 10, "Reroll.SameFactionOnly", true, TabGroup.Addons)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Reroll]);

        AnnounceReroll = new BooleanOptionItem(Id + 11, "Reroll.AnnounceReroll", false, TabGroup.Addons)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Reroll]);

        OnlyHostEnabledRoles = new BooleanOptionItem(Id + 12, "Reroll.OnlyHostEnabledRoles", true, TabGroup.Addons)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Reroll]);

        IgnoreVanillaRoles = new BooleanOptionItem(Id + 13, "Reroll.IgnoreVanillaRoles", false, TabGroup.Addons)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Reroll]);

        ConsumeOnSuccess = new BooleanOptionItem(Id + 14, "Reroll.ConsumeOnSuccess", true, TabGroup.Addons)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Reroll]);
    }

    public static void Init()
    {
        Pending = [];
    }

    public static void OnMeetingStart()
    {
        Pending = [];
    }

    public static bool TryQueueCommandTrigger(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost || !player) return false;

        if (!player.Is(CustomRoles.Reroll) || !player.IsAlive() || Silencer.ForSilencer.Contains(player.PlayerId))
        {
            SendPrivateMessage(player, "Reroll.CommandUnavailable");
            return false;
        }

        if (Pending.ContainsKey(player.PlayerId))
        {
            SendPrivateMessage(player, "Reroll.AlreadyQueued");
            return false;
        }

        Pending[player.PlayerId] = new(player.GetTeam());
        SendPrivateMessage(player, "Reroll.Pending");

        return true;
    }

    private static void SendPrivateMessage(PlayerControl player, string key)
    {
        Utils.SendMessage(
            Translator.GetString(key),
            player.PlayerId,
            Utils.ColorString(Utils.GetRoleColor(CustomRoles.Reroll), Translator.GetString("Reroll")),
            importance: MessageImportance.Low
        );
    }

    public static void ResolveAfterMeeting(NetworkedPlayerInfo lastExiled)
    {
        foreach ((byte playerId, PendingReroll pending) in Pending)
        {
            PlayerControl player = Utils.GetPlayerById(playerId);
            if (!player) continue;

            bool changed = false;
            bool wasExiled = lastExiled && lastExiled.PlayerId == playerId;

            if (!wasExiled && player.IsAlive())
                changed = TryResolveAliveRole(player, pending.TeamAtTrigger);

            switch (changed)
            {
                case true when ConsumeOnSuccess.GetBool():
                    Main.PlayerStates[playerId].RemoveSubRole(CustomRoles.Reroll);
                    break;
                case false:
                    player.Notify(Translator.GetString("Reroll.Failed"));
                    break;
            }
        }
        
        Pending.Clear();
    }

    private static bool TryResolveAliveRole(PlayerControl player, Team teamAtTrigger)
    {
        List<CustomRoles> pool = GetAliveRolePool(player, teamAtTrigger);
        if (pool.Count == 0) return false;

        CustomRoles newRole = pool.RandomElement();
        player.RpcSetCustomRole(newRole);
        player.RpcChangeRoleBasis(newRole);
        RemoveIncompatibleSubRoles(player);
        NotifySuccess(player);
        return true;
    }

    private static void NotifySuccess(PlayerControl player)
    {
        if (AnnounceReroll.GetBool())
        {
            LateTask.New(() =>
                Utils.SendMessage(
                    string.Format(Translator.GetString("Reroll.Announce"), player.GetRealName()),
                    title: Utils.ColorString(Utils.GetRoleColor(CustomRoles.Reroll), Translator.GetString("Reroll")),
                    importance: MessageImportance.High
                ), 3.5f, "Reroll public announcement");
        }

        player.Notify(Translator.GetString("Reroll.SuccessAlive"));
    }

    private static List<CustomRoles> GetAliveRolePool(PlayerControl player, Team teamAtTrigger)
    {
        CustomRoles currentRole = player.GetCustomRole();
        bool onlyHostEnabledRoles = OnlyHostEnabledRoles.GetBool();
        bool ignoreVanillaRoles = IgnoreVanillaRoles.GetBool();
        bool sameFactionOnly = SameFactionOnly.GetBool();
        List<CustomRoles> pool = new(Main.CustomRoleValues.Length);

        foreach (CustomRoles role in Main.CustomRoleValues)
        {
            if (role >= CustomRoles.NotAssigned || role == CustomRoles.GM || role == currentRole) continue;
            if (role.IsGhostRole() || role.IsForOtherGameMode() || role.IsNotAssignableMidGame()) continue;
            if (AliveRoleDenylist.Contains(role)) continue;
            if (ignoreVanillaRoles && role.IsVanilla()) continue;
            if (onlyHostEnabledRoles && role.GetMode() == 0) continue;
            if (sameFactionOnly && role.GetTeam() != teamAtTrigger) continue;

            pool.Add(role);
        }

        return pool;
    }

    private static void RemoveIncompatibleSubRoles(PlayerControl player)
    {
        if (!Main.PlayerStates.TryGetValue(player.PlayerId, out PlayerState state)) return;

        foreach (CustomRoles subRole in state.SubRoles.ToArray())
        {
            int index = state.SubRoles.IndexOf(subRole);
            if (index == -1) continue;

            state.SubRoles.RemoveAt(index);
            bool isCompatible = CustomRolesHelper.CheckAddonConflict(subRole, player, true);
            state.SubRoles.Insert(index, subRole);

            if (!isCompatible)
                state.RemoveSubRole(subRole);
        }
    }
}
