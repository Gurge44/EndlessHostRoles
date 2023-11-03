using Hazel;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TOHE.Roles.Impostor
{
    public static class Stealth
    {
        private static readonly int Id = 641800;
        private static List<byte> playerIdList = new();

        private static OptionItem optionExcludeImpostors;
        private static OptionItem optionDarkenDuration;

        public static void SetupCustomOption()
        {
            Options.SetupSingleRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Stealth, 1);
            optionExcludeImpostors = BooleanOptionItem.Create(Id + 10, "StealthExcludeImpostors", false, TabGroup.ImpostorRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Stealth]);
            optionDarkenDuration = FloatOptionItem.Create(Id + 20, "StealthDarkenDuration", new(0.5f, 5f, 0.5f), 1f, TabGroup.ImpostorRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Stealth])
                .SetValueFormat(OptionFormat.Seconds);
        }

        private static bool excludeImpostors;
        private static float darkenDuration;
        private static float darkenTimer;
        private static PlayerControl[] darkenedPlayers;
        private static SystemTypes? darkenedRoom = null;

        public static void Init()
        {
            playerIdList = new();
        }
        public static void Add(byte playerId)
        {
            excludeImpostors = optionExcludeImpostors.GetBool();
            darkenDuration = darkenTimer = optionDarkenDuration.GetFloat();
            darkenedPlayers = null;

            playerIdList.Add(playerId);
        }
        public static bool IsEnable => playerIdList.Any();
        public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = Options.DefaultKillCooldown;
        public static void OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (!IsEnable) return;
            if (!killer.CanUseKillButton() || killer == null || target == null)
            {
                return;
            }
            var playersToDarken = FindPlayersInSameRoom(target);
            if (playersToDarken == null)
            {
                Logger.Info("The room will not dim because the hit detection for the room cannot be obtained.", "Stealth");
                return;
            }
            if (excludeImpostors)
            {
                playersToDarken = playersToDarken.Where(player => !player.Is(CustomRoles.Impostor)).ToList();
            }
            DarkenPlayers(playersToDarken);
        }
        /// <summary>Get all players in the same room as you</summary>
        private static List<PlayerControl> FindPlayersInSameRoom(PlayerControl killedPlayer)
        {
            var room = killedPlayer.GetPlainShipRoom();
            if (room == null)
            {
                return null;
            }
            var roomArea = room.roomArea;
            var roomName = room.RoomId;
            RpcDarken(roomName);
            return Main.AllAlivePlayerControls.Where(player => player != Utils.GetPlayerById(playerIdList[0]) && player.Collider.IsTouching(roomArea)).ToList();
        }
        /// <summary>Give the given player zero visibility for <see cref="darkenDuration"/> seconds.</summary>
        private static void DarkenPlayers(List<PlayerControl> playersToDarken)
        {
            darkenedPlayers = playersToDarken.ToArray();
            for (int i = 0; i < playersToDarken.Count; i++)
            {
                PlayerControl player = playersToDarken[i];
                Main.PlayerStates[player.PlayerId].IsBlackOut = true;
                player.MarkDirtySettings();
            }
        }
        public static void OnFixedUpdate(PlayerControl player)
        {
            if (!IsEnable) return;
            if (!AmongUsClient.Instance.AmHost)
            {
                return;
            }
            // when you're darkening someone
            if (darkenedPlayers != null)
            {
                // reduce timer
                darkenTimer -= Time.fixedDeltaTime;
                // When the timer reaches 0, return everyone's vision and reset the timer and darkening player.
                if (darkenTimer <= 0)
                {
                    ResetDarkenState();
                }
            }
        }
        public static void OnStartMeeting()
        {
            if (!IsEnable) return;
            if (AmongUsClient.Instance.AmHost)
            {
                ResetDarkenState();
            }
        }
        private static void RpcDarken(SystemTypes? roomType)
        {
            if (!IsEnable) return;
            Logger.Info($"Set the darkened room to {roomType?.ToString() ?? "null"}", "Stealth");
            darkenedRoom = roomType;
            SendRPC(roomType);
        }
        private static void SendRPC(SystemTypes? roomType)
        {
            if (!IsEnable) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.PenguinSync, SendOption.Reliable, -1);
            writer.Write((byte?)roomType ?? byte.MaxValue);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void ReceiveRPC(MessageReader reader)
        {
            var roomId = reader.ReadByte();
            darkenedRoom = roomId == byte.MaxValue ? null : (SystemTypes)roomId;
        }
        /// <summary>Removes the darkening effect that has occurred.</summary>
        private static void ResetDarkenState()
        {
            if (!IsEnable) return;
            if (darkenedPlayers != null)
            {
                for (int i = 0; i < darkenedPlayers.Length; i++)
                {
                    PlayerControl player = darkenedPlayers[i];
                    Main.PlayerStates[player.PlayerId].IsBlackOut = false;
                    player.MarkDirtySettings();
                }
                darkenedPlayers = null;
            }
            darkenTimer = darkenDuration;
            RpcDarken(null);
            Utils.NotifyRoles(SpecifySeer: Utils.GetPlayerById(playerIdList[0]));
        }

        public static string GetSuffix(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isHUD = false)
        {
            if (!IsEnable) return string.Empty;
            seen ??= seer;
            // During the meeting, unless it's my suffix or it's dark everywhere, I won't show anything.
            return isForMeeting || seer != Utils.GetPlayerById(playerIdList[0]) || seen != Utils.GetPlayerById(playerIdList[0]) || !darkenedRoom.HasValue || (seer.IsModClient() && !isHUD)
                ? string.Empty
                : string.Format(Translator.GetString("StealthDarkened"), DestroyableSingleton<TranslationController>.Instance.GetString(darkenedRoom.Value));
        }
    }
}
