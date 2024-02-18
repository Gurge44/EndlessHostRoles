using System;
using System.Linq;
using UnityEngine;

namespace TOHE.Roles.Crewmate
{
    internal class Perceiver
    {
        private static int Id => 643360;
        private static OptionItem Radius;
        public static OptionItem CD;
        public static OptionItem Limit;
        public static OptionItem PerceiverAbilityUseGainWithEachTaskCompleted;
        public static OptionItem AbilityChargesWhenFinishedTasks;

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Perceiver);
            Radius = FloatOptionItem.Create(Id + 2, "PerceiverRadius", new(0.25f, 10f, 0.25f), 2.5f, TabGroup.CrewmateRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Perceiver])
                .SetValueFormat(OptionFormat.Multiplier);
            CD = Options.CreateCDSetting(Id + 3, TabGroup.CrewmateRoles, CustomRoles.Perceiver);
            Limit = IntegerOptionItem.Create(Id + 4, "AbilityUseLimit", new(0, 20, 1), 0, TabGroup.CrewmateRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Perceiver])
                .SetValueFormat(OptionFormat.Times);
            PerceiverAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(Id + 5, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.1f), 0.4f, TabGroup.CrewmateRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Perceiver])
                .SetValueFormat(OptionFormat.Times);
            AbilityChargesWhenFinishedTasks = FloatOptionItem.Create(Id + 6, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.1f), 0.2f, TabGroup.CrewmateRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Perceiver])
                .SetValueFormat(OptionFormat.Times);
        }

        public static void Add(byte id) => id.SetAbilityUseLimit(Limit.GetInt());

        public static void UseAbility(PlayerControl pc)
        {
            if (pc == null || pc.GetAbilityUseLimit() < 1f) return;

            var killers = Main.AllAlivePlayerControls.Where(x => !x.Is(Team.Crewmate) && x.HasKillButton() && Vector2.Distance(x.Pos(), pc.Pos()) <= Radius.GetFloat()).ToArray();
            pc.Notify(string.Format(Translator.GetString("PerceiverNotify"), killers.Length));

            pc.RpcRemoveAbilityUse();
        }

        public static string GetProgressText(byte id) => $"<#777777>-</color> <#ff{(id.GetAbilityUseLimit() < 1f ? "0000" : "ffff")}>{Math.Round(id.GetAbilityUseLimit(), 1)}</color>";
    }
}