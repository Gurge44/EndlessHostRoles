using System;

namespace TOHE.Roles.Crewmate
{
    internal class Convener
    {
        private static int Id => 643350;
        public static OptionItem CD;
        public static OptionItem Limit;
        public static OptionItem ConvenerAbilityUseGainWithEachTaskCompleted;
        public static OptionItem AbilityChargesWhenFinishedTasks;

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

        public static void Add(byte playerId) => playerId.SetAbilityUseLimit(Limit.GetInt());

        public static void UseAbility(PlayerControl pc, int ventId = 0, bool isPet = false)
        {
            if (pc == null || pc.GetAbilityUseLimit() < 1f) return;

            if (isPet)
            {
                Utils.TPAll(pc.Pos());
            }
            else
            {
                _ = new LateTask(() => { pc.MyPhysics.RpcBootFromVent(ventId); }, 0.5f, "Convener RpcBootFromVent");
                _ = new LateTask(() => { Utils.TPAll(pc.Pos()); }, 1f, "Convener TP");
            }

            pc.RpcRemoveAbilityUse();
        }

        public static string GetProgressText(byte id) => $"<#777777>-</color> <#ff{(id.GetAbilityUseLimit() < 1f ? "0000" : "ffff")}>{Math.Round(id.GetAbilityUseLimit(), 1)}</color>";
    }
}