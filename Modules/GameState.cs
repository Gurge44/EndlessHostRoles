using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.AddOns.Common;
using EHR.AddOns.Crewmate;
using EHR.AddOns.GhostRoles;
using EHR.Coven;
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
        Allergy,
        Swooped,
        Echoed,
        Dragged,
        Mauled,
        WipedOut,
        OutOfOxygen,
        Retribution,
        Taxes,
        DidntVote,
        SkippedVote,
        Deafened,
        Patrolled,
        Misguess,
        LossOfBlood,

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

    public readonly PlayerControl Player = Utils.GetPlayerById(playerId, false);

    private readonly byte PlayerId = playerId;
    public readonly List<CustomRoles> SubRoles = [];
    public readonly Dictionary<byte, string> TargetColorData = [];
    public CountTypes countTypes = CountTypes.Crew;
    public PlainShipRoom LastRoom;
    public CustomRoles MainRole = CustomRoles.NotAssigned;
    public NetworkedPlayerInfo.PlayerOutfit NormalOutfit;
    public (DateTime TimeStamp, byte ID) RealKiller = (DateTime.MinValue, byte.MaxValue);
    public RoleBase Role = new VanillaRole();

    private int RoleChangeTimes = -1;

    public readonly List<CustomRoles> RoleHistory = [];
    public bool IsDead { get; set; }

    // ReSharper disable once InconsistentNaming
    public DeathReason deathReason { get; set; } = DeathReason.etc;
    public bool IsBlackOut { get; set; }

    public bool IsSuicide => deathReason is DeathReason.Suicide or DeathReason.Fall;
    public TaskState TaskState { get; set; } = new();

    public void SetMainRole(CustomRoles role)
    {
        try { Role.Remove(PlayerId); }
        catch (Exception e) { Utils.ThrowException(e); }

        if (Main.IntroDestroyed && (RoleHistory.Count == 0 || RoleHistory[^1] != MainRole))
            RoleHistory.Add(MainRole);

        FortuneTeller.OnRoleChange(PlayerId, MainRole, role);

        bool previousHasTasks = Utils.HasTasks(Player.Data, false);

        countTypes = role.GetCountTypes();

        if (CustomTeamManager.GetCustomTeam(PlayerId) != null && !CustomTeamManager.IsSettingEnabledForPlayerTeam(PlayerId, CTAOption.WinWithOriginalTeam))
            countTypes = CountTypes.CustomTeam;

        SubRoles.ForEach(SetAddonCountTypes);

        if (!Player.HasKillButton() && role == CustomRoles.Renegade)
            Player.RpcChangeRoleBasis(CustomRoles.Renegade);

        Role = role.GetRoleClass();

        if (!role.RoleExist(true)) Role.Init();

        MainRole = role;

        Role.Add(PlayerId);

        Logger.Info($"ID {PlayerId} ({Player.GetRealName()}) => {role}, CountTypes => {countTypes}", "SetMainRole");

        if (Main.IntroDestroyed && GameStates.InGame)
        {
            if (!previousHasTasks && Utils.HasTasks(Player.Data, false))
            {
                Player.RpcResetTasks();
                if (!AmongUsClient.Instance.AmHost) LateTask.New(() => TaskState.Init(Player), 1f, log: false);
            }

            if (role.IsVanilla() || role.ToString().Contains("EHR"))
                Main.AbilityUseLimit.Remove(PlayerId);
        }

        if (!AmongUsClient.Instance.AmHost) return;

        if (Main.IntroDestroyed && GameStates.InGame)
        {
            Player.ResetKillCooldown();

            if (PlayerId == PlayerControl.LocalPlayer.PlayerId && GameStates.IsInTask)
            {
                HudManager.Instance.SetHudActive(true);
                RemoveDisableDevicesPatch.UpdateDisableDevices();
                HudSpritePatch.ForceUpdate = true;
            }

            if (Decryptor.On) Decryptor.Instances.ForEach(x => x.OnRoleChange(PlayerId));

            if (!role.Is(Team.Impostor) && !(role == CustomRoles.Traitor && Traitor.CanGetImpostorOnlyAddons.GetBool()))
                SubRoles.ToArray().DoIf(x => x.IsImpOnlyAddon(), RemoveSubRole);

            if (role is CustomRoles.Sidekick or CustomRoles.Necromancer or CustomRoles.Deathknight or CustomRoles.Renegade)
                SubRoles.ToArray().DoIf(StartGameHostPatch.BasisChangingAddons.ContainsKey, RemoveSubRole);

            if (role == CustomRoles.Sidekick && Jackal.Instances.FindFirst(x => x.SidekickId == byte.MaxValue || x.SidekickId.GetPlayer() == null, out Jackal jackal))
                jackal.SidekickId = PlayerId;

            if (Options.CurrentGameMode == CustomGameMode.Standard && GameStates.IsInTask && !AntiBlackout.SkipTasks)
                Player.Notify(string.Format(Translator.GetString("RoleChangedNotify"), role.ToColoredString()), 10f);

            if (Options.UsePets.GetBool() && Player.CurrentOutfit.PetId == "")
                PetsHelper.SetPet(Player, PetsHelper.GetPetId());

            Utils.NotifyRoles(SpecifySeer: Player);
            Utils.NotifyRoles(SpecifyTarget: Player);
        }

        CheckMurderPatch.TimeSinceLastKill.Remove(PlayerId);

        if (!Main.IntroDestroyed || PlayerControl.LocalPlayer.PlayerId != PlayerId || Options.CurrentGameMode != CustomGameMode.Standard) return;

        RoleChangeTimes++;
        if (RoleChangeTimes >= 4) Achievements.Type.Transformer.Complete();
    }

    public void SetSubRole(CustomRoles role, bool replaceAll = false)
    {
        switch (role)
        {
            case CustomRoles.Cleansed:
                replaceAll = true;
                break;
            case CustomRoles.BananaMan when Main.IntroDestroyed && !SubRoles.Contains(CustomRoles.BananaMan):
                LateTask.New(() => Utils.RpcChangeSkin(Player, new()), 0.2f, log: false);
                break;
        }

        if (replaceAll)
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
                countTypes = Options.MadmateCountMode.GetInt() switch
                {
                    0 => CountTypes.OutOfGame,
                    1 => CountTypes.Impostor,
                    2 => CountTypes.Crew,
                    _ => throw new NotImplementedException()
                };

                SubRoles.Remove(CustomRoles.Entranced);
                SubRoles.Remove(CustomRoles.Charmed);
                SubRoles.Remove(CustomRoles.Contagious);
                SubRoles.Remove(CustomRoles.Rascal);
                SubRoles.Remove(CustomRoles.Loyal);
                SubRoles.Remove(CustomRoles.Undead);
                SubRoles.Remove(CustomRoles.Stressed);
                Utils.NotifyRoles(SpecifySeer: Player);
                Utils.NotifyRoles(SpecifyTarget: Player);
                break;
            case CustomRoles.Charmed:
                countTypes = Cultist.CharmedCountMode.GetInt() switch
                {
                    0 => CountTypes.OutOfGame,
                    1 => CountTypes.Cultist,
                    2 => countTypes,
                    _ => throw new NotImplementedException()
                };

                SubRoles.Remove(CustomRoles.Entranced);
                SubRoles.Remove(CustomRoles.Madmate);
                SubRoles.Remove(CustomRoles.Contagious);
                SubRoles.Remove(CustomRoles.Rascal);
                SubRoles.Remove(CustomRoles.Loyal);
                SubRoles.Remove(CustomRoles.Undead);
                SubRoles.Remove(CustomRoles.Stressed);
                Utils.NotifyRoles(SpecifySeer: Player);
                Utils.NotifyRoles(SpecifyTarget: Player);
                break;
            case CustomRoles.Undead:
                countTypes = Necromancer.UndeadCountMode.GetInt() switch
                {
                    0 => CountTypes.OutOfGame,
                    1 => CountTypes.Necromancer,
                    2 => countTypes,
                    _ => throw new NotImplementedException()
                };

                SubRoles.Remove(CustomRoles.Entranced);
                SubRoles.Remove(CustomRoles.Madmate);
                SubRoles.Remove(CustomRoles.Contagious);
                SubRoles.Remove(CustomRoles.Rascal);
                SubRoles.Remove(CustomRoles.Loyal);
                SubRoles.Remove(CustomRoles.Charmed);
                SubRoles.Remove(CustomRoles.Stressed);
                Utils.NotifyRoles(SpecifySeer: Player);
                Utils.NotifyRoles(SpecifyTarget: Player);
                break;
            case CustomRoles.Entranced:
                countTypes = Siren.EntrancedCountMode.GetInt() switch
                {
                    0 => CountTypes.OutOfGame,
                    1 => CountTypes.Coven,
                    2 => countTypes,
                    _ => throw new NotImplementedException()
                };

                SubRoles.Remove(CustomRoles.Charmed);
                SubRoles.Remove(CustomRoles.Madmate);
                SubRoles.Remove(CustomRoles.Contagious);
                SubRoles.Remove(CustomRoles.Rascal);
                SubRoles.Remove(CustomRoles.Loyal);
                SubRoles.Remove(CustomRoles.Undead);
                SubRoles.Remove(CustomRoles.Stressed);
                Utils.NotifyRoles(SpecifySeer: Player);
                Utils.NotifyRoles(SpecifyTarget: Player);
                break;
            case CustomRoles.LastImpostor:
                SubRoles.Remove(CustomRoles.Mare);
                break;
            case CustomRoles.Contagious:
                countTypes = Virus.ContagiousCountMode.GetInt() switch
                {
                    0 => CountTypes.OutOfGame,
                    1 => CountTypes.Virus,
                    2 => countTypes,
                    _ => throw new NotImplementedException()
                };

                SubRoles.Remove(CustomRoles.Entranced);
                SubRoles.Remove(CustomRoles.Madmate);
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

        if (role is CustomRoles.Flash or CustomRoles.Dynamo or CustomRoles.Spurt)
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
            if (Enchanter.EnchantedPlayers.Contains(PlayerId))
                deathReason = Enum.GetValues<DeathReason>()[..^8].RandomElement();

            RPC.SendDeathReason(PlayerId, deathReason);
            Utils.CheckAndSpawnAdditionalRenegade(Utils.GetPlayerInfoById(PlayerId));
        }
    }

    public void InitTask(PlayerControl player)
    {
        TaskState.Init(player);
    }

    public void UpdateTask(PlayerControl player)
    {
        TaskState.Update(player);
    }

    public byte GetRealKiller()
    {
        return IsDead && RealKiller.TimeStamp != DateTime.MinValue ? RealKiller.ID : byte.MaxValue;
    }

    public int GetKillCount()
    {
        return Main.PlayerStates.Values.Count(state => state.PlayerId != PlayerId && state.GetRealKiller() == PlayerId);
    }
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
        CompletedTasksCount = 0;
        AllTasksCount = player.Data.Tasks.Count;
        Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags()}: TaskCounts = {CompletedTasksCount}/{AllTasksCount}", "TaskState.Init");
    }

    public void Update(PlayerControl player)
    {
        try
        {
            Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags()}: UpdateTask", "TaskState.Update");
            GameData.Instance.RecomputeTaskCounts();
            Logger.Info($"TotalTaskCounts = {GameData.Instance.CompletedTasks}/{GameData.Instance.TotalTasks}", "TaskState.Update");

            if (Options.CurrentGameMode is CustomGameMode.HotPotato or CustomGameMode.NaturalDisasters or CustomGameMode.RoomRush or CustomGameMode.Quiz or CustomGameMode.TheMindGame or CustomGameMode.Mingle)
                player.Notify(Translator.GetString("DoingTasksIsPointlessInThisGameMode"), 10f);

            if (AllTasksCount == -1) Init(player);

            if (!HasTasks) return;

            if (AmongUsClient.Instance.AmHost)
            {
                Clerk.DidTaskThisRound.Add(player.PlayerId);
                
                bool alive = player.IsAlive();

                if (alive && Options.CurrentGameMode == CustomGameMode.Speedrun)
                {
                    if (CompletedTasksCount + 1 >= AllTasksCount)
                        Speedrun.OnTaskFinish(player);

                    Speedrun.ResetTimer(player);
                }

                // Ability Use Gain with this task completed
                if (alive && !Main.HasJustStarted)
                {
                    switch (player.GetCustomRole())
                    {
                        case CustomRoles.Hacker:
                            if (!player.IsModdedClient() && Hacker.UseLimit.ContainsKey(player.PlayerId))
                                Hacker.UseLimit[player.PlayerId] += Hacker.HackerAbilityUseGainWithEachTaskCompleted.GetFloat();
                            else if (Hacker.UseLimitSeconds.ContainsKey(player.PlayerId))
                                Hacker.UseLimitSeconds[player.PlayerId] += Hacker.HackerAbilityUseGainWithEachTaskCompleted.GetInt() * Hacker.ModdedClientAbilityUseSecondsMultiplier.GetInt();

                            if (Hacker.UseLimitSeconds.ContainsKey(player.PlayerId))
                                Hacker.SendRPC(player.PlayerId, Hacker.UseLimitSeconds[player.PlayerId]);

                            break;
                    }

                    float add = Utils.GetSettingNameAndValueForRole(player.GetCustomRole(), "AbilityUseGainWithEachTaskCompleted");
                    
                    if (Math.Abs(add - float.MaxValue) > 0.5f && add > 0)
                    {
                        if (player.Is(CustomRoles.Composter)) add *= Composter.AbilityUseGainMultiplier.GetFloat();
                        player.RpcIncreaseAbilityUseLimitBy(add);
                    }
                }

                try { Main.PlayerStates[player.PlayerId].Role.OnTaskComplete(player, CompletedTasksCount, AllTasksCount); }
                catch (Exception e) { Utils.ThrowException(e); }

                List<CustomRoles> addons = Main.PlayerStates[player.PlayerId].SubRoles;

                if (addons.Contains(CustomRoles.Deadlined)) Deadlined.SetDone(player);
                if (addons.Contains(CustomRoles.Stressed)) Stressed.OnTaskComplete(player);
                if (addons.Contains(CustomRoles.Unlucky) && alive && IRandom.Instance.Next(0, 100) < Options.UnluckyTaskSuicideChance.GetInt()) player.Suicide();

                if (GhostRolesManager.AssignedGhostRoles.TryGetValue(player.PlayerId, out (CustomRoles Role, IGhostRole Instance) ghostRole))
                {
                    if (ghostRole is { Role: CustomRoles.Phantasm, Instance: Phantasm phantasm } && CompletedTasksCount + 1 >= AllTasksCount)
                        phantasm.OnFinishedTasks(player);

                    if (ghostRole is { Role: CustomRoles.Haunter, Instance: Haunter haunter })
                    {
                        if (CompletedTasksCount + 1 >= AllTasksCount)
                            haunter.OnFinishedTasks(player);
                        else if (RemainingTasksCount - 1 <= Haunter.TasksBeforeBeingKnown.GetInt())
                            haunter.OnOneTaskLeft(player);
                    }
                }

                Simon.RemoveTarget(player, Simon.Instruction.Task);
                Wyrd.CheckPlayerAction(player, Wyrd.Action.Task);

                // Update the player's task count for Task Managers
                foreach (PlayerControl pc in Main.AllAlivePlayerControls)
                {
                    if (pc.Is(CustomRoles.TaskManager) && pc.PlayerId != player.PlayerId)
                        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: player);
                }
            }
        }
        catch (Exception e) { Utils.ThrowException(e); }

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

    public PlayerVersion(string ver, string tagStr, string forkId) : this(Version.Parse(ver), tagStr, forkId) { }

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
    public enum ServerType
    {
        Vanilla,
        Modded,
        Niko,
        Local,
        Custom
    }

    public static bool InGame;
    public static bool AlreadyDied;
    public static bool IsModHost => PlayerControl.LocalPlayer.IsHost() || PlayerControl.AllPlayerControls.ToArray().Any(x => x.IsHost() && x.IsModdedClient());
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

    public static ServerType CurrentServerType
    {
        get
        {
            if (IsFreePlay || IsLocalGame || IsNotJoined) return ServerType.Local;

            string regionName = Utils.GetRegionName();

            return regionName switch
            {
                "Local Game" => ServerType.Custom,
                "EU" or "NA" or "AS" => ServerType.Vanilla,
                "MEU" or "MAS" or "MNA" => ServerType.Modded,
                _ => regionName.Contains("Niko", StringComparison.OrdinalIgnoreCase) ? ServerType.Niko : ServerType.Custom
            };
        }
    }

    /**********TOP ZOOM.cs***********/
    public static bool IsShip => ShipStatus.Instance != null;
    public static bool IsCanMove => PlayerControl.LocalPlayer != null && PlayerControl.LocalPlayer.CanMove;
    public static bool IsDead => PlayerControl.LocalPlayer != null && !PlayerControl.LocalPlayer.IsAlive();
}

public static class MeetingStates
{
    public static DeadBody[] DeadBodies;

    public static int MeetingNum;
    public static bool MeetingCalled;
    public static bool FirstMeeting = true;
    public static bool IsEmergencyMeeting => ReportTarget == null;
    public static bool IsExistDeadBody => DeadBodies.Length > 0;

    public static NetworkedPlayerInfo ReportTarget { get; set; }

}
