using UnityEngine;
using InnerNet;
using Hazel;
using System.Collections.Generic;

// Credit: https://github.com/Rabek009/MoreGamemodes/blob/e054eb498094dfca0a365fc6b6fea8d17f9974d7/Modules/CustomObjects/CustomNetObject.cs
namespace EHR
{
    public class CustomNetObject
    {
        public void RpcChangeSprite(string sprite)
        {
            PC.RpcSetName(sprite);
        }

        public void RpcTeleport(Vector2 position)
        {
            PC.NetTransform.RpcSnapTo(position);
            Position = position;
        }

        public void Despawn()
        {
            PC.Despawn();
            CustomObjects.Remove(this);
        }
        
        public void Hide(PlayerControl player)
        {
            if (player.AmOwner)
            {
                PC.Visible = false;
                return;
            }
            MessageWriter writer = MessageWriter.Get(SendOption.None);
            writer.StartMessage(6);
            writer.Write(AmongUsClient.Instance.GameId);
            writer.WritePacked(player.GetClientId());
            writer.StartMessage(5);
            writer.WritePacked(PC.NetId);
            writer.EndMessage();
            writer.EndMessage();
            AmongUsClient.Instance.SendOrDisconnect(writer);
            writer.Recycle();
        }

        public virtual void OnFixedUpdate()
        {

        }

        public void CreateNetObject(string sprite, Vector2 position)
        {
            PC = Object.Instantiate(AmongUsClient.Instance.PlayerPrefab, Vector2.zero, Quaternion.identity);
            PC.PlayerId = 255;
            PC.isNew = false;
            PC.notRealPlayer = true;
            AmongUsClient.Instance.NetIdCnt += 1U;
            MessageWriter msg = MessageWriter.Get(SendOption.None);
			msg.StartMessage(5);
			msg.Write(AmongUsClient.Instance.GameId);
			AmongUsClient.Instance.WriteSpawnMessage(PC, -2, SpawnFlags.None, msg);
			msg.EndMessage();
			msg.StartMessage(6);
			msg.Write(AmongUsClient.Instance.GameId);
			msg.WritePacked(int.MaxValue);
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
			msg.EndMessage();
			AmongUsClient.Instance.SendOrDisconnect(msg);
			msg.Recycle();
            if (PlayerControl.AllPlayerControls.Contains(PC))
                PlayerControl.AllPlayerControls.Remove(PC);
            new LateTask(() => {
                PC.RpcSetName(sprite);
                PC.NetTransform.RpcSnapTo(position);
            }, 0.1f);
            Position = position;
            PC.cosmetics.currentBodySprite.BodySprite.color = Color.clear;
            PC.cosmetics.colorBlindText.color = Color.clear;
            Sprite = sprite;
            ++MaxId;
            Id = MaxId;
            CustomObjects.Add(this);
            foreach (var pc in PlayerControl.AllPlayerControls)
            {
                if (pc.AmOwner) continue;
                new LateTask(() => {
                    CustomRpcSender sender = CustomRpcSender.Create("SetFakeData", SendOption.None);
                    MessageWriter writer = sender.stream;
                    sender.StartMessage(pc.GetClientId());
                    writer.StartMessage(1);
                    {
                        writer.WritePacked(PC.NetId);
                        writer.Write(pc.PlayerId);
                    }
                    writer.EndMessage();
                    sender.StartRpc(PC.NetId, (byte)RpcCalls.MurderPlayer)
                        .WriteNetObject(PC)
                        .Write((int)MurderResultFlags.FailedError)
                        .EndRpc();
                    writer.StartMessage(1);
                    {
                        writer.WritePacked(PC.NetId);
                        writer.Write((byte)255);
                    }
                    writer.EndMessage();
                    sender.EndMessage();
                    sender.SendMessage();
                }, 0.1f);
            }
        }

        public static List<CustomNetObject> CustomObjects;
        public static int MaxId = -1;
        public PlayerControl PC;
        public string Sprite;
        public int Id;
        public Vector2 Position;
    }
}