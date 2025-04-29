using System;
using System.Collections.Generic;
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

        PlayerControl dummyImp = PlayerControl.LocalPlayer;

        var hasValue = false;
        var sender = CustomRpcSender.Create("AntiBlackout.RevivePlayersAndSetDummyImp", SendOption.Reliable);

        foreach (PlayerControl seer in Main.AllPlayerControls)
        {
            if (seer.IsModdedClient()) continue;

            if (Options.CurrentGameMode is CustomGameMode.Speedrun or CustomGameMode.SoloKombat or CustomGameMode.FFA or CustomGameMode.CaptureTheFlag or CustomGameMode.KingOfTheZones)
            {
                sender.RpcSetRole(seer, RoleTypes.Impostor, seer.OwnerId, noRpcForSelf: true);

                foreach (PlayerControl target in Main.AllPlayerControls)
                {
                    if (target.PlayerId != seer.PlayerId)
                        sender.RpcSetRole(target, RoleTypes.Crewmate, seer.OwnerId, noRpcForSelf: true);
                }

                hasValue = true;
                RestartMessageIfTooLong();
            }
            else
            {
                RoleTypes selfRoleType = seer.GetRoleTypes();
                bool seerIsAliveAndHasKillButton = seer.HasKillButton() && seer.IsAlive() && Options.CurrentGameMode is CustomGameMode.Standard or CustomGameMode.HideAndSeek;

                foreach (PlayerControl target in Main.AllPlayerControls)
                {
                    if (seerIsAliveAndHasKillButton)
                        sender.RpcSetRole(target, target.PlayerId != seer.PlayerId ? RoleTypes.Crewmate : selfRoleType, seer.GetClientId(), noRpcForSelf: true);
                    else
                        sender.RpcSetRole(target, target.PlayerId == dummyImp.PlayerId ? RoleTypes.Impostor : RoleTypes.Crewmate, seer.GetClientId(), noRpcForSelf: true);

                    hasValue = true;
                    RestartMessageIfTooLong();
                }
            }
        }

        sender.SendMessage(!hasValue);
        return;

        void RestartMessageIfTooLong()
        {
            if (sender.stream.Length > 800)
            {
                sender.SendMessage();
                sender = CustomRpcSender.Create("AntiBlackout.RevivePlayersAndSetDummyImp", SendOption.Reliable);
                hasValue = false;
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
            LateTask.New(RestoreIsDeadByExile, 0.3f, "AntiBlackOut_RestoreIsDeadByExile");
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
            playerInfo.MarkDirty();
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
                        sender.AutoStartRpc(pc.NetId, (byte)RpcCalls.Exiled);
                        sender.EndRpc();
                        hasValue = true;
                    }
                }

                sender.SendMessage(!hasValue);
            }, 0.3f, "AntiBlackout.AfterMeetingTasks");
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }

    public static void SetRealPlayerRoles()
    {
        if (CustomWinnerHolder.WinnerTeam != CustomWinner.Default) return;

        var hasValue = false;

        var sender = CustomRpcSender.Create("AntiBlackout.SetRealPlayerRoles", SendOption.Reliable);

        List<PlayerControl> selfExiled = [];

        switch (Options.CurrentGameMode)
        {
            case CustomGameMode.Standard:
            case CustomGameMode.HideAndSeek:
            {
                StartGameHostPatch.RpcSetRoleReplacer.ResetRoleMapMidGame();

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

                    if (seer.AmOwner)
                    {
                        target.SetRole(changedRoleType);
                        continue;
                    }

                    sender.RpcSetRole(target, changedRoleType, seer.OwnerId, noRpcForSelf: true);
                    hasValue = true;
                    RestartMessageIfTooLong();
                }

                foreach (PlayerControl pc in selfExiled)
                {
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
            case CustomGameMode.Quiz:
            {
                foreach (PlayerControl pc in Main.AllPlayerControls)
                {
                    sender.RpcSetRole(pc, RoleTypes.Crewmate, pc.OwnerId, noRpcForSelf: true);
                    RestartMessageIfTooLong();

                    if (pc.IsModdedClient()) continue;

                    if (!pc.IsAlive())
                    {
                        sender.AutoStartRpc(pc.NetId, (byte)RpcCalls.MurderPlayer, pc.OwnerId);
                        sender.WriteNetObject(pc);
                        sender.Write((int)MurderResultFlags.Succeeded);
                        sender.EndRpc();

                        pc.ReactorFlash(0.2f);
                    }

                    RestartMessageIfTooLong();
                }

                hasValue = true;
                break;
            }
            case CustomGameMode.FFA:
            case CustomGameMode.SoloKombat:
            case CustomGameMode.CaptureTheFlag:
            case CustomGameMode.KingOfTheZones:
            {
                sender.RpcSetRole(PlayerControl.LocalPlayer, RoleTypes.Crewmate, noRpcForSelf: true);
                RestartMessageIfTooLong();

                foreach (PlayerControl pc in Main.AllPlayerControls)
                {
                    if (pc.IsAlive())
                        sender.RpcSetRole(pc, RoleTypes.Impostor, pc.OwnerId, noRpcForSelf: true);
                    else
                    {
                        if (pc.IsModdedClient()) continue;
                        sender.AutoStartRpc(pc.NetId, (byte)RpcCalls.MurderPlayer, pc.OwnerId);
                        sender.WriteNetObject(pc);
                        sender.Write((int)MurderResultFlags.Succeeded);
                        sender.EndRpc();

                        pc.ReactorFlash(0.2f);
                    }

                    RestartMessageIfTooLong();
                }

                hasValue = true;
                break;
            }
        }

        foreach (PlayerControl pc in Main.AllPlayerControls)
        {
            hasValue |= sender.RpcResetAbilityCooldown(pc);
            hasValue |= sender.SetKillCooldown(pc);
            RestartMessageIfTooLong();
        }

        sender.SendMessage(!hasValue);

        return;

        void RestartMessageIfTooLong()
        {
            if (sender.stream.Length > 800)
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