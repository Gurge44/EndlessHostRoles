using AmongUs.GameOptions;
using Hazel;
using System;
using System.Linq;
using TOHE.Modules;
using static TOHE.Options;
using static TOHE.Translator;
using static TOHE.Utils;

namespace TOHE.Roles.Neutral
{
    internal class SoulHunter : RoleBase
    {
        private static int Id => 643400;

        public static OptionItem CanVent;
        private static OptionItem HasImpostorVision;
        public static OptionItem NumOfSoulsToWin;
        private static OptionItem WaitingTimeAfterMeeting;
        private static OptionItem TimeToKillTarget;
        private static OptionItem GetSoulForSuicide;

        private byte SoulHunterId;
        public PlayerControl SoulHunter_;
        public int Souls;
        public (byte ID, long START_TIMESTAMP, bool FROZEN) CurrentTarget = (byte.MaxValue, 0, false);
        private long LastUpdate;
        private float NormalSpeed;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.SoulHunter);
            CanVent = BooleanOptionItem.Create(Id + 3, "CanVent", true, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.SoulHunter]);
            HasImpostorVision = BooleanOptionItem.Create(Id + 4, "ImpostorVision", true, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.SoulHunter]);
            NumOfSoulsToWin = IntegerOptionItem.Create(Id + 5, "SoulHunterNumOfSoulsToWin", new(1, 14, 1), 3, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.SoulHunter]);
            WaitingTimeAfterMeeting = IntegerOptionItem.Create(Id + 6, "SoulHunterFreezeTimeAfterMeeting", new(0, 90, 1), 3, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.SoulHunter])
                .SetValueFormat(OptionFormat.Seconds);
            TimeToKillTarget = IntegerOptionItem.Create(Id + 7, "SoulHunterTimeToKillTarget", new(1, 90, 1), 30, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.SoulHunter])
                .SetValueFormat(OptionFormat.Seconds);
            GetSoulForSuicide = BooleanOptionItem.Create(Id + 8, "SoulHunterGetSoulForSuicide", true, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.SoulHunter]);
        }

        public override void Init()
        {
            SoulHunterId = byte.MaxValue;
            SoulHunter_ = null;
            Souls = 0;
            CurrentTarget = (byte.MaxValue, 0, false);
            LastUpdate = 0;
            NormalSpeed = Main.NormalOptions.PlayerSpeedMod;
        }

        public override void Add(byte playerId)
        {
            SoulHunterId = playerId;
            _ = new LateTask(() => { SoulHunter_ = GetPlayerById(playerId); }, 3f, log: false);
            Souls = 0;
            CurrentTarget = (byte.MaxValue, 0, false);
            LastUpdate = 0;
            NormalSpeed = Main.AllPlayerSpeed[playerId];

            if (!AmongUsClient.Instance.AmHost) return;
            if (!Main.ResetCamPlayerList.Contains(playerId))
                Main.ResetCamPlayerList.Add(playerId);
        }

        public override bool IsEnable => SoulHunterId != byte.MaxValue;
        public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = WaitingTimeAfterMeeting.GetFloat() + 1.5f;
        public override void ApplyGameOptions(IGameOptions opt, byte id) => opt.SetVision(HasImpostorVision.GetBool());
        public override bool CanUseImpostorVentButton(PlayerControl pc) => CanVent.GetBool();
        public override bool CanUseKillButton(PlayerControl pc) => pc.IsAlive();

        public static bool IsSoulHunterTarget(byte id) => Main.PlayerStates.Any(x => x.Value.Role is SoulHunter { IsEnable: true, IsTargetBlocked: true } sh && sh.CurrentTarget.ID == id);
        public static SoulHunter GetSoulHunter(byte targetId) => Main.PlayerStates.FirstOrDefault(x => x.Value.Role is SoulHunter { IsEnable: true, IsTargetBlocked: true } sh && sh.CurrentTarget.ID == targetId).Value.Role as SoulHunter;

        void SendRPC()
        {
            var writer = CreateCustomRoleRPC(CustomRPC.SyncSoulHunter);
            writer.Write(SoulHunterId);
            writer.Write(Souls);
            writer.Write(CurrentTarget.ID);
            writer.Write(CurrentTarget.START_TIMESTAMP.ToString());
            writer.Write(CurrentTarget.FROZEN);
            EndRPC(writer);
        }

        public static void ReceiveRPC(MessageReader reader)
        {
            byte id = reader.ReadByte();
            if (Main.PlayerStates[id].Role is not SoulHunter sh) return;
            sh.Souls = reader.ReadInt32();
            sh.CurrentTarget.ID = reader.ReadByte();
            sh.CurrentTarget.START_TIMESTAMP = long.Parse(reader.ReadString());
            sh.CurrentTarget.FROZEN = reader.ReadBoolean();
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (SoulHunter_ == null || target == null) return false;

            if (CurrentTarget.ID == byte.MaxValue || CurrentTarget.START_TIMESTAMP == 0)
            {
                CurrentTarget.ID = target.PlayerId;
                CurrentTarget.START_TIMESTAMP = 0;
                SoulHunter_.SetKillCooldown(5f);
                Logger.Info($"Marked Next Target: {target.GetNameWithRole()}", "SoulHunter");
                SendRPC();
                return false;
            }

            if (CurrentTarget.ID == target.PlayerId && CurrentTarget.START_TIMESTAMP != 0)
            {
                CurrentTarget.ID = byte.MaxValue;
                CurrentTarget.START_TIMESTAMP = 0;
                LastUpdate = 0;
                Souls++;
                SoulHunter_.Notify(GetString("SoulHunterNotifySuccess"));
                _ = new LateTask(() => { SoulHunter_.SetKillCooldown(1f); }, 0.1f, log: false);
                Logger.Info("Killed Target", "SoulHunter");
                SendRPC();
                return true;
            }

            CurrentTarget.ID = byte.MaxValue;
            CurrentTarget.START_TIMESTAMP = 0;
            LastUpdate = TimeStamp;
            SoulHunter_.Suicide();
            target.Notify(GetString("SoulHunterTargetNotifySurvived"));
            Logger.Info("Killed Incorrect Player => Suicide", "SoulHunter");
            SendRPC();
            return false;
        }

        public override void AfterMeetingTasks()
        {
            if (!IsEnable || CurrentTarget.ID == byte.MaxValue) return;

            long now = TimeStamp;

            CurrentTarget.FROZEN = true;
            NormalSpeed = Main.AllPlayerSpeed[SoulHunterId];
            Main.AllPlayerSpeed[SoulHunterId] = Main.MinSpeed;
            SoulHunter_.MarkDirtySettings();

            CurrentTarget.START_TIMESTAMP = now;

            PlayerControl target = GetPlayerById(CurrentTarget.ID);
            int waitingTime = WaitingTimeAfterMeeting.GetInt();

            if (!SoulHunter_.IsModClient()) SoulHunter_.Notify(string.Format(GetString("SoulHunterNotifyFreeze"), target.GetRealName(), waitingTime + 1));
            target.Notify(string.Format(GetString("SoulHunterTargetNotify"), SoulHunter_.GetRealName()), 300f);
            LastUpdate = now;

            SendRPC();
            Logger.Info($"Waiting to being hunting (in {waitingTime}s)", "SoulHunter");
        }

        public override void OnFixedUpdate(PlayerControl pc)
        {
            if (!IsEnable || !GameStates.IsInTask || !SoulHunter_.IsAlive() || CurrentTarget.ID == byte.MaxValue) return;

            if (!CurrentTarget.FROZEN && Math.Abs(Main.AllPlayerSpeed[SoulHunterId] - NormalSpeed) > 0.1f)
            {
                Main.AllPlayerSpeed[SoulHunterId] = NormalSpeed;
                SoulHunter_.MarkDirtySettings();
            }

            if (CurrentTarget.START_TIMESTAMP == 0) return;

            long now = TimeStamp;
            if (LastUpdate >= now) return;
            LastUpdate = now;

            PlayerControl target = GetPlayerById(CurrentTarget.ID);
            string targetName = target.GetRealName();
            int waitingTime = WaitingTimeAfterMeeting.GetInt();
            int timeToKill = TimeToKillTarget.GetInt();

            if (!target.IsAlive() || target.Data.Disconnected)
            {
                CurrentTarget.ID = byte.MaxValue;
                CurrentTarget.START_TIMESTAMP = 0;
                CurrentTarget.FROZEN = false;

                Main.AllPlayerSpeed[SoulHunterId] = NormalSpeed;
                SoulHunter_.MarkDirtySettings();

                if (GetSoulForSuicide.GetBool()) Souls++;

                SoulHunter_.Notify(GetString("SoulHunterNotifySuccess"));
                Logger.Info("Target Died/Disconnected", "SoulHunter");
                SendRPC();

                return;
            }

            if (CurrentTarget.FROZEN)
            {
                if (CurrentTarget.START_TIMESTAMP + waitingTime < now)
                {
                    CurrentTarget.FROZEN = false;
                    Main.AllPlayerSpeed[SoulHunterId] = NormalSpeed;
                    SoulHunter_.MarkDirtySettings();

                    CurrentTarget.START_TIMESTAMP = now;
                    SendRPC();
                }
                else if (!SoulHunter_.IsModClient())
                {
                    SoulHunter_.Notify(string.Format(GetString("SoulHunterNotifyFreeze"), targetName, waitingTime - (now - CurrentTarget.START_TIMESTAMP) + 1));
                }
            }
            else
            {
                if (CurrentTarget.START_TIMESTAMP + timeToKill < now)
                {
                    CurrentTarget.ID = byte.MaxValue;
                    CurrentTarget.START_TIMESTAMP = 0;
                    SoulHunter_.Suicide();
                    target.Notify(GetString("SoulHunterTargetNotifySurvived"));
                    SendRPC();
                }
                else if (!SoulHunter_.IsModClient())
                {
                    SoulHunter_.Notify(string.Format(GetString("SoulHunterNotify"), timeToKill - (now - CurrentTarget.START_TIMESTAMP) + 1, targetName));
                }
            }
        }

        bool IsTargetBlocked => IsEnable && CurrentTarget.ID != byte.MaxValue && CurrentTarget.START_TIMESTAMP != 0;

        public static string HUDText(byte id)
        {
            if (Main.PlayerStates[id].Role is not SoulHunter { IsEnable: true } sh) return string.Empty;
            if (!sh.IsTargetBlocked) return string.Empty;
            return sh.CurrentTarget.FROZEN ? string.Format(GetString("SoulHunterNotifyFreeze"), GetPlayerById(sh.CurrentTarget.ID).GetRealName(), WaitingTimeAfterMeeting.GetInt() - (TimeStamp - sh.CurrentTarget.START_TIMESTAMP) + 1) : string.Format(GetString("SoulHunterNotify"), TimeToKillTarget.GetInt() - (TimeStamp - sh.CurrentTarget.START_TIMESTAMP) + 1, GetPlayerById(sh.CurrentTarget.ID).GetRealName());
        }

        public override string GetProgressText(byte id, bool comms)
        {
            if (Main.PlayerStates[id].Role is not SoulHunter { IsEnable: true } sh) return string.Empty;
            int souldsNeeded = NumOfSoulsToWin.GetInt();
            return $"<#777777>-</color> <#{(sh.Souls >= souldsNeeded ? "00ff00" : "ffffff")}>{sh.Souls}/{souldsNeeded}</color>";
        }
    }
}