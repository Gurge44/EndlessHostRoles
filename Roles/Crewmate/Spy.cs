namespace TOHE.Roles.Crewmate
{
    using Hazel;
    using System;
    using System.Collections.Generic;
    using System.Text;
    using UnityEngine;
    using static TOHE.Options;
    using static TOHE.Utils;

    public static class Spy
    {
        private static readonly int Id = 640400;
        private static List<byte> playerIdList = [];
        public static bool change;
        public static Dictionary<byte, float> UseLimit = [];
        public static Dictionary<byte, long> SpyRedNameList = [];

        public static OptionItem SpyRedNameDur;
        public static OptionItem UseLimitOpt;
        public static OptionItem SpyAbilityUseGainWithEachTaskCompleted;
        public static OptionItem AbilityChargesWhenFinishedTasks;

        public static void SetupCustomOption()
        {
            SetupSingleRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Spy, 1);
            UseLimitOpt = IntegerOptionItem.Create(Id + 10, "AbilityUseLimit", new(0, 20, 1), 1, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Spy])
                .SetValueFormat(OptionFormat.Times);
            SpyRedNameDur = FloatOptionItem.Create(Id + 11, "SpyRedNameDur", new(0f, 70f, 1f), 3f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Spy])
                .SetValueFormat(OptionFormat.Seconds);
            SpyAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(Id + 12, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.1f), 0.5f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Spy])
                .SetValueFormat(OptionFormat.Times);
            AbilityChargesWhenFinishedTasks = FloatOptionItem.Create(Id + 13, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.1f), 0.2f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Spy])
                .SetValueFormat(OptionFormat.Times);
        }
        public static void Init()
        {
            playerIdList = [];
            UseLimit = [];
            SpyRedNameList = [];
            change = false;
        }
        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            UseLimit.Add(playerId, UseLimitOpt.GetInt());
        }
        public static bool IsEnable => playerIdList.Count > 0;
        public static void SendRPC(int operate, byte id = byte.MaxValue, bool changeColor = false)
        {
            if (!IsEnable || !DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncSpy, SendOption.Reliable, -1);
            writer.Write(operate);
            switch (operate)
            {
                case 1: // Red Name Add
                    writer.Write(id);
                    writer.Write(SpyRedNameList[id].ToString());
                    break;
                case 2: // Ability Use
                    writer.Write(id);
                    writer.Write(UseLimit[id]);
                    break;
                case 3: // Red Name Remove
                    writer.Write(id);
                    writer.Write(changeColor);
                    break;
            }
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void ReceiveRPC(MessageReader reader)
        {
            int operate = reader.ReadInt32();
            switch (operate)
            {
                case 1:
                    byte susId = reader.ReadByte();
                    string stimeStamp = reader.ReadString();
                    if (long.TryParse(stimeStamp, out long timeStamp)) SpyRedNameList[susId] = timeStamp;
                    return;
                case 2:
                    byte spyId = reader.ReadByte();
                    UseLimit[spyId] = reader.ReadSingle();
                    return;
                case 3:
                    SpyRedNameList.Remove(reader.ReadByte());
                    change = reader.ReadBoolean();
                    return;
            }
        }
        public static void OnKillAttempt(PlayerControl killer, PlayerControl target)
        {
            if (killer == null || target == null || !target.Is(CustomRoles.Spy) || killer.PlayerId == target.PlayerId || UseLimit[target.PlayerId] < 1) return;

            UseLimit[target.PlayerId] -= 1;
            SendRPC(2, id: target.PlayerId);
            SpyRedNameList.TryAdd(killer.PlayerId, TimeStamp);
            SendRPC(1, id: killer.PlayerId);
            NotifyRoles(SpecifySeer: target, SpecifyTarget: killer);
        }
        public static void OnFixedUpdate(PlayerControl pc)
        {
            if (pc == null || !pc.Is(CustomRoles.Spy) || SpyRedNameList.Count == 0) return;

            bool change = false;

            foreach (var x in SpyRedNameList)
            {
                if (x.Value + SpyRedNameDur.GetInt() < TimeStamp || !GameStates.IsInTask)
                {
                    SpyRedNameList.Remove(x.Key);
                    change = true;
                    SendRPC(3, id: x.Key, changeColor: change);
                }
            }

            if (change && GameStates.IsInTask) { NotifyRoles(SpecifySeer: pc); }
        }
        public static string GetProgressText(byte playerId, bool comms)
        {
            if (GetPlayerById(playerId) == null) return string.Empty;

            var sb = new StringBuilder();

            Color TextColor1;
            if (UseLimit[playerId] < 1) TextColor1 = Color.red;
            else TextColor1 = Color.white;

            sb.Append(GetTaskCount(playerId, comms));
            sb.Append(ColorString(TextColor1, $" <color=#777777>-</color> {Math.Round(UseLimit[playerId], 1)}"));

            return sb.ToString();
        }
    }
}