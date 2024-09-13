﻿using System.Collections.Generic;
using UnityEngine;
using static EHR.Options;

namespace EHR.Crewmate
{
    internal class Bodyguard : RoleBase
    {
        public static bool On;
        private static List<Bodyguard> Instances = [];
        private PlayerControl BodyguardPC;
        public override bool IsEnable => On;

        public override void Init()
        {
            On = false;
            Instances = [];
        }

        public override void Add(byte playerId)
        {
            On = true;
            Instances.Add(this);
            BodyguardPC = Utils.GetPlayerById(playerId);
        }

        public override void SetupCustomOption()
        {
            SetupRoleOptions(8400, TabGroup.CrewmateRoles, CustomRoles.Bodyguard);
            BodyguardProtectRadius = new FloatOptionItem(8410, "BodyguardProtectRadius", new(0.5f, 5f, 0.5f), 1.5f, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bodyguard])
                .SetValueFormat(OptionFormat.Multiplier);
            BodyguardKillsKiller = new BooleanOptionItem(8411, "BodyguardKillsKiller", false, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bodyguard]);
        }

        public static bool OnAnyoneCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (killer.IsCrewmate()) return true;

            if (killer.PlayerId != target.PlayerId)
            {
                foreach (var bodyguard in Instances)
                {
                    if (bodyguard.BodyguardPC.PlayerId == target.PlayerId) continue;

                    float dis = Vector2.Distance(bodyguard.BodyguardPC.Pos(), target.Pos());
                    if (dis > BodyguardProtectRadius.GetFloat()) return true;

                    if (bodyguard.BodyguardPC.IsMadmate() && killer.Is(Team.Impostor))
                    {
                        Logger.Info($"{bodyguard.BodyguardPC.GetRealName()} is a madmate, so they chose to ignore the murder scene", "Bodyguard");
                        continue;
                    }

                    if (BodyguardKillsKiller.GetBool() && bodyguard.BodyguardPC.RpcCheckAndMurder(killer, check: true)) bodyguard.BodyguardPC.Kill(killer);
                    else killer.SetKillCooldown();
                    bodyguard.BodyguardPC.Suicide(PlayerState.DeathReason.Sacrifice, killer);
                    Logger.Info($"{bodyguard.BodyguardPC.GetRealName()} stood up and died for {killer.GetRealName()}", "Bodyguard");
                    return false;
                }
            }

            return true;
        }
    }
}