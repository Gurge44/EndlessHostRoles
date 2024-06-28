using AmongUs.GameOptions;
using EHR.Neutral;
using HarmonyLib;
using Hazel;
using InnerNet;
using UnityEngine;

namespace EHR.Modules
{
    // https://github.com/Rabek009/MoreGamemodes
    internal static class RoleBasisChanger
    {
        public static bool IsChangeInProgress;

        public static void ChangeRoleBasis(this PlayerControl player, RoleTypes targetVNRole, bool canOverride = true)
        {
            if (!AmongUsClient.Instance.AmHost) return;

            if (player.IsHost())
            {
                player.RpcSetRole(targetVNRole);
                player.SyncSettings();

                Utils.NotifyRoles(SpecifySeer: player);
                Utils.NotifyRoles(SpecifyTarget: player);

                HudManager.Instance.SetHudActive(player, player.Data.Role, !GameStates.IsMeeting);

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
            GameData.Instance.AddPlayer(newplayer, newplayer.GetClient());

            newplayer.RpcSetRole(targetVNRole, canOverride);

            GameData.Instance.DirtyAllData();
            newplayer.ReactorFlash(0.2f);
            newplayer.TP(position);

            LateTask.New(() => { IsChangeInProgress = false; }, 5f, log: false);
        }

        [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.Spawn))]
        public static class InnerNetClientSpawnPatch
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
        public static class InnerNetClientDespawnPatch
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