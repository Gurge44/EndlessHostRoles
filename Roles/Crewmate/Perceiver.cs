using Hazel;
using System.Collections.Generic;
using System.Linq;

namespace TOHE.Roles.Crewmate
{
    internal class Perceiver
    {
        private static int Id => 643360;
        private static OptionItem Radius;
        public static OptionItem CD;
        public static OptionItem Limit;
        public static OptionItem PerceiverAbilityUseGainWithEachTaskCompleted;
        public static readonly Dictionary<byte, float> UseLimit = [];
        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Perceiver);
            Radius = FloatOptionItem.Create(Id + 2, "PerceiverRadius", new(0.05f, 5f, 0.05f), 0.5f, TabGroup.CrewmateRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Perceiver])
                .SetValueFormat(OptionFormat.Multiplier);
            CD = Options.CreateCDSetting(Id + 3, TabGroup.CrewmateRoles, CustomRoles.Perceiver);
            Limit = IntegerOptionItem.Create(Id + 4, "AbilityUseLimit", new(0, 20, 1), 0, TabGroup.CrewmateRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Perceiver])
                .SetValueFormat(OptionFormat.Times);
            PerceiverAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(Id + 5, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.1f), 0.4f, TabGroup.CrewmateRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Perceiver])
                .SetValueFormat(OptionFormat.Times);
        }
        public static void Init() => UseLimit.Clear();
        public static void Add(byte id) => UseLimit[id] = Limit.GetInt();
        public static void SendRPC(byte id)
        {
            var writer = Utils.CreateCustomRoleRPC(CustomRPC.SyncPerceiver);
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
        public static void UseAbility(PlayerControl pc)
        {
            if (pc == null || !UseLimit.TryGetValue(pc.PlayerId, out var limit) || limit < 1f) return;

            var killers = Main.AllAlivePlayerControls.Where(x => !x.Is(Team.Crewmate) && x.HasKillButton() && UnityEngine.Vector2.Distance(x.Pos(), pc.Pos()) <= Radius.GetFloat()).ToArray();
            pc.Notify(string.Format(Translator.GetString("PerceiverNotify"), killers.Length));

            UseLimit[pc.PlayerId]--;
            SendRPC(pc.PlayerId);
        }
        public static string GetProgressText(byte id) => UseLimit.TryGetValue(id, out var limit) ? $"<#777777>-</color> <#ff{(limit < 1 ? "0000" : "ffff")}>{System.Math.Round(limit, 1)}</color>" : string.Empty;
    }
}
