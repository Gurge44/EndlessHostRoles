﻿using System.Collections.Generic;
using AmongUs.GameOptions;
using Hazel;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Crewmate;

public class Deputy : RoleBase
{
    private const int Id = 6500;
    private static List<byte> PlayerIdList = [];

    public static OptionItem HandcuffCooldown;
    public static OptionItem HandcuffMax;
    public static OptionItem DeputyHandcuffCDForTarget;
    private static OptionItem DeputyHandcuffDelay;
    public static OptionItem UsePet;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Deputy);

        HandcuffCooldown = new FloatOptionItem(Id + 10, "DeputyHandcuffCooldown", new(0f, 60f, 0.5f), 17.5f, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Deputy])
            .SetValueFormat(OptionFormat.Seconds);

        DeputyHandcuffCDForTarget = new FloatOptionItem(Id + 14, "DeputyHandcuffCDForTarget", new(0f, 180f, 0.5f), 15f, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Deputy])
            .SetValueFormat(OptionFormat.Seconds);

        HandcuffMax = new IntegerOptionItem(Id + 12, "DeputyHandcuffMax", new(1, 20, 1), 4, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Deputy])
            .SetValueFormat(OptionFormat.Times);

        DeputyHandcuffDelay = new IntegerOptionItem(Id + 11, "DeputyHandcuffDelay", new(0, 20, 1), 5, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Deputy])
            .SetValueFormat(OptionFormat.Seconds);

        UsePet = CreatePetUseSetting(Id + 13, CustomRoles.Deputy);
    }

    public override void Init()
    {
        PlayerIdList = [];
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        playerId.SetAbilityUseLimit(HandcuffMax.GetFloat());
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = HandcuffCooldown.GetFloat();
    }

    public override bool CanUseKillButton(PlayerControl player)
    {
        return player.IsAlive() && player.GetAbilityUseLimit() >= 1;
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return false;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        opt.SetVision(false);
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer.GetAbilityUseLimit() < 1) return false;

        if (target != null && !target.Is(CustomRoles.Deputy))
        {
            killer.RpcRemoveAbilityUse();

            killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Deputy), GetString("DeputyHandcuffedPlayer")));

            LateTask.New(() =>
            {
                if (GameStates.IsInTask)
                {
                    var sender = CustomRpcSender.Create("Deputy.OnCheckMurder", SendOption.Reliable);
                    var hasValue = false;
                    hasValue |= sender.SetKillCooldown(target, DeputyHandcuffCDForTarget.GetFloat());
                    hasValue |= sender.Notify(target, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Deputy), GetString("HandcuffedByDeputy")));

                    if (target.IsModdedClient()) hasValue |= sender.RpcResetAbilityCooldown(target);
                    else hasValue |= sender.RpcGuardAndKill(target, target);

                    sender.SendMessage(!hasValue);
                }
            }, DeputyHandcuffDelay.GetInt(), "DeputyHandcuffDelay");

            killer.SetKillCooldown();

            return false;
        }

        killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Deputy), GetString("DeputyInvalidTarget")));
        return false;
    }
}