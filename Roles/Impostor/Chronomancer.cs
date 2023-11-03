using Hazel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static TOHE.Options;

namespace TOHE.Roles.Impostor
{
    public static class Chronomancer
    {
        private static readonly int Id = 642100;
        public static List<byte> playerIdList = new();

        private static OptionItem KCD;

        private static bool isRampaging;
        private static int chargePercent;
        private static long lastUpdate;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Chronomancer);
            KCD = FloatOptionItem.Create(Id + 11, "KillCooldown", new(0f, 180f, 2.5f), 25f, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Chronomancer])
                .SetValueFormat(OptionFormat.Seconds);
        }
        public static void SendRPC()
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetAdmireLimit, SendOption.Reliable, -1);
            writer.Write(isRampaging);
            writer.Write(chargePercent);
            writer.Write(lastUpdate);
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
            playerIdList = new();
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
        public static bool IsEnable => playerIdList.Any();
        public static void OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (killer == null) return;
            if (target == null) return;
            if (!killer.Is(CustomRoles.Chronomancer)) return;

            if (chargePercent <= 0) return;

            if (!isRampaging)
            {
                isRampaging = true;
                killer.ResetKillCooldown();
                killer.SyncSettings();
            }
        }
        public static void OnFixedUpdate(PlayerControl pc)
        {
            if (pc == null) return;
            if (!pc.Is(CustomRoles.Chronomancer)) return;
            if (lastUpdate == Utils.GetTimeStamp()) return;

            lastUpdate = Utils.GetTimeStamp();

            if (isRampaging)
            {
                chargePercent -= 25;
                if (chargePercent <= 0)
                {
                    chargePercent = 0;
                    isRampaging = false;
                    pc.ResetKillCooldown();
                    pc.SyncSettings();
                    pc.SetKillCooldown();
                    pc.Notify(string.Format(Translator.GetString("ChronomancerPercent"), chargePercent));
                }
            }
            else if (pc.killTimer <= 0)
            {
                chargePercent += 5;
                if (chargePercent > 100) chargePercent = 100;
                pc.Notify(string.Format(Translator.GetString("ChronomancerPercent"), chargePercent));
            }
        }
    }
}
