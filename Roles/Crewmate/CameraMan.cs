using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using UnityEngine;

namespace EHR.Crewmate
{
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

        private bool IsTeleported;
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
                MapNames.Mira => new(15.3f, 3.8f),
                MapNames.Polus => new(3.0f, -12.0f),
                MapNames.Dleks => new(-13.5f, -5.5f),
                MapNames.Airship => new(5.8f, -10.8f),
                MapNames.Fungle => new(9.5f, 1.2f),
                _ => throw new NotImplementedException()
            };
        }

        public override void Add(byte playerId)
        {
            PlayerIdList.Add(playerId);
            playerId.SetAbilityUseLimit(UseLimitOpt.GetInt());
            IsTeleported = false;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            AURoleOptions.EngineerCooldown = VentCooldown.GetFloat();
            AURoleOptions.EngineerInVentMaxTime = 1f;
        }

        public override void OnEnterVent(PlayerControl pc, Vent vent)
        {
            if (pc == null) return;

            if (pc.GetAbilityUseLimit() >= 1)
            {
                pc.RpcRemoveAbilityUse();

                LateTask.New(() =>
                {
                    BasePos = pc.Pos();
                    if (pc.TP(CameraPosition)) IsTeleported = true;
                }, UsePets.GetBool() ? 0.1f : 2f, "CameraMan Teleport");
            }
            else
            {
                if (!NameNotifyManager.Notifies.ContainsKey(pc.PlayerId)) pc.Notify(Translator.GetString("OutOfAbilityUsesDoMoreTasks"));
            }
        }

        public override void OnFixedUpdate(PlayerControl pc)
        {
            if (!IsTeleported || !pc.IsAlive() || !GameStates.IsInTask || Vector2.Distance(pc.Pos(), CameraPosition) <= DisableDevice.UsableDistance) return;

            pc.TP(BasePos);
            IsTeleported = false;
        }

        public override void OnReportDeadBody()
        {
            IsTeleported = false;
        }
    }
}