using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Roles.AddOns.Crewmate;
using EHR.Roles.AddOns.GhostRoles;
using EHR.Roles.Crewmate;
using EHR.Roles.Impostor;
using EHR.Roles.Neutral;
using HarmonyLib;
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

        etc = -1
    }

    readonly byte PlayerId = playerId;
    public CountTypes countTypes = CountTypes.OutOfGame;
    public PlainShipRoom LastRoom;
    public CustomRoles MainRole = CustomRoles.NotAssigned;
    public (DateTime TIMESTAMP, byte ID) RealKiller = (DateTime.MinValue, byte.MaxValue);
    public RoleBase Role = new VanillaRole();
    public List<CustomRoles> SubRoles = [];
    public Dictionary<byte, string> TargetColorData = [];
    public TaskState taskState = new();
    public bool IsDead { get; set; }
#pragma warning disable IDE1006 // Naming Styles
    // ReSharper disable once InconsistentNaming
    public DeathReason deathReason { get; set; } = DeathReason.etc;
#pragma warning restore IDE1006 // Naming Styles
    public bool IsBlackOut { get; set; }

    public bool IsSuicide => deathReason == DeathReason.Suicide;
    public TaskState TaskState => taskState;

    public CustomRoles GetCustomRole()
    {
        var RoleInfo = Utils.GetPlayerInfoById(PlayerId);
        return RoleInfo.Role == null
            ? MainRole
            : RoleInfo.Role.Role switch
            {
                RoleTypes.Crewmate => CustomRoles.Crewmate,
                RoleTypes.Engineer => CustomRoles.Engineer,
                RoleTypes.Scientist => CustomRoles.Scientist,
                RoleTypes.GuardianAngel => CustomRoles.GuardianAngel,
                RoleTypes.Impostor => CustomRoles.Impostor,
                RoleTypes.Shapeshifter => CustomRoles.Shapeshifter,
                _ => CustomRoles.Crewmate,
            };
    }

    public void SetMainRole(CustomRoles role)
    {
        countTypes = role switch
        {
            CustomRoles.DarkHide => !DarkHide.SnatchesWin.GetBool() ? CountTypes.DarkHide : CountTypes.Crew,
            CustomRoles.Arsonist => Options.ArsonistKeepsGameGoing.GetBool() ? CountTypes.Arsonist : CountTypes.Crew,
            _ => role.GetCountTypes(),
        };

        Role = role.GetRoleClass();

        if (!role.RoleExist(countDead: true))
        {
            Role.Init();
        }

        MainRole = role;

        Role.Add(PlayerId);

        if (!Main.HasJustStarted)
        {
            var pc = Utils.GetPlayerById(PlayerId);
            pc.ResetKillCooldown();
            pc.SyncSettings();
            Utils.NotifyRoles(SpecifySeer: pc);
            Utils.NotifyRoles(SpecifyTarget: pc);
            if (PlayerId == PlayerControl.LocalPlayer.PlayerId && GameStates.IsInTask)
            {
                HudManager.Instance.SetHudActive(true);
                RemoveDisableDevicesPatch.UpdateDisableDevices();
            }
        }
    }

    public void SetSubRole(CustomRoles role, bool AllReplace = false)
    {
        if (role == CustomRoles.Cleansed)
            AllReplace = true;
        if (AllReplace)
            SubRoles.Do(item => SubRoles.Remove(item));

        if (!SubRoles.Contains(role))
            SubRoles.Add(role);

        switch (role)
        {
            case CustomRoles.Bloodlust:
                countTypes = CountTypes.Bloodlust;
                break;
            case CustomRoles.Madmate:
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
                SubRoles.Remove(CustomRoles.Rogue);
                SubRoles.Remove(CustomRoles.Rascal);
                SubRoles.Remove(CustomRoles.Loyal);
                SubRoles.Remove(CustomRoles.Undead);
                break;
            case CustomRoles.Charmed:
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
                SubRoles.Remove(CustomRoles.Rogue);
                SubRoles.Remove(CustomRoles.Rascal);
                SubRoles.Remove(CustomRoles.Loyal);
                SubRoles.Remove(CustomRoles.Undead);
                break;
            case CustomRoles.Undead:
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
                SubRoles.Remove(CustomRoles.Rogue);
                SubRoles.Remove(CustomRoles.Rascal);
                SubRoles.Remove(CustomRoles.Loyal);
                SubRoles.Remove(CustomRoles.Charmed);
                break;
            case CustomRoles.LastImpostor:
                SubRoles.Remove(CustomRoles.Mare);
                break;
            case CustomRoles.Recruit:
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
                SubRoles.Remove(CustomRoles.Rogue);
                SubRoles.Remove(CustomRoles.Rascal);
                SubRoles.Remove(CustomRoles.Loyal);
                SubRoles.Remove(CustomRoles.Loyal);
                SubRoles.Remove(CustomRoles.Undead);
                break;
            case CustomRoles.Contagious:
                countTypes = Virus.ContagiousCountMode.GetInt() switch
                {
                    0 => CountTypes.OutOfGame,
                    1 => CountTypes.Virus,
                    2 => countTypes,
                    _ => throw new NotImplementedException()
                };
                SubRoles.Remove(CustomRoles.Madmate);
                SubRoles.Remove(CustomRoles.Recruit);
                SubRoles.Remove(CustomRoles.Rogue);
                SubRoles.Remove(CustomRoles.Charmed);
                SubRoles.Remove(CustomRoles.Rascal);
                SubRoles.Remove(CustomRoles.Loyal);
                SubRoles.Remove(CustomRoles.Undead);
                break;
            case CustomRoles.Rogue:
                countTypes = CountTypes.Rogue;
                SubRoles.Remove(CustomRoles.Madmate);
                SubRoles.Remove(CustomRoles.Recruit);
                SubRoles.Remove(CustomRoles.Charmed);
                SubRoles.Remove(CustomRoles.Contagious);
                SubRoles.Remove(CustomRoles.Rascal);
                SubRoles.Remove(CustomRoles.Loyal);
                SubRoles.Remove(CustomRoles.Undead);
                break;
        }
    }

    public void RemoveSubRole(CustomRoles role)
    {
        if (SubRoles.Contains(role))
            SubRoles.Remove(role);
    }

    public void SetDead()
    {
        IsDead = true;
        if (AmongUsClient.Instance.AmHost)
        {
            RPC.SendDeathReason(PlayerId, deathReason);
        }
    }

    public void InitTask(PlayerControl player) => taskState.Init(player);
    public void UpdateTask(PlayerControl player) => taskState.Update(player);

    public byte GetRealKiller() => IsDead && RealKiller.TIMESTAMP != DateTime.MinValue ? RealKiller.ID : byte.MaxValue;
    public int GetKillCount(bool ExcludeSelfKill = false) => Main.PlayerStates.Values.Where(state => !(ExcludeSelfKill && state.PlayerId == PlayerId) && state.GetRealKiller() == PlayerId).ToArray().Length;
}

public class TaskState
{
    public static int InitialTotalTasks;
    public int AllTasksCount = -1;
    public int CompletedTasksCount;
    public bool hasTasks;
    public int RemainingTasksCount => AllTasksCount - CompletedTasksCount;
    public bool IsTaskFinished => RemainingTasksCount <= 0 && hasTasks;

    public void Init(PlayerControl player)
    {
        Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags().RemoveHtmlTags()}: InitTask", "TaskState.Init");
        if (player == null || player.Data?.Tasks == null) return;
        if (!Utils.HasTasks(player.Data, false))
        {
            AllTasksCount = 0;
            return;
        }

        hasTasks = true;
        AllTasksCount = player.Data.Tasks.Count;
        Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags().RemoveHtmlTags()}: TaskCounts = {CompletedTasksCount}/{AllTasksCount}", "TaskState.Init");
    }

    public void Update(PlayerControl player)
    {
        Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags().RemoveHtmlTags()}: UpdateTask", "TaskState.Update");
        GameData.Instance.RecomputeTaskCounts();
        Logger.Info($"TotalTaskCounts = {GameData.Instance.CompletedTasks}/{GameData.Instance.TotalTasks}", "TaskState.Update");

        if (AllTasksCount == -1) Init(player);

        if (!hasTasks) return;

        if (AmongUsClient.Instance.AmHost)
        {
            bool alive = player.IsAlive();

            if (player.Is(CustomRoles.Unlucky) && alive)
            {
                var Ue = IRandom.Instance;
                if (Ue.Next(0, 100) < Options.UnluckyTaskSuicideChance.GetInt())
                {
                    player.Suicide();
                }
            }

            if (alive && Mastermind.ManipulatedPlayers.ContainsKey(player.PlayerId))
            {
                Mastermind.OnManipulatedPlayerTaskComplete(player);
            }

            // Ability Use Gain with this task completed
            if (alive)
            {
                switch (player.GetCustomRole())
                {
                    case CustomRoles.SabotageMaster:
                        if (Main.PlayerStates[player.PlayerId].Role is not SabotageMaster sm) break;
                        sm.UsedSkillCount -= SabotageMaster.SMAbilityUseGainWithEachTaskCompleted.GetFloat();
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

            Main.PlayerStates[player.PlayerId].Role.OnTaskComplete(player, CompletedTasksCount, AllTasksCount);

            var addons = Main.PlayerStates[player.PlayerId].SubRoles;

            if (addons.Contains(CustomRoles.Stressed)) Stressed.OnTaskComplete(player);
            if (GhostRolesManager.AssignedGhostRoles.TryGetValue(player.PlayerId, out var ghostRole))
            {
                if (ghostRole is { Role: CustomRoles.Specter, Instance: Specter specter } && (CompletedTasksCount + 1 >= AllTasksCount)) specter.OnFinishedTasks(player);
                if (ghostRole is { Role: CustomRoles.Haunter, Instance: Haunter haunter })
                {
                    if (CompletedTasksCount + 1 >= AllTasksCount) haunter.OnFinishedTasks(player);
                    else if (CompletedTasksCount + 1 >= Haunter.TasksBeforeBeingKnown.GetInt()) haunter.OnOneTaskLeft(player);
                }
            }

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

public class PlayerVersion(Version ver, string tag_str, string forkId)
{
    public readonly string forkId = forkId;
    public readonly string tag = tag_str;
    public readonly Version version = ver;

    public PlayerVersion(string ver, string tag_str, string forkId) : this(Version.Parse(ver), tag_str, forkId)
    {
    }

    public bool IsEqual(PlayerVersion pv)
    {
        return pv.version == version && pv.tag == tag;
    }
#pragma warning disable CA1041 // Provide ObsoleteAttribute message
    [Obsolete]
    public PlayerVersion(string ver, string tag_str) : this(Version.Parse(ver), tag_str, string.Empty)
    {
    }

    [Obsolete]
    public PlayerVersion(Version ver, string tag_str) : this(ver, tag_str, string.Empty)
    {
    }
#pragma warning restore CA1041 // Provide ObsoleteAttribute message
}

public static class GameStates
{
    public static bool InGame;
    public static bool AlreadyDied;
    public static bool IsModHost => PlayerControl.AllPlayerControls.ToArray().Any(x => x.PlayerId == 0 && x.IsModClient());
    public static bool IsLobby => AmongUsClient.Instance.GameState == InnerNetClient.GameStates.Joined;
    public static bool IsInGame => InGame;
    public static bool IsEnded => AmongUsClient.Instance.GameState == InnerNetClient.GameStates.Ended;
    public static bool IsNotJoined => AmongUsClient.Instance.GameState == InnerNetClient.GameStates.NotJoined;
    public static bool IsOnlineGame => AmongUsClient.Instance.NetworkMode == NetworkModes.OnlineGame;
    public static bool IsLocalGame => AmongUsClient.Instance.NetworkMode == NetworkModes.LocalGame;
    public static bool IsFreePlay => AmongUsClient.Instance.NetworkMode == NetworkModes.FreePlay;
    public static bool IsInTask => InGame && !MeetingHud.Instance;
    public static bool IsMeeting => InGame && MeetingHud.Instance;
    public static bool IsVoting => IsMeeting && MeetingHud.Instance.state is MeetingHud.VoteStates.Voted or MeetingHud.VoteStates.NotVoted;

    public static bool IsCountDown => GameStartManager.InstanceExists && GameStartManager.Instance.startState == GameStartManager.StartingStates.Countdown;

    /**********TOP ZOOM.cs***********/
    public static bool IsShip => ShipStatus.Instance != null;
    public static bool IsCanMove => PlayerControl.LocalPlayer?.CanMove is true;
    public static bool IsDead => PlayerControl.LocalPlayer?.Data?.IsDead is true;
}

public static class MeetingStates
{
    public static DeadBody[] DeadBodies;

    public static bool MeetingCalled;
    public static bool FirstMeeting = true;
    public static bool IsEmergencyMeeting => ReportTarget == null;
    public static bool IsExistDeadBody => DeadBodies.Length > 0;

    public static GameData.PlayerInfo ReportTarget { get; set; }
}