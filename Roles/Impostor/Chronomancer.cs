using System.Collections.Generic;
using EHR.Modules;
using Hazel;
using static EHR.Options;

namespace EHR.Impostor
{
    public class Chronomancer : RoleBase
    {
        private const int Id = 642100;
        public static List<byte> playerIdList = [];

        private static OptionItem KCD;
        private static OptionItem ChargeInterval;
        private static OptionItem ChargeLossInterval;
        private int ChargePercent;
        private byte ChronomancerId;

        private bool IsRampaging;
        private long LastUpdate;
        public override bool IsEnable => playerIdList.Count > 0;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Chronomancer);
            KCD = new FloatOptionItem(Id + 11, "KillCooldown", new(0f, 180f, 2.5f), 25f, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Chronomancer])
                .SetValueFormat(OptionFormat.Seconds);
            ChargeInterval = new IntegerOptionItem(Id + 12, "ChargeInterval", new(1, 20, 1), 5, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Chronomancer])
                .SetValueFormat(OptionFormat.Percent);
            ChargeLossInterval = new IntegerOptionItem(Id + 13, "ChargeLossInterval", new(1, 50, 1), 25, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Chronomancer])
                .SetValueFormat(OptionFormat.Percent);
        }

        void SendRPC()
        {
            if (!Utils.DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncChronomancer, SendOption.Reliable);
            writer.Write(ChronomancerId);
            writer.Write(IsRampaging);
            writer.Write(ChargePercent);
            writer.Write(LastUpdate.ToString());
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public void ReceiveRPC(bool isRampaging, int chargePercent, long lastUpdate)
        {
            IsRampaging = isRampaging;
            ChargePercent = chargePercent;
            LastUpdate = lastUpdate;
        }

        public override void Init()
        {
            playerIdList = [];
            IsRampaging = false;
            ChargePercent = 0;
            LastUpdate = Utils.TimeStamp + 30;
            ChronomancerId = byte.MaxValue;
        }

        public override void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            IsRampaging = false;
            ChargePercent = 0;
            LastUpdate = Utils.TimeStamp + 10;
            ChronomancerId = playerId;
        }

        public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = IsRampaging ? 0.01f : KCD.GetFloat();

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (ChargePercent <= 0) return base.OnCheckMurder(killer, target);

            if (!IsRampaging)
            {
                IsRampaging = true;
                SendRPC();
                killer.ResetKillCooldown();
                killer.SyncSettings();
            }

            return base.OnCheckMurder(killer, target);
        }

        public override void OnFixedUpdate(PlayerControl pc)
        {
            if (pc == null) return;
            if (!pc.Is(CustomRoles.Chronomancer)) return;
            if (!GameStates.IsInTask) return;
            if (LastUpdate >= Utils.TimeStamp) return;

            LastUpdate = Utils.TimeStamp;

            bool notify = false;
            var beforeCharge = ChargePercent;

            if (IsRampaging)
            {
                ChargePercent -= ChargeLossInterval.GetInt();
                if (ChargePercent <= 0)
                {
                    ChargePercent = 0;
                    IsRampaging = false;
                    pc.ResetKillCooldown();
                    pc.SyncSettings();
                    pc.SetKillCooldown();
                }

                notify = true;
            }
            else if (Main.KillTimers[pc.PlayerId] <= 0 && !MeetingStates.FirstMeeting)
            {
                ChargePercent += ChargeInterval.GetInt();
                if (ChargePercent > 100) ChargePercent = 100;
                notify = true;
            }

            if (notify && !pc.IsModClient())
            {
                pc.Notify(string.Format(Translator.GetString("ChronomancerPercent"), ChargePercent), 300f);
            }

            if (beforeCharge != ChargePercent && pc.IsModClient() && !pc.IsHost())
            {
                SendRPC();
            }
        }

        public override string GetSuffix(PlayerControl pc, PlayerControl _, bool hud = false, bool m = false)
        {
            if (!hud || Main.PlayerStates[pc.PlayerId].Role is not Chronomancer cm) return string.Empty;
            return cm.ChargePercent > 0 ? string.Format(Translator.GetString("ChronomancerPercent"), cm.ChargePercent) : string.Empty;
        }

        public override void OnReportDeadBody()
        {
            LastUpdate = Utils.TimeStamp;
            ChargePercent = 0;
            IsRampaging = false;
            SendRPC();
        }

        public override void AfterMeetingTasks()
        {
            OnReportDeadBody();
        }

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            hud.AbilityButton?.ToggleVisible(false);
        }
    }
}