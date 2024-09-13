using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using UnityEngine;

namespace EHR.Neutral
{
    public class Technician : RoleBase
    {
        public static bool On;

        private static OptionItem WinsAlone;
        private static OptionItem RequiredPoints;
        public static OptionItem CanVent;
        private static OptionItem VentCooldown;
        private static OptionItem MaxInVentTime;
        private static readonly Dictionary<SystemTypes, OptionItem> PointGains = [];
        private bool FixedSabotage;

        private bool Ignore;
        public bool IsWon;
        private PlayerControl TechnicianPC;

        public override bool IsEnable => On;

        public override void SetupCustomOption()
        {
            StartSetup(646450)
                .AutoSetupOption(ref WinsAlone, false)
                .AutoSetupOption(ref RequiredPoints, 5, new IntegerValueRule(1, 50, 1))
                .AutoSetupOption(ref CanVent, false)
                .AutoSetupOption(ref VentCooldown, 30f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds, overrideParent: CanVent)
                .AutoSetupOption(ref MaxInVentTime, 5f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds, overrideParent: CanVent);

            int i = 0;
            foreach (var system in new[] { SystemTypes.Electrical, SystemTypes.Comms, SystemTypes.LifeSupp, SystemTypes.Reactor })
            {
                PointGains[system] = new IntegerOptionItem(646960 + i, $"Technician.PointGain.{system}", new(0, 10, 1), 1, TabGroup.NeutralRoles)
                    .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Technician]);
                i++;
            }
        }

        public override void Init()
        {
            On = false;
        }

        public override void Add(byte playerId)
        {
            On = true;
            IsWon = false;
            Ignore = false;
            TechnicianPC = playerId.GetPlayer();
            FixedSabotage = false;
            playerId.SetAbilityUseLimit(0);
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            if (CanVent.GetBool())
            {
                AURoleOptions.EngineerCooldown = VentCooldown.GetFloat();
                AURoleOptions.EngineerInVentMaxTime = MaxInVentTime.GetFloat();
            }
        }

        public override void OnReportDeadBody() => Ignore = true;
        public override void AfterMeetingTasks() => Ignore = false;

        private static SystemTypes GetActualSystemType(SystemTypes systemTypes) => systemTypes switch
        {
            SystemTypes.Laboratory => SystemTypes.Reactor,
            SystemTypes.HeliSabotage => SystemTypes.Reactor,
            _ => systemTypes
        };

        private static bool GetsAnyPoint(SystemTypes systemType) => PointGains.TryGetValue(GetActualSystemType(systemType), out var pointGain) && pointGain.GetInt() > 0;

        void IncreasePoints(SystemTypes systemType)
        {
            var actualSystemType = GetActualSystemType(systemType);
            TechnicianPC.RpcIncreaseAbilityUseLimitBy(PointGains[actualSystemType].GetInt());
            if (TechnicianPC.GetAbilityUseLimit() >= RequiredPoints.GetInt())
            {
                if (WinsAlone.GetBool())
                {
                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Technician);
                    CustomWinnerHolder.WinnerIds.Add(TechnicianPC.PlayerId);
                }
                else IsWon = true;
            }
        }

        public static void RepairSystem(byte playerId, SystemTypes systemType, byte amount)
        {
            if (Main.PlayerStates[playerId].IsDead) return;
            if (Main.PlayerStates[playerId].Role is not Technician technician) return;
            if (!GetsAnyPoint(systemType) || technician.IsWon || technician.Ignore) return;

            switch (systemType)
            {
                case SystemTypes.Reactor:
                case SystemTypes.Laboratory:
                {
                    if (amount.HasAnyBit(ReactorSystemType.AddUserOp))
                    {
                        ShipStatus.Instance.UpdateSystem((MapNames)Main.NormalOptions.MapId == MapNames.Polus ? SystemTypes.Laboratory : SystemTypes.Reactor, playerId.GetPlayer(), ReactorSystemType.ClearCountdown);
                        technician.IncreasePoints(systemType);
                    }

                    break;
                }
                case SystemTypes.HeliSabotage:
                {
                    var tags = (HeliSabotageSystem.Tags)(amount & HeliSabotageSystem.TagMask);
                    if (tags == HeliSabotageSystem.Tags.ActiveBit)
                    {
                        technician.FixedSabotage = false;
                    }

                    if (!technician.FixedSabotage && tags == HeliSabotageSystem.Tags.FixBit)
                    {
                        technician.FixedSabotage = true;
                        var consoleId = amount & HeliSabotageSystem.IdMask;
                        var otherConsoleId = (consoleId + 1) % 2;
                        ShipStatus.Instance.UpdateSystem(SystemTypes.HeliSabotage, playerId.GetPlayer(), (byte)(otherConsoleId | (int)HeliSabotageSystem.Tags.FixBit));
                        technician.IncreasePoints(systemType);
                    }

                    break;
                }
                case SystemTypes.LifeSupp:
                {
                    if (amount.HasAnyBit(LifeSuppSystemType.AddUserOp))
                    {
                        ShipStatus.Instance.UpdateSystem(SystemTypes.LifeSupp, playerId.GetPlayer(), LifeSuppSystemType.ClearCountdown);
                        technician.IncreasePoints(systemType);
                    }

                    break;
                }
                case SystemTypes.Comms:
                {
                    if (Main.CurrentMap is MapNames.Mira or MapNames.Fungle)
                    {
                        var tags = (HqHudSystemType.Tags)(amount & HqHudSystemType.TagMask);
                        if (tags == HqHudSystemType.Tags.ActiveBit)
                        {
                            technician.FixedSabotage = false;
                        }

                        if (!technician.FixedSabotage && tags == HqHudSystemType.Tags.FixBit)
                        {
                            technician.FixedSabotage = true;
                            var consoleId = amount & HqHudSystemType.IdMask;
                            var otherConsoleId = (consoleId + 1) % 2;
                            ShipStatus.Instance.UpdateSystem(SystemTypes.Comms, playerId.GetPlayer(), (byte)(otherConsoleId | (int)HqHudSystemType.Tags.FixBit));
                            technician.IncreasePoints(systemType);
                        }
                    }
                    else if (amount == 0) technician.IncreasePoints(systemType);

                    break;
                }
            }
        }

        public static void SwitchSystemRepair(byte playerId, SwitchSystem switchSystem, byte amount)
        {
            if (Main.PlayerStates[playerId].IsDead) return;
            if (!GetsAnyPoint(SystemTypes.Electrical) || Main.PlayerStates[playerId].Role is not Technician technician) return;

            if (amount.HasBit(SwitchSystem.DamageSystem)) return;

            var fixbit = 1 << amount;
            switchSystem.ActualSwitches = (byte)(switchSystem.ExpectedSwitches ^ fixbit);

            technician.IncreasePoints(SystemTypes.Electrical);
        }

        public override string GetProgressText(byte playerId, bool comms)
        {
            var points = (int)Math.Round(playerId.GetAbilityUseLimit());
            var needed = RequiredPoints.GetInt();
            var color = points >= needed ? Color.green : Color.white;
            return Utils.ColorString(color, $"{points}/{needed}");
        }
    }
}