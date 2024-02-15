using Hazel;
using System;
using System.Collections.Generic;
using System.Linq;
using static TOHE.Options;
using static TOHE.Translator;

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

        public static Dictionary<byte, int> Timer;

        public static Dictionary<byte, long> lastUpdate;
        public static Dictionary<byte, List<int>> PreviouslyEnteredVents;

        public static bool countRepairSabotage;

        public static void SetupCustomOption()
        {
            SetupAdtRoleOptions(Id, CustomRoles.Damocles, canSetNum: true);
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

            Timer = [];
            lastUpdate = [];
            PreviouslyEnteredVents = [];
            countRepairSabotage = true;
        }

        public static void Update(PlayerControl pc)
        {
            byte id = pc.PlayerId;
            long now = Utils.GetTimeStamp();
            if ((lastUpdate.TryGetValue(id, out var ts) && ts >= now) || !GameStates.IsInTask || pc == null) return;
            if (!pc.IsAlive())
            {
                Main.PlayerStates[id].RemoveSubRole(CustomRoles.Damocles);
                return;
            }
            lastUpdate[id] = now;
            if (!Timer.ContainsKey(id)) Timer[id] = StartingTime + 8;

            Timer[id]--;

            if (Timer[id] < 0)
            {
                Timer[id] = 0;
                pc.Suicide();
            }

            if (pc.IsNonHostModClient()) SendRPC(id);
            if (!pc.IsModClient()) Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }

        public static void SendRPC(byte playerId)
        {
            if (!Utils.DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncDamoclesTimer, SendOption.Reliable, -1);
            writer.Write(playerId);
            writer.Write(Timer[playerId]);
            writer.Write(lastUpdate[playerId].ToString());
            writer.Write(PreviouslyEnteredVents[playerId].Count);
            if (PreviouslyEnteredVents[playerId].Count > 0) foreach (var vent in PreviouslyEnteredVents[playerId].ToArray()) writer.Write(vent);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public static void ReceiveRPC(MessageReader reader)
        {
            byte playerId = reader.ReadByte();
            Timer[playerId] = reader.ReadInt32();
            lastUpdate[playerId] = long.Parse(reader.ReadString());
            var elements = reader.ReadInt32();
            if (elements > 0) for (int i = 0; i < elements; i++) PreviouslyEnteredVents[playerId].Add(reader.ReadInt32());
        }

        public static void OnMurder(byte id)
        {
            Timer[id] += TimeAfterKill;
        }

        public static void OnOtherImpostorMurder()
        {
            AdjustTime(10);
        }

        public static void OnEnterVent(byte id, int ventId)
        {
            if (!PreviouslyEnteredVents.ContainsKey(id)) PreviouslyEnteredVents[id] = [];
            if (PreviouslyEnteredVents[id].Contains(ventId)) return;

            PreviouslyEnteredVents[id].Add(ventId);
            Timer[id] += 10;
        }

        public static void AfterMeetingTasks()
        {
            PreviouslyEnteredVents.Clear();

            AdjustTime(TimeAfterMeeting);
            countRepairSabotage = true;
        }

        public static void OnMeetingStart()
        {
            AdjustTime(9);
        }

        public static void OnCrewmateEjected()
        {
            AdjustTimeByPercent(1.3);
        }

        public static void OnRepairSabotage(byte id)
        {
            Timer[id] -= 15;
        }

        public static void OnImpostorDeath()
        {
            AdjustTime(-20);
        }

        public static void OnReport(byte id)
        {
            Timer[id] = (int)Math.Round(Timer[id] * 0.9);
        }

        public static void OnImpostorEjected()
        {
            AdjustTimeByPercent(0.8);
        }

        private static void AdjustTime(int change)
        {
            foreach (var item in Timer.Keys.ToArray())
            {
                Timer[item] += change;
            }
        }

        private static void AdjustTimeByPercent(double percent)
        {
            foreach (var item in Timer.Keys.ToArray())
            {
                Timer[item] = (int)Math.Round(Timer[item] * percent);
            }
        }

        public static string GetProgressText(byte id) => string.Format(GetString("DamoclesTimeLeft"), Timer.TryGetValue(id, out var time) ? time : StartingTime);
    }
}