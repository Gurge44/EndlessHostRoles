using AmongUs.GameOptions;
using Hazel;
using static TOHE.Options;
using static TOHE.Translator;
using static TOHE.Utils;

namespace TOHE.Roles.Neutral
{
    internal class SoulHunter
    {
        private static int Id => 643400;
        private static byte SoulHunterId;
        public static PlayerControl SoulHunter_;

        public static OptionItem CanVent;
        private static OptionItem HasImpostorVision;
        public static OptionItem NumOfSoulsToWin;
        private static OptionItem WaitingTimeAfterMeeting;
        private static OptionItem TimeToKillTarget;

        public static int Souls;
        public static (byte ID, long START_TIMESTAMP, bool FROZEN) CurrentTarget;
        private static long LastUpdate;
        private static float NormalSpeed;

        public static void SetupCustomOption()
        {
            SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.SoulHunter, 1, zeroOne: false);
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
        }
        public static void Init()
        {
            SoulHunterId = byte.MaxValue;
            SoulHunter_ = null;
            Souls = 0;
            CurrentTarget = (byte.MaxValue, 0, false);
            LastUpdate = 0;
            NormalSpeed = Main.NormalOptions.PlayerSpeedMod;
        }
        public static void Add(byte playerId)
        {
            SoulHunterId = playerId;
            _ = new LateTask(() => { SoulHunter_ = GetPlayerById(playerId); }, 3f, log: false);
            Souls = 0;
            CurrentTarget = (byte.MaxValue, 0, false);
            LastUpdate = 0;
            NormalSpeed = Main.NormalOptions.PlayerSpeedMod;

            if (!AmongUsClient.Instance.AmHost) return;
            if (!Main.ResetCamPlayerList.Contains(playerId))
                Main.ResetCamPlayerList.Add(playerId);
        }
        public static bool IsEnable => SoulHunterId != byte.MaxValue;
        public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = WaitingTimeAfterMeeting.GetFloat() + 1.5f;
        public static void ApplyGameOptions(IGameOptions opt) => opt.SetVision(HasImpostorVision.GetBool());
        public static void SendRPC()
        {
            var writer = CreateCustomRoleRPC(CustomRPC.SyncSoulHunter);
            writer.Write(Souls);
            writer.Write(CurrentTarget.ID);
            writer.Write(CurrentTarget.START_TIMESTAMP.ToString());
            writer.Write(CurrentTarget.FROZEN);
            EndRPC(writer);
        }
        public static void ReceiveRPC(MessageReader reader)
        {
            Souls = reader.ReadInt32();
            CurrentTarget.ID = reader.ReadByte();
            CurrentTarget.START_TIMESTAMP = long.Parse(reader.ReadString());
            CurrentTarget.FROZEN = reader.ReadBoolean();
        }
        public static bool OnCheckMurder(PlayerControl target)
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
            else if (CurrentTarget.ID == target.PlayerId && CurrentTarget.START_TIMESTAMP != 0)
            {
                CurrentTarget.ID = byte.MaxValue;
                CurrentTarget.START_TIMESTAMP = 0;
                LastUpdate = 0;
                Souls++;
                SoulHunter_.Notify(GetString("SoulHunterNotifySuccess"));
                _ = new LateTask(() => { SoulHunter_.SetKillCooldown(1f); }, 0.1f, log: false);
                Logger.Info($"Killed Target", "SoulHunter");
                SendRPC();
                return true;
            }
            else
            {
                CurrentTarget.ID = byte.MaxValue;
                CurrentTarget.START_TIMESTAMP = 0;
                LastUpdate = GetTimeStamp();
                SoulHunter_.Suicide();
                target.Notify(GetString("SoulHunterTargetNotifySurvived"));
                Logger.Info($"Killed Incorrect Player => Suicide", "SoulHunter");
                SendRPC();
                return false;
            }
        }
        public static void AfterMeetingTasks()
        {
            if (!IsEnable || CurrentTarget.ID == byte.MaxValue) return;

            long now = GetTimeStamp();

            CurrentTarget.FROZEN = true;
            NormalSpeed = Main.AllPlayerSpeed[SoulHunterId];
            Main.AllPlayerSpeed[SoulHunterId] = Main.MinSpeed;
            SoulHunter_.MarkDirtySettings();

            CurrentTarget.START_TIMESTAMP = now;

            PlayerControl target = GetPlayerById(CurrentTarget.ID);
            int waitingTime = WaitingTimeAfterMeeting.GetInt();

            SoulHunter_.Notify(string.Format(GetString("SoulHunterNotifyFreeze"), target.GetRealName(), waitingTime + 1));
            target.Notify(string.Format(GetString("SoulHunterTargetNotify"), SoulHunter_.GetRealName()), 300f);
            LastUpdate = now;

            SendRPC();
            Logger.Info($"Waiting to being hunting (in {waitingTime}s)", "SoulHunter");
        }
        public static void OnFixedUpdate()
        {
            if (!IsEnable || !GameStates.IsInTask || !SoulHunter_.IsAlive() || CurrentTarget.ID == byte.MaxValue) return;

            if (!CurrentTarget.FROZEN && Main.AllPlayerSpeed[SoulHunterId] != NormalSpeed)
            {
                Main.AllPlayerSpeed[SoulHunterId] = NormalSpeed;
                SoulHunter_.MarkDirtySettings();
            }

            if (CurrentTarget.START_TIMESTAMP == 0) return;

            long now = GetTimeStamp();
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
                Souls++;
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
        public static bool IsTargetBlocked => IsEnable && CurrentTarget.ID != byte.MaxValue && CurrentTarget.START_TIMESTAMP != 0;
        public static string HUDText
        {
            get
            {
                if (!IsTargetBlocked) return string.Empty;
                if (CurrentTarget.FROZEN) return string.Format(GetString("SoulHunterNotifyFreeze"), GetPlayerById(CurrentTarget.ID).GetRealName(), WaitingTimeAfterMeeting.GetInt() - (GetTimeStamp() - CurrentTarget.START_TIMESTAMP) + 1);
                else return string.Format(GetString("SoulHunterNotify"), TimeToKillTarget.GetInt() - (GetTimeStamp() - CurrentTarget.START_TIMESTAMP) + 1, GetPlayerById(CurrentTarget.ID).GetRealName());
            }
        }
        public static string ProgressText
        {
            get
            {
                if (!IsEnable) return string.Empty;
                int souldsNeeded = NumOfSoulsToWin.GetInt();
                return $"<#777777>-</color> <#{(Souls >= souldsNeeded ? "00ff00" : "ffffff")}>{Souls}/{souldsNeeded}</color>";
            }
        }
    }
}
