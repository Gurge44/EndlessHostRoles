using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.AddOns.Common;
using EHR.AddOns.Crewmate;
using EHR.AddOns.GhostRoles;
using EHR.Crewmate;
using EHR.Modules;
using EHR.Neutral;
using InnerNet;

namespace EHR;

public class PlayerState(byte playerId)
{
    public enum DeathReason
    {
        Kill,
        Vote,
        Suicide,
        Spell,
        Curse,
        FollowingSuicide,
        Bite,
        Poison,
        Bombed,
        Misfire,
        Torched,
        Sniped,
        Revenge,
        Execution,
        Disconnected,
        Fall,
        AFK,

        // EHR
        Gambled,
        Eaten,
        Sacrifice,
        Quantization,
        Overtired,
        Ashamed,
        PissedOff,
        Dismembered,
        LossOfHead,
        Trialed,
        Infected,
        Demolished,
        YinYanged,
        Kamikazed,
        RNG,
        WrongAnswer,
        Consumed,
        BadLuck,
        Asthma,
        Assumed,
        Negotiation,
        Trapped,
        Stung,
        Scavenged,

        // Natural Disasters
        Meteor,
        Lava,
        Tornado,
        Lightning,
        Drowned,
        Sunken,
        Collapsed,

        etc = -1
    }

    public readonly PlayerControl Player = Utils.GetPlayerById(playerId, fast: false);

    private readonly byte PlayerId = playerId;
    public readonly List<CustomRoles> SubRoles = [];
    public readonly Dictionary<byte, string> TargetColorData = [];
    public CountTypes countTypes = CountTypes.Crew;
    public PlainShipRoom LastRoom;
    public CustomRoles MainRole = CustomRoles.NotAssigned;
    public (DateTime TimeStamp, byte ID) RealKiller = (DateTime.MinValue, byte.MaxValue);
    public RoleBase Role = new VanillaRole();
    public bool IsDead { get; set; }
#pragma warning disable IDE1006 // Naming Styles
    // ReSharper disable once InconsistentNaming
    public DeathReason deathReason { get; set; } = DeathReason.etc;
#pragma warning restore IDE1006 // Naming Styles
    public bool IsBlackOut { get; set; }

    public bool IsSuicide => deathReason == DeathReason.Suicide;
    public TaskState TaskState { get; set; } = new();

    public void SetMainRole(CustomRoles role)
    {
        countTypes = role.GetCountTypes();

        if (SubRoles.Contains(CustomRoles.Recruit))
        {
            countTypes = Jackal.SidekickCountMode.GetValue() switch
            {
                0 => CountTypes.Jackal,
                1 => CountTypes.OutOfGame,
                _ => role.GetCountTypes()
            };
        }

        if (CustomTeamManager.GetCustomTeam(PlayerId) != null && !CustomTeamManager.IsSettingEnabledForPlayerTeam(PlayerId, CTAOption.WinWithOriginalTeam))
            countTypes = CountTypes.CustomTeam;

        SubRoles.ForEach(SetAddonCountTypes);

        if (!Player.HasKillButton() && role == CustomRoles.Refugee)
            Player.RpcChangeRoleBasis(CustomRoles.Refugee);

        Role = role.GetRoleClass();

        if (!role.RoleExist(countDead: true))
        {
            Role.Init();
        }

        MainRole = role;

        Role.Add(PlayerId);

        Logger.Info($"ID {PlayerId} ({Player?.GetRealName()}) => {role}, CountTypes => {countTypes}", "SetMainRole");

        if (!AmongUsClient.Instance.AmHost) return;

        if (!Main.HasJustStarted)
        {
            Player.ResetKillCooldown();
            Player.SyncSettings();
            Utils.NotifyRoles(SpecifySeer: Player);
            Utils.NotifyRoles(SpecifyTarget: Player);
            if (PlayerId == PlayerControl.LocalPlayer.PlayerId && GameStates.IsInTask)
            {
                HudManager.Instance.SetHudActive(true);
                RemoveDisableDevicesPatch.UpdateDisableDevices();
            }

            if (Lyncher.On) Lyncher.Instances.ForEach(x => x.OnRoleChange(PlayerId));
            if (!role.Is(Team.Impostor)) SubRoles.ToArray().DoIf(x => x.IsImpOnlyAddon(), RemoveSubRole);
            if (role is CustomRoles.Sidekick or CustomRoles.Necromancer or CustomRoles.Deathknight or CustomRoles.Refugee) SubRoles.ToArray().DoIf(StartGameHostPatch.BasisChangingAddons.ContainsKey, RemoveSubRole);
            if (role == CustomRoles.Sidekick && Jackal.Instances.FindFirst(x => x.SidekickId == byte.MaxValue || x.SidekickId.GetPlayer() == null, out var jackal))
                jackal.SidekickId = PlayerId;

            LateTask.New(() => Player.CheckAndSetUnshiftState(), 1f, log: false);
        }

        CheckMurderPatch.TimeSinceLastKill.Remove(PlayerId);
    }

    public void SetSubRole(CustomRoles role, bool AllReplace = false)
    {
        if (role == CustomRoles.Cleansed)
            AllReplace = true;
        if (AllReplace)
        {
            SubRoles.Clear();
            Utils.SendRPC(CustomRPC.RemoveSubRole, PlayerId, 2);
        }

        if (!SubRoles.Contains(role))
            SubRoles.Add(role);

        SetAddonCountTypes(role);

        Logger.Info($" ID {PlayerId} ({Player?.GetRealName()}) => {role}, CountTypes => {countTypes}", "SetSubRole");
    }

    private void SetAddonCountTypes(CustomRoles role)
    {
        switch (role)
        {
            case CustomRoles.Bloodlust:
                countTypes = CountTypes.Bloodlust;
                SubRoles.Remove(CustomRoles.Stressed);
                break;
            case CustomRoles.Madmate:
                TaskState.HasTasks = false;
                TaskState.AllTasksCount = 0;
                countTypes = Options.MadmateCountMode.GetInt() switch
                {
                    0 => CountTypes.OutOfGame,
                    1 => CountTypes.Impostor,
                    2 => CountTypes.Crew,
                    _ => throw new NotImplementedException()
                };
                SubRoles.Remove(CustomRoles.Charmed);
                SubRoles.Remove(CustomRoles.Recruit);
                SubRoles.Remove(CustomRoles.Contagious);
                SubRoles.Remove(CustomRoles.Rascal);
                SubRoles.Remove(CustomRoles.Loyal);
                SubRoles.Remove(CustomRoles.Undead);
                SubRoles.Remove(CustomRoles.Stressed);
                Utils.NotifyRoles(SpecifySeer: Player);
                Utils.NotifyRoles(SpecifyTarget: Player);
                break;
            case CustomRoles.Charmed:
                TaskState.HasTasks = false;
                TaskState.AllTasksCount = 0;
                countTypes = Succubus.CharmedCountMode.GetInt() switch
                {
                    0 => CountTypes.OutOfGame,
                    1 => CountTypes.Succubus,
                    2 => countTypes,
                    _ => throw new NotImplementedException()
                };
                SubRoles.Remove(CustomRoles.Madmate);
                SubRoles.Remove(CustomRoles.Recruit);
                SubRoles.Remove(CustomRoles.Contagious);
                SubRoles.Remove(CustomRoles.Rascal);
                SubRoles.Remove(CustomRoles.Loyal);
                SubRoles.Remove(CustomRoles.Undead);
                SubRoles.Remove(CustomRoles.Stressed);
                Utils.NotifyRoles(SpecifySeer: Player);
                Utils.NotifyRoles(SpecifyTarget: Player);
                break;
            case CustomRoles.Undead:
                TaskState.HasTasks = false;
                TaskState.AllTasksCount = 0;
                countTypes = Necromancer.UndeadCountMode.GetInt() switch
                {
                    0 => CountTypes.OutOfGame,
                    1 => CountTypes.Necromancer,
                    2 => countTypes,
                    _ => throw new NotImplementedException()
                };
                SubRoles.Remove(CustomRoles.Madmate);
                SubRoles.Remove(CustomRoles.Recruit);
                SubRoles.Remove(CustomRoles.Contagious);
                SubRoles.Remove(CustomRoles.Rascal);
                SubRoles.Remove(CustomRoles.Loyal);
                SubRoles.Remove(CustomRoles.Charmed);
                SubRoles.Remove(CustomRoles.Stressed);
                Utils.NotifyRoles(SpecifySeer: Player);
                Utils.NotifyRoles(SpecifyTarget: Player);
                break;
            case CustomRoles.LastImpostor:
                SubRoles.Remove(CustomRoles.Mare);
                break;
            case CustomRoles.Recruit:
                TaskState.HasTasks = false;
                TaskState.AllTasksCount = 0;
                countTypes = Jackal.SidekickCountMode.GetInt() switch
                {
                    0 => CountTypes.Jackal,
                    1 => CountTypes.OutOfGame,
                    2 => countTypes,
                    _ => throw new NotImplementedException()
                };
                SubRoles.Remove(CustomRoles.Madmate);
                SubRoles.Remove(CustomRoles.Charmed);
                SubRoles.Remove(CustomRoles.Contagious);
                SubRoles.Remove(CustomRoles.Rascal);
                SubRoles.Remove(CustomRoles.Loyal);
                SubRoles.Remove(CustomRoles.Loyal);
                SubRoles.Remove(CustomRoles.Undead);
                Utils.NotifyRoles(SpecifySeer: Player);
                Utils.NotifyRoles(SpecifyTarget: Player);
                break;
            case CustomRoles.Contagious:
                TaskState.HasTasks = false;
                TaskState.AllTasksCount = 0;
                countTypes = Virus.ContagiousCountMode.GetInt() switch
                {
                    0 => CountTypes.OutOfGame,
                    1 => CountTypes.Virus,
                    2 => countTypes,
                    _ => throw new NotImplementedException()
                };
                SubRoles.Remove(CustomRoles.Madmate);
                SubRoles.Remove(CustomRoles.Recruit);
                SubRoles.Remove(CustomRoles.Charmed);
                SubRoles.Remove(CustomRoles.Rascal);
                SubRoles.Remove(CustomRoles.Loyal);
                SubRoles.Remove(CustomRoles.Undead);
                SubRoles.Remove(CustomRoles.Stressed);
                Utils.NotifyRoles(SpecifySeer: Player);
                Utils.NotifyRoles(SpecifyTarget: Player);
                break;
        }
    }

    public void RemoveSubRole(CustomRoles role)
    {
        SubRoles.Remove(role);

        if (role is CustomRoles.Flashman or CustomRoles.Dynamo or CustomRoles.Spurt)
        {
            Main.AllPlayerSpeed[PlayerId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
            PlayerGameOptionsSender.SetDirty(PlayerId);
        }

        Utils.SendRPC(CustomRPC.RemoveSubRole, PlayerId, 1, (int)role);
    }

    public void SetDead()
    {
        IsDead = true;
        if (AmongUsClient.Instance.AmHost)
        {
            RPC.SendDeathReason(PlayerId, deathReason);
            Utils.CheckAndSpawnAdditionalRefugee(Utils.GetPlayerInfoById(PlayerId));
        }
    }

    public void InitTask(PlayerControl player) => TaskState.Init(player);
    public void UpdateTask(PlayerControl player) => TaskState.Update(player);

    public byte GetRealKiller() => IsDead && RealKiller.TimeStamp != DateTime.MinValue ? RealKiller.ID : byte.MaxValue;
    public int GetKillCount(bool ExcludeSelfKill = false) => Main.PlayerStates.Values.Where(state => !(ExcludeSelfKill && state.PlayerId == PlayerId) && state.GetRealKiller() == PlayerId).ToArray().Length;
}

public class TaskState
{
    public static int InitialTotalTasks;
    public int AllTasksCount = -1;
    public int CompletedTasksCount;
    public bool HasTasks;
    public int RemainingTasksCount => AllTasksCount - CompletedTasksCount;
    public bool IsTaskFinished => RemainingTasksCount <= 0 && HasTasks;

    public void Init(PlayerControl player)
    {
        Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags()}: InitTask", "TaskState.Init");
        if (player == null || player.Data?.Tasks == null) return;
        if (!Utils.HasTasks(player.Data, false))
        {
            AllTasksCount = 0;
            return;
        }

        HasTasks = true;
        AllTasksCount = player.Data.Tasks.Count;
        Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags()}: TaskCounts = {CompletedTasksCount}/{AllTasksCount}", "TaskState.Init");
    }

    public void Update(PlayerControl player)
    {
        Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags().RemoveHtmlTags()}: UpdateTask", "TaskState.Update");
        GameData.Instance.RecomputeTaskCounts();
        Logger.Info($"TotalTaskCounts = {GameData.Instance.CompletedTasks}/{GameData.Instance.TotalTasks}", "TaskState.Update");

        if (AllTasksCount == -1) Init(player);

        if (!HasTasks) return;

        if (AmongUsClient.Instance.AmHost)
        {
            bool alive = player.IsAlive();

            if (alive && Options.CurrentGameMode == CustomGameMode.Speedrun)
            {
                if (CompletedTasksCount + 1 >= AllTasksCount) SpeedrunManager.OnTaskFinish(player);
                SpeedrunManager.ResetTimer(player);
            }

            // Ability Use Gain with this task completed
            if (alive && !Main.HasJustStarted)
            {
                switch (player.GetCustomRole())
                {
                    case CustomRoles.SabotageMaster:
                        if (Main.PlayerStates[player.PlayerId].Role is not SabotageMaster sm) break;
                        sm.UsedSkillCount -= SabotageMaster.SmAbilityUseGainWithEachTaskCompleted.GetFloat();
                        sm.SendRPC();
                        break;
                    case CustomRoles.NiceHacker:
                        if (!player.IsModClient() && NiceHacker.UseLimit.ContainsKey(player.PlayerId)) NiceHacker.UseLimit[player.PlayerId] += NiceHacker.NiceHackerAbilityUseGainWithEachTaskCompleted.GetFloat();
                        else if (NiceHacker.UseLimitSeconds.ContainsKey(player.PlayerId)) NiceHacker.UseLimitSeconds[player.PlayerId] += NiceHacker.NiceHackerAbilityUseGainWithEachTaskCompleted.GetInt() * NiceHacker.ModdedClientAbilityUseSecondsMultiplier.GetInt();
                        if (NiceHacker.UseLimitSeconds.ContainsKey(player.PlayerId)) NiceHacker.SendRPC(player.PlayerId, NiceHacker.UseLimitSeconds[player.PlayerId]);
                        break;
                }

                float add = Utils.GetSettingNameAndValueForRole(player.GetCustomRole(), "AbilityUseGainWithEachTaskCompleted");
                if (Math.Abs(add - float.MaxValue) > 0.5f && add > 0) player.RpcIncreaseAbilityUseLimitBy(add);
            }

            try
            {
                Main.PlayerStates[player.PlayerId].Role.OnTaskComplete(player, CompletedTasksCount, AllTasksCount);
            }
            catch (Exception e)
            {
                Utils.ThrowException(e);
            }

            var addons = Main.PlayerStates[player.PlayerId].SubRoles;

            if (addons.Contains(CustomRoles.Deadlined)) Deadlined.SetDone(player);
            if (addons.Contains(CustomRoles.Stressed)) Stressed.OnTaskComplete(player);
            if (addons.Contains(CustomRoles.Unlucky) && alive && IRandom.Instance.Next(0, 100) < Options.UnluckyTaskSuicideChance.GetInt()) player.Suicide();
            if (GhostRolesManager.AssignedGhostRoles.TryGetValue(player.PlayerId, out var ghostRole))
            {
                if (ghostRole is { Role: CustomRoles.Specter, Instance: Specter specter } && (CompletedTasksCount + 1 >= AllTasksCount)) specter.OnFinishedTasks(player);
                if (ghostRole is { Role: CustomRoles.Haunter, Instance: Haunter haunter })
                {
                    if (CompletedTasksCount + 1 >= AllTasksCount) haunter.OnFinishedTasks(player);
                    else if (CompletedTasksCount + 1 >= Haunter.TasksBeforeBeingKnown.GetInt()) haunter.OnOneTaskLeft(player);
                }
            }

            Simon.RemoveTarget(player, Simon.Instruction.Task);

            // Update the player's task count for Task Managers
            foreach (var pc in Main.AllAlivePlayerControls)
            {
                if (pc.Is(CustomRoles.TaskManager) && pc.PlayerId != player.PlayerId)
                {
                    Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: player);
                }
            }
        }

        if (CompletedTasksCount >= AllTasksCount) return;

        CompletedTasksCount++;

        CompletedTasksCount = Math.Min(AllTasksCount, CompletedTasksCount);
        Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags()}: TaskCounts = {CompletedTasksCount}/{AllTasksCount}", "TaskState.Update");
    }
}

public class PlayerVersion(Version ver, string tagStr, string forkId)
{
    public readonly string forkId = forkId;
    public readonly string tag = tagStr;
    public readonly Version version = ver;

    public PlayerVersion(string ver, string tagStr, string forkId) : this(Version.Parse(ver), tagStr, forkId)
    {
    }

    public override bool Equals(object obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        return obj.GetType() == GetType() && Equals((PlayerVersion)obj);
    }

    private bool Equals(PlayerVersion other)
    {
        return forkId == other.forkId && tag == other.tag && Equals(version, other.version);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(forkId, tag, version);
    }
}

public static class GameStates
{
    public static bool InGame;
    public static bool AlreadyDied;
    public static bool IsModHost => PlayerControl.AllPlayerControls.ToArray().Any(x => x.IsHost() && x.IsModClient());
    public static bool IsLobby => AmongUsClient.Instance.GameState == InnerNetClient.GameStates.Joined;
    public static bool IsInGame => InGame;
    public static bool IsEnded => GameEndChecker.Ended || AmongUsClient.Instance.GameState == InnerNetClient.GameStates.Ended;
    public static bool IsNotJoined => AmongUsClient.Instance.GameState == InnerNetClient.GameStates.NotJoined;
    public static bool IsOnlineGame => AmongUsClient.Instance.NetworkMode == NetworkModes.OnlineGame;
    public static bool IsLocalGame => AmongUsClient.Instance.NetworkMode == NetworkModes.LocalGame;
    public static bool IsFreePlay => AmongUsClient.Instance.NetworkMode == NetworkModes.FreePlay;
    public static bool IsInTask => InGame && !MeetingHud.Instance;
    public static bool IsMeeting => InGame && MeetingHud.Instance;
    public static bool IsVoting => IsMeeting && MeetingHud.Instance.state is MeetingHud.VoteStates.Voted or MeetingHud.VoteStates.NotVoted;

    public static bool IsCountDown => GameStartManager.InstanceExists && GameStartManager.Instance.startState == GameStartManager.StartingStates.Countdown;

    public static bool IsVanillaServer
    {
        get
        {
            if (!IsOnlineGame) return false;

            const string domain = "among.us";

            // From Reactor.gg
            return ServerManager.Instance.CurrentRegion?.TryCast<StaticHttpRegionInfo>() is { } regionInfo &&
                   regionInfo.PingServer.EndsWith(domain, StringComparison.Ordinal) &&
                   regionInfo.Servers.All(serverInfo => serverInfo.Ip.EndsWith(domain, StringComparison.Ordinal));
        }
    }

    /**********TOP ZOOM.cs***********/
    public static bool IsShip => ShipStatus.Instance != null;
    public static bool IsCanMove => PlayerControl.LocalPlayer != null && PlayerControl.LocalPlayer.CanMove;
    public static bool IsDead => PlayerControl.LocalPlayer != null && PlayerControl.LocalPlayer.Data != null && PlayerControl.LocalPlayer.Data.IsDead;
}

public static class MeetingStates
{
    public static DeadBody[] DeadBodies;

    public static bool MeetingCalled;
    public static bool FirstMeeting = true;
    public static bool IsEmergencyMeeting => ReportTarget == null;
    public static bool IsExistDeadBody => DeadBodies.Length > 0;

    public static NetworkedPlayerInfo ReportTarget { get; set; }
}