using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using UnityEngine;
using static EHR.Options;

namespace EHR.Crewmate
{
    public class Tracefinder : RoleBase
    {
        private const int Id = 6100;
        private static List<byte> PlayerIdList = [];
        private static OptionItem VitalsDuration;
        private static OptionItem VitalsCooldown;
        private static OptionItem ArrowDelayMin;
        private static OptionItem ArrowDelayMax;

        public static bool On;
        private byte TracefinderId;

        public override bool IsEnable => PlayerIdList.Count > 0;

        public override void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Tracefinder);

            VitalsCooldown = new FloatOptionItem(Id + 10, "VitalsCooldown", new(1f, 60f, 1f), 25f, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Tracefinder])
                .SetValueFormat(OptionFormat.Seconds);

            VitalsDuration = new FloatOptionItem(Id + 11, "VitalsDuration", new(1f, 30f, 1f), 1f, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Tracefinder])
                .SetValueFormat(OptionFormat.Seconds);

            ArrowDelayMin = new FloatOptionItem(Id + 12, "ArrowDelayMin", new(1f, 30f, 1f), 2f, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Tracefinder])
                .SetValueFormat(OptionFormat.Seconds);

            ArrowDelayMax = new FloatOptionItem(Id + 13, "ArrowDelayMax", new(1f, 30f, 1f), 7f, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Tracefinder])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public override void Init()
        {
            PlayerIdList = [];
            On = false;
        }

        public override void Add(byte playerId)
        {
            PlayerIdList.Add(playerId);
            TracefinderId = playerId;
            On = true;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte id)
        {
            AURoleOptions.ScientistCooldown = VitalsCooldown.GetFloat();
            AURoleOptions.ScientistBatteryCharge = VitalsDuration.GetFloat();
        }

        public override void OnReportDeadBody()
        {
            foreach (byte apc in PlayerIdList) LocateArrow.RemoveAllTarget(apc);
        }

        public static void OnPlayerDead(PlayerControl target)
        {
            if (!On || !GameStates.IsInTask || target == null || target.Data.Disconnected) return;

            float delay;

            if (ArrowDelayMax.GetFloat() < ArrowDelayMin.GetFloat())
                delay = 0f;
            else
                delay = IRandom.Instance.Next((int)ArrowDelayMin.GetFloat(), (int)ArrowDelayMax.GetFloat() + 1);

            delay = Math.Max(delay, 0.15f);

            LateTask.New(() =>
            {
                if (GameStates.IsInTask)
                {
                    foreach (byte id in PlayerIdList)
                    {
                        PlayerControl pc = Utils.GetPlayerById(id);
                        if (pc == null || !pc.IsAlive()) continue;

                        LocateArrow.Add(id, target.transform.position);
                        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
                    }
                }
            }, delay, "Tracefinder arrow delay");
        }

        public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
        {
            if (seer.PlayerId != TracefinderId) return string.Empty;

            if (target != null && seer.PlayerId != target.PlayerId) return string.Empty;

            return Utils.ColorString(Color.white, LocateArrow.GetArrows(seer));
        }
    }
}