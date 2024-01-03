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

        public static bool countRepairSabotage;

        public static void SetupCustomOption()
        {
            SetupAdtRoleOptions(Id, CustomRoles.Damocles, canSetNum: false);
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
            PreviouslyEnteredVents = [];
            countRepairSabotage = true;
        }

        public static void Update(PlayerControl pc)
        {
            if (lastUpdate >= GetTimeStamp() || !GameStates.IsInTask || pc == null) return;
            if (!pc.IsAlive())
            {
                Main.PlayerStates[pc.PlayerId].RemoveSubRole(CustomRoles.Damocles);
                return;
            }
            lastUpdate = GetTimeStamp();

            Timer--;

            if (Timer < 0)
            {
                Timer = 0;
                pc.Suicide();
            }

            if (pc.IsNonHostModClient()) SendRPC();
            NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }

        public static void SendRPC()
        {
            if (!DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncDamoclesTimer, SendOption.Reliable, -1);
            writer.Write(Timer);
            writer.Write(lastUpdate.ToString());
            writer.Write(PreviouslyEnteredVents.Count);
            if (PreviouslyEnteredVents.Count > 0) foreach (var vent in PreviouslyEnteredVents.ToArray()) writer.Write(vent);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public static void ReceiveRPC(MessageReader reader)
        {
            Timer = reader.ReadInt32();
            lastUpdate = long.Parse(reader.ReadString());
            var elements = reader.ReadInt32();
            if (elements > 0) for (int i = 0; i < elements; i++) PreviouslyEnteredVents.Add(reader.ReadInt32());
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
            countRepairSabotage = true;
        }

        public static void OnMeetingStart()
        {
            Timer += 9;
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

        public static string GetProgressText() => string.Format(GetString("DamoclesTimeLeft"), Timer);
    }
}