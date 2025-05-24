using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Patches;
using Hazel;

namespace EHR;

// Most code is from https://github.com/EnhancedNetwork/TownofHost-Enhanced, as stated in the README credits

public static class AntiBlackout
{
    public static bool SkipTasks;
    private static Dictionary<byte, (bool IsDead, bool Disconnected)> IsDeadCache = [];
    private static readonly LogHandler Logger = EHR.Logger.Handler("AntiBlackout");
    private static PlayerControl DummyImp;

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
        if (CustomWinnerHolder.WinnerTeam != CustomWinner.Default || PlayerControl.AllPlayerControls.Count < 2) return;

        var players = Main.AllAlivePlayerControls;
        if (CheckForEndVotingPatch.TempExiledPlayer != null) players = players.Where(x => x.PlayerId != CheckForEndVotingPatch.TempExiledPlayer.PlayerId).ToArray();
        DummyImp = players.MinBy(x => x.PlayerId);
        if (DummyImp == null) return;

        foreach (PlayerControl seer in Main.AllPlayerControls)
        {
            if (seer.IsModdedClient()) continue;

            if (Options.CurrentGameMode is CustomGameMode.Speedrun or CustomGameMode.SoloKombat or CustomGameMode.FFA or CustomGameMode.CaptureTheFlag or CustomGameMode.KingOfTheZones)
            {
                seer.RpcSetRoleDesync(RoleTypes.Impostor, seer.OwnerId);

                foreach (PlayerControl target in Main.AllPlayerControls)
                {
                    if (target.PlayerId != seer.PlayerId)
                        target.RpcSetRoleDesync(RoleTypes.Crewmate, seer.OwnerId);
                }
            }
            else
            {
                RoleTypes selfRoleType = seer.GetRoleTypes();
                bool seerIsAliveAndHasKillButton = seer.HasKillButton() && seer.IsAlive() && Options.CurrentGameMode is CustomGameMode.Standard or CustomGameMode.HideAndSeek;

                foreach (PlayerControl target in Main.AllPlayerControls)
                {
                    if (seerIsAliveAndHasKillButton)
                        target.RpcSetRoleDesync(target.PlayerId != seer.PlayerId ? RoleTypes.Crewmate : selfRoleType, seer.GetClientId());
                    else
                        target.RpcSetRoleDesync(target.PlayerId == DummyImp.PlayerId ? RoleTypes.Impostor : RoleTypes.Crewmate, seer.GetClientId());
                }
            }
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
            LateTask.New(RestoreIsDeadByExile, 0.3f + Utils.CalculatePingDelay(), "AntiBlackOut_RestoreIsDeadByExile");
        }
    }

    private static void RestoreIsDeadByExile()
    {
        var sender = CustomRpcSender.Create("AntiBlackout RestoreIsDeadByExile", SendOption.Reliable);
        var hasValue = false;

        foreach (PlayerControl player in Main.AllPlayerControls)
        {
            if (player.Data.IsDead && !player.Data.Disconnected)
            {
                player.Exiled();
                sender.AutoStartRpc(player.NetId, (byte)RpcCalls.Exiled);
                sender.EndRpc();
                hasValue = true;
            }
        }

        sender.SendMessage(!hasValue);
    }

    public static void SendGameData([CallerMemberName] string callerMethodName = "")
    {
        Logger.Info($"SendGameData is called from {callerMethodName}");

        foreach (NetworkedPlayerInfo playerInfo in GameData.Instance.AllPlayers)
        {
            playerInfo.MarkDirty();
            AmongUsClient.Instance.SendAllStreamedObjects();
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

    public static void AfterMeetingTasks()
    {
        try
        {
            LateTask.New(() =>
            {
                var sender = CustomRpcSender.Create("AntiBlackout.SetDeadAfterMeetingTasks", SendOption.Reliable);
                var hasValue = false;

                foreach (PlayerControl pc in Main.AllAlivePlayerControls)
                {
                    if (!pc.IsAlive())
                    {
                        pc.Exiled();
                        sender.AutoStartRpc(pc.NetId, (byte)RpcCalls.Exiled);
                        sender.EndRpc();
                        hasValue = true;
                    }
                }

                sender.SendMessage(!hasValue);
            }, 0.3f + Utils.CalculatePingDelay(), "AntiBlackout.AfterMeetingTasks");
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }

    public static void SetRealPlayerRoles()
    {
        if (CustomWinnerHolder.WinnerTeam != CustomWinner.Default) return;
        
        var sender = CustomRpcSender.Create("AntiBlackout.SetRealPlayerRoles", SendOption.Reliable);
        var hasValue = false;

        List<PlayerControl> selfExiled = [];

        switch (Options.CurrentGameMode)
        {
            case CustomGameMode.Standard:
            case CustomGameMode.HideAndSeek:
            {
                foreach (((byte seerId, byte targetId), (RoleTypes roletype, _)) in StartGameHostPatch.RpcSetRoleReplacer.RoleMap)
                {
                    PlayerControl seer = seerId.GetPlayer();
                    PlayerControl target = targetId.GetPlayer();

                    if (seer == null || target == null) continue;

                    bool self = seerId == targetId;
                    bool dead = target.Data.IsDead;
                    RoleTypes changedRoleType = roletype;

                    if (dead)
                    {
                        if (self)
                        {
                            selfExiled.Add(seer);

                            if (target.HasGhostRole()) changedRoleType = RoleTypes.GuardianAngel;
                            else if (target.Is(Team.Impostor) || target.HasDesyncRole()) changedRoleType = RoleTypes.ImpostorGhost;
                            else changedRoleType = RoleTypes.CrewmateGhost;
                        }
                        else
                            changedRoleType = roletype is RoleTypes.Impostor or RoleTypes.Shapeshifter or RoleTypes.Phantom ? RoleTypes.ImpostorGhost : RoleTypes.CrewmateGhost;
                    }

                    target.RpcSetRoleDesync(changedRoleType, seer.OwnerId);
                }

                foreach (PlayerControl pc in selfExiled)
                {
                    pc.Exiled();
                    sender.AutoStartRpc(pc.NetId, (byte)RpcCalls.Exiled);
                    sender.EndRpc();
                    hasValue = true;

                    if (!pc.IsModdedClient() && pc.PlayerId == ExileControllerWrapUpPatch.LastExiled?.PlayerId)
                    {
                        sender.AutoStartRpc(pc.NetId, (byte)RpcCalls.MurderPlayer, pc.OwnerId);
                        sender.WriteNetObject(pc);
                        sender.Write((int)MurderResultFlags.Succeeded);
                        sender.EndRpc();

                        pc.ReactorFlash(0.2f);
                    }

                    RestartMessageIfTooLong();
                }

                break;
            }
            case CustomGameMode.Speedrun:
            case CustomGameMode.MoveAndStop:
            case CustomGameMode.HotPotato:
            case CustomGameMode.NaturalDisasters:
            case CustomGameMode.RoomRush:
            case CustomGameMode.TheMindGame:
            case CustomGameMode.Quiz:
            {
                foreach (PlayerControl pc in Main.AllPlayerControls)
                {
                    pc.RpcSetRoleDesync(Speedrun.CanKill.Contains(pc.PlayerId) ? RoleTypes.Impostor : RoleTypes.Crewmate, pc.OwnerId);

                    if (pc.IsModdedClient()) continue;

                    if (!pc.IsAlive())
                    {
                        sender.AutoStartRpc(pc.NetId, (byte)RpcCalls.MurderPlayer, pc.OwnerId);
                        sender.WriteNetObject(pc);
                        sender.Write((int)MurderResultFlags.Succeeded);
                        sender.EndRpc();

                        hasValue = true;
                        RestartMessageIfTooLong();

                        pc.ReactorFlash(0.2f);
                    }
                }

                break;
            }
            case CustomGameMode.FFA:
            case CustomGameMode.SoloKombat:
            case CustomGameMode.CaptureTheFlag:
            case CustomGameMode.KingOfTheZones:
            {
                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(DummyImp.NetId, (byte)RpcCalls.SetRole, SendOption.Reliable);
                writer.Write((ushort)RoleTypes.Crewmate);
                writer.Write(true);
                AmongUsClient.Instance.FinishRpcImmediately(writer);

                foreach (PlayerControl pc in Main.AllPlayerControls)
                {
                    if (pc.IsAlive())
                        pc.RpcSetRoleDesync(RoleTypes.Impostor, pc.OwnerId);
                    else
                    {
                        if (pc.IsModdedClient()) continue;
                        
                        sender.AutoStartRpc(pc.NetId, (byte)RpcCalls.MurderPlayer, pc.OwnerId);
                        sender.WriteNetObject(pc);
                        sender.Write((int)MurderResultFlags.Succeeded);
                        sender.EndRpc();

                        hasValue = true;
                        RestartMessageIfTooLong();

                        pc.ReactorFlash(0.2f);
                    }
                }

                break;
            }
        }

        sender.SendMessage(!hasValue);

        LateTask.New(() =>
        {
            foreach (PlayerControl pc in Main.AllPlayerControls)
            {
                pc.RpcResetAbilityCooldown();
                pc.SetKillCooldown();
            }
        }, 0.2f, log: false);

        return;

        void RestartMessageIfTooLong()
        {
            if (sender.stream.Length > 400)
            {
                sender.SendMessage();
                sender = CustomRpcSender.Create("AntiBlackout.SetRealPlayerRoles", SendOption.Reliable);
                hasValue = false;
            }
        }
    }

    public static void ResetAfterMeeting()
    {
        LateTask.New(() => SkipTasks = false, 1f, "Reset Blackout");
    }

    public static void Reset()
    {
        Logger.Info("==Reset==");
        IsDeadCache ??= [];
        IsDeadCache.Clear();
        IsCached = false;
        SkipTasks = false;
    }
}