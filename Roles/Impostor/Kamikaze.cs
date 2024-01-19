using Epic.OnlineServices;
using Hazel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static TOHE.Options;
using static TOHE.Translator;
using static TOHE.Utils;

namespace TOHE.Roles.Impostor
{
    internal class Kamikaze
    {
        private static int Id => 643310;
        public static bool IsEnable = false;

        private static readonly Dictionary<byte, List<byte>> MarkedPlayers = [];
        public static readonly Dictionary<byte, float> MarkLimit = [];

        private static OptionItem MarkCD;
        private static OptionItem KamikazeLimitOpt;
        public static OptionItem KamikazeAbilityUseGainWithEachKill;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Kamikaze);
            MarkCD = FloatOptionItem.Create(Id + 2, "KamikazeMarkCD", new(0f, 180f, 2.5f), 30f, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Kamikaze])
                .SetValueFormat(OptionFormat.Seconds);
            KamikazeLimitOpt = IntegerOptionItem.Create(Id + 5, "AbilityUseLimit", new(0, 5, 1), 1, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Kamikaze])
                .SetValueFormat(OptionFormat.Times);
            KamikazeAbilityUseGainWithEachKill = FloatOptionItem.Create(Id + 6, "AbilityUseGainWithEachKill", new(0f, 5f, 0.1f), 0.5f, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Kamikaze])
                .SetValueFormat(OptionFormat.Times);
        }

        public static void Init()
        {
            MarkedPlayers.Clear();
            MarkLimit.Clear();
            IsEnable = false;
        }

        public static void Add(byte playerId)
        {
            MarkedPlayers[playerId] = [];
            MarkLimit[playerId] = KamikazeLimitOpt.GetInt();
            IsEnable = true;
        }

        public static void SendRPCSyncLimit(byte playerId)
        {
            if (!IsEnable || !DoRPC || playerId == 0) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncKamikazeLimit, SendOption.Reliable, -1);
            writer.Write(playerId);
            writer.Write(MarkLimit[playerId]);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        private static void SendRPCAddTarget(byte kamikazeId, byte targetId)
        {
            if (!IsEnable || !DoRPC || kamikazeId == 0) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.KamikazeAddTarget, SendOption.Reliable, -1);
            writer.Write(kamikazeId);
            writer.Write(targetId);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public static void ReceiveRPCSyncLimit(MessageReader reader)
        {
            byte playerId = reader.ReadByte();
            float limit = reader.ReadSingle();
            MarkLimit[playerId] = limit;
        }

        public static void ReceiveRPCAddTarget(MessageReader reader)
        {
            byte playerId = reader.ReadByte();
            byte targetId = reader.ReadByte();
            MarkedPlayers[playerId].Add(targetId);
        }

        public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (killer == null || target == null) return false;
            if (MarkLimit.TryGetValue(killer.PlayerId, out var limit) && limit < 1) return true;
            return killer.CheckDoubleTrigger(target, () =>
            {
                MarkedPlayers[killer.PlayerId].Add(target.PlayerId);
                SendRPCAddTarget(killer.PlayerId, target.PlayerId);
                killer.SetKillCooldown(MarkCD.GetFloat());
                MarkLimit[killer.PlayerId]--;
                SendRPCSyncLimit(killer.PlayerId);
            });
        }

        public static void OnFixedUpdate()
        {
            if (!IsEnable) return;

            foreach (var kvp in MarkedPlayers)
            {
                var kamikazePc = GetPlayerById(kvp.Key);
                if (kamikazePc.IsAlive()) continue;

                foreach (var id in kvp.Value)
                {
                    var victim = GetPlayerById(id);
                    if (victim == null || !victim.IsAlive()) continue;
                    victim.Suicide(PlayerState.DeathReason.Kamikazed, kamikazePc);
                }

                kvp.Value.Clear();
                MarkedPlayers.Remove(kvp.Key);
                Logger.Info($"Murder {kamikazePc.GetRealName()}'s targets: {string.Join(", ", kvp.Value.Select(x => GetPlayerById(x).GetNameWithRole()))}", "Kamikaze");
            }
        }
    }
}
