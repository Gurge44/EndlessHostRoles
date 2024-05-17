﻿using System.Collections.Generic;
using AmongUs.GameOptions;
using static EHR.Options;

namespace EHR.Roles.Crewmate
{
    internal class Ventguard : RoleBase
    {
        public static bool On;

        public static List<int> BlockedVents = [];
        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            SetupSingleRoleOptions(5525, TabGroup.CrewmateRoles, CustomRoles.Ventguard);
            VentguardAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(5527, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 1f, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Ventguard])
                .SetValueFormat(OptionFormat.Times);
            VentguardAbilityChargesWhenFinishedTasks = FloatOptionItem.Create(5530, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Ventguard])
                .SetValueFormat(OptionFormat.Times);
            VentguardMaxGuards = IntegerOptionItem.Create(5528, "VentguardMaxGuards", new(1, 30, 1), 3, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Ventguard]);
            VentguardBlockDoesNotAffectCrew = BooleanOptionItem.Create(5529, "VentguardBlockDoesNotAffectCrew", true, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Ventguard]);
        }

        public override void Add(byte playerId)
        {
            On = true;
            playerId.SetAbilityUseLimit(VentguardMaxGuards.GetInt());
        }

        public override void Init()
        {
            On = false;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            AURoleOptions.EngineerInVentMaxTime = 1f;
            AURoleOptions.EngineerCooldown = 15f;
        }

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            hud.AbilityButton.buttonLabelText.text = Translator.GetString("VentguardVentButtonText");
        }

        public override void OnEnterVent(PlayerControl pc, Vent vent)
        {
            if (pc.GetAbilityUseLimit() >= 1)
            {
                pc.RpcRemoveAbilityUse();
                if (!BlockedVents.Contains(vent.Id)) BlockedVents.Add(vent.Id);
                pc.Notify(Translator.GetString("VentBlockSuccess"));
            }
            else
            {
                pc.Notify(Translator.GetString("OutOfAbilityUsesDoMoreTasks"));
            }
        }
    }
}