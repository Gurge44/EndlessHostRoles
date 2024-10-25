using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using Hazel;
using UnityEngine;

namespace EHR.Impostor;

public sealed class Stealth : RoleBase
{
    private static readonly LogHandler Logger = EHR.Logger.Handler(nameof(Stealth));

    private static OptionItem OptionExcludeImpostors;
    private static OptionItem OptionDarkenDuration;

    public static bool On;
    private float darkenDuration;
    private PlayerControl[] darkenedPlayers;
    private SystemTypes? darkenedRoom;
    private float darkenTimer;

    private bool excludeImpostors;
    private PlayerControl StealthPC;
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(641900)
            .AutoSetupOption(ref OptionExcludeImpostors, true)
            .AutoSetupOption(ref OptionDarkenDuration, 5f, new FloatValueRule(0.5f, 30f, 0.5f), OptionFormat.Seconds);
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (!base.OnCheckMurder(killer, target))
        {
            return true;
        }

        var playersToDarken = FindPlayersInSameRoom(target);
        if (playersToDarken == null)
        {
            Logger.Info("No players to darken");
            return true;
        }

        if (excludeImpostors)
        {
            playersToDarken = playersToDarken.Where(player => !player.Is(CustomRoles.Impostor));
        }

        DarkenPlayers(playersToDarken);
        return true;
    }

    private IEnumerable<PlayerControl> FindPlayersInSameRoom(PlayerControl killedPlayer)
    {
        var room = killedPlayer.GetPlainShipRoom();
        if (room == null)
        {
            return null;
        }

        var roomArea = room.roomArea;
        var roomName = room.RoomId;
        RpcDarken(roomName);
        return Main.AllAlivePlayerControls.Where(player => player != StealthPC && player.Collider.IsTouching(roomArea));
    }

    private void DarkenPlayers(IEnumerable<PlayerControl> playersToDarken)
    {
        darkenedPlayers = playersToDarken.ToArray();
        foreach (var player in darkenedPlayers)
        {
            Main.PlayerStates[player.PlayerId].IsBlackOut = true;
            player.MarkDirtySettings();
        }
    }

    public override void Init()
    {
        On = false;
        excludeImpostors = OptionExcludeImpostors.GetBool();
        darkenDuration = OptionDarkenDuration.GetFloat();
    }

    public override void Add(byte playerId)
    {
        On = true;
        StealthPC = playerId.GetPlayer();
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!AmongUsClient.Instance.AmHost)
        {
            return;
        }

        if (darkenedPlayers != null)
        {
            darkenTimer -= Time.fixedDeltaTime;
            if (darkenTimer <= 0)
            {
                ResetDarkenState();
            }
        }
    }

    public override void OnReportDeadBody()
    {
        if (AmongUsClient.Instance.AmHost)
        {
            ResetDarkenState();
        }
    }

    private void RpcDarken(SystemTypes? roomType)
    {
        Logger.Info($"Darkened room set to {roomType?.ToString() ?? "null"}");
        darkenedRoom = roomType;
        Utils.SendRPC(CustomRPC.SyncRoleData, StealthPC.PlayerId, (byte?)roomType ?? byte.MaxValue);
    }

    public void ReceiveRPC(MessageReader reader)
    {
        var roomId = reader.ReadByte();
        darkenedRoom = roomId == byte.MaxValue ? null : (SystemTypes)roomId;
    }

    private void ResetDarkenState()
    {
        if (darkenedPlayers != null)
        {
            foreach (var player in darkenedPlayers)
            {
                Main.PlayerStates[player.PlayerId].IsBlackOut = false;
                player.MarkDirtySettings();
            }

            darkenedPlayers = null;
        }

        darkenTimer = darkenDuration;
        RpcDarken(null);
        Utils.NotifyRoles(SpecifySeer: StealthPC);
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl seen, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (isForMeeting || seer != StealthPC || seen != StealthPC || !darkenedRoom.HasValue)
        {
            return base.GetSuffix(seer, seen, isForMeeting, isForHud);
        }

        return string.Format(Translator.GetString("StealthDarkened"), DestroyableSingleton<TranslationController>.Instance.GetString(darkenedRoom.Value));
    }
}