﻿using EHR.Modules;
using UnityEngine;
using static EHR.Options;

namespace EHR.Crewmate;

internal class Demolitionist : RoleBase
{
    public override bool IsEnable => false;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(5550, TabGroup.CrewmateRoles, CustomRoles.Demolitionist);

        DemolitionistVentTime = new FloatOptionItem(5552, "DemolitionistVentTime", new(0f, 90f, 1f), 5f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Demolitionist])
            .SetValueFormat(OptionFormat.Seconds);

        DemolitionistKillerDiesOnMeetingCall = new BooleanOptionItem(5553, "DemolitionistKillerDiesOnMeetingCall", false, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Demolitionist]);
    }

    public override void Init() { }

    public override void Add(byte playerId) { }

    public static void OnDeath(PlayerControl killer, PlayerControl target)
    {
        if (killer == null || target == null || killer.PlayerId == target.PlayerId || !killer.IsAlive() || !GameStates.IsInTask) return;
        
        string warningMark = Utils.ColorString(Color.yellow, "\u26a0");
        killer.Notify($"{warningMark} {Utils.ColorString(Utils.GetRoleColor(CustomRoles.Demolitionist), Translator.GetString("OnDemolitionistDead"))} {warningMark}");
        killer.KillFlash();

        LateTask.New(() =>
        {
            if (!killer.inVent && killer.PlayerId != target.PlayerId)
            {
                if ((DemolitionistKillerDiesOnMeetingCall.GetBool() || GameStates.IsInTask) && killer.IsAlive())
                {
                    killer.Suicide(PlayerState.DeathReason.Demolished, target);
                    RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
                }
            }
            else
            {
                RPC.PlaySoundRPC(killer.PlayerId, Sounds.TaskComplete);
                killer.SetKillCooldown(Main.AllPlayerKillCooldown[killer.PlayerId] - (DemolitionistVentTime.GetFloat() + 0.5f));
            }
        }, DemolitionistVentTime.GetFloat() + 0.5f, "DemolitionistCheck");
    }
}