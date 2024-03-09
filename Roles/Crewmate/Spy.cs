using Hazel;
using System.Collections.Generic;
using TOHE.Modules;

namespace TOHE.Roles.Crewmate
{
    using static Options;
    using static Utils;

    public class Spy : RoleBase
    {
        private const int Id = 640400;
        private static List<byte> playerIdList = [];
        public static bool change;
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

        public override void Init()
        {
            playerIdList = [];
            SpyRedNameList = [];
            change = false;
        }

        public override void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            playerId.SetAbilityUseLimit(UseLimitOpt.GetInt());
            change = false;
        }

        public override bool IsEnable => playerIdList.Count > 0;

        public static void SendRPC(int operate, byte id = byte.MaxValue, bool changeColor = false)
        {
            if (!DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncSpy, SendOption.Reliable);
            writer.Write(operate);
            switch (operate)
            {
                case 1: // Red Name Add
                    writer.Write(id);
                    writer.Write(SpyRedNameList[id].ToString());
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
                case 3:
                    SpyRedNameList.Remove(reader.ReadByte());
                    change = reader.ReadBoolean();
                    return;
            }
        }

        public static bool OnKillAttempt(PlayerControl killer, PlayerControl target) // Special handling for Spy ---- remains as a static method
        {
            if (killer == null || target == null || !target.Is(CustomRoles.Spy) || killer.PlayerId == target.PlayerId || target.GetAbilityUseLimit() < 1) return true;

            target.RpcRemoveAbilityUse();
            SpyRedNameList.TryAdd(killer.PlayerId, TimeStamp);
            SendRPC(1, id: killer.PlayerId);
            NotifyRoles(SpecifySeer: target, SpecifyTarget: killer);

            return false;
        }

        public override void OnFixedUpdate(PlayerControl pc)
        {
            if (pc == null || !pc.Is(CustomRoles.Spy) || SpyRedNameList.Count == 0) return;

            foreach (var x in SpyRedNameList)
            {
                if (x.Value + SpyRedNameDur.GetInt() < TimeStamp || !GameStates.IsInTask)
                {
                    SpyRedNameList.Remove(x.Key);
                    change = true;
                    SendRPC(3, id: x.Key, changeColor: true);
                }
            }

            if (change && GameStates.IsInTask)
            {
                NotifyRoles(SpecifySeer: pc);
            }
        }
    }
}