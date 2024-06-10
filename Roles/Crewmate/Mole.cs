﻿using AmongUs.GameOptions;
using static EHR.Options;

namespace EHR.Roles.Crewmate
{
    internal class Mole : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        private static int Id => 64400;

        public override void Add(byte playerId)
        {
            On = true;
        }

        public override void Init()
        {
            On = false;
        }

        public static void SetupCustomOption() => SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Mole);

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            AURoleOptions.EngineerInVentMaxTime = 1f;
            AURoleOptions.EngineerCooldown = 5f;
        }

        public override void OnExitVent(PlayerControl pc, Vent vent)
        {
            LateTask.New(() => { pc.TPtoRndVent(); }, 0.5f, "Mole TP");
        }

        public override void OnPet(PlayerControl pc)
        {
            pc.TPtoRndVent();
        }
    }
}