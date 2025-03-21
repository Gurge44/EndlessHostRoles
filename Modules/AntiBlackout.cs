using System;
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

        foreach (NetworkedPlayerInfo info in GameData.Instance.AllPlayers)
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

        foreach (PlayerControl seer in Main.AllPlayerControls)
        {
            if (seer.IsModdedClient()) continue;

            var seerIsAliveAndHasKillButton = seer.HasKillButton() && seer.IsAlive();
            var sender = CustomRpcSender.Create(sendOption: SendOption.Reliable);

            foreach (var target in Main.AllPlayerControls)
            {
                try
                {
                    if (seer.PlayerId == target.PlayerId && seerIsAliveAndHasKillButton) continue;

                    RoleTypes targetRoleType = !seerIsAliveAndHasKillButton && target.PlayerId == dummyImp.PlayerId
                        ? RoleTypes.Impostor
                        : RoleTypes.Crewmate;

                    sender.RpcSetRole(target, targetRoleType, seer.GetClientId());
                }
                catch (Exception e) { Utils.ThrowException(e); }
            }

            sender.SendMessage();
        }
    }

    public static void RestoreIsDead(bool doSend = true, [CallerMemberName] string callerMethodName = "")
    {
        Logger.Info($"RestoreIsDead is called from {callerMethodName}");

        foreach (NetworkedPlayerInfo info in GameData.Instance.AllPlayers)
        {
            if (info == null) continue;

            if (IsDeadCache.TryGetValue(info.PlayerId, out (bool IsDead, bool Disconnected) val))
            {
                info.IsDead = val.IsDead;
                info.Disconnected = val.Disconnected;
            }
        }

        IsDeadCache.Clear();
        IsCached = false;

        if (doSend)
        {
            SendGameData();
            LateTask.New(RestoreIsDeadByExile, 0.3f, "AntiBlackOut.RestoreIsDeadByExile");
        }
    }

    private static void RestoreIsDeadByExile()
    {
        var sender = CustomRpcSender.Create("AntiBlackout.RestoreIsDeadByExile", SendOption.Reliable);
        Main.AllPlayerControls.DoIf(x => x.Data.IsDead && !x.Data.Disconnected, x => sender.AutoStartRpc(x.NetId, (byte)RpcCalls.Exiled).EndRpc());
        sender.SendMessage();
    }

    public static void SendGameData([CallerMemberName] string callerMethodName = "")
    {
        Logger.Info($"SendGameData is called from {callerMethodName}");

        foreach (NetworkedPlayerInfo playerinfo in GameData.Instance.AllPlayers)
        {
            try
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
            catch (Exception e) { Utils.ThrowException(e); }
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

        var seerGroups = StartGameHostPatch.RpcSetRoleReplacer.RoleMap
            .GroupBy(entry => entry.Key.SeerID)
            .ToDictionary(group => group.Key, group => group.ToList());

        foreach ((byte seerId, List<KeyValuePair<(byte SeerID, byte TargetID), (RoleTypes RoleType, CustomRoles CustomRole)>> list) in seerGroups)
        {
            var seer = seerId.GetPlayer();

            if (seer == null || seer.IsModdedClient() || seer.IsLocalPlayer()) continue;

            var sender = CustomRpcSender.Create(sendOption: SendOption.Reliable);
            var hasValue = false;

            foreach (((_, byte targetId), (RoleTypes roletype, _)) in list)
            {
                try
                {
                    var target = targetId.GetPlayer();

                    if (target == null) continue;

                    var isSelf = seerId == targetId;
                    var isDead = target.Data.IsDead;
                    var changedRoleType = roletype;

                    switch (isDead)
                    {
                        case true when isSelf:
                        {
                            sender.AutoStartRpc(seer.NetId, (byte)RpcCalls.Exiled, seer.GetClientId());
                            sender.EndRpc();
                            hasValue = true;

                            if (target.HasGhostRole()) changedRoleType = RoleTypes.GuardianAngel;
                            else if (target.IsImpostor() || target.HasDesyncRole()) changedRoleType = RoleTypes.ImpostorGhost;
                            else changedRoleType = RoleTypes.CrewmateGhost;

                            break;
                        }
                        case true:
                        {
                            var seerIsKiller = seer.IsImpostor() || seer.HasDesyncRole();

                            if (!seerIsKiller && target.IsImpostor()) changedRoleType = RoleTypes.ImpostorGhost;
                            else changedRoleType = RoleTypes.CrewmateGhost;

                            break;
                        }
                        case false when isSelf && seer.HasKillButton():
                            continue;
                    }

                    sender.RpcSetRole(target, changedRoleType, seer.GetClientId());
                    hasValue = true;
                }
                catch (Exception e) { Utils.ThrowException(e); }
            }

            sender.SendMessage(dispose: !hasValue);
        }
    }

    private static void ResetAllCooldowns()
    {
        foreach (PlayerControl seer in Main.AllPlayerControls)
        {
            try
            {
                if (seer.IsAlive())
                {
                    seer.RpcResetAbilityCooldown();

                    if (Main.AllPlayerKillCooldown.TryGetValue(seer.PlayerId, out float kcd) && kcd >= 2f)
                        seer.SetKillCooldown(kcd - 2f);
                }
                else if (seer.HasGhostRole()) seer.RpcResetAbilityCooldown();
            }
            catch (Exception e) { Utils.ThrowException(e); }
        }

        CheckMurderPatch.TimeSinceLastKill.SetAllValues(0f);
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