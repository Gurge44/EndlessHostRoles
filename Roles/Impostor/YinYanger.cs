using Hazel;
using System.Collections.Generic;
using UnityEngine;
using static TOHE.Options;
using static TOHE.Utils;

namespace TOHE.Roles.Impostor
{
    public class YinYanger : RoleBase
    {
        private const int Id = 642870;
        private byte YinYangerId = byte.MaxValue;
        private List<byte> YinYangedPlayers = [];

        private static OptionItem YinYangCD;
        private static OptionItem KCD;

        // ReSharper disable once InconsistentNaming
        private PlayerControl YinYanger_ => GetPlayerById(YinYangerId);

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.YinYanger);
            YinYangCD = FloatOptionItem.Create(Id + 5, "YinYangCD", new(0f, 60f, 2.5f), 12.5f, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.YinYanger])
                .SetValueFormat(OptionFormat.Seconds);
            KCD = FloatOptionItem.Create(Id + 6, "KillCooldown", new(0f, 60f, 2.5f), 25f, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.YinYanger])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public override void Init()
        {
            YinYangerId = byte.MaxValue;
            YinYangedPlayers = [];
        }

        public override void Add(byte playerId)
        {
            YinYangerId = playerId;
        }

        public override bool IsEnable => YinYangerId != byte.MaxValue;

        public override void SetKillCooldown(byte playerId)
        {
            Main.AllPlayerKillCooldown[playerId] = YinYangedPlayers.Count == 2 ? KCD.GetFloat() : YinYangCD.GetFloat();
        }

        void SendRPC(bool isClear, byte playerId = byte.MaxValue)
        {
            if (!DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncYinYanger, SendOption.Reliable);
            writer.Write(YinYangerId);
            writer.Write(isClear);
            if (!isClear) writer.Write(playerId);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public static void ReceiveRPC(MessageReader reader)
        {
            byte yyId = reader.ReadByte();
            if (Main.PlayerStates[yyId].Role is not YinYanger yy) return;
            bool isClear = reader.ReadBoolean();
            if (!isClear)
            {
                byte playerId = reader.ReadByte();
                yy.YinYangedPlayers.Add(playerId);
            }
            else yy.YinYangedPlayers.Clear();
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (!IsEnable) return false;
            if (killer == null || target == null || !killer.Is(CustomRoles.YinYanger)) return false;

            if (YinYangedPlayers.Count == 2)
            {
                return true;
            }

            if (YinYangedPlayers.Contains(target.PlayerId))
            {
                return false;
            }

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

        public override void OnReportDeadBody()
        {
            if (!IsEnable) return;
            YinYangedPlayers.Clear();
            YinYanger_?.ResetKillCooldown();
            SendRPC(true);
        }

        public override void OnGlobalFixedUpdate(PlayerControl player)
        {
            if (!GameStates.IsInTask) return;

            foreach (var state in Main.PlayerStates)
            {
                if (state.Value.Role is not YinYanger { IsEnable: true, YinYangedPlayers.Count: 2 } yy) continue;

                var yyPc = yy.YinYanger_;
                var pc1 = GetPlayerById(yy.YinYangedPlayers[0]);
                var pc2 = GetPlayerById(yy.YinYangedPlayers[1]);

                if (pc1 == null || pc2 == null || yyPc == null) return;
                if (!pc1.IsAlive() || !pc2.IsAlive() || !yyPc.IsAlive()) return;

                if (Vector2.Distance(pc1.Pos(), pc2.Pos()) <= 2f)
                {
                    if (!yyPc.RpcCheckAndMurder(pc1, true)
                        || !yyPc.RpcCheckAndMurder(pc2, true)) return;

                    pc1.Suicide(PlayerState.DeathReason.YinYanged, yyPc);
                    pc2.Suicide(PlayerState.DeathReason.YinYanged, yyPc);
                }
            }
        }

        public static string ModeText(PlayerControl pc)
        {
            if (Main.PlayerStates[pc.PlayerId].Role is YinYanger { IsEnable: true } yy)
            {
                return yy.YinYangedPlayers.Count == 2 ? $"<color=#00ffa5>{Translator.GetString("Mode")}:</color> {Translator.GetString("YinYangModeNormal")}" : $"<color=#00ffa5>{Translator.GetString("Mode")}:</color> {Translator.GetString("YinYangMode")} ({yy.YinYangedPlayers.Count}/2)";
            }

            return string.Empty;
        }
    }
}