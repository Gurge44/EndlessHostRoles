using AmongUs.GameOptions;
using HarmonyLib;
using Hazel;
using InnerNet;
using TOHE.Roles.Neutral;
using UnityEngine;

namespace TOHE.Modules
{
    // https://github.com/Rabek009/MoreGamemodes
    internal static class RoleBasisChanger
    {
        public static bool IsChangeInProgress;

        public static void ChangeRoleBasis(this PlayerControl player, RoleTypes targetVNRole)
        {
            if (!AmongUsClient.Instance.AmHost) return;

            if (player.PlayerId == PlayerControl.LocalPlayer.PlayerId)
            {
                PlayerControl.LocalPlayer.RpcSetRole(targetVNRole);
                PlayerControl.LocalPlayer.SyncSettings();

                Utils.NotifyRoles(SpecifySeer: PlayerControl.LocalPlayer);
                Utils.NotifyRoles(SpecifyTarget: PlayerControl.LocalPlayer);

                HudManager.Instance.SetHudActive(PlayerControl.LocalPlayer, PlayerControl.LocalPlayer.Data.Role, !GameStates.IsMeeting);

                return;
            }

            IsChangeInProgress = true;

            Vector2 position = player.Pos();
            PlayerControl PlayerPrefab = AmongUsClient.Instance.PlayerPrefab;
            PlayerControl newplayer = Object.Instantiate(PlayerPrefab, position, Quaternion.identity);

            newplayer.PlayerId = player.PlayerId;
            newplayer.FriendCode = player.FriendCode;
            newplayer.Puid = player.Puid;

            ClientData pclient = player.GetClient();

            player.TP(Pelican.GetBlackRoomPS());
            AmongUsClient.Instance.Despawn(player);
            AmongUsClient.Instance.Spawn(newplayer, player.OwnerId);
            pclient.Character = newplayer;

            newplayer.OwnerId = player.OwnerId;

            pclient.InScene = true;
            pclient.IsReady = true;

            newplayer.MyPhysics.ResetMoveState();

            GameData.Instance.RemovePlayer(player.PlayerId);
            GameData.Instance.AddPlayer(newplayer);

            newplayer.RpcSetRole(targetVNRole);

            GameData.Instance.SetDirty();
            newplayer.ReactorFlash(0.2f);
            newplayer.TP(position);

            _ = new LateTask(() => { IsChangeInProgress = false; }, 5f, log: false);
        }

        [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.Spawn))]
        public static class Client_SpawnDisable
        {
            public static bool Prefix(InnerNetClient __instance, [HarmonyArgument(0)] InnerNetObject netObjParent, [HarmonyArgument(1)] int ownerId, [HarmonyArgument(2)] SpawnFlags flags)
            {
                if (!IsChangeInProgress) return true;

                ownerId = (ownerId == -3) ? __instance.ClientId : ownerId;
                MessageWriter messageWriter = __instance.Streams[0];
                __instance.WriteSpawnMessage(netObjParent, ownerId, flags, messageWriter);
                return false;
            }
        }

        [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.Despawn))]
        public static class Client_DespawnDisable
        {
            public static bool Prefix(InnerNetClient __instance, [HarmonyArgument(0)] InnerNetObject objToDespawn)
            {
                if (!IsChangeInProgress) return true;

                MessageWriter messageWriter = __instance.Streams[0];
                messageWriter.StartMessage(5);
                messageWriter.WritePacked(objToDespawn.NetId);
                messageWriter.EndMessage();
                __instance.RemoveNetObject(objToDespawn);
                return false;
            }
        }
    }
}