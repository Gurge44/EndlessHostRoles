using System.Collections.Generic;
using System.Linq;
using Hazel;
using UnityEngine;

namespace TOHE.Roles.Impostor
{
    public class Stealth : RoleBase
    {
        private const int Id = 641900;
        private static List<byte> playerIdList = [];

        private static OptionItem optionExcludeImpostors;
        private static OptionItem optionDarkenDuration;

        public static void SetupCustomOption()
        {
            Options.SetupSingleRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Stealth, 1);
            optionExcludeImpostors = BooleanOptionItem.Create(Id + 10, "StealthExcludeImpostors", false, TabGroup.ImpostorRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Stealth]);
            optionDarkenDuration = FloatOptionItem.Create(Id + 20, "StealthDarkenDuration", new(0.5f, 10f, 0.5f), 3f, TabGroup.ImpostorRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Stealth])
                .SetValueFormat(OptionFormat.Seconds);
        }

        private static bool excludeImpostors;
        private static float darkenDuration;
        private static float darkenTimer;
        private static PlayerControl[] darkenedPlayers;
        private static SystemTypes? darkenedRoom;

        public override void Init()
        {
            playerIdList = [];
        }

        public override void Add(byte playerId)
        {
            excludeImpostors = optionExcludeImpostors.GetBool();
            darkenDuration = darkenTimer = optionDarkenDuration.GetFloat();
            darkenedPlayers = null;

            playerIdList.Add(playerId);
        }

        public override bool IsEnable => playerIdList.Count > 0;
        public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = Options.DefaultKillCooldown;

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (!IsEnable) return true;
            if (!killer.CanUseKillButton() || killer == null || target == null)
            {
                return false;
            }

            var playersToDarken = FindPlayersInSameRoom(target);
            if (playersToDarken == null)
            {
                Logger.Info("The room will not dim because the hit detection for the room cannot be obtained.", "Stealth");
                return true;
            }

            if (excludeImpostors)
            {
                playersToDarken = playersToDarken.Where(player => !player.Is(CustomRoleTypes.Impostor)).ToArray();
            }

            DarkenPlayers(playersToDarken);
            return true;
        }

        /// <summary>Get all players in the same room as you</summary>
        static PlayerControl[] FindPlayersInSameRoom(PlayerControl killedPlayer)
        {
            var room = killedPlayer.GetPlainShipRoom();
            if (room == null)
            {
                return null;
            }

            var roomArea = room.roomArea;
            var roomName = room.RoomId;
            RpcDarken(roomName);
            return Main.AllAlivePlayerControls.Where(player => player != Utils.GetPlayerById(playerIdList[0]) && player.Collider.IsTouching(roomArea)).ToArray();
        }

        /// <summary>Give the given player zero visibility for <see cref="darkenDuration"/> seconds.</summary>
        static void DarkenPlayers(PlayerControl[] playersToDarken)
        {
            darkenedPlayers = [.. playersToDarken];
            foreach (PlayerControl player in playersToDarken)
            {
                Main.PlayerStates[player.PlayerId].IsBlackOut = true;
                player.MarkDirtySettings();
            }
        }

        public override void OnFixedUpdate(PlayerControl player)
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

        public override void OnReportDeadBody()
        {
            if (!IsEnable) return;
            if (AmongUsClient.Instance.AmHost)
            {
                ResetDarkenState();
            }
        }

        void RpcDarken(SystemTypes? roomType)
        {
            if (!IsEnable) return;
            Logger.Info($"Set the darkened room to {roomType?.ToString() ?? "null"}", "Stealth");
            darkenedRoom = roomType;
            SendRPC(roomType);
        }

        void SendRPC(SystemTypes? roomType)
        {
            if (!IsEnable) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.PenguinSync, SendOption.Reliable);
            writer.Write((byte?)roomType ?? byte.MaxValue);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public static void ReceiveRPC(MessageReader reader)
        {
            var roomId = reader.ReadByte();
            darkenedRoom = roomId == byte.MaxValue ? null : (SystemTypes)roomId;
        }

        /// <summary>Removes the darkening effect that has occurred.</summary>
        void ResetDarkenState()
        {
            if (!IsEnable) return;
            if (darkenedPlayers != null)
            {
                foreach (PlayerControl player in darkenedPlayers)
                {
                    Main.PlayerStates[player.PlayerId].IsBlackOut = false;
                    player.MarkDirtySettings();
                }

                darkenedPlayers = null;
            }

            darkenTimer = darkenDuration;
            RpcDarken(null);
            Utils.NotifyRoles(SpecifySeer: Utils.GetPlayerById(playerIdList[0]), SpecifyTarget: Utils.GetPlayerById(playerIdList[0]));
        }

        public static string GetSuffix(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isHUD = false)
        {
            if (Main.PlayerStates[seer.PlayerId].Role is not Stealth { IsEnable: true }) return string.Empty;
            seen ??= seer;
            // During the meeting, unless it's my suffix, or it's dark everywhere, I won't show anything.
            return isForMeeting || seen != seer || !darkenedRoom.HasValue || (seer.IsModClient() && !isHUD)
                ? string.Empty
                : string.Format(Translator.GetString("StealthDarkened"), DestroyableSingleton<TranslationController>.Instance.GetString(darkenedRoom.Value));
        }
    }
}