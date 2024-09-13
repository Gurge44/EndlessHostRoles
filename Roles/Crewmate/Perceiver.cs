﻿using System.Linq;
using AmongUs.GameOptions;
using UnityEngine;

namespace EHR.Crewmate
{
    internal class Perceiver : RoleBase
    {
        private static OptionItem Radius;
        public static OptionItem CD;
        public static OptionItem Limit;
        public static OptionItem PerceiverAbilityUseGainWithEachTaskCompleted;
        public static OptionItem AbilityChargesWhenFinishedTasks;

        public static bool On;
        private static int Id => 643360;

        public override bool IsEnable => On;

        public override void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Perceiver);
            Radius = new FloatOptionItem(Id + 2, "PerceiverRadius", new(0.25f, 10f, 0.25f), 2.5f, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Perceiver])
                .SetValueFormat(OptionFormat.Multiplier);
            CD = Options.CreateCDSetting(Id + 3, TabGroup.CrewmateRoles, CustomRoles.Perceiver);
            Limit = new IntegerOptionItem(Id + 4, "AbilityUseLimit", new(0, 20, 1), 0, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Perceiver])
                .SetValueFormat(OptionFormat.Times);
            PerceiverAbilityUseGainWithEachTaskCompleted = new FloatOptionItem(Id + 5, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 0.4f, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Perceiver])
                .SetValueFormat(OptionFormat.Times);
            AbilityChargesWhenFinishedTasks = new FloatOptionItem(Id + 6, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Perceiver])
                .SetValueFormat(OptionFormat.Times);
        }

        public override void Init()
        {
            On = false;
        }

        public override void Add(byte id)
        {
            On = true;
            id.SetAbilityUseLimit(Limit.GetInt());
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            if (Options.UsePets.GetBool()) return;
            AURoleOptions.EngineerCooldown = CD.GetFloat();
            AURoleOptions.EngineerInVentMaxTime = 1f;
        }

        public override void OnPet(PlayerControl pc)
        {
            UseAbility(pc);
        }

        public override void OnEnterVent(PlayerControl pc, Vent vent)
        {
            UseAbility(pc);
        }

        public static void UseAbility(PlayerControl pc)
        {
            if (pc == null || pc.GetAbilityUseLimit() < 1f) return;

            var killers = Main.AllAlivePlayerControls.Where(x => !x.Is(Team.Crewmate) && x.HasKillButton() && Vector2.Distance(x.Pos(), pc.Pos()) <= Radius.GetFloat()).ToArray();
            pc.Notify(string.Format(Translator.GetString("PerceiverNotify"), killers.Length), 7f);

            pc.RpcRemoveAbilityUse();
        }
    }
}