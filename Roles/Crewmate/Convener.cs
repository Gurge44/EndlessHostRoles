using Hazel;
using System;
using System.Collections.Generic;

namespace TOHE.Roles.Crewmate
{
    internal class Convener
    {
        private static int Id => 643350;
        public static OptionItem CD;
        public static OptionItem Limit;
        public static OptionItem ConvenerAbilityUseGainWithEachTaskCompleted;
        public static OptionItem AbilityChargesWhenFinishedTasks;
        public static readonly Dictionary<byte, float> UseLimit = [];
        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Convener);
            CD = Options.CreateCDSetting(Id + 2, TabGroup.CrewmateRoles, CustomRoles.Convener);
            Limit = IntegerOptionItem.Create(Id + 3, "AbilityUseLimit", new(0, 20, 1), 0, TabGroup.CrewmateRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Convener])
                .SetValueFormat(OptionFormat.Times);
            ConvenerAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(Id + 4, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.1f), 0.4f, TabGroup.CrewmateRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Convener])
                .SetValueFormat(OptionFormat.Times);
            AbilityChargesWhenFinishedTasks = FloatOptionItem.Create(Id + 5, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.1f), 0.2f, TabGroup.CrewmateRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Convener])
                .SetValueFormat(OptionFormat.Times);
        }
        public static void Init() => UseLimit.Clear();
        public static void Add(byte playerId) => UseLimit[playerId] = Limit.GetInt();
        public static void SendRPC(byte id)
        {
            var writer = Utils.CreateCustomRoleRPC(CustomRPC.SyncConvener);
            writer.Write(id);
            writer.Write(UseLimit[id]);
            Utils.EndRPC(writer);
        }
        public static void ReceiveRPC(MessageReader reader)
        {
            byte id = reader.ReadByte();
            float limit = reader.ReadSingle();
            UseLimit[id] = limit;
        }
        public static void UseAbility(PlayerControl pc, int ventId = 0, bool isPet = false)
        {
            if (pc == null || !UseLimit.TryGetValue(pc.PlayerId, out var limit) || limit < 1f) return;

            if (isPet)
            {
                Utils.TPAll(pc.Pos());
            }
            else
            {
                _ = new LateTask(() => { pc.MyPhysics.RpcBootFromVent(ventId); }, 0.5f, "Convener RpcBootFromVent");
                _ = new LateTask(() => { Utils.TPAll(pc.Pos()); }, 1f, "Convener TP");
            }

            UseLimit[pc.PlayerId]--;
            SendRPC(pc.PlayerId);
        }
        public static string GetProgressText(byte id) => UseLimit.TryGetValue(id, out var limit) ? $"<#777777>-</color> <#ff{(limit < 1f ? "0000" : "ffff")}>{Math.Round(limit, 1)}</color>" : string.Empty;
    }
}
