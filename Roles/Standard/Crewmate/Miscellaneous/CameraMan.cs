using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using UnityEngine;

namespace EHR.Roles;

using static Options;

public class CameraMan : RoleBase
{
    private const int Id = 641600;
    private static List<byte> PlayerIdList = [];

    public static OptionItem VentCooldown;
    public static OptionItem UseLimitOpt;
    public static OptionItem TPBackWhenMoveAway;
    public static OptionItem CameraManAbilityUseGainWithEachTaskCompleted;
    public static OptionItem AbilityChargesWhenFinishedTasks;

    private static Vector2 CameraPosition;
    private Vector2 BasePos;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.CameraMan);

        VentCooldown = new FloatOptionItem(Id + 10, "VentCooldown", new(0f, 70f, 1f), 15f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.CameraMan])
            .SetValueFormat(OptionFormat.Seconds);

        UseLimitOpt = new IntegerOptionItem(Id + 11, "AbilityUseLimit", new(0, 20, 1), 1, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.CameraMan])
            .SetValueFormat(OptionFormat.Times);

        TPBackWhenMoveAway = new BooleanOptionItem(Id + 14, "TPBackWhenMoveAway", true, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.CameraMan]);

        CameraManAbilityUseGainWithEachTaskCompleted = new FloatOptionItem(Id + 12, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 1f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.CameraMan])
            .SetValueFormat(OptionFormat.Times);

        AbilityChargesWhenFinishedTasks = new FloatOptionItem(Id + 13, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.CameraMan])
            .SetValueFormat(OptionFormat.Times);
    }

    public override void Init()
    {
        PlayerIdList = [];

        CameraPosition = Main.CurrentMap switch
        {
            MapNames.Skeld => new(-13.5f, -5.5f),
            MapNames.MiraHQ => new(15.3f, 3.8f),
            MapNames.Polus => new(3.0f, -12.0f),
            MapNames.Dleks => new(13.5f, -5.5f),
            MapNames.Airship => new(5.8f, -10.8f),
            MapNames.Fungle => new(9.5f, 1.2f),
            (MapNames)6 => new(-4.23f, -33.38f),
            _ => throw new NotImplementedException()
        };
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        playerId.SetAbilityUseLimit(UseLimitOpt.GetFloat());
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        AURoleOptions.EngineerCooldown = VentCooldown.GetFloat();
        AURoleOptions.EngineerInVentMaxTime = 1f;
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        if (UsePets.GetBool()) return;
        if (pc == null) return;

        if (pc.GetAbilityUseLimit() >= 1)
        {
            pc.RpcRemoveAbilityUse();
            LateTask.New(() => UseAbility(pc), 2f, "CameraMan Teleport");
        }
        else
            pc.Notify(Translator.GetString("OutOfAbilityUsesDoMoreTasks"));
    }

    public override void OnPet(PlayerControl pc)
    {
        if (pc == null) return;

        if (pc.GetAbilityUseLimit() >= 1)
        {
            pc.RpcRemoveAbilityUse();
            UseAbility(pc);
        }
        else
            pc.Notify(Translator.GetString("OutOfAbilityUsesDoMoreTasks"));
    }

    private void UseAbility(PlayerControl pc)
    {
        BasePos = pc.Pos();
        pc.RPCPlayCustomSound("teleport");
            
        if (pc.TP(CameraPosition) && TPBackWhenMoveAway.GetBool())
        {
            Main.Instance.StartCoroutine(Coroutine());

            System.Collections.IEnumerator Coroutine()
            {
                yield return new WaitForSecondsRealtime(2f);
                        
                while (GameStates.IsInTask && pc.IsAlive() && FastVector2.DistanceWithinRange(pc.Pos(), CameraPosition, DisableDevice.UsableDistance))
                    yield return new WaitForSecondsRealtime(0.5f);
                        
                if (!pc.IsAlive() || !GameStates.IsInTask) yield break;
                
                pc.TP(BasePos);
            }
        }
    }

    public override bool CanUseVent(PlayerControl pc, int ventId)
    {
        return !IsThisRole(pc) || pc.Is(CustomRoles.Nimble) || pc.GetClosestVent()?.Id == ventId;
    }
}
