using Hazel;
using System.Collections.Generic;
using UnityEngine;
using static TOHE.Options;
using static TOHE.Utils;

namespace TOHE.Roles.Impostor
{
    public static class YinYanger
    {
        private static readonly int Id = 642870;
        private static byte YinYangerId = byte.MaxValue;
        private static List<byte> YinYangedPlayers = [];

        private static OptionItem YinYangCD;
        private static OptionItem KCD;

        private static PlayerControl YinYanger_ => GetPlayerById(YinYangerId);

        public static void SetupCustomOption()
        {
            SetupSingleRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.YinYanger, 1);
            YinYangCD = FloatOptionItem.Create(Id + 5, "YinYangCD", new(0f, 60f, 2.5f), 12.5f, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.YinYanger])
                .SetValueFormat(OptionFormat.Seconds);
            KCD = FloatOptionItem.Create(Id + 6, "KillCooldown", new(0f, 60f, 2.5f), 25f, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.YinYanger])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public static void Init()
        {
            YinYangerId = byte.MaxValue;
            YinYangedPlayers = [];
        }

        public static void Add(byte playerId)
        {
            YinYangerId = playerId;
        }

        public static bool IsEnable => YinYangerId != byte.MaxValue;

        public static void SetKillCooldown(byte playerId)
        {
            Main.AllPlayerKillCooldown[playerId] = YinYangedPlayers.Count == 2 ? KCD.GetFloat() : YinYangCD.GetFloat();
        }

        public static void SendRPC(bool isClear, byte playerId = byte.MaxValue)
        {
            if (!IsEnable || !DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncYinYanger, SendOption.Reliable, -1);
            writer.Write(isClear);
            if (!isClear) writer.Write(playerId);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public static void ReceiveRPC(MessageReader reader)
        {
            if (!IsEnable) return;
            bool isClear = reader.ReadBoolean();
            if (!isClear)
            {
                byte playerId = reader.ReadByte();
                YinYangedPlayers.Add(playerId);
            }
            else YinYangedPlayers.Clear();
        }

        public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (!IsEnable) return false;
            if (killer == null || target == null) return false;

            if (YinYangedPlayers.Count == 2)
            {
                return true;
            }
            else
            {
                if (YinYangedPlayers.Contains(target.PlayerId))
                {
                    return false;
                }
                else
                {
                    YinYangedPlayers.Add(target.PlayerId);
                    SendRPC(false, target.PlayerId);

                    if (YinYangedPlayers.Count == 2)
                    {
                        killer.ResetKillCooldown();
                        killer.SyncSettings();
                    }
                    killer.SetKillCooldown();
                    return false;
                }
            }
        }

        public static void OnReportDeadBody()
        {
            if (!IsEnable) return;
            YinYangedPlayers.Clear();
            YinYanger_?.ResetKillCooldown();
            SendRPC(true);
        }

        public static void OnFixedUpdate()
        {
            if (!IsEnable) return;
            if (!GameStates.IsInTask) return;
            if (YinYangedPlayers.Count < 2) return;

            var yy = YinYanger_;
            var pc1 = GetPlayerById(YinYangedPlayers[0]);
            var pc2 = GetPlayerById(YinYangedPlayers[1]);

            if (pc1 == null || pc2 == null || yy == null) return;
            if (!pc1.IsAlive() || !pc2.IsAlive() || !yy.IsAlive()) return;

            if (Vector2.Distance(pc1.Pos(), pc2.Pos()) <= 2f)
            {
                if (!yy.RpcCheckAndMurder(pc1, true)
                 || !yy.RpcCheckAndMurder(pc2, true)) return;

                pc1.Suicide(PlayerState.DeathReason.YinYanged, yy);
                pc2.Suicide(PlayerState.DeathReason.YinYanged, yy);
            }
        }

        public static string ModeText => YinYangedPlayers.Count == 2 ? "<color=#00ffa5>Mode:</color> Kill" : $"<color=#00ffa5>Mode:</color> Yin Yang ({YinYangedPlayers.Count}/2)";
    }
}
