using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static TOHE.Options;
using static TOHE.Utils;
using static TOHE.Translator;
using TOHE.Roles.Neutral;
using UnityEngine.Bindings;
using AmongUs.GameOptions;
using Hazel;

namespace TOHE.Roles.Impostor
{
    public static class Duellist
    {
        private static readonly int Id = 642850;
        private static List<byte> playerIdList = [];
        private static Dictionary<byte, byte> DuelPair = [];
        private static OptionItem SSCD;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Duellist);
            SSCD = FloatOptionItem.Create(Id + 5, "ShapeshiftCooldown", new(0f, 60f, 2.5f), 15f, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Duellist])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public static void Init()
        {
            playerIdList = [];
            DuelPair = [];
        }

        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
        }

        public static void ApplyGameOptions()
        {
            AURoleOptions.ShapeshifterCooldown = SSCD.GetFloat();
        }

        private static void SendRPC(byte duellistId, byte targetId)
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetDruidLimit, SendOption.Reliable, -1);
            writer.Write(duellistId);
            writer.Write(targetId);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public static void ReceiveRPC(MessageReader reader)
        {
            byte duellistId = reader.ReadByte();
            byte targetId = reader.ReadByte();
            DuelPair[duellistId] = targetId;
        }

        public static void OnShapeshift(PlayerControl duellist, PlayerControl target)
        {
            if (duellist == null || target == null) return;
            if (target.inMovingPlat || target.onLadder || target.MyPhysics.Animations.IsPlayingEnterVentAnimation() || target.MyPhysics.Animations.IsPlayingAnyLadderAnimation() || !target.IsAlive())
            {
                duellist.Notify(GetString("TargetCannotBeTeleported"));
                return;
            }

            var pos = Pelican.GetBlackRoomPS();
            duellist.TP(pos);
            target.TP(pos);
            DuelPair[duellist.PlayerId] = target.PlayerId;
            SendRPC(duellist.PlayerId, target.PlayerId);
        }

        public static void OnFixedUpdate()
        {
            if (!playerIdList.Any() || !DuelPair.Any()) return;
            foreach (var pair in DuelPair)
            {
                var duellist = GetPlayerById(pair.Key);
                var target = GetPlayerById(pair.Value);
                var DAlive = duellist.IsAlive();
                var TAlive = target.IsAlive();
                if (!DAlive && !TAlive) continue;
                else if (DAlive && !TAlive) duellist.TPtoRndVent();
                else if (TAlive && !DAlive) target.TPtoRndVent();
                else continue;
            }
        }
    }
}
