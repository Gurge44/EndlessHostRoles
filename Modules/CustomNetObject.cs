using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.InnerNet.GameDataMessages;
using EHR.Crewmate;
using EHR.Impostor;
using EHR.Modules;
using HarmonyLib;
using Hazel;
using InnerNet;
using UnityEngine;
using Tree = EHR.Crewmate.Tree;

// Credit: https://github.com/Rabek009/MoreGamemodes/blob/e054eb498094dfca0a365fc6b6fea8d17f9974d7/Modules/AllObjects
// Huge thanks to Rabek009 for this code!

namespace EHR
{
    public class CustomNetObject
    {
        public static readonly List<CustomNetObject> AllObjects = [];
        private static int MaxId = -1;

        protected int Id;
        public PlayerControl playerControl;
        public Vector2 Position;
        protected string Sprite;

        public void RpcChangeSprite(string sprite)
        {
            if (!AmongUsClient.Instance.AmHost) return;
            if (this is not DisasterWarningTimer) Logger.Info($" Change Custom Net Object {GetType().Name} (ID {Id}) sprite", "CNO.RpcChangeSprite");

            Sprite = sprite;

            string name = PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].PlayerName;
            int colorId = PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].ColorId;
            string hatId = PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].HatId;
            string skinId = PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].SkinId;
            string petId = PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].PetId;
            string visorId = PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].VisorId;
            var sender = CustomRpcSender.Create("CustomNetObject.RpcChangeSprite", this is BedWarsItemGenerator ? SendOption.None : SendOption.Reliable, log: false);
            MessageWriter writer = sender.stream;
            sender.StartMessage();
            PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].PlayerName = "<size=14><br></size>" + sprite;
            PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].ColorId = 0;
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

            try { playerControl.Shapeshift(PlayerControl.LocalPlayer, false); }
            catch (Exception e) { Utils.ThrowException(e); }

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

            playerControl.transform.FindChild("Names").FindChild("NameText_TMP").gameObject.SetActive(true);
            LateTask.New(() => Utils.SendRPC(CustomRPC.FixModdedClientCNO, playerControl, true), 0.3f);
        }

        public void TP(Vector2 position)
        {
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

            if (this is not ShapeshiftMenuElement)
            {
                LateTask.New(() =>
                {
                    var sender = CustomRpcSender.Create("FixModdedClientCNOText", SendOption.Reliable);

                    sender.AutoStartRpc(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.FixModdedClientCNO, player.OwnerId)
                        .WriteNetObject(playerControl)
                        .Write(false)
                        .EndRpc();

                    sender.SendMessage();
                }, 0.4f);
            }

            MessageWriter writer = MessageWriter.Get(SendOption.Reliable);
            writer.StartMessage(6);
            writer.Write(AmongUsClient.Instance.GameId);
            writer.WritePacked(player.OwnerId);
            writer.StartMessage(5);
            writer.WritePacked(playerControl.NetId);
            writer.EndMessage();
            writer.EndMessage();
            AmongUsClient.Instance.SendOrDisconnect(writer);
            writer.Recycle();
        }

        protected virtual void OnFixedUpdate()
        {
            try
            {
                if (!AmongUsClient.Instance.AmHost) return;
            
                if (AmongUsClient.Instance.AmClient)
                {
                    try { playerControl.NetTransform.SnapTo(Position, (ushort)(playerControl.NetTransform.lastSequenceId + 1U)); }
                    catch { }
                }

                ushort num = (ushort)(playerControl.NetTransform.lastSequenceId + 2U);
                MessageWriter messageWriter = AmongUsClient.Instance.StartRpcImmediately(playerControl.NetTransform.NetId, 21, SendOption.None);
                NetHelpers.WriteVector2(Position, messageWriter);
                messageWriter.Write(num);
                AmongUsClient.Instance.FinishRpcImmediately(messageWriter);
            }
            catch { }
        }

        protected void CreateNetObject(string sprite, Vector2 position)
        {
            if (GameStates.IsEnded || !AmongUsClient.Instance.AmHost) return;
            
            Logger.Info($" Create Custom Net Object {GetType().Name} (ID {MaxId + 1}) at {position} - Time since game start: {Utils.TimeStamp - IntroCutsceneDestroyPatch.IntroDestroyTS}s", "CNO.CreateNetObject");

            if (Options.CurrentGameMode == CustomGameMode.Standard && (!GameStates.InGame || !Main.IntroDestroyed || Utils.TimeStamp - IntroCutsceneDestroyPatch.IntroDestroyTS < 10))
            {
                if (GameStates.InGame && (!Main.IntroDestroyed || Utils.TimeStamp - IntroCutsceneDestroyPatch.IntroDestroyTS < 10))
                {
                    Main.Instance.StartCoroutine(CoRoutine());
                    
                    System.Collections.IEnumerator CoRoutine()
                    {
                        Logger.Info("Delaying CNO Spawn", "CustomNetObject.CreateNetObject");
                        while (GameStates.InGame && !GameStates.IsEnded && (!Main.IntroDestroyed || Utils.TimeStamp - IntroCutsceneDestroyPatch.IntroDestroyTS < 10)) yield return null;
                        yield return new WaitForSeconds(3f);
                        if (!GameStates.InGame || GameStates.IsEnded || GameStates.IsMeeting || ExileController.Instance || AntiBlackout.SkipTasks) yield break;
                        CreateNetObject(sprite, position);
                    }
                }
                
                return;
            }
            
            playerControl = Object.Instantiate(AmongUsClient.Instance.PlayerPrefab, Vector2.zero, Quaternion.identity);
            playerControl.PlayerId = 254;
            playerControl.isNew = false;
            playerControl.notRealPlayer = true;

            try { playerControl.NetTransform.SnapTo(new Vector2(50f, 50f)); }
            catch (Exception e) { Utils.ThrowException(e); }

            AmongUsClient.Instance.NetIdCnt += 1U;
            MessageWriter msg = MessageWriter.Get(SendOption.Reliable);
            msg.StartMessage(5);
            msg.Write(AmongUsClient.Instance.GameId);
            msg.StartMessage(4);
            SpawnGameDataMessage item = AmongUsClient.Instance.CreateSpawnMessage(playerControl, -2, SpawnFlags.None);
            item.SerializeValues(msg);
            msg.EndMessage();

            if (GameStates.CurrentServerType == GameStates.ServerType.Vanilla)
            {
                for (uint i = 1; i <= 3; ++i)
                {
                    msg.StartMessage(4);
                    msg.WritePacked(2U);
                    msg.WritePacked(-2);
                    msg.Write((byte)SpawnFlags.None);
                    msg.WritePacked(1);
                    msg.WritePacked(AmongUsClient.Instance.NetIdCnt - i);
                    msg.StartMessage(1);
                    msg.EndMessage();
                    msg.EndMessage();
                }
            }

            msg.EndMessage();
            AmongUsClient.Instance.SendOrDisconnect(msg);
            msg.Recycle();

            if (PlayerControl.AllPlayerControls.Contains(playerControl))
                PlayerControl.AllPlayerControls.Remove(playerControl);

            if (this is not ShapeshiftMenuElement)
            {
                LateTask.New(() =>
                {
                    string name = PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].PlayerName;
                    int colorId = PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].ColorId;
                    string hatId = PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].HatId;
                    string skinId = PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].SkinId;
                    string petId = PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].PetId;
                    string visorId = PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].VisorId;
                    var sender = CustomRpcSender.Create("CustomNetObject.CreateNetObject", SendOption.Reliable, log: false);
                    MessageWriter writer = sender.stream;
                    sender.StartMessage();
                    PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].PlayerName = "<size=14><br></size>" + sprite;
                    PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].ColorId = 0;
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

                    try { playerControl.Shapeshift(PlayerControl.LocalPlayer, false); }
                    catch (Exception e) { Utils.ThrowException(e); }
                    
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

                    try { playerControl.NetTransform.SnapTo(Position); }
                    catch (Exception e) { Utils.ThrowException(e); }
                    
                    sender.StartRpc(playerControl.NetTransform.NetId, (byte)RpcCalls.SnapTo)
                        .WriteVector2(Position)
                        .Write(playerControl.NetTransform.lastSequenceId)
                        .EndRpc();

                    sender.EndMessage();
                    sender.SendMessage();
                }, 0.2f);
            }

            playerControl.cosmetics.currentBodySprite.BodySprite.color = Color.clear;
            playerControl.cosmetics.colorBlindText.color = Color.clear;
            Position = position;
            Sprite = sprite;
            ++MaxId;
            Id = MaxId;
            if (MaxId == int.MaxValue) MaxId = int.MinValue;

            AllObjects.Add(this);

            LateTask.New(() =>
            {
                foreach (PlayerControl pc in Main.AllPlayerControls)
                {
                    if (pc.AmOwner) continue;

                    var sender = CustomRpcSender.Create("CustomNetObject.CreateNetObject (2)", SendOption.Reliable, log: false);
                    MessageWriter writer = sender.stream;
                    sender.StartMessage(pc.OwnerId);
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
                }
            
                playerControl.CachedPlayerData = PlayerControl.LocalPlayer.Data;
            }, 0.1f);

            LateTask.New(() => playerControl.transform.FindChild("Names").FindChild("NameText_TMP").gameObject.SetActive(true), 0.3f); // Fix for Host
            LateTask.New(() => Utils.SendRPC(CustomRPC.FixModdedClientCNO, playerControl, true), 0.6f); // Fix for Non-Host Modded
        }
        
        public virtual void OnMeeting()
        {
            if (!AmongUsClient.Instance.AmHost) return;
            
            MessageWriter writer = MessageWriter.Get(SendOption.Reliable);
            writer.StartMessage(5);
            writer.Write(AmongUsClient.Instance.GameId);
			writer.StartMessage(5);
			writer.WritePacked(playerControl.NetId);
			writer.EndMessage();
            writer.EndMessage();
            AmongUsClient.Instance.SendOrDisconnect(writer);
            writer.Recycle();
            
            Main.Instance.StartCoroutine(WaitForMeetingEnd());
            return;

            System.Collections.IEnumerator WaitForMeetingEnd()
            {
                while (ReportDeadBodyPatch.MeetingStarted || GameStates.IsMeeting || ExileController.Instance || AntiBlackout.SkipTasks) yield return null;
                yield return new WaitForSeconds(1f);
                while (ReportDeadBodyPatch.MeetingStarted || GameStates.IsMeeting || ExileController.Instance || AntiBlackout.SkipTasks) yield return null;
                if (GameStates.IsEnded || !GameStates.InGame || GameStates.IsLobby) yield break;

                try
                {
                    AmongUsClient.Instance.RemoveNetObject(playerControl);
                    Object.Destroy(playerControl.gameObject);
                    playerControl = Object.Instantiate(AmongUsClient.Instance.PlayerPrefab, Vector2.zero, Quaternion.identity);
                    playerControl.PlayerId = 254;
                    playerControl.isNew = false;
                    playerControl.notRealPlayer = true;

                    try { playerControl.NetTransform.SnapTo(new Vector2(50f, 50f)); }
                    catch (Exception e) { Utils.ThrowException(e); }
                    
                    AmongUsClient.Instance.NetIdCnt += 1U;
                    MessageWriter msg = MessageWriter.Get(SendOption.Reliable);
                    msg.StartMessage(5);
                    msg.Write(AmongUsClient.Instance.GameId);
                    msg.StartMessage(4);
                    SpawnGameDataMessage item = AmongUsClient.Instance.CreateSpawnMessage(playerControl, -2, SpawnFlags.None);
                    item.SerializeValues(msg);
                    msg.EndMessage();

                    if (GameStates.CurrentServerType == GameStates.ServerType.Vanilla)
                    {
                        for (uint i = 1; i <= 3; ++i)
                        {
                            msg.StartMessage(4);
                            msg.WritePacked(2U);
                            msg.WritePacked(-2);
                            msg.Write((byte)SpawnFlags.None);
                            msg.WritePacked(1);
                            msg.WritePacked(AmongUsClient.Instance.NetIdCnt - i);
                            msg.StartMessage(1);
                            msg.EndMessage();
                            msg.EndMessage();
                        }
                    }

                    msg.EndMessage();
                    AmongUsClient.Instance.SendOrDisconnect(msg);
                    msg.Recycle();
                    if (PlayerControl.AllPlayerControls.Contains(playerControl))
                        PlayerControl.AllPlayerControls.Remove(playerControl);
                    playerControl.cosmetics.currentBodySprite.BodySprite.color = Color.clear;
                    playerControl.cosmetics.colorBlindText.color = Color.clear;
                }
                catch (Exception e) { Utils.ThrowException(e); }

                yield return new WaitForSeconds(0.1f);

                try
                {
                    foreach (var pc in PlayerControl.AllPlayerControls)
                    {
                        try
                        {
                            if (pc.AmOwner) continue;
                            CustomRpcSender sender = CustomRpcSender.Create("CustomNetObject.OnMeeting", SendOption.Reliable);
                            MessageWriter writer2 = sender.stream;
                            sender.StartMessage(pc.OwnerId);
                            writer2.StartMessage(1);
                            {
                                writer2.WritePacked(playerControl.NetId);
                                writer2.Write(pc.PlayerId);
                            }
                            writer2.EndMessage();
                            sender.StartRpc(playerControl.NetId, (byte)RpcCalls.MurderPlayer)
                                .WriteNetObject(playerControl)
                                .Write((int)MurderResultFlags.FailedError)
                                .EndRpc();
                            writer2.StartMessage(1);
                            {
                                writer2.WritePacked(playerControl.NetId);
                                writer2.Write((byte)254);
                            }
                            writer2.EndMessage();
                            sender.EndMessage();
                            sender.SendMessage();
                        }
                        catch (Exception e) { Utils.ThrowException(e); }
                    }
                    playerControl.CachedPlayerData = PlayerControl.LocalPlayer.Data;
                }
                catch (Exception e) { Utils.ThrowException(e); }

                yield return new WaitForSeconds(0.1f);

                try
                {
                    string name = PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].PlayerName;
                    int colorId = PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].ColorId;
                    string hatId = PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].HatId;
                    string skinId = PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].SkinId;
                    string petId = PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].PetId;
                    string visorId = PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].VisorId;
                    var sender = CustomRpcSender.Create("CustomNetObject.CreateNetObject", SendOption.Reliable, log: false);
                    MessageWriter writer2 = sender.stream;
                    sender.StartMessage();
                    PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].PlayerName = "<size=14><br></size>" + Sprite;
                    PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].ColorId = 0;
                    PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].HatId = "";
                    PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].SkinId = "";
                    PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].PetId = "";
                    PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].VisorId = "";
                    writer2.StartMessage(1);
                    {
                        writer2.WritePacked(PlayerControl.LocalPlayer.Data.NetId);
                        PlayerControl.LocalPlayer.Data.Serialize(writer2, false);
                    }
                    writer2.EndMessage();

                    try { playerControl.Shapeshift(PlayerControl.LocalPlayer, false); }
                    catch (Exception e) { Utils.ThrowException(e); }

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
                    writer2.StartMessage(1);
                    {
                        writer2.WritePacked(PlayerControl.LocalPlayer.Data.NetId);
                        PlayerControl.LocalPlayer.Data.Serialize(writer2, false);
                    }
                    writer2.EndMessage();

                    try { playerControl.NetTransform.SnapTo(Position); }
                    catch (Exception e) { Utils.ThrowException(e); }
                    
                    sender.StartRpc(playerControl.NetTransform.NetId, (byte)RpcCalls.SnapTo)
                        .WriteVector2(Position)
                        .Write(playerControl.NetTransform.lastSequenceId)
                        .EndRpc();

                    sender.EndMessage();
                    sender.SendMessage();
                }
                catch (Exception e) { Utils.ThrowException(e); }
            }
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

        public static void AfterMeeting()
        {
            AllObjects.OfType<ShapeshiftMenuElement>().ToArray().Do(x => x.Despawn());
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
            LateTask.New(() => Main.AllAlivePlayerControls.ExceptBy(visibleList, x => x.PlayerId).Do(Hide), 0.7f);
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
            LateTask.New(() => Main.AllAlivePlayerControls.ExceptBy(visibleList, x => x.PlayerId).Do(Hide), 0.7f);
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
            LateTask.New(() => Main.AllAlivePlayerControls.ExceptBy(visibleList, x => x.PlayerId).Do(Hide), 0.7f);
        }
    }

    internal sealed class Toilet : CustomNetObject
    {
        internal Toilet(Vector2 position, IEnumerable<PlayerControl> hideList)
        {
            CreateNetObject("<size=100%><font=\"VCR SDF\"><line-height=67%><alpha=#00>\u2588<#e6e6e6>\u2588<#e6e6e6>\u2588<#e6e6e6>\u2588<#e6e6e6>\u2588<#e6e6e6>\u2588<#e6e6e6>\u2588<#e6e6e6>\u2588<#e6e6e6>\u2588<alpha=#00>\u2588<br><#e6e6e6>\u2588<#d3d4ce>\u2588<#d3d4ce>\u2588<#d3d4ce>\u2588<#d3d4ce>\u2588<#d3d4ce>\u2588<#d3d4ce>\u2588<#d3d4ce>\u2588<#d3d4ce>\u2588<#e6e6e6>\u2588<br><#e6e6e6>\u2588<#d3d4ce>\u2588<#d3d4ce>\u2588<#d3d4ce>\u2588<#d3d4ce>\u2588<#d3d4ce>\u2588<#d3d4ce>\u2588<#d3d4ce>\u2588<#d3d4ce>\u2588<#e6e6e6>\u2588<br><alpha=#00>\u2588<#e6e6e6>\u2588<#d3d4ce>\u2588<#d3d4ce>\u2588<#d3d4ce>\u2588<#d3d4ce>\u2588<#d3d4ce>\u2588<#d3d4ce>\u2588<#e6e6e6>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<#e6e6e6>\u2588<#e6e6e6>\u2588<#d3d4ce>\u2588<#dedede>\u2588<#dedede>\u2588<#d3d4ce>\u2588<#e6e6e6>\u2588<#e6e6e6>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<#bfbfbf>\u2588<#454545>\u2588<#333333>\u2588<#333333>\u2588<#333333>\u2588<#333333>\u2588<#333333>\u2588<#333333>\u2588<#bfbfbf>\u2588<br><alpha=#00>\u2588<#bfbfbf>\u2588<#bfbfbf>\u2588<#454545>\u2588<#454545>\u2588<#454545>\u2588<#454545>\u2588<#454545>\u2588<#454545>\u2588<#bfbfbf>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<#bfbfbf>\u2588<#bfbfbf>\u2588<#bfbfbf>\u2588<#bfbfbf>\u2588<#bfbfbf>\u2588<#bfbfbf>\u2588<#bfbfbf>\u2588<#bfbfbf>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<#dedede>\u2588<#dedede>\u2588<#dedede>\u2588<#dedede>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<#dedede>\u2588<#dedede>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br></color></line-height></font></size>", position);
            LateTask.New(() => hideList.Do(Hide), 0.7f);
        }
    }

    internal sealed class BlackHole : CustomNetObject
    {
        internal BlackHole(Vector2 position)
        {
            CreateNetObject("<size=100%><font=\"VCR SDF\"><line-height=67%><alpha=#00>█<alpha=#00>█<#000000>█<#19131c>█<#000000>█<#000000>█<alpha=#00>█<alpha=#00>█<br><alpha=#00>█<#412847>█<#000000>█<#19131c>█<#000000>█<#412847>█<#260f26>█<alpha=#00>█<br><#000000>█<#412847>█<#412847>█<#000000>█<#260f26>█<#1c0d1c>█<#19131c>█<#000000>█<br><#19131c>█<#000000>█<#412847>█<#1c0d1c>█<#1c0d1c>█<#000000>█<#19131c>█<#000000>█<br><#000000>█<#000000>█<#260f26>█<#1c0d1c>█<#1c0d1c>█<#000000>█<#000000>█<#260f26>█<br><#000000>█<#260f26>█<#1c0d1c>█<#1c0d1c>█<#19131c>█<#412847>█<#412847>█<#19131c>█<br><alpha=#00>█<#260f26>█<#412847>█<#412847>█<#19131c>█<#260f26>█<#19131c>█<alpha=#00>█<br><alpha=#00>█<alpha=#00>█<#412847>█<#260f26>█<#260f26>█<#000000>█<alpha=#00>█<alpha=#00>█<br></line-height></size>", position);
        }

        public override void OnMeeting()
        {
            if (Abyssbringer.ShouldDespawnCNOOnMeeting) Despawn();
            else base.OnMeeting();
        }
    }

    internal sealed class SprayedArea : CustomNetObject
    {
        public SprayedArea(Vector2 position, IEnumerable<byte> visibleList)
        {
            CreateNetObject("<size=100%><font=\"VCR SDF\"><line-height=67%><alpha=#00>\u2588<alpha=#00>\u2588<#ffd000>\u2588<#ffd000>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<#ffd000>\u2588<#ffd000>\u2588<#ffd000>\u2588<#ffd000>\u2588<alpha=#00>\u2588<br><#ffd000>\u2588<#ffd000>\u2588<#ffd000>\u2588<#ffd000>\u2588<#ffd000>\u2588<#ffd000>\u2588<br><#ffd000>\u2588<#ffd000>\u2588<#ffd000>\u2588<#ffd000>\u2588<#ffd000>\u2588<#ffd000>\u2588<br><alpha=#00>\u2588<#ffd000>\u2588<#ffd000>\u2588<#ffd000>\u2588<#ffd000>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<#ffd000>\u2588<#ffd000>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br></line-height></size>", position);
            LateTask.New(() => Main.AllAlivePlayerControls.ExceptBy(visibleList, x => x.PlayerId).Do(Hide), 0.7f);
        }
    }

    internal sealed class CatcherTrap : CustomNetObject
    {
        public CatcherTrap(Vector2 position, PlayerControl catcher)
        {
            CreateNetObject("<size=100%><font=\"VCR SDF\"><line-height=67%><alpha=#00>\u2588<alpha=#00>\u2588<#ccffda>\u2588<#ccffda>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<#ccffda>\u2588<#ccffda>\u2588<#ccffda>\u2588<#ccffda>\u2588<alpha=#00>\u2588<br><#ccffda>\u2588<#ccffda>\u2588<#ccffda>\u2588<#ccffda>\u2588<#ccffda>\u2588<#ccffda>\u2588<br><#ccffda>\u2588<#ccffda>\u2588<#ccffda>\u2588<#ccffda>\u2588<#ccffda>\u2588<#ccffda>\u2588<br><alpha=#00>\u2588<#ccffda>\u2588<#ccffda>\u2588<#ccffda>\u2588<#ccffda>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<#ccffda>\u2588<#ccffda>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br></line-height></size>", position);
            LateTask.New(() => Main.AllAlivePlayerControls.Without(catcher).Do(Hide), 0.7f);
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
            CreateNetObject("<size=80%><font=\"VCR SDF\"><line-height=67%><alpha=#00>\u2588<alpha=#00>\u2588<#fcfcfc>\u2588<#fcfcfc>\u2588<#fcfcfc>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<#fcfcfc>\u2588<#fcfcfc>\u2588<#fcfcfc>\u2588<#fcfcfc>\u2588<#fcfcfc>\u2588<br><#fcfcfc>\u2588<#fcfcfc>\u2588<#fcfcfc>\u2588<#fcfcfc>\u2588<#cfcfcf>\u2588<#cfcfcf>\u2588<br><#fcfcfc>\u2588<#fcfcfc>\u2588<#fcfcfc>\u2588<#fcfcfc>\u2588<#fcfcfc>\u2588<#fcfcfc>\u2588<br><alpha=#00>\u2588<#fcfcfc>\u2588<#fcfcfc>\u2588<#fcfcfc>\u2588<#fcfcfc>\u2588<#fcfcfc>\u2588<br><alpha=#00>\u2588<#fcfcfc>\u2588<#fcfcfc>\u2588<#fcfcfc>\u2588<alpha=#00>\u2588<#fcfcfc>\u2588<br></line-height></size>", position);
            LateTask.New(() => Main.AllAlivePlayerControls.Without(whisperer).Do(Hide), 0.7f);
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
            
            if (oldTime <= 0)
            {
                Despawn();
                return;
            }
            
            Timer -= UnityEngine.Time.fixedDeltaTime;
            if (Time != oldTime) RpcChangeSprite($"<size=250%>{Time:N0}</size>\n{Disaster}");
        }
    }

    public sealed class NaturalDisaster : CustomNetObject
    {
        public NaturalDisaster(Vector2 position, float time, string sprite, string disasterName, SystemTypes? room)
        {
            string name = Translator.GetString($"ND_{disasterName}");

            if (room.HasValue)
            {
                name = $"<#ff0000>{name}</color>";
                Main.AllAlivePlayerControls.DoIf(x => x.IsInRoom(room.Value), x => x.ReactorFlash());
            }

            WarningTimer = new(position, time, name);
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

    internal sealed class BlueBed : BedWars.Bed
    {
        public BlueBed(Vector2 position)
        {
            BaseSprite = "<size=70%><line-height=67%><alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<br><#c7deff>█<#c7deff>█<#00aeff>█<#00aeff>█<#00aeff>█<#00aeff>█<br><#c7deff>█<#c7deff>█<#00aeff>█<#00aeff>█<#00aeff>█<#00aeff>█<br><#c7deff>█<#c7deff>█<#00aeff>█<#00aeff>█<#00aeff>█<#00aeff>█<br><#82531a>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<#82531a>█<br><alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<br></line-height></size>";
            UpdateStatus(false);
            CreateNetObject(GetSprite(), position);
        }
    }

    internal sealed class GreenBed : BedWars.Bed
    {
        public GreenBed(Vector2 position)
        {
            BaseSprite = "<size=70%><line-height=67%><alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<br><#baffd0>█<#baffd0>█<#00ff7b>█<#00ff7b>█<#00ff7b>█<#00ff7b>█<br><#baffd0>█<#baffd0>█<#00ff7b>█<#00ff7b>█<#00ff7b>█<#00ff7b>█<br><#baffd0>█<#baffd0>█<#00ff7b>█<#00ff7b>█<#00ff7b>█<#00ff7b>█<br><#82531a>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<#82531a>█<br><alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<br></line-height></size>";
            UpdateStatus(false);
            CreateNetObject(GetSprite(), position);
        }
    }

    internal sealed class YellowBed : BedWars.Bed
    {
        public YellowBed(Vector2 position)
        {
            BaseSprite = "<size=70%><line-height=67%><alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<br><#fffebd>█<#fffebd>█<#ffff00>█<#ffff00>█<#ffff00>█<#ffff00>█<br><#fffebd>█<#fffebd>█<#ffff00>█<#ffff00>█<#ffff00>█<#ffff00>█<br><#fffebd>█<#fffebd>█<#ffff00>█<#ffff00>█<#ffff00>█<#ffff00>█<br><#82531a>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<#82531a>█<br><alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<br></line-height></size>";
            UpdateStatus(false);
            CreateNetObject(GetSprite(), position);
        }
    }

    internal sealed class RedBed : BedWars.Bed
    {
        public RedBed(Vector2 position)
        {
            BaseSprite = "<size=70%><line-height=67%><alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<br><#ffbbb5>█<#ffbbb5>█<#ff0008>█<#ff0008>█<#ff0008>█<#ff0008>█<br><#ffbbb5>█<#ffbbb5>█<#ff0008>█<#ff0008>█<#ff0008>█<#ff0008>█<br><#ffbbb5>█<#ffbbb5>█<#ff0008>█<#ff0008>█<#ff0008>█<#ff0008>█<br><#82531a>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<#82531a>█<br><alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<br></line-height></size>";
            UpdateStatus(false);
            CreateNetObject(GetSprite(), position);
        }
    }

    internal sealed class BedWarsItemGenerator : CustomNetObject
    {
        private readonly string ItemSprite;

        public BedWarsItemGenerator(Vector2 position, string itemSprite)
        {
            ItemSprite = itemSprite;
            CreateNetObject($"{itemSprite} 0", position);
        }

        public void SetCount(int count)
        {
            RpcChangeSprite($"{ItemSprite} {count}");
        }
    }

    internal sealed class BedWarsShop : CustomNetObject
    {
        public BedWarsShop(Vector2 position, string sprite)
        {
            CreateNetObject(sprite, position);
        }
    }

    internal sealed class TNT : CustomNetObject
    {
        private readonly Vector2 Location;
        private float timer;

        public TNT(Vector2 location)
        {
            Location = location;
            timer = 4f;
            CreateNetObject("<size=100%><line-height=67%><alpha=#00>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<#000000>█<#ffea00>█<br><alpha=#00>█<alpha=#00>█<alpha=#00>█<#000000>█<alpha=#00>█<alpha=#00>█<br><alpha=#00>█<#ff0004>█<#ff0004>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<br><#ff0004>█<#ff0004>█<#ff0004>█<#ff0004>█<alpha=#00>█<alpha=#00>█<br><#ff0004>█<#ff0004>█<#ff0004>█<#ff0004>█<alpha=#00>█<alpha=#00>█<br><alpha=#00>█<#ff0004>█<#ff0004>█<alpha=#00>█<alpha=#00>█<alpha=#00>█<br></line-height></size>", location);
        }

        protected override void OnFixedUpdate()
        {
            base.OnFixedUpdate();
            if (Options.CurrentGameMode != CustomGameMode.BedWars) return;
            timer -= Time.fixedDeltaTime;

            if (timer <= 0f)
            {
                BedWars.OnTNTExplode(Location);
                Despawn();
            }
        }
    }

    internal sealed class Portal : CustomNetObject
    {
        public Portal(Vector2 position)
        {
            CreateNetObject("<size=70%><line-height=67%><alpha=#00>█<#2b006b>█<#2b006b>█<#2b006b>█<#2b006b>█<alpha=#00>█<br><alpha=#00>█<#2b006b>█<#fa69ff>█<#fa69ff>█<#2b006b>█<alpha=#00>█<br><alpha=#00>█<#2b006b>█<#fa69ff>█<#fa69ff>█<#2b006b>█<alpha=#00>█<br><alpha=#00>█<#2b006b>█<#fa69ff>█<#fa69ff>█<#2b006b>█<alpha=#00>█<br><alpha=#00>█<#2b006b>█<#fa69ff>█<#fa69ff>█<#2b006b>█<alpha=#00>█<br><alpha=#00>█<#2b006b>█<#2b006b>█<#2b006b>█<#2b006b>█<alpha=#00>█<br></line-height></size>", position);
        }
    }

    internal sealed class Plant : CustomNetObject
    {
        public bool Spawned;

        public Plant(Vector2 position)
        {
            Position = position;
            Spawned = false;
        }

        public void SpawnIfNotSpawned()
        {
            if (Spawned) return;
            CreateNetObject("<size=100%><line-height=67%><alpha=#00>█<#00ff15>█<alpha=#00>█<#00ff15>█<alpha=#00>█<#00ff15>█<br><alpha=#00>█<#00ff15>█<alpha=#00>█<#00ff15>█<alpha=#00>█<#00ff15>█<br><#00ff15>█<#00ff15>█<alpha=#00>█<#00ff15>█<alpha=#00>█<#00ff15>█<br><#00ff15>█<alpha=#00>█<alpha=#00>█<#00ff15>█<alpha=#00>█<#00ff15>█<br><#00ff15>█<alpha=#00>█<#00ff15>█<#00ff15>█<alpha=#00>█<#00ff15>█<br><#00ff15>█<alpha=#00>█<#00ff15>█<alpha=#00>█<alpha=#00>█<#00ff15>█<br></line-height></size>", Position);
            Spawned = true;
        }
    }

    internal sealed class Seed : CustomNetObject
    {
        public bool Spawned;
        private readonly string Color;

        public Seed(Vector2 position, string color)
        {
            Position = position;
            Spawned = false;
            Color = color;
        }

        public void SpawnIfNotSpawned()
        {
            if (Spawned) return;
            CreateNetObject($"<size=100%><line-height=67%><alpha=#00>█<#{Color}>█<alpha=#00>█<#{Color}>█<alpha=#00>█<#{Color}>█<br><alpha=#00>█<#{Color}>█<alpha=#00>█<#{Color}>█<alpha=#00>█<#{Color}>█<br><#{Color}>█<#{Color}>█<alpha=#00>█<#{Color}>█<alpha=#00>█<#{Color}>█<br><#{Color}>█<alpha=#00>█<alpha=#00>█<#{Color}>█<alpha=#00>█<#{Color}>█<br><#{Color}>█<alpha=#00>█<#{Color}>█<#{Color}>█<alpha=#00>█<#{Color}>█<br><#{Color}>█<alpha=#00>█<#{Color}>█<alpha=#00>█<alpha=#00>█<#{Color}>█<br></line-height></size>", Position);
            Spawned = true;
        }
    }

    internal sealed class FallenTree : CustomNetObject
    {
        public FallenTree(Vector2 position)
        {
            CreateNetObject(Tree.FallenSprite, position);
        }
    }

    internal sealed class ShapeshiftMenuElement : CustomNetObject
    {
        public ShapeshiftMenuElement(byte visibleTo)
        {
            CreateNetObject(string.Empty, new Vector2(0f, 0f));
            LateTask.New(() => Main.AllPlayerControls.DoIf(x => x.PlayerId != visibleTo, Hide), 0.7f);
        }
    }

    public sealed class DeathracePowerUp : CustomNetObject
    {
        public readonly Deathrace.PowerUp PowerUp;
        
        public DeathracePowerUp(Vector2 position, Deathrace.PowerUp powerUp)
        {
            PowerUp = powerUp;
            
            char icon = powerUp switch
            {
                Deathrace.PowerUp.Smoke => '♨',
                Deathrace.PowerUp.Taser => '〄',
                Deathrace.PowerUp.EnergyDrink => '∂',
                Deathrace.PowerUp.Grenade => '♁',
                Deathrace.PowerUp.Ice => '☃',
                _ => throw new ArgumentOutOfRangeException(nameof(powerUp), powerUp, "Unhandled power-up type")
            };
            
            Color color = powerUp switch
            {
                Deathrace.PowerUp.Smoke => new Color(0.5f, 0.5f, 0.5f),
                Deathrace.PowerUp.Taser => new Color(1f, 1f, 0f),
                Deathrace.PowerUp.EnergyDrink => new Color(1f, 0.5f, 0f),
                Deathrace.PowerUp.Grenade => new Color(1f, 0f, 0f),
                Deathrace.PowerUp.Ice => new Color(0f, 1f, 1f),
                _ => throw new ArgumentOutOfRangeException(nameof(powerUp), powerUp, "Unhandled power-up type")
            };
            
            CreateNetObject(Utils.ColorString(color, $"{icon}\n<size=80%>{Translator.GetString($"Deathrace.PowerUpDisplay.{powerUp}").ToUpper()}</size>"), position);
        }
    }
    
    internal sealed class Snowball : CustomNetObject
    {
        public PlayerControl Thrower;
        private Vector2 Direction;
        public bool Active;

        public Snowball(Vector2 from, Vector2 direction, PlayerControl thrower)
        {
            Thrower = thrower;
            Direction = direction;
            CreateNetObject("<line-height=97%><cspace=0.16em><#0000>W</color><mark=#e4fdff>WWWW</mark><#0000>W</color>\n<mark=#e4fdff>WWWWWW</mark>\n<mark=#e4fdff>WWWWWW</mark>\n<mark=#e4fdff>WWWWWW</mark>\n<mark=#e4fdff>WWWWWW</mark>\n<#0000>W</color><mark=#e4fdff>WWWW</mark><#0000>W", from);
            Active = true;
        }

        protected override void OnFixedUpdate()
        {
            base.OnFixedUpdate();
            
            if (!Active) return;
            
            Vector2 newPos = Position + Direction * Time.fixedDeltaTime * Snowdown.SnowballThrowSpeed;
            
            if ((PhysicsHelpers.AnythingBetween(Position, newPos, Constants.ShipOnlyMask, false)) ||
                newPos.x < Snowdown.MapBounds.X.Left || newPos.x > Snowdown.MapBounds.X.Right || newPos.y < Snowdown.MapBounds.Y.Bottom || newPos.y > Snowdown.MapBounds.Y.Top)
            {
                SetInactive();
                return;
            }

            TP(newPos);
        }

        public void SetInactive()
        {
            TP(new(50f, 50f));
            Active = false;
        }

        public void Reuse(Vector2 from, Vector2 direction, PlayerControl thrower)
        {
            TP(from);
            Thrower = thrower;
            Direction = direction;
            Active = true;
        }
    }
}

// This method sometimes throws an exception, preventing further code from running
// Fixed by wrapping each line in try-catch
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RawSetName))]
static class RawSetNameErrorFixPatch
{
    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] string name)
    {
        try { __instance.gameObject.name = name; }
        catch { }
        
        try { __instance.cosmetics.SetName(name); }
        catch { }
        
        try { __instance.cosmetics.SetNameMask(true); }
        catch { }
        
        return false;
    }
}