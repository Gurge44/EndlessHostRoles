using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AmongUs.InnerNet.GameDataMessages;
using EHR.Gamemodes;
using EHR.Modules;
using EHR.Roles;
using Hazel;
using InnerNet;
using UnityEngine;
using Tree = EHR.Roles.Tree;

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
        private string Sprite;

        private int SnapToSendFrameCount;
        
        private bool IsPooled;

        public void RpcChangeSprite(string sprite)
        {
            if (!AmongUsClient.Instance.AmHost) return;
            if (this is not NaturalDisaster nd || !nd.SpawnTimer.IsRunning) Logger.Info($" Change Custom Net Object {GetType().Name} (ID {Id}) sprite", "CNO.RpcChangeSprite");

            bool notImportant = this is BedWarsItemGenerator || (this is NaturalDisaster n && n.SpawnTimer.Elapsed.TotalSeconds < n.TotalWarningTime - 1 && Options.CurrentGameMode != CustomGameMode.NaturalDisasters && GameStates.CurrentServerType == GameStates.ServerType.Vanilla);
            SendOption channel = notImportant ? SendOption.None : SendOption.Reliable;
            
            DataFlagRateLimiter.Enqueue(() =>
            {
                Sprite = sprite;

                string name = PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].PlayerName;
                int colorId = PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].ColorId;
                string hatId = PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].HatId;
                string skinId = PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].SkinId;
                string petId = PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].PetId;
                string visorId = PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].VisorId;
                var sender = CustomRpcSender.Create("CustomNetObject.RpcChangeSprite", channel, log: false);
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
            }, channel);
        }

        public void TP(Vector2 position)
        {
            Position = position;
            SnapToSendFrameCount = 30;
        }
        
        private bool TryReusePooledObject(string sprite, Vector2 position)
        {
            try
            {
                if (this is not NaturalDisaster) return false;

                CustomNetObject pooled = AllObjects.Find(x => x.IsPooled && x.playerControl);
                if (pooled == null) return false;

                Logger.Info($" Reusing pooled Custom Net Object NaturalDisaster (ID {pooled.Id})", "CNO.CreateNetObject");

                playerControl = pooled.playerControl;
                Id = pooled.Id;
                IsPooled = false;

                AllObjects.Remove(pooled);
                AllObjects.Add(this);

                TP(position);
                RpcChangeSprite(sprite);

                return true;
            }
            catch (Exception e)
            {
                Utils.ThrowException(e);
                return false;
            }
        }

        public void Despawn(bool canPool = true)
        {
            if (!AmongUsClient.Instance.AmHost) return;

            try
            {
                if (canPool && this is NaturalDisaster && playerControl)
                {
                    Logger.Info($" Pooled Custom Net Object {GetType().Name} (ID {Id})", "CNO.Despawn");
                    IsPooled = true;
                    TP(new Vector2(50f, 50f));
                    return;
                }
                
                Logger.Info($" Despawn Custom Net Object {GetType().Name} (ID {Id})", "CNO.Despawn");

                if (playerControl)
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

        protected void Hide(IEnumerable<PlayerControl> players)
        {
            int messages = 0;

            MessageWriter packedWriter = MessageWriter.Get(SendOption.Reliable);
            packedWriter.StartMessage(26);
            packedWriter.WritePacked(AmongUsClient.Instance.GameId);
            
            foreach (PlayerControl player in players)
            {
                if (packedWriter.Length > 500 || messages >= AmongUsClient.Instance.GetMaxMessagePackingLimit())
                {
                    messages = 0;
                    packedWriter.EndMessage();
                    AmongUsClient.Instance.SendOrDisconnect(packedWriter);
                    packedWriter.Clear(SendOption.Reliable);
                    packedWriter.StartMessage(26);
                    packedWriter.WritePacked(AmongUsClient.Instance.GameId);
                }

                if (Hide(player, packedWriter))
                    messages++;
            }

            if (messages > 0)
            {
                packedWriter.EndMessage();
                AmongUsClient.Instance.SendOrDisconnect(packedWriter);
            }
            
            packedWriter.Recycle();
        }

        private bool Hide(PlayerControl player, MessageWriter packedWriter)
        {
            if (!AmongUsClient.Instance.AmHost) return false;
            Logger.Info($" Hide Custom Net Object {GetType().Name} (ID {Id}) from {player.GetNameWithRole()}", "CNO.Hide");

            if (player.AmOwner)
            {
                LateTask.New(() => playerControl.transform.FindChild("Names").FindChild("NameText_TMP").gameObject.SetActive(false), 0.1f);
                playerControl.Visible = false;
                return false;
            }

            if (this is not ShapeshiftMenuElement && player.IsNonHostModdedClient())
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

            packedWriter.StartMessage(6);
            packedWriter.Write(AmongUsClient.Instance.GameId);
            packedWriter.WritePacked(player.OwnerId);
            packedWriter.StartMessage(5);
            packedWriter.WritePacked(playerControl.NetId);
            packedWriter.EndMessage();
            packedWriter.EndMessage();

            return true;
        }

        protected virtual void OnFixedUpdate()
        {
            try
            {
                if (!AmongUsClient.Instance.AmHost) return;
                
                if (SnapToSendFrameCount++ < 5) return;
                SnapToSendFrameCount = 0;
            
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
            if (GameStates.IsEnded || !AmongUsClient.Instance.AmHost || TryReusePooledObject(sprite, position)) return;
            
            Logger.Info($" Create Custom Net Object {GetType().Name} (ID {MaxId + 1}) at {position} - Time since game start: {Utils.TimeStamp - IntroCutsceneDestroyPatch.IntroDestroyTS}s", "CNO.CreateNetObject");
            Main.Instance.StartCoroutine(CoRoutine());
            return;
            
            IEnumerator CoRoutine()
            {
                bool tooEarly = !Main.IntroDestroyed || Utils.TimeStamp - IntroCutsceneDestroyPatch.IntroDestroyTS < 10;
                
                if (Options.CurrentGameMode == CustomGameMode.Standard && (!GameStates.InGame || tooEarly))
                {
                    if (GameStates.InGame && tooEarly)
                    {
                        Logger.Info("Delaying CNO Spawn", "CustomNetObject.CreateNetObject");
                        while (GameStates.InGame && !GameStates.IsEnded && (!Main.IntroDestroyed || Utils.TimeStamp - IntroCutsceneDestroyPatch.IntroDestroyTS < 10)) yield return null;
                        yield return new WaitForSecondsRealtime(3f);
                        if (!GameStates.InGame || GameStates.IsEnded || GameStates.IsMeeting || ExileController.Instance || AntiBlackout.SkipTasks) yield break;
                    }
                    else
                        yield break;
                }

                var qa = DataFlagRateLimiter.Enqueue(() =>
                {
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
                });

                yield return qa.Wait();
                if (qa.Dropped) yield break;

                if (PlayerControl.AllPlayerControls.Contains(playerControl))
                    PlayerControl.AllPlayerControls.Remove(playerControl);

                playerControl.cosmetics.currentBodySprite.BodySprite.color = Color.clear;
                playerControl.cosmetics.colorBlindText.color = Color.clear;
                Position = position;
                Sprite = sprite;
                ++MaxId;
                Id = MaxId;
                if (MaxId == int.MaxValue) MaxId = int.MinValue;

                AllObjects.Add(this);

                yield return new WaitForSecondsRealtime(0.1f);

                if (PlayerControl.AllPlayerControls.Count > 1)
                {
                    int messages = 0;
                    MessageWriter stream = MessageWriter.Get(SendOption.Reliable);
                    stream.StartMessage(26);
                    stream.WritePacked(AmongUsClient.Instance.GameId);

                    foreach (PlayerControl pc in Main.EnumeratePlayerControls())
                    {
                        if (pc.AmOwner) continue;

                        if (stream.Length > 500 || messages + 3 > AmongUsClient.Instance.GetMaxMessagePackingLimit())
                        {
                            stream.EndMessage();
                            qa = DataFlagRateLimiter.Enqueue(() => AmongUsClient.Instance.SendOrDisconnect(stream), cleanup: stream.Recycle);
                            yield return qa.Wait();
                            if (qa.Dropped) yield break;
                            messages = 0;
                            stream.Clear(SendOption.Reliable);
                            stream.StartMessage(26);
                            stream.WritePacked(AmongUsClient.Instance.GameId);
                        }

                        stream.StartMessage(6);
                        stream.Write(AmongUsClient.Instance.GameId);
                        stream.WritePacked(pc.OwnerId);
                        stream.StartMessage(1);
                        stream.WritePacked(playerControl.NetId);
                        stream.Write(pc.PlayerId);
                        stream.EndMessage();
                        stream.StartMessage(2);
                        stream.WritePacked(playerControl.NetId);
                        stream.Write((byte)RpcCalls.MurderPlayer);
                        stream.WriteNetObject(playerControl);
                        stream.Write((int)MurderResultFlags.FailedError);
                        stream.EndMessage();
                        stream.StartMessage(1);
                        stream.WritePacked(playerControl.NetId);
                        stream.Write((byte)254);
                        stream.EndMessage();
                        stream.EndMessage();

                        messages += 3;
                    }

                    stream.EndMessage();
                    qa = DataFlagRateLimiter.Enqueue(() => AmongUsClient.Instance.SendOrDisconnect(stream));
                    yield return qa.Wait();
                    stream.Recycle();
                    if (qa.Dropped) yield break;
                }

                playerControl.CachedPlayerData = PlayerControl.LocalPlayer.Data;

                yield return new WaitForSecondsRealtime(0.15f);
                
                if (this is not ShapeshiftMenuElement)
                {
                    yield return DataFlagRateLimiter.Enqueue(() =>
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
                    }).Wait();
                }
            }
        }
        
        public virtual void OnMeeting()
        {
            if (!AmongUsClient.Instance.AmHost) return;
            
            Despawn();
            
            Main.Instance.StartCoroutine(WaitForMeetingEnd());
            return;

            IEnumerator WaitForMeetingEnd()
            {
                yield return new WaitForSecondsRealtime(10f);
                while (ReportDeadBodyPatch.MeetingStarted || GameStates.IsMeeting || ExileController.Instance || AntiBlackout.SkipTasks) yield return null;
                yield return new WaitForSecondsRealtime(3f);
                while (ReportDeadBodyPatch.MeetingStarted || GameStates.IsMeeting || ExileController.Instance || AntiBlackout.SkipTasks) yield return null;
                if (GameStates.IsEnded || !GameStates.InGame || GameStates.IsLobby) yield break;

                CreateNetObject(Sprite, Position);
            }
        }

        public static void FixedUpdate()
        {
            for (int index = AllObjects.Count - 1; index >= 0; index--)
                AllObjects[index]?.OnFixedUpdate();
        }

        public static CustomNetObject Get(int id)
        {
            return AllObjects.Find(x => x.Id == id);
        }

        public static void Reset()
        {
            try
            {
                AllObjects.ToArray().Do(x => x.Despawn(canPool: false));
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
            CreateNetObject("<line-height=97%><cspace=0.16em><#0000>WW</color><mark=#bababa>WWWW</mark><#0000>WW\nW</color><mark=#bababa>WW</mark><mark=#8c8c8c>WW</mark><mark=#bababa>WW</mark><#0000>W</color>\n<mark=#bababa>WW</mark><mark=#8c8c8c>WWWW</mark><mark=#bababa>WW</mark>\n<mark=#bababa>W</mark><mark=#8c8c8c>WW</mark><mark=#636363>WW</mark><mark=#8c8c8c>WW</mark><mark=#bababa>W</mark>\n<mark=#bababa>W</mark><mark=#8c8c8c>WW</mark><mark=#636363>WW</mark><mark=#8c8c8c>WW</mark><mark=#bababa>W</mark>\n<mark=#bababa>WW</mark><mark=#8c8c8c>WWWW</mark><mark=#bababa>WW</mark>\n<#0000>W</color><mark=#bababa>WW</mark><mark=#8c8c8c>WW</mark><mark=#bababa>WW</mark><#0000>W\nWW</color><mark=#bababa>WWWW</mark><#0000>WW", position);
            LateTask.New(() => Hide(Main.EnumerateAlivePlayerControls().ExceptBy(visibleList, x => x.PlayerId)), 0.4f);
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

        public override void OnMeeting()
        {
            Despawn();
        }
    }

    internal sealed class PlayerDetector : CustomNetObject
    {
        public PlayerDetector(Vector2 position, List<byte> visibleList, out int id)
        {
            CreateNetObject("<line-height=97%><cspace=0.16em><#0000>WW</color><mark=#33e6b0>W</mark><#0000>WW</color><mark=#33e6b0>W</mark><#0000>WW\nW</color><mark=#33e6b0>W</mark><#0000>WWWW</color><mark=#33e6b0>W</mark><#0000>W</color>\n<mark=#33e6b0>W</mark><#0000>WW</color><mark=#33e6b0>WW</mark><#0000>WW</color><mark=#33e6b0>W</mark>\n<mark=#33e6b0>W</mark><#0000>W</color><mark=#33e6b0>W</mark><mark=#000000>WW</mark><mark=#33e6b0>W</mark><#0000>W</color><mark=#33e6b0>W</mark>\n<mark=#33e6b0>W</mark><#0000>W</color><mark=#33e6b0>W</mark><mark=#000000>WW</mark><mark=#33e6b0>W</mark><#0000>W</color><mark=#33e6b0>W</mark>\n<mark=#33e6b0>W</mark><#0000>WW</color><mark=#33e6b0>WW</mark><#0000>WW</color><mark=#33e6b0>W</mark>\n<#0000>W</color><mark=#33e6b0>W</mark><#0000>WWWW</color><mark=#33e6b0>W</mark><#0000>W\nWW</color><mark=#33e6b0>W</mark><#0000>WW</color><mark=#33e6b0>W</mark><#0000>WW", position);
            LateTask.New(() => Hide(Main.EnumerateAlivePlayerControls().ExceptBy(visibleList, x => x.PlayerId)), 0.4f);
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
            LateTask.New(() => Hide(Main.EnumerateAlivePlayerControls().ExceptBy(visibleList, x => x.PlayerId)), 0.4f);
        }
    }

    internal sealed class Toilet : CustomNetObject
    {
        internal Toilet(Vector2 position, IEnumerable<PlayerControl> hideList)
        {
            CreateNetObject("<line-height=97%><cspace=0.16em><#0000>W</color><mark=#e6e6e6>WWWWWWWW</mark><#0000>W</color>\n<mark=#e6e6e6>W</mark><mark=#d3d4ce>WWWWWWWW</mark><mark=#e6e6e6>W</mark>\n<mark=#e6e6e6>W</mark><mark=#d3d4ce>WWWWWWWW</mark><mark=#e6e6e6>W</mark>\n<#0000>W</color><mark=#e6e6e6>W</mark><mark=#d3d4ce>WWWWWW</mark><mark=#e6e6e6>W</mark><#0000>W\nW</color><mark=#e6e6e6>WW</mark><mark=#d3d4ce>W</mark><mark=#dedede>WW</mark><mark=#d3d4ce>W</mark><mark=#e6e6e6>WW</mark><#0000>W\nW</color><mark=#bfbfbf>W</mark><mark=#454545>W</mark><mark=#333333>WWWWWW</mark><mark=#bfbfbf>W</mark>\n<#0000>W</color><mark=#bfbfbf>WW</mark><mark=#454545>WWWWWW</mark><mark=#bfbfbf>W</mark>\n<#0000>WW</color><mark=#bfbfbf>WWWWWWWW</mark>\n<#0000>WWW</color><mark=#dedede>WWWW</mark><#0000>WWW\nWWWW</color><mark=#dedede>WW</mark><#0000>WWWW", position);
            LateTask.New(() => Hide(hideList), 0.4f);
        }
    }

    internal sealed class BlackHole : CustomNetObject
    {
        internal BlackHole(Vector2 position)
        {
            CreateNetObject("<line-height=97%><cspace=0.16em><#0000>WW</color><mark=#000000>WWWW</mark><#0000>WW\nW</color><mark=#000000>WWWWWW</mark><#0000>W</color>\n<mark=#000000>WWWWWWWW\nWWWWWWWW\nWWWWWWWW\nWWWWWWWW</mark>\n<#0000>W</color><mark=#000000>WWWWWW</mark><#0000>W\nWW</color><mark=#000000>WWWW</mark><#0000>WW", position);
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
            CreateNetObject("<line-height=97%><cspace=0.16em><#0000>WW</color><mark=#ffd000>WW</mark><#0000>WW\nW</color><mark=#ffd000>WWWW</mark><#0000>W</color>\n<mark=#ffd000>WWWWWW</mark>\n<mark=#ffd000>WWWWWW</mark>\n<#0000>W</color><mark=#ffd000>WWWW</mark><#0000>W\nWW</color><mark=#ffd000>WW</mark><#0000>WW", position);
            LateTask.New(() => Hide(Main.EnumerateAlivePlayerControls().ExceptBy(visibleList, x => x.PlayerId)), 0.4f);
        }

        public override void OnMeeting()
        {
            Despawn();
        }
    }

    internal sealed class CatcherTrap : CustomNetObject
    {
        public CatcherTrap(Vector2 position, PlayerControl catcher)
        {
            CreateNetObject("<line-height=97%><cspace=0.16em><#0000>WW</color><mark=#ccffda>WW</mark><#0000>WW\nW</color><mark=#ccffda>WWWW</mark><#0000>W</color>\n<mark=#ccffda>WWWWWW</mark>\n<mark=#ccffda>WWWWWW</mark>\n<#0000>W</color><mark=#ccffda>WWWW</mark><#0000>W\nWW</color><mark=#ccffda>WW</mark><#0000>WW", position);
            LateTask.New(() => Hide(Main.EnumerateAlivePlayerControls().Without(catcher)), 0.4f);
        }

        public override void OnMeeting()
        {
            Despawn();
        }
    }

    internal sealed class YellowFlag : CustomNetObject
    {
        public YellowFlag(Vector2 position)
        {
            CreateNetObject("<line-height=97%><cspace=0.16em><mark=#000000>W</mark><mark=#ffff00>WWW</mark><#0000>WW</color>\n<mark=#000000>W</mark><mark=#ffff00>WWWWW</mark>\n<mark=#000000>W</mark><#0000>WW</color><mark=#ffff00>WWW</mark>\n<mark=#000000>W</mark><#0000>WWWWW</color>\n<mark=#000000>W</mark><#0000>WWWWW</color>\n<mark=#000000>W</mark><#0000>WWWWW", position);
        }
    }

    internal sealed class BlueFlag : CustomNetObject
    {
        public BlueFlag(Vector2 position)
        {
            CreateNetObject("<line-height=97%><cspace=0.16em><mark=#000000>W</mark><mark=#0000ff>WWW</mark><#0000>WW</color>\n<mark=#000000>W</mark><mark=#0000ff>WWWWW</mark>\n<mark=#000000>W</mark><#0000>WW</color><mark=#0000ff>WWW</mark>\n<mark=#000000>W</mark><#0000>WWWWW</color>\n<mark=#000000>W</mark><#0000>WWWWW</color>\n<mark=#000000>W</mark><#0000>WWWWW", position);
        }
    }

    internal sealed class SoulObject : CustomNetObject
    {
        public SoulObject(Vector2 position, PlayerControl whisperer)
        {
            CreateNetObject("<size=80%><line-height=97%><cspace=0.16em><#0000>WW</color><mark=#fcfcfc>WWW</mark><#0000>W\nW</color><mark=#fcfcfc>WWWWW</mark>\n<mark=#fcfcfc>WWWW</mark><mark=#cfcfcf>WW</mark>\n<mark=#fcfcfc>WWWWWW</mark>\n<#0000>W</color><mark=#fcfcfc>WWWWW</mark>\n<#0000>W</color><mark=#fcfcfc>WWW</mark><#0000>W</color><mark=#fcfcfc>W", position);
            LateTask.New(() => Hide(Main.EnumerateAlivePlayerControls().Without(whisperer)), 0.4f);
        }
    }

    public sealed class NaturalDisaster : CustomNetObject
    {
        public NaturalDisaster(Vector2 position, int time, string sprite, string disasterName, SystemTypes? room)
        {
            string name = Translator.GetString($"ND_{disasterName}");
            string warning = $"<size=250%>{time}</size>\n{name}";

            if (room.HasValue)
            {
                warning = $"<#ff4444>{warning}</color>";

                try { Main.EnumerateAlivePlayerControls().DoIf(x => x.IsInRoom(room.Value), x => x.ReactorFlash()); }
                catch (Exception e) { Utils.ThrowException(e); }
            }

            TotalWarningTime = time;
            DisasterSprite = sprite;
            DisasterName = disasterName;
            DisasterNameTranslated = name;
            Room = room;
            
            SpawnTimer = Stopwatch.StartNew();

            CreateNetObject(warning, position);
        }

        public SystemTypes? Room { get; }
        public string DisasterName { get; }
        public Stopwatch SpawnTimer { get; }
        public int TotalWarningTime { get; }
        private string DisasterSprite { get; }
        
        private int TimeInt => (int)(SpawnTimer.Elapsed.TotalSeconds);
        private int PreviousTimeInt { get; set; }
        private string DisasterNameTranslated { get; }

        public void Update()
        {
            if (!SpawnTimer.IsRunning) return;
            
            int newTime = TimeInt;

            if (newTime >= TotalWarningTime)
            {
                SpawnTimer.Stop();
                if (!Room.HasValue && !string.IsNullOrEmpty(DisasterSprite)) RpcChangeSprite(DisasterSprite);
            }
            else
            {
                if (PreviousTimeInt == newTime) return;
                PreviousTimeInt = newTime;
                string warning = $"<size=250%>{TotalWarningTime - newTime}</size>\n{DisasterNameTranslated}";
                if (Room.HasValue) warning = $"<#ff4444>{warning}</color>";
                RpcChangeSprite(warning);
            }
        }
    }

    internal sealed class Lightning : CustomNetObject
    {
        private readonly Stopwatch Timer = Stopwatch.StartNew();

        public Lightning(Vector2 position)
        {
            CreateNetObject("<line-height=97%><cspace=0.16em><#0000>WWWWW</color><mark=#c6c7c3>W</mark>\n<#0000>W</color><mark=#c6c7c3>W</mark><#0000>WW</color><mark=#c6c7c3>W</mark><#0000>W</color>\n<mark=#c6c7c3>W</mark><#0000>W</color><mark=#fffb00>WW</mark><#0000>WW\nWW</color><mark=#fffb00>WW</mark><#0000>W</color><mark=#c6c7c3>W</mark>\n<mark=#c6c7c3>W</mark><#0000>WWWWW\nWW</color><mark=#c6c7c3>WW</mark><#0000>WW", position);
        }

        protected override void OnFixedUpdate()
        {
            base.OnFixedUpdate();
            if (Timer.Elapsed.TotalSeconds >= 3f) Despawn();
        }
    }

    internal sealed class BlueBed : BedWars.Bed
    {
        public BlueBed(Vector2 position)
        {
            BaseSprite = "<size=70%><line-height=97%><cspace=0.16em><mark=#c7deff>WW</mark><mark=#00aeff>WWWW</mark>\n<mark=#c7deff>WW</mark><mark=#00aeff>WWWW</mark>\n<mark=#c7deff>WW</mark><mark=#00aeff>WWWW</mark>\n<mark=#82531a>W</mark><#0000>WWWW</color><mark=#82531a>W";
            UpdateStatus(false);
            CreateNetObject(GetSprite(), position);
        }
    }

    internal sealed class GreenBed : BedWars.Bed
    {
        public GreenBed(Vector2 position)
        {
            BaseSprite = "<size=70%><line-height=97%><cspace=0.16em><mark=#baffd0>WW</mark><mark=#00ff7b>WWWW</mark>\n<mark=#baffd0>WW</mark><mark=#00ff7b>WWWW</mark>\n<mark=#baffd0>WW</mark><mark=#00ff7b>WWWW</mark>\n<mark=#82531a>W</mark><#0000>WWWW</color><mark=#82531a>W";
            UpdateStatus(false);
            CreateNetObject(GetSprite(), position);
        }
    }

    internal sealed class YellowBed : BedWars.Bed
    {
        public YellowBed(Vector2 position)
        {
            BaseSprite = "<size=70%><line-height=97%><cspace=0.16em><mark=#fffebd>WW</mark><mark=#ffff00>WWWW</mark>\n<mark=#fffebd>WW</mark><mark=#ffff00>WWWW</mark>\n<mark=#fffebd>WW</mark><mark=#ffff00>WWWW</mark>\n<mark=#82531a>W</mark><#0000>WWWW</color><mark=#82531a>W";
            UpdateStatus(false);
            CreateNetObject(GetSprite(), position);
        }
    }

    internal sealed class RedBed : BedWars.Bed
    {
        public RedBed(Vector2 position)
        {
            BaseSprite = "<size=70%><line-height=97%><cspace=0.16em><mark=#ffbbb5>WW</mark><mark=#ff0008>WWWW</mark>\n<mark=#ffbbb5>WW</mark><mark=#ff0008>WWWW</mark>\n<mark=#ffbbb5>WW</mark><mark=#ff0008>WWWW</mark>\n<mark=#82531a>W</mark><#0000>WWWW</color><mark=#82531a>W";
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
            CreateNetObject("<line-height=97%><cspace=0.16em><#0000>WWWW</color><mark=#000000>W</mark><mark=#ffea00>W</mark>\n<#0000>WWW</color><mark=#000000>W</mark><#0000>WW\nW</color><mark=#ff0004>WW</mark><#0000>WWW</color>\n<mark=#ff0004>WWWW</mark><#0000>WW</color>\n<mark=#ff0004>WWWW</mark><#0000>WW\nW</color><mark=#ff0004>WW</mark><#0000>WWW", location);
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

        public override void OnMeeting()
        {
            if (Options.CurrentGameMode == CustomGameMode.BedWars)
                base.OnMeeting();
            else
                Despawn();
        }
    }

    internal sealed class Portal : CustomNetObject
    {
        public Portal(Vector2 position)
        {
            CreateNetObject("<size=70%><line-height=97%><cspace=0.16em><mark=#2b006b>WWWW</mark>\n<mark=#2b006b>W</mark><mark=#fa69ff>WW</mark><mark=#2b006b>W</mark>\n<mark=#2b006b>W</mark><mark=#fa69ff>WW</mark><mark=#2b006b>W</mark>\n<mark=#2b006b>W</mark><mark=#fa69ff>WW</mark><mark=#2b006b>W</mark>\n<mark=#2b006b>W</mark><mark=#fa69ff>WW</mark><mark=#2b006b>W</mark>\n<mark=#2b006b>WWWW", position);
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
            CreateNetObject("<line-height=67%><alpha=#00>█<#00ff15>█<alpha=#00>█<#00ff15>█<alpha=#00>█<#00ff15>█<br><alpha=#00>█<#00ff15>█<alpha=#00>█<#00ff15>█<alpha=#00>█<#00ff15>█<br><#00ff15>█<#00ff15>█<alpha=#00>█<#00ff15>█<alpha=#00>█<#00ff15>█<br><#00ff15>█<alpha=#00>█<alpha=#00>█<#00ff15>█<alpha=#00>█<#00ff15>█<br><#00ff15>█<alpha=#00>█<#00ff15>█<#00ff15>█<alpha=#00>█<#00ff15>█<br><#00ff15>█<alpha=#00>█<#00ff15>█<alpha=#00>█<alpha=#00>█<#00ff15>█", Position);
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
            LateTask.New(() => Hide(Main.EnumeratePlayerControls().Where(x => x.PlayerId != visibleTo)), 0.4f);
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