using Hazel;
using System;
using System.Collections.Generic;
using static TOHE.Options;
using static TOHE.Translator;
using static TOHE.Utils;

namespace TOHE.Roles.AddOns.Impostor
{
    public static class Damocles
    {
        private static readonly int Id = 14670;

        private static OptionItem DamoclesExtraTimeAfterKill;
        private static OptionItem DamoclesExtraTimeAfterMeeting;
        private static OptionItem DamoclesStartingTime;

        private static int TimeAfterKill;
        private static int TimeAfterMeeting;
        private static int StartingTime;

        public static int Timer;

        public static long lastUpdate;
        public static List<int> PreviouslyEnteredVents;

        public static void SetupCustomOption()
        {
            SetupSingleRoleOptions(Id, TabGroup.Addons, CustomRoles.Damocles, 1);
            DamoclesExtraTimeAfterKill = IntegerOptionItem.Create(Id + 10, "DamoclesExtraTimeAfterKill", new(0, 60, 1), 30, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Damocles])
                .SetValueFormat(OptionFormat.Seconds);
            DamoclesExtraTimeAfterMeeting = IntegerOptionItem.Create(Id + 11, "DamoclesExtraTimeAfterMeeting", new(0, 60, 1), 30, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Damocles])
                .SetValueFormat(OptionFormat.Seconds);
            DamoclesStartingTime = IntegerOptionItem.Create(Id + 12, "DamoclesStartingTime", new(0, 60, 1), 30, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Damocles])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public static void Initialize()
        {
            TimeAfterKill = DamoclesExtraTimeAfterKill.GetInt();
            TimeAfterMeeting = DamoclesExtraTimeAfterMeeting.GetInt();
            StartingTime = DamoclesStartingTime.GetInt();

            Timer = StartingTime;
            lastUpdate = GetTimeStamp() + 10;
            PreviouslyEnteredVents = new();
        }

        public static void Update(PlayerControl pc)
        {
            if (lastUpdate >= GetTimeStamp() || !GameStates.IsInTask) return;
            lastUpdate = GetTimeStamp();

            Timer--;

            if (pc.IsModClient() && pc.PlayerId != 0) SendRPC();
            NotifyRoles(SpecifySeer: pc);
        }

        public static void SendRPC()
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncDamoclesTimer, SendOption.Reliable, -1);
            writer.Write(Timer);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public static void ReceiveRPC(MessageReader reader)
        {
            Timer = reader.ReadInt32();
        }

        public static void OnMurder()
        {
            Timer += TimeAfterKill;
        }

        public static void OnOtherImpostorMurder()
        {
            Timer += 10;
        }

        public static void OnEnterVent(int ventId)
        {
            if (PreviouslyEnteredVents.Contains(ventId)) return;

            PreviouslyEnteredVents.Add(ventId);
            Timer += 10;
        }

        public static void AfterMeetingTasks()
        {
            PreviouslyEnteredVents.Clear();

            Timer += TimeAfterMeeting;
        }

        public static void OnCrewmateEjected()
        {
            Timer = (int)Math.Round(Timer * 1.3);
        }

        public static void OnRepairSabotage()
        {
            Timer -= 15;
        }

        public static void OnImpostorDeath()
        {
            Timer -= 20;
        }

        public static void OnReport()
        {
            Timer = (int)Math.Round(Timer * 0.9);
        }

        public static void OnImpostorEjected()
        {
            Timer = (int)Math.Round(Timer * 0.8);
        }

        public static string GetProgressText()
        {
            return string.Format(GetString("DamoclesTimeLeft"), Timer);
        }
    }
}
