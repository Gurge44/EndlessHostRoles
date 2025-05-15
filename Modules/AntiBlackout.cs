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
                sender.RpcSetRole(seer, RoleTypes.Impostor, seer.OwnerId, noRpcForSelf: false);

                foreach (PlayerControl target in Main.AllPlayerControls)
                {
                    if (target.PlayerId != seer.PlayerId)
                        sender.RpcSetRole(target, RoleTypes.Crewmate, seer.OwnerId, noRpcForSelf: false);
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
                        sender.RpcSetRole(target, target.PlayerId != seer.PlayerId ? RoleTypes.Crewmate : selfRoleType, seer.GetClientId(), noRpcForSelf: false);
                    else
                        sender.RpcSetRole(target, target.PlayerId == dummyImp.PlayerId ? RoleTypes.Impostor : RoleTypes.Crewmate, seer.GetClientId(), noRpcForSelf: false);

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

        byte[] keys = Main.AllPlayerControls.Select(x => x.PlayerId).Concat(Main.PlayerStates.Keys).Append(byte.MaxValue).ToArray();
        Dictionary<byte, CustomRpcSender> senders = keys.ToDictionary(x => x, _ => CustomRpcSender.Create("AntiBlackout.SetRealPlayerRoles", SendOption.Reliable));
        Dictionary<byte, bool> hasValue = keys.ToDictionary(x => x, _ => false);

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

                    senders[seer.PlayerId].RpcSetRole(target, changedRoleType, seer.OwnerId, noRpcForSelf: false);
                    hasValue[seer.PlayerId] = true;
                    RestartMessageIfTooLong(seer.PlayerId);
                }

                foreach (PlayerControl pc in selfExiled)
                {
                    CustomRpcSender sender = senders[byte.MaxValue];
                    sender.AutoStartRpc(pc.NetId, (byte)RpcCalls.Exiled);
                    sender.EndRpc();
                    hasValue[byte.MaxValue] = true;

                    if (!pc.IsModdedClient() && pc.PlayerId == ExileControllerWrapUpPatch.LastExiled?.PlayerId)
                    {
                        sender = senders[pc.PlayerId];
                        sender.AutoStartRpc(pc.NetId, (byte)RpcCalls.MurderPlayer, pc.OwnerId);
                        sender.WriteNetObject(pc);
                        sender.Write((int)MurderResultFlags.Succeeded);
                        sender.EndRpc();

                        hasValue[pc.PlayerId] = true;

                        pc.ReactorFlash(0.2f);
                    }

                    RestartMessageIfTooLong(byte.MaxValue, pc.PlayerId);
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
                    senders[pc.PlayerId].RpcSetRole(pc, RoleTypes.Crewmate, pc.OwnerId, noRpcForSelf: false);
                    hasValue[pc.PlayerId] = true;
                    RestartMessageIfTooLong(pc.PlayerId);

                    if (pc.IsModdedClient()) continue;

                    if (!pc.IsAlive())
                    {
                        CustomRpcSender sender = senders[pc.PlayerId];
                        sender.AutoStartRpc(pc.NetId, (byte)RpcCalls.MurderPlayer, pc.OwnerId);
                        sender.WriteNetObject(pc);
                        sender.Write((int)MurderResultFlags.Succeeded);
                        sender.EndRpc();

                        hasValue[pc.PlayerId] = true;

                        pc.ReactorFlash(0.2f);
                    }

                    RestartMessageIfTooLong(pc.PlayerId);
                }

                break;
            }
            case CustomGameMode.FFA:
            case CustomGameMode.SoloKombat:
            case CustomGameMode.CaptureTheFlag:
            case CustomGameMode.KingOfTheZones:
            {
                senders[byte.MaxValue].RpcSetRole(PlayerControl.LocalPlayer, RoleTypes.Crewmate, noRpcForSelf: false);
                hasValue[byte.MaxValue] = true;
                RestartMessageIfTooLong(byte.MaxValue);

                foreach (PlayerControl pc in Main.AllPlayerControls)
                {
                    CustomRpcSender sender = senders[pc.PlayerId];

                    if (pc.IsAlive())
                    {
                        sender.RpcSetRole(pc, RoleTypes.Impostor, pc.OwnerId, noRpcForSelf: false);
                        hasValue[pc.PlayerId] = true;
                    }
                    else
                    {
                        if (pc.IsModdedClient()) continue;
                        sender.AutoStartRpc(pc.NetId, (byte)RpcCalls.MurderPlayer, pc.OwnerId);
                        sender.WriteNetObject(pc);
                        sender.Write((int)MurderResultFlags.Succeeded);
                        sender.EndRpc();

                        hasValue[pc.PlayerId] = true;

                        pc.ReactorFlash(0.2f);
                    }

                    RestartMessageIfTooLong(pc.PlayerId);
                }

                break;
            }
        }

        foreach (PlayerControl pc in Main.AllPlayerControls)
        {
            CustomRpcSender sender = senders[pc.PlayerId];
            hasValue[pc.PlayerId] |= sender.RpcResetAbilityCooldown(pc);
            hasValue[pc.PlayerId] |= sender.SetKillCooldown(pc);
            RestartMessageIfTooLong(pc.PlayerId);
        }

        senders.Do(x => x.Value.SendMessage(dispose: !hasValue[x.Key]));

        return;

        void RestartMessageIfTooLong(params List<byte> ids)
        {
            foreach (var kvp in senders)
            {
                if (!ids.Contains(kvp.Key)) continue;

                if (kvp.Value.stream.Length > 800)
                {
                    kvp.Value.SendMessage();
                    hasValue[kvp.Key] = false;
                }
            }

            ids.ForEach(x => senders[x] = CustomRpcSender.Create("AntiBlackout.SetRealPlayerRoles", SendOption.Reliable));
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