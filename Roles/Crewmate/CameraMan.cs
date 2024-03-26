using AmongUs.GameOptions;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace EHR.Roles.Crewmate
{
    using static Options;

    public class CameraMan : RoleBase
    {
        private const int Id = 641600;
        private static List<byte> playerIdList = [];

        public static OptionItem VentCooldown;
        public static OptionItem UseLimitOpt;
        public static OptionItem CameraManAbilityUseGainWithEachTaskCompleted;
        public static OptionItem AbilityChargesWhenFinishedTasks;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.CameraMan);
            VentCooldown = FloatOptionItem.Create(Id + 10, "VentCooldown", new(0f, 70f, 1f), 15f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.CameraMan])
                .SetValueFormat(OptionFormat.Seconds);
            UseLimitOpt = IntegerOptionItem.Create(Id + 11, "AbilityUseLimit", new(0, 20, 1), 1, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.CameraMan])
                .SetValueFormat(OptionFormat.Times);
            CameraManAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(Id + 12, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.1f), 1f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.CameraMan])
                .SetValueFormat(OptionFormat.Times);
            AbilityChargesWhenFinishedTasks = FloatOptionItem.Create(Id + 13, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.1f), 0.2f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.CameraMan])
                .SetValueFormat(OptionFormat.Times);
        }

        public override void Init()
        {
            playerIdList = [];
        }

        public override void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            playerId.SetAbilityUseLimit(UseLimitOpt.GetInt());
        }

        public override bool IsEnable => playerIdList.Count > 0;

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            AURoleOptions.EngineerCooldown = VentCooldown.GetFloat();
            AURoleOptions.EngineerInVentMaxTime = 1f;
        }

        public override void OnEnterVent(PlayerControl pc, Vent vent)
        {
            if (pc == null) return;
            if (!pc.Is(CustomRoles.CameraMan)) return;

            if (pc.GetAbilityUseLimit() >= 1)
            {
                pc.RpcRemoveAbilityUse();

                Vector2 pos = Main.CurrentMap switch
                {
                    MapNames.Skeld => new(-13.5f, -5.5f),
                    MapNames.Mira => new(15.3f, 3.8f),
                    MapNames.Polus => new(3.0f, -12.0f),
                    MapNames.Dleks => new(-13.5f, -5.5f),
                    MapNames.Airship => new(5.8f, -10.8f),
                    MapNames.Fungle => new(9.5f, 1.2f),
                    _ => throw new NotImplementedException(),
                };

                _ = new LateTask(() => { pc.TP(pos); }, UsePets.GetBool() ? 0.1f : 2f, "CameraMan Teleport");
            }
            else
            {
                if (!NameNotifyManager.Notice.ContainsKey(pc.PlayerId)) pc.Notify(Translator.GetString("OutOfAbilityUsesDoMoreTasks"));
            }
        }
    }
}