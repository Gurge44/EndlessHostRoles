using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;

namespace EHR;

public static class AntiBlackout
{
    public static int ExilePlayerId = -1;
    public static bool SkipTasks;
    private static Dictionary<byte, (bool IsDead, bool Disconnected)> IsDeadCache = [];
    private static readonly LogHandler Logger = EHR.Logger.Handler("AntiBlackout");

/*
    public static bool CheckBlackOut()
    {
        HashSet<byte> Impostors = [];
        HashSet<byte> Crewmates = [];
        HashSet<byte> NeutralKillers = [];

        var lastExiled = ExileControllerWrapUpPatch.AntiBlackoutLastExiled;
        foreach (var pc in Main.AllAlivePlayerControls)
        {
            // If a player is ejected, do not count them as alive
            if (lastExiled != null && pc.PlayerId == lastExiled.PlayerId) continue;

            if (pc.Is(Team.Impostor)) Impostors.Add(pc.PlayerId);
            else if (pc.IsNeutralKiller()) NeutralKillers.Add(pc.PlayerId);
            else Crewmates.Add(pc.PlayerId);
        }
        var numAliveImpostors = Impostors.Count;
        var numAliveCrewmates = Crewmates.Count;
        var numAliveNeutralKillers = NeutralKillers.Count;

        EHR.Logger.Info($" Impostors: {numAliveImpostors}, Crewmates: {numAliveCrewmates}, Neutral Killers: {numAliveNeutralKillers}", "AntiBlackout Num Alive");

        bool con1 = numAliveImpostors <= 0; // All real impostors are dead
        bool con2 = (numAliveNeutralKillers + numAliveCrewmates) <= numAliveImpostors; // Alive Impostors >= other teams sum
        bool con3 = numAliveNeutralKillers == 1 && numAliveImpostors == 1 && numAliveCrewmates <= 2; // One Impostor and one Neutral Killer is alive and living Crewmates are very few

        var blackOutIsActive = con1 || con2 || con3;

        EHR.Logger.Info($" {blackOutIsActive}", "BlackOut Is Active");
        return blackOutIsActive;
    }
*/

    private static bool IsCached { get; set; }

    public static void SetIsDead(bool doSend = true, [CallerMemberName] string callerMethodName = "")
    {
        SkipTasks = true;
        RevivePlayersAndSetDummyImp();
        Logger.Info($"SetIsDead is called from {callerMethodName}");
        if (IsCached) return;
        IsDeadCache.Clear();
        foreach (var info in GameData.Instance.AllPlayers)
        {
            if (info == null) continue;
            IsDeadCache[info.PlayerId] = (info.IsDead, info.Disconnected);
            info.IsDead = false;
            info.Disconnected = false;
        }

        IsCached = true;
        if (doSend) SendGameData();
    }

    private static void RevivePlayersAndSetDummyImp()
    {
        if (CustomWinnerHolder.WinnerTeam != CustomWinner.Default) return;

        PlayerControl dummyImp = Main.AllAlivePlayerControls.First(x => x.PlayerId != ExilePlayerId);

        foreach (var seer in Main.AllPlayerControls)
        {
            if (seer.IsHost() || seer.IsModClient()) continue;
            foreach (var target in Main.AllPlayerControls)
            {
                RoleTypes targetRoleType = target.PlayerId == dummyImp.PlayerId ? RoleTypes.Impostor : RoleTypes.Crewmate;
                target.RpcSetRoleDesync(targetRoleType, seer.GetClientId());
            }
        }
    }

    public static void RestoreIsDead(bool doSend = true, [CallerMemberName] string callerMethodName = "")
    {
        Logger.Info($"RestoreIsDead is called from {callerMethodName}");
        foreach (var info in GameData.Instance.AllPlayers)
        {
            if (info == null) continue;
            if (IsDeadCache.TryGetValue(info.PlayerId, out var val))
            {
                info.IsDead = val.IsDead;
                info.Disconnected = val.Disconnected;
            }
        }

        IsDeadCache.Clear();
        IsCached = false;
        if (doSend) SendGameData();
    }

    public static void SendGameData([CallerMemberName] string callerMethodName = "")
    {
        Logger.Info($"SendGameData is called from {callerMethodName}");
        foreach (var playerinfo in GameData.Instance.AllPlayers)
        {
            MessageWriter writer = MessageWriter.Get(SendOption.Reliable);
            writer.StartMessage(5); //0x05 GameData
            {
                writer.Write(AmongUsClient.Instance.GameId);
                writer.StartMessage(1); //0x01 Data
                {
                    writer.WritePacked(playerinfo.NetId);
                    playerinfo.Serialize(writer, true);
                }
                writer.EndMessage();
            }
            writer.EndMessage();

            AmongUsClient.Instance.SendOrDisconnect(writer);
            writer.Recycle();
        }
    }

    public static void OnDisconnect(NetworkedPlayerInfo player)
    {
        // Execution conditions: Client is the host, IsDead is overridden, player is already disconnected
        if (!AmongUsClient.Instance.AmHost || !IsCached || !player.Disconnected) return;
        IsDeadCache[player.PlayerId] = (true, true);
        RevivePlayersAndSetDummyImp();
        player.IsDead = player.Disconnected = false;
        SendGameData();
    }

    public static void SetRealPlayerRoles()
    {
        if (CustomWinnerHolder.WinnerTeam != CustomWinner.Default) return;

        foreach (((byte seerId, byte targetId), (RoleTypes roletype, _)) in StartGameHostPatch.RpcSetRoleReplacer.RoleMap)
        {
            if (seerId == 0) continue; // Skip the host

            var seer = Utils.GetPlayerById(seerId);
            var target = Utils.GetPlayerById(targetId);

            if (seer == null || target == null) continue;
            if (seer.IsModClient()) continue;

            var self = seerId == targetId;
            var changedRoleType = roletype;
            if (target.Data.IsDead)
            {
                if (self)
                {
                    target.RpcExile();

                    if (target.GetCustomRole().IsGhostRole() || target.HasGhostRole()) changedRoleType = RoleTypes.GuardianAngel;
                    else if (target.Is(Team.Impostor) || target.HasDesyncRole()) changedRoleType = RoleTypes.ImpostorGhost;
                    else changedRoleType = RoleTypes.CrewmateGhost;
                }
                else
                {
                    var seerIsKiller = seer.Is(Team.Impostor) || seer.HasDesyncRole();
                    if (!seerIsKiller && target.Is(Team.Impostor)) changedRoleType = RoleTypes.ImpostorGhost;
                    else changedRoleType = RoleTypes.CrewmateGhost;
                }
            }

            target.RpcSetRoleDesync(changedRoleType, seer.GetClientId());
        }
    }

    private static void ResetAllCooldowns()
    {
        foreach (var seer in Main.AllPlayerControls)
        {
            if (seer.IsAlive())
            {
                seer.SetKillCooldown();
                seer.RpcResetAbilityCooldown();
            }
            else if (seer.GetCustomRole().IsGhostRole() || seer.HasGhostRole())
            {
                seer.RpcResetAbilityCooldown();
            }
        }
    }

    public static void ResetAfterMeeting()
    {
        SkipTasks = false;
        ExilePlayerId = -1;
        ResetAllCooldowns();
    }

    public static void Reset()
    {
        Logger.Info("==Reset==");
        IsDeadCache ??= [];
        IsDeadCache.Clear();
        IsCached = false;
        ExilePlayerId = -1;
        SkipTasks = false;
    }
}