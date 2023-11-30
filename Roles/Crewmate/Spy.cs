namespace TOHE.Roles.Crewmate
{
    using Hazel;
    using System;
    using System.Collections.Generic;
    using System.Linq;
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

        public static void SetupCustomOption()
        {
            SetupSingleRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Spy, 1);
            UseLimitOpt = IntegerOptionItem.Create(Id + 10, "AbilityUseLimit", new(0, 20, 1), 1, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Spy])
            .SetValueFormat(OptionFormat.Times);
            SpyRedNameDur = FloatOptionItem.Create(Id + 11, "SpyRedNameDur", new(0f, 70f, 1f), 3f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Spy])
                .SetValueFormat(OptionFormat.Seconds);
            SpyAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(Id + 12, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.1f), 0.5f, TabGroup.CrewmateRoles, false)
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
        public static void SendRPC(byte susId)
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SpyRedNameSync, SendOption.Reliable, -1);
            writer.Write(susId);
            writer.Write(SpyRedNameList[susId].ToString());
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void SendAbilityRPC(byte spyId)
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SpyAbilitySync, SendOption.Reliable, -1);
            writer.Write(spyId);
            writer.Write(UseLimit[spyId]);
            AmongUsClient.Instance.FinishRpcImmediately(writer);

        }
        public static void SendRPC(byte susId, bool changeColor)
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SpyRedNameRemove, SendOption.Reliable, -1);
            //writer.Write(spyId);
            writer.Write(susId);
            writer.Write(changeColor);
            TOHE.Logger.Info($"RPC to remove player {susId} from red name list and change `change` to {changeColor}", "Spy");
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void ReceiveRPC(MessageReader reader, bool isRemove = false, bool isAbility = false)
        {
            if (isAbility)
            {
                byte spyId = reader.ReadByte();
                UseLimit[spyId] = reader.ReadSingle();
                return;
            }
            else if (isRemove)
            {
                SpyRedNameList.Remove(reader.ReadByte());
                change = reader.ReadBoolean();
                return;
            }
            byte susId = reader.ReadByte();
            string stimeStamp = reader.ReadString();
            if (long.TryParse(stimeStamp, out long timeStamp)) SpyRedNameList[susId] = timeStamp;
        }
        public static void OnKillAttempt(PlayerControl killer, PlayerControl target)
        {
            if (killer == null) return;
            if (target == null) return;
            if (!target.Is(CustomRoles.Spy)) return;
            if (killer.PlayerId == target.PlayerId) return;

            if (UseLimit[target.PlayerId] >= 1)
            {
                UseLimit[target.PlayerId] -= 1;
                SendAbilityRPC(target.PlayerId);
                SpyRedNameList.TryAdd(killer.PlayerId, GetTimeStamp());
                SendRPC(killer.PlayerId);
                NotifyRoles(SpecifySeer: target, SpecifyTarget: killer);
            }
        }
        public static void OnFixedUpdate(PlayerControl pc)
        {
            if (pc == null) return;
            if (!pc.Is(CustomRoles.Spy)) return;
            if (!SpyRedNameList.Any()) return;

            bool change = false;

            foreach (var x in SpyRedNameList)
            {
                if (x.Value + SpyRedNameDur.GetInt() < GetTimeStamp() || !GameStates.IsInTask)
                {
                    SpyRedNameList.Remove(x.Key);
                    change = true;
                    SendRPC(x.Key, change);
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