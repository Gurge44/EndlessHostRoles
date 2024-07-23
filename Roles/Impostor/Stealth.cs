using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using Hazel;
using UnityEngine;

namespace EHR.Impostor
{
    public class Stealth : RoleBase
    {
        private const int Id = 641900;
        private static List<byte> PlayerIdList = [];

        private static OptionItem OptionExcludeImpostors;
        private static OptionItem OptionDarkenDuration;

        private static bool ExcludeImpostors;
        private static float DarkenDuration;
        private PlayerControl[] DarkenedPlayers = [];
        private SystemTypes? DarkenedRoom;
        private float DarkenTimer;

        private PlayerControl StealthPC;

        public override bool IsEnable => PlayerIdList.Count > 0;

        public static void SetupCustomOption()
        {
            Options.SetupSingleRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Stealth);
            OptionExcludeImpostors = new BooleanOptionItem(Id + 10, "StealthExcludeImpostors", false, TabGroup.ImpostorRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Stealth]);
            OptionDarkenDuration = new FloatOptionItem(Id + 20, "StealthDarkenDuration", new(0.5f, 10f, 0.5f), 3f, TabGroup.ImpostorRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Stealth])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public override void Init()
        {
            PlayerIdList = [];
            ExcludeImpostors = OptionExcludeImpostors.GetBool();
            DarkenDuration = DarkenTimer = OptionDarkenDuration.GetFloat();
        }

        public override void Add(byte playerId)
        {
            StealthPC = Utils.GetPlayerById(playerId);
            DarkenedPlayers = null;

            PlayerIdList.Add(playerId);
        }

        public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = Options.DefaultKillCooldown;

        public override void OnMurder(PlayerControl killer, PlayerControl target)
        {
            if (!IsEnable) return;

            var playersToDarken = FindPlayersInSameRoom(target);
            if (playersToDarken == null)
            {
                Logger.Info("The room will not dim because the hit detection for the room cannot be obtained.", "Stealth");
                return;
            }

            if (ExcludeImpostors)
            {
                playersToDarken = playersToDarken.Where(player => !player.Is(CustomRoleTypes.Impostor)).ToArray();
            }

            DarkenPlayers(playersToDarken);
        }

        /// <summary>Get all players in the same room as you</summary>
        PlayerControl[] FindPlayersInSameRoom(PlayerControl killedPlayer)
        {
            var room = killedPlayer.GetPlainShipRoom();
            if (room == null) return null;

            RpcDarken(room.RoomId);
            return Main.AllAlivePlayerControls.Where(p => p.PlayerId != StealthPC.PlayerId && p.GetPlainShipRoom() == room).ToArray();
        }

        /// <summary>Give the given player zero visibility for <see cref="DarkenDuration"/> seconds.</summary>
        void DarkenPlayers(PlayerControl[] playersToDarken)
        {
            DarkenedPlayers = [.. playersToDarken];
            foreach (PlayerControl player in playersToDarken)
            {
                Main.PlayerStates[player.PlayerId].IsBlackOut = true;
                player.MarkDirtySettings();
            }
        }

        public override void OnFixedUpdate(PlayerControl player)
        {
            if (!IsEnable || !AmongUsClient.Instance.AmHost) return;

            // when you're darkening someone
            if (DarkenedPlayers != null)
            {
                // reduce timer
                DarkenTimer -= Time.fixedDeltaTime;
                // When the timer reaches 0, return everyone's vision and reset the timer and darkening player.
                if (DarkenTimer <= 0)
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
            DarkenedRoom = roomType;
            SendRPC(roomType);
        }

        void SendRPC(SystemTypes? roomType)
        {
            if (!IsEnable) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.StealthDarken, SendOption.Reliable);
            writer.Write(StealthPC.PlayerId);
            writer.Write((byte?)roomType ?? byte.MaxValue);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public static void ReceiveRPC(MessageReader reader)
        {
            var roomId = reader.ReadByte();
            var stealthId = reader.ReadByte();
            ((Stealth)Main.PlayerStates[stealthId].Role).DarkenedRoom = roomId == byte.MaxValue ? null : (SystemTypes)roomId;
        }

        /// <summary>Removes the darkening effect that has occurred.</summary>
        void ResetDarkenState()
        {
            if (!IsEnable) return;
            if (DarkenedPlayers != null)
            {
                foreach (PlayerControl player in DarkenedPlayers)
                {
                    Main.PlayerStates[player.PlayerId].IsBlackOut = false;
                    player.MarkDirtySettings();
                }

                DarkenedPlayers = null;
            }

            DarkenTimer = DarkenDuration;
            RpcDarken(null);
            Utils.NotifyRoles(SpecifySeer: StealthPC, SpecifyTarget: StealthPC);
        }

        public override string GetSuffix(PlayerControl seer, PlayerControl seen, bool hud = false, bool meeting = false)
        {
            if (Main.PlayerStates[seer.PlayerId].Role is not Stealth { IsEnable: true }) return string.Empty;
            seen ??= seer;
            // During the meeting, unless it's my suffix, or it's dark everywhere, I won't show anything.
            return meeting || seen != seer || !DarkenedRoom.HasValue || (seer.IsModClient() && !hud)
                ? string.Empty
                : string.Format(Translator.GetString("StealthDarkened"), DestroyableSingleton<TranslationController>.Instance.GetString(DarkenedRoom.Value));
        }
    }
}