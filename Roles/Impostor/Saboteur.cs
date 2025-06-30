using System;
using System.Collections.Generic;
using static EHR.Options;

namespace EHR.Impostor;

internal class Saboteur : RoleBase
{
    public static bool On;
    private byte SaboteurId;
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(10005, TabGroup.ImpostorRoles, CustomRoles.Saboteur);

        SaboteurCD = new FloatOptionItem(10015, "KillCooldown", new(0f, 180f, 0.5f), 15f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Saboteur])
            .SetValueFormat(OptionFormat.Seconds);

        SaboteurCDAfterMeetings = new FloatOptionItem(10016, "AfterMeetingKillCooldown", new(0f, 180f, 0.5f), 25f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Saboteur])
            .SetValueFormat(OptionFormat.Seconds);
    }

    public override void Add(byte playerId)
    {
        On = true;
        SaboteurId = playerId;
    }

    public override void Init()
    {
        On = false;
        SaboteurId = byte.MaxValue;
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return base.CanUseKillButton(pc) && Utils.IsAnySabotageActive();
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = SaboteurCDAfterMeetings.GetFloat();
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (Math.Abs(Main.AllPlayerKillCooldown[killer.PlayerId] - SaboteurCD.GetFloat()) > 0.5f)
        {
            Main.AllPlayerKillCooldown[killer.PlayerId] = SaboteurCD.GetFloat();
            killer.SyncSettings();
        }

        return base.OnCheckMurder(killer, target);
    }

    public override void OnMurder(PlayerControl killer, PlayerControl target)
    {
        MapNames map = Main.CurrentMap;

        List<SystemTypes> availableSystems =
        [
            SystemTypes.Comms,
            map == MapNames.Fungle ? SystemTypes.MushroomMixupSabotage : SystemTypes.Electrical,
            map switch
            {
                MapNames.Polus => SystemTypes.Laboratory,
                MapNames.Airship => SystemTypes.HeliSabotage,
                _ => SystemTypes.Reactor
            }
        ];

        if (map is MapNames.Skeld or MapNames.Dleks or MapNames.MiraHQ)
            availableSystems.Add(SystemTypes.LifeSupp);

        availableSystems.RemoveAll(Utils.IsActive);

        if (availableSystems.Count == 0) return;

        SystemTypes sabo = availableSystems.RandomElement();

        switch (sabo)
        {
            case SystemTypes.Reactor:
            case SystemTypes.LifeSupp:
            case SystemTypes.Comms:
            case SystemTypes.Laboratory:
            case SystemTypes.HeliSabotage:
                ShipStatus.Instance.UpdateSystem(sabo, killer, 128);
                break;
            case SystemTypes.MushroomMixupSabotage:
                ShipStatus.Instance.UpdateSystem(sabo, killer, 1);
                break;
            case SystemTypes.Electrical:
                byte num = 4;

                for (var index = 0; index < 5; ++index)
                {
                    if (BoolRange.Next())
                        num |= (byte)(1 << index);
                }

                ShipStatus.Instance.RpcUpdateSystem(sabo, (byte)(num | 128U));
                break;
        }
    }

    public override void OnReportDeadBody()
    {
        Main.AllPlayerKillCooldown[SaboteurId] = SaboteurCDAfterMeetings.GetFloat();
    }
}