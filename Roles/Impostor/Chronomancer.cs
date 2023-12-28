using Hazel;
using System.Collections.Generic;
using static TOHE.Options;

namespace TOHE.Roles.Impostor
{
    public static class Chronomancer
    {
        private static readonly int Id = 642100;
        public static List<byte> playerIdList = [];

        private static OptionItem KCD;
        private static OptionItem ChargeInterval;
        private static OptionItem ChargeLossInterval;

        private static bool isRampaging;
        private static int chargePercent;
        private static long lastUpdate;

        public static void SetupCustomOption()
        {
            SetupSingleRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Chronomancer, 1);
            KCD = FloatOptionItem.Create(Id + 11, "KillCooldown", new(0f, 180f, 2.5f), 25f, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Chronomancer])
                .SetValueFormat(OptionFormat.Seconds);
            ChargeInterval = IntegerOptionItem.Create(Id + 12, "ChargeInterval", new(1, 20, 1), 5, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Chronomancer])
                .SetValueFormat(OptionFormat.Seconds);
            ChargeLossInterval = IntegerOptionItem.Create(Id + 13, "ChargeLossInterval", new(1, 50, 1), 25, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Chronomancer])
                .SetValueFormat(OptionFormat.Seconds);
        }
        public static void SendRPC()
        {
            if (!IsEnable || !Utils.DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetAdmireLimit, SendOption.Reliable, -1);
            writer.Write(isRampaging);
            writer.Write(chargePercent);
            writer.Write(lastUpdate.ToString());
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void ReceiveRPC(MessageReader reader)
        {
            isRampaging = reader.ReadBoolean();
            chargePercent = reader.ReadInt32();
            lastUpdate = long.Parse(reader.ReadString());
        }
        public static void Init()
        {
            playerIdList = [];
            isRampaging = false;
            chargePercent = 0;
            lastUpdate = Utils.GetTimeStamp() + 30;
        }
        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            lastUpdate = Utils.GetTimeStamp() + 10;
        }
        public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = isRampaging ? 0.01f : KCD.GetFloat();
        public static bool IsEnable => playerIdList.Count > 0;
        public static bool IsRPCNecessary => Utils.DoRPC;
        public static void OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (killer == null) return;
            if (target == null) return;
            if (!killer.Is(CustomRoles.Chronomancer)) return;

            if (chargePercent <= 0) return;

            if (!isRampaging)
            {
                isRampaging = true;
                SendRPC();
                killer.ResetKillCooldown();
                killer.SyncSettings();
            }
        }
        public static void OnFixedUpdate(PlayerControl pc)
        {
            if (pc == null) return;
            if (!pc.Is(CustomRoles.Chronomancer)) return;
            if (!GameStates.IsInTask) return;
            if (lastUpdate >= Utils.GetTimeStamp()) return;

            lastUpdate = Utils.GetTimeStamp();

            bool notify = false;
            var beforeCharge = chargePercent;

            if (isRampaging)
            {
                chargePercent -= ChargeLossInterval.GetInt();
                if (chargePercent <= 0)
                {
                    chargePercent = 0;
                    isRampaging = false;
                    pc.ResetKillCooldown();
                    pc.SyncSettings();
                    pc.SetKillCooldown();
                }
                notify = true;
            }
            else if (Main.KillTimers[pc.PlayerId] <= 0)
            {
                chargePercent += ChargeInterval.GetInt();
                if (chargePercent > 100) chargePercent = 100;
                notify = true;
            }

            if (notify && !pc.IsModClient())
            {
                pc.Notify(string.Format(Translator.GetString("ChronomancerPercent"), chargePercent), 300f);
            }

            if (beforeCharge != chargePercent && pc.IsModClient() && pc.PlayerId != 0)
            {
                SendRPC();
            }
        }
        public static string GetHudText() => chargePercent > 0 ? string.Format(Translator.GetString("ChronomancerPercent"), chargePercent) : string.Empty;
        public static void OnReportDeadBody()
        {
            lastUpdate = Utils.GetTimeStamp();
            chargePercent = 0;
            isRampaging = false;
            SendRPC();
        }
    }
}
