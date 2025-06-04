using System;
using System.Collections.Generic;
using System.Linq;
using EHR;
using EHR.Crewmate;
using EHR.Modules;
using HarmonyLib;
using Hazel;
using InnerNet;
using TMPro;
using UnityEngine;

// Credit: https://github.com/Rabek009/MoreGamemodes/blob/e054eb498094dfca0a365fc6b6fea8d17f9974d7/Modules/AllObjects
// Huge thanks to Rabek009 for this code!

namespace EHR
{
    public class CustomNetObject
    {
        public static List<CustomNetObject> AllObjects = [];
        private static int MaxId = -1;

        private static List<CustomNetObject> TempDespawnedObjects = [];
        protected int Id;
        public PlayerControl playerControl;
        public Vector2 Position;
        protected string Sprite;

        public void RpcChangeSprite(string sprite)
        {
            if (!AmongUsClient.Instance.AmHost) return;
            if (this is not DisasterWarningTimer) Logger.Info($" Change Custom Net Object {GetType().Name} (ID {Id}) sprite", "CNO.RpcChangeSprite");

            Sprite = sprite;

            LateTask.New(() =>
            {
                playerControl.RawSetName(sprite);
                string name = PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].PlayerName;
                int colorId = PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].ColorId;
                string hatId = PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].HatId;
                string skinId = PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].SkinId;
                string petId = PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].PetId;
                string visorId = PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].VisorId;
                var sender = CustomRpcSender.Create("SetFakeData", SendOption.Reliable);
                MessageWriter writer = sender.stream;
                sender.StartMessage();
                PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].PlayerName = "<size=14><br></size>" + sprite;
                PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].ColorId = 255;
                PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].HatId = "";
                PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].SkinId = "";
                PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].PetId = "";
                PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].VisorId = "";
                writer.StartMessage(1);
                {
                    writer.WritePacked(PlayerControl.LocalPlayer.Data.NetId);
                    PlayerControl.LocalPlayer.Data.Serialize(writer, false);
                }
                writer.EndMessage();

                sender.StartRpc(playerControl.NetId, (byte)RpcCalls.Shapeshift)
                    .WriteNetObject(PlayerControl.LocalPlayer)
                    .Write(false)
                    .EndRpc();

                PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].PlayerName = name;
                PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].ColorId = colorId;
                PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].HatId = hatId;
                PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].SkinId = skinId;
                PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].PetId = petId;
                PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].VisorId = visorId;
                writer.StartMessage(1);
                {
                    writer.WritePacked(PlayerControl.LocalPlayer.Data.NetId);
                    PlayerControl.LocalPlayer.Data.Serialize(writer, false);
                }
                writer.EndMessage();

                sender.EndMessage();
                sender.SendMessage();
            }, 0f);
        }

        public void TP(Vector2 position)
        {
            if (AmongUsClient.Instance.AmClient) playerControl.NetTransform.SnapTo(position, (ushort)(playerControl.NetTransform.lastSequenceId + 1U));
            ushort num = (ushort)(playerControl.NetTransform.lastSequenceId + 2U);
            MessageWriter messageWriter = AmongUsClient.Instance.StartRpcImmediately(playerControl.NetTransform.NetId, 21, SendOption.None);
            NetHelpers.WriteVector2(position, messageWriter);
            messageWriter.Write(num);
            AmongUsClient.Instance.FinishRpcImmediately(messageWriter);

            Position = position;
        }

        public void Despawn()
        {
            if (!AmongUsClient.Instance.AmHost) return;
            Logger.Info($" Despawn Custom Net Object {GetType().Name} (ID {Id})", "CNO.Despawn");

            try
            {
                if (playerControl != null)
                {
                    MessageWriter writer = MessageWriter.Get(SendOption.Reliable);
                    writer.StartMessage(5);
                    writer.Write(AmongUsClient.Instance.GameId);
                    writer.StartMessage(5);
                    writer.WritePacked(playerControl.NetId);
                    writer.EndMessage();
                    writer.EndMessage();
                    AmongUsClient.Instance.SendOrDisconnect(writer);
                    writer.Recycle();

                    AmongUsClient.Instance.RemoveNetObject(playerControl);
                    Object.Destroy(playerControl.gameObject);
                }
                
                AllObjects.Remove(this);
            }
            catch (Exception e) { Utils.ThrowException(e); }
        }

        protected void Hide(PlayerControl player)
        {
            if (!AmongUsClient.Instance.AmHost) return;
            Logger.Info($" Hide Custom Net Object {GetType().Name} (ID {Id}) from {player.GetNameWithRole()}", "CNO.Hide");

            if (player.AmOwner)
            {
                LateTask.New(() => playerControl.transform.FindChild("Names").FindChild("NameText_TMP").gameObject.SetActive(false), 0.1f);
                playerControl.Visible = false;
                return;
            }

            LateTask.New(() =>
            {
                var sender = CustomRpcSender.Create("FixModdedClientCNOText", SendOption.Reliable);

                sender.AutoStartRpc(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.FixModdedClientCNO, player.GetClientId())
                    .WriteNetObject(playerControl)
                    .Write(false)
                    .EndRpc();

                sender.SendMessage();
            }, 0.4f);

            MessageWriter writer = MessageWriter.Get(SendOption.Reliable);
            writer.StartMessage(6);
            writer.Write(AmongUsClient.Instance.GameId);
            writer.WritePacked(player.GetClientId());
            writer.StartMessage(5);
            writer.WritePacked(playerControl.NetId);
            writer.EndMessage();
            writer.EndMessage();
            AmongUsClient.Instance.SendOrDisconnect(writer);
            writer.Recycle();
        }

        protected virtual void OnFixedUpdate() { }

        protected void CreateNetObject(string sprite, Vector2 position)
        {
            if (GameStates.IsEnded || !AmongUsClient.Instance.AmHost) return;
            Logger.Info($" Create Custom Net Object {GetType().Name} (ID {MaxId + 1}) at {position}", "CNO.CreateNetObject");
            playerControl = Object.Instantiate(AmongUsClient.Instance.PlayerPrefab, Vector2.zero, Quaternion.identity);
            playerControl.PlayerId = 254;
            playerControl.isNew = false;
            playerControl.notRealPlayer = true;
            AmongUsClient.Instance.NetIdCnt += 1U;
            MessageWriter msg = MessageWriter.Get(SendOption.Reliable);
            msg.StartMessage(5);
            msg.Write(AmongUsClient.Instance.GameId);
            msg.StartMessage(4);
            msg.WritePacked(playerControl.SpawnId);
            msg.WritePacked(-2);
            msg.Write((byte)SpawnFlags.None);
            InnerNetObject[] componentsInChildren = playerControl.GetComponentsInChildren<InnerNetObject>();
            msg.WritePacked(componentsInChildren.Length);

            for (int index = 0; index < componentsInChildren.Length; ++index)
            {
                InnerNetObject innerNetObject = componentsInChildren[index];
                innerNetObject.OwnerId = -2;
                innerNetObject.SpawnFlags = SpawnFlags.None;

                if (innerNetObject.NetId == 0U)
                {
                    innerNetObject.NetId = AmongUsClient.Instance.NetIdCnt++;
                    InnerNetObjectCollection allObjects = AmongUsClient.Instance.allObjects;
                    allObjects.allObjects.Add(innerNetObject);
                    allObjects.allObjectsFast.Add(innerNetObject.NetId, innerNetObject);
                }

                msg.WritePacked(innerNetObject.NetId);
                msg.StartMessage(1);
                innerNetObject.Serialize(msg, true);
                msg.EndMessage();
            }

            msg.EndMessage();
            msg.EndMessage();
            AmongUsClient.Instance.SendOrDisconnect(msg);
            msg.Recycle();

            if (GameStates.CurrentServerType == GameStates.ServerType.Vanilla)
            {
                MessageWriter msg2 = MessageWriter.Get(SendOption.Reliable);
                msg2.StartMessage(6);
                msg2.Write(AmongUsClient.Instance.GameId);
                msg2.WritePacked(int.MaxValue);

                for (uint i = 1; i <= 3; ++i)
                {
                    msg2.StartMessage(4);
                    msg2.WritePacked(2U);
                    msg2.WritePacked(-2);
                    msg2.Write((byte)SpawnFlags.None);
                    msg2.WritePacked(1);
                    msg2.WritePacked(AmongUsClient.Instance.NetIdCnt - i);
                    msg2.StartMessage(1);
                    msg2.EndMessage();
                    msg2.EndMessage();
                }

                msg2.EndMessage();
                AmongUsClient.Instance.SendOrDisconnect(msg2);
                msg2.Recycle();
            }

            if (PlayerControl.AllPlayerControls.Contains(playerControl))
                PlayerControl.AllPlayerControls.Remove(playerControl);

            LateTask.New(() =>
            {
                playerControl.NetTransform.RpcSnapTo(position);
                playerControl.RawSetName(sprite);
                string name = PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].PlayerName;
                int colorId = PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].ColorId;
                string hatId = PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].HatId;
                string skinId = PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].SkinId;
                string petId = PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].PetId;
                string visorId = PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].VisorId;
                var sender = CustomRpcSender.Create("SetFakeData", SendOption.Reliable);
                MessageWriter writer = sender.stream;
                sender.StartMessage();
                PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].PlayerName = "<size=14><br></size>" + sprite;
                PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].ColorId = 255;
                PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].HatId = "";
                PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].SkinId = "";
                PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].PetId = "";
                PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].VisorId = "";
                writer.StartMessage(1);
                {
                    writer.WritePacked(PlayerControl.LocalPlayer.Data.NetId);
                    PlayerControl.LocalPlayer.Data.Serialize(writer, false);
                }
                writer.EndMessage();

                sender.StartRpc(playerControl.NetId, (byte)RpcCalls.Shapeshift)
                    .WriteNetObject(PlayerControl.LocalPlayer)
                    .Write(false)
                    .EndRpc();

                PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].PlayerName = name;
                PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].ColorId = colorId;
                PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].HatId = hatId;
                PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].SkinId = skinId;
                PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].PetId = petId;
                PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].VisorId = visorId;
                writer.StartMessage(1);
                {
                    writer.WritePacked(PlayerControl.LocalPlayer.Data.NetId);
                    PlayerControl.LocalPlayer.Data.Serialize(writer, false);
                }
                writer.EndMessage();

                sender.EndMessage();
                sender.SendMessage();
            }, 0.2f);

            Position = position;
            Sprite = sprite;
            ++MaxId;
            Id = MaxId;
            if (MaxId == int.MaxValue) MaxId = int.MinValue;

            AllObjects.Add(this);

            foreach (PlayerControl pc in Main.AllPlayerControls)
            {
                if (pc.AmOwner) continue;

                LateTask.New(() =>
                {
                    var sender = CustomRpcSender.Create("SetFakeData", SendOption.Reliable);
                    MessageWriter writer = sender.stream;
                    sender.StartMessage(pc.GetClientId());
                    writer.StartMessage(1);
                    {
                        writer.WritePacked(playerControl.NetId);
                        writer.Write(pc.PlayerId);
                    }
                    writer.EndMessage();

                    sender.StartRpc(playerControl.NetId, (byte)RpcCalls.MurderPlayer)
                        .WriteNetObject(playerControl)
                        .Write((int)MurderResultFlags.FailedError)
                        .EndRpc();

                    writer.StartMessage(1);
                    {
                        writer.WritePacked(playerControl.NetId);
                        writer.Write((byte)254);
                    }
                    writer.EndMessage();

                    sender.EndMessage();
                    sender.SendMessage();
                }, 0.1f);
            }

            LateTask.New(() => playerControl.transform.FindChild("Names").FindChild("NameText_TMP").gameObject.SetActive(true), 0.1f); // Fix for Host
            LateTask.New(() => Utils.SendRPC(CustomRPC.FixModdedClientCNO, playerControl, true), 0.4f); // Fix for Non-Host Modded

            LateTask.New(() => RpcChangeSprite(sprite), 0.6f);
        }

        public static void FixedUpdate()
        {
            foreach (CustomNetObject cno in AllObjects.ToArray())
                cno?.OnFixedUpdate();
        }

        public static CustomNetObject Get(int id)
        {
            return AllObjects.FirstOrDefault(x => x.Id == id);
        }

        public static void Reset()
        {
            try
            {
                AllObjects.ToArray().Do(x => x.Despawn());
                AllObjects.Clear();
            }
            catch (Exception e) { Utils.ThrowException(e); }
        }

        public static void OnMeeting()
        {
            TempDespawnedObjects = AllObjects.ToList();
            Reset();
        }

        public static void AfterMeeting()
        {
            TempDespawnedObjects.ForEach(x => x.CreateNetObject(x.Sprite, x.Position));
            TempDespawnedObjects.Clear();
        }
    }


    internal sealed class TornadoObject : CustomNetObject
    {
        private readonly long SpawnTimeStamp;
        private bool Gone;

        public TornadoObject(Vector2 position, IEnumerable<byte> visibleList)
        {
            SpawnTimeStamp = Utils.TimeStamp;
            CreateNetObject("<size=100%><font=\"VCR SDF\"><line-height=67%><alpha=#00>\u2588<alpha=#00>\u2588<#bababa>\u2588<#bababa>\u2588<#bababa>\u2588<#bababa>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<#bababa>\u2588<#bababa>\u2588<#8c8c8c>\u2588<#8c8c8c>\u2588<#bababa>\u2588<#bababa>\u2588<alpha=#00>\u2588<br><#bababa>\u2588<#bababa>\u2588<#8c8c8c>\u2588<#8c8c8c>\u2588<#8c8c8c>\u2588<#8c8c8c>\u2588<#bababa>\u2588<#bababa>\u2588<br><#bababa>\u2588<#8c8c8c>\u2588<#8c8c8c>\u2588<#636363>\u2588<#636363>\u2588<#8c8c8c>\u2588<#8c8c8c>\u2588<#bababa>\u2588<br><#bababa>\u2588<#8c8c8c>\u2588<#8c8c8c>\u2588<#636363>\u2588<#636363>\u2588<#8c8c8c>\u2588<#8c8c8c>\u2588<#bababa>\u2588<br><#bababa>\u2588<#bababa>\u2588<#8c8c8c>\u2588<#8c8c8c>\u2588<#8c8c8c>\u2588<#8c8c8c>\u2588<#bababa>\u2588<#bababa>\u2588<br><alpha=#00>\u2588<#bababa>\u2588<#bababa>\u2588<#8c8c8c>\u2588<#8c8c8c>\u2588<#bababa>\u2588<#bababa>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<#bababa>\u2588<#bababa>\u2588<#bababa>\u2588<#bababa>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br></color></line-height></font></size>", position);
            Main.AllAlivePlayerControls.ExceptBy(visibleList, x => x.PlayerId).Do(Hide);
        }

        protected override void OnFixedUpdate()
        {
            base.OnFixedUpdate();

            try
            {
                if (Gone) return;

                if (SpawnTimeStamp + Tornado.TornadoDuration.GetInt() < Utils.TimeStamp)
                {
                    Gone = true;
                    Despawn();
                }
            }
            catch (NullReferenceException)
            {
                try { Despawn(); }
                finally { Gone = true; }
            }
        }
    }

    internal sealed class PlayerDetector : CustomNetObject
    {
        public PlayerDetector(Vector2 position, List<byte> visibleList, out int id)
        {
            CreateNetObject("<size=100%><font=\"VCR SDF\"><line-height=67%><alpha=#00>\u2588<alpha=#00>\u2588<#33e6b0>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<#33e6b0>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<#33e6b0>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<#33e6b0>\u2588<alpha=#00>\u2588<br><#33e6b0>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<#33e6b0>\u2588<#33e6b0>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<#33e6b0>\u2588<br><#33e6b0>\u2588<alpha=#00>\u2588<#33e6b0>\u2588<#000000>\u2588<#000000>\u2588<#33e6b0>\u2588<alpha=#00>\u2588<#33e6b0>\u2588<br><#33e6b0>\u2588<alpha=#00>\u2588<#33e6b0>\u2588<#000000>\u2588<#000000>\u2588<#33e6b0>\u2588<alpha=#00>\u2588<#33e6b0>\u2588<br><#33e6b0>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<#33e6b0>\u2588<#33e6b0>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<#33e6b0>\u2588<br><alpha=#00>\u2588<#33e6b0>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<#33e6b0>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<#33e6b0>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<#33e6b0>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br></color></line-height></font></size>", position);
            Main.AllAlivePlayerControls.ExceptBy(visibleList, x => x.PlayerId).Do(Hide);
            id = Id;
        }
    }

    internal sealed class AdventurerItem : CustomNetObject
    {
        public readonly Adventurer.Resource Resource;

        internal AdventurerItem(Vector2 position, Adventurer.Resource resource, IEnumerable<byte> visibleList)
        {
            Resource = resource;
            (char Icon, Color Color) data = Adventurer.ResourceDisplayData[resource];
            CreateNetObject($"<size=300%><font=\"VCR SDF\"><line-height=67%>{Utils.ColorString(data.Color, data.Icon.ToString())}</line-height></font></size>", position);
            Main.AllAlivePlayerControls.ExceptBy(visibleList, x => x.PlayerId).Do(Hide);
        }
    }

    internal sealed class Toilet : CustomNetObject
    {
        internal Toilet(Vector2 position, IEnumerable<PlayerControl> hideList)
        {
            CreateNetObject("<size=100%><font=\"VCR SDF\"><line-height=67%><alpha=#00>\u2588<#e6e6e6>\u2588<#e6e6e6>\u2588<#e6e6e6>\u2588<#e6e6e6>\u2588<#e6e6e6>\u2588<#e6e6e6>\u2588<#e6e6e6>\u2588<#e6e6e6>\u2588<alpha=#00>\u2588<br><#e6e6e6>\u2588<#d3d4ce>\u2588<#d3d4ce>\u2588<#d3d4ce>\u2588<#d3d4ce>\u2588<#d3d4ce>\u2588<#d3d4ce>\u2588<#d3d4ce>\u2588<#d3d4ce>\u2588<#e6e6e6>\u2588<br><#e6e6e6>\u2588<#d3d4ce>\u2588<#d3d4ce>\u2588<#d3d4ce>\u2588<#d3d4ce>\u2588<#d3d4ce>\u2588<#d3d4ce>\u2588<#d3d4ce>\u2588<#d3d4ce>\u2588<#e6e6e6>\u2588<br><alpha=#00>\u2588<#e6e6e6>\u2588<#d3d4ce>\u2588<#d3d4ce>\u2588<#d3d4ce>\u2588<#d3d4ce>\u2588<#d3d4ce>\u2588<#d3d4ce>\u2588<#e6e6e6>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<#e6e6e6>\u2588<#e6e6e6>\u2588<#d3d4ce>\u2588<#dedede>\u2588<#dedede>\u2588<#d3d4ce>\u2588<#e6e6e6>\u2588<#e6e6e6>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<#bfbfbf>\u2588<#454545>\u2588<#333333>\u2588<#333333>\u2588<#333333>\u2588<#333333>\u2588<#333333>\u2588<#333333>\u2588<#bfbfbf>\u2588<br><alpha=#00>\u2588<#bfbfbf>\u2588<#bfbfbf>\u2588<#454545>\u2588<#454545>\u2588<#454545>\u2588<#454545>\u2588<#454545>\u2588<#454545>\u2588<#bfbfbf>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<#bfbfbf>\u2588<#bfbfbf>\u2588<#bfbfbf>\u2588<#bfbfbf>\u2588<#bfbfbf>\u2588<#bfbfbf>\u2588<#bfbfbf>\u2588<#bfbfbf>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<#dedede>\u2588<#dedede>\u2588<#dedede>\u2588<#dedede>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<#dedede>\u2588<#dedede>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br></color></line-height></font></size>", position);
            hideList.Do(Hide);
        }
    }

    internal sealed class BlackHole : CustomNetObject
    {
        internal BlackHole(Vector2 position)
        {
            CreateNetObject("<size=100%><font=\"VCR SDF\"><line-height=67%><alpha=#00>█<alpha=#00>█<#000000>█<#19131c>█<#000000>█<#000000>█<alpha=#00>█<alpha=#00>█<br><alpha=#00>█<#412847>█<#000000>█<#19131c>█<#000000>█<#412847>█<#260f26>█<alpha=#00>█<br><#000000>█<#412847>█<#412847>█<#000000>█<#260f26>█<#1c0d1c>█<#19131c>█<#000000>█<br><#19131c>█<#000000>█<#412847>█<#1c0d1c>█<#1c0d1c>█<#000000>█<#19131c>█<#000000>█<br><#000000>█<#000000>█<#260f26>█<#1c0d1c>█<#1c0d1c>█<#000000>█<#000000>█<#260f26>█<br><#000000>█<#260f26>█<#1c0d1c>█<#1c0d1c>█<#19131c>█<#412847>█<#412847>█<#19131c>█<br><alpha=#00>█<#260f26>█<#412847>█<#412847>█<#19131c>█<#260f26>█<#19131c>█<alpha=#00>█<br><alpha=#00>█<alpha=#00>█<#412847>█<#260f26>█<#260f26>█<#000000>█<alpha=#00>█<alpha=#00>█<br></line-height></size>", position);
        }
    }

    internal sealed class SprayedArea : CustomNetObject
    {
        public SprayedArea(Vector2 position, IEnumerable<byte> visibleList)
        {
            CreateNetObject("<size=100%><font=\"VCR SDF\"><line-height=67%><alpha=#00>\u2588<alpha=#00>\u2588<#ffd000>\u2588<#ffd000>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<#ffd000>\u2588<#ffd000>\u2588<#ffd000>\u2588<#ffd000>\u2588<alpha=#00>\u2588<br><#ffd000>\u2588<#ffd000>\u2588<#ffd000>\u2588<#ffd000>\u2588<#ffd000>\u2588<#ffd000>\u2588<br><#ffd000>\u2588<#ffd000>\u2588<#ffd000>\u2588<#ffd000>\u2588<#ffd000>\u2588<#ffd000>\u2588<br><alpha=#00>\u2588<#ffd000>\u2588<#ffd000>\u2588<#ffd000>\u2588<#ffd000>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<#ffd000>\u2588<#ffd000>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br></line-height></size>", position);
            Main.AllAlivePlayerControls.ExceptBy(visibleList, x => x.PlayerId).Do(Hide);
        }
    }

    internal sealed class CatcherTrap : CustomNetObject
    {
        public CatcherTrap(Vector2 position, PlayerControl catcher)
        {
            CreateNetObject("<size=100%><font=\"VCR SDF\"><line-height=67%><alpha=#00>\u2588<alpha=#00>\u2588<#ccffda>\u2588<#ccffda>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<#ccffda>\u2588<#ccffda>\u2588<#ccffda>\u2588<#ccffda>\u2588<alpha=#00>\u2588<br><#ccffda>\u2588<#ccffda>\u2588<#ccffda>\u2588<#ccffda>\u2588<#ccffda>\u2588<#ccffda>\u2588<br><#ccffda>\u2588<#ccffda>\u2588<#ccffda>\u2588<#ccffda>\u2588<#ccffda>\u2588<#ccffda>\u2588<br><alpha=#00>\u2588<#ccffda>\u2588<#ccffda>\u2588<#ccffda>\u2588<#ccffda>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<#ccffda>\u2588<#ccffda>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br></line-height></size>", position);
            Main.AllAlivePlayerControls.Without(catcher).Do(Hide);
        }
    }

    internal sealed class YellowFlag : CustomNetObject
    {
        public YellowFlag(Vector2 position)
        {
            CreateNetObject("<size=100%><font=\"VCR SDF\"><line-height=67%><#000000>\u2588<#ffff00>\u2588<#ffff00>\u2588<#ffff00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><#000000>\u2588<#ffff00>\u2588<#ffff00>\u2588<#ffff00>\u2588<#ffff00>\u2588<#ffff00>\u2588<br><#000000>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<#ffff00>\u2588<#ffff00>\u2588<#ffff00>\u2588<br><#000000>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><#000000>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><#000000>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br></line-height></size>", position);
        }
    }

    internal sealed class BlueFlag : CustomNetObject
    {
        public BlueFlag(Vector2 position)
        {
            CreateNetObject("<size=100%><font=\"VCR SDF\"><line-height=67%><#000000>\u2588<#0000ff>\u2588<#0000ff>\u2588<#0000ff>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><#000000>\u2588<#0000ff>\u2588<#0000ff>\u2588<#0000ff>\u2588<#0000ff>\u2588<#0000ff>\u2588<br><#000000>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<#0000ff>\u2588<#0000ff>\u2588<#0000ff>\u2588<br><#000000>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><#000000>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><#000000>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br></line-height></size>", position);
        }
    }

    internal sealed class SoulObject : CustomNetObject
    {
        public SoulObject(Vector2 position, PlayerControl whisperer)
        {
            CreateNetObject("<size=100%><font=\"VCR SDF\"><line-height=67%><alpha=#00>\u2588<alpha=#00>\u2588<#fcfcfc>\u2588<#fcfcfc>\u2588<#fcfcfc>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<#fcfcfc>\u2588<#fcfcfc>\u2588<#fcfcfc>\u2588<#fcfcfc>\u2588<#fcfcfc>\u2588<br><#fcfcfc>\u2588<#fcfcfc>\u2588<#fcfcfc>\u2588<#fcfcfc>\u2588<#cfcfcf>\u2588<#cfcfcf>\u2588<br><#fcfcfc>\u2588<#fcfcfc>\u2588<#fcfcfc>\u2588<#fcfcfc>\u2588<#fcfcfc>\u2588<#fcfcfc>\u2588<br><alpha=#00>\u2588<#fcfcfc>\u2588<#fcfcfc>\u2588<#fcfcfc>\u2588<#fcfcfc>\u2588<#fcfcfc>\u2588<br><alpha=#00>\u2588<#fcfcfc>\u2588<#fcfcfc>\u2588<#fcfcfc>\u2588<alpha=#00>\u2588<#fcfcfc>\u2588<br></line-height></size>", position);
            Main.AllAlivePlayerControls.Without(whisperer).Do(Hide);
        }
    }

    internal sealed class DisasterWarningTimer : CustomNetObject
    {
        public DisasterWarningTimer(Vector2 position, float time, string disaster)
        {
            CreateNetObject($"<size=250%>{Math.Ceiling(time):N0}</size>\n{disaster}", position);
            Disaster = disaster;
            Timer = time;
        }

        private string Disaster { get; }
        private float Timer { get; set; }
        private int Time => (int)Math.Ceiling(Timer);

        protected override void OnFixedUpdate()
        {
            base.OnFixedUpdate();
            int oldTime = Time;
            Timer -= UnityEngine.Time.fixedDeltaTime;
            if (Time != oldTime) RpcChangeSprite($"<size=250%>{Time:N0}</size>\n{Disaster}");
        }
    }

    public sealed class NaturalDisaster : CustomNetObject
    {
        public NaturalDisaster(Vector2 position, float time, string sprite, string disasterName, SystemTypes? room)
        {
            WarningTimer = new(position, time, Translator.GetString($"ND_{disasterName}"));
            SpawnTimer = time;
            Sprite = sprite;
            DisasterName = disasterName;
            Room = room;
        }

        public SystemTypes? Room { get; }
        public string DisasterName { get; }
        public float SpawnTimer { get; private set; }
        private DisasterWarningTimer WarningTimer { get; }

        public void Update()
        {
            if (float.IsNaN(SpawnTimer)) return;

            SpawnTimer -= Time.fixedDeltaTime;

            if (SpawnTimer <= 0f)
            {
                WarningTimer.Despawn();
                CreateNetObject(Sprite, WarningTimer.Position);
                SpawnTimer = float.NaN;
            }
        }
    }

    internal sealed class Lightning : CustomNetObject
    {
        private float Timer = 5f;

        public Lightning(Vector2 position)
        {
            CreateNetObject("<size=100%><font=\"VCR SDF\"><line-height=67%><alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<#c6c7c3>\u2588<br><alpha=#00>\u2588<#c6c7c3>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<#c6c7c3>\u2588<alpha=#00>\u2588<br><#c6c7c3>\u2588<alpha=#00>\u2588<#fffb00>\u2588<#fffb00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<#fffb00>\u2588<#fffb00>\u2588<alpha=#00>\u2588<#c6c7c3>\u2588<br><#c6c7c3>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<#c6c7c3>\u2588<#c6c7c3>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br></line-height></size>", position);
        }

        protected override void OnFixedUpdate()
        {
            base.OnFixedUpdate();
            Timer -= Time.fixedDeltaTime;
            if (Timer <= 0) Despawn();
        }
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RawSetName))]
internal static class RawSetNamePatch
{
    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] string name)
    {
        if (!CustomGameMode.NaturalDisasters.IsActiveOrIntegrated()) return true;

        var exception = false;

        try { __instance.gameObject.name = name; }
        catch { exception = true; }

        try { __instance.cosmetics.SetName(name); }
        catch { exception = true; }

        try { __instance.cosmetics.SetNameMask(true); }
        catch { exception = true; }

        LateTask.New(() =>
        {
            switch (exception)
            {
                case true when __instance != null:
                    EHR.Logger.Warn($"Failed to set name for {__instance.GetRealName()}, trying alternative method", "RawSetNamePatch");
                    __instance.transform.FindChild("Names").FindChild("NameText_TMP").GetComponent<TextMeshPro>().text = name;
                    EHR.Logger.Msg($"Successfully set name for {__instance.GetRealName()}", "RawSetNamePatch");
                    break;
                case true:
                    // Complete error, don't log this, or it will spam the console
                    break;
            }
        }, 0.5f, log: false);

        return false;
    }
}