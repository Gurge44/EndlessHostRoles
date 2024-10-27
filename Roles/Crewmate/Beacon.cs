﻿using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EHR.Crewmate
{
    internal class Beacon : RoleBase
    {
        private static List<PlayerControl> Beacons = [];
        
        private static OptionItem VisionIncrease;
        private static OptionItem Radius;
        private static List<byte> AffectedPlayers = [];
        private static Dictionary<byte, long> LastChange = [];

        public static bool On;
        private static int Id => 643480;
        public override bool IsEnable => On;
        public static float IncreasedVision => VisionIncrease.GetFloat() * 5f;

        public override void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Beacon);
            VisionIncrease = new FloatOptionItem(Id + 2, "BeaconVisionIncrease", new(0.05f, 1.25f, 0.05f), 0.5f, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Beacon])
                .SetValueFormat(OptionFormat.Multiplier);
            Radius = new FloatOptionItem(Id + 3, "PerceiverRadius", new(0.1f, 5f, 0.1f), 1.5f, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Beacon])
                .SetValueFormat(OptionFormat.Multiplier);
        }

        public override void Init()
        {
            Beacons = [];
            AffectedPlayers = [];
            LastChange = [];
            On = false;
        }

        public override void Add(byte playerId)
        {
            On = true;
            Beacons.Add(playerId.GetPlayer());
        }

        public static bool IsAffectedPlayer(byte id) => Utils.IsActive(SystemTypes.Electrical) && AffectedPlayers.Contains(id);

        public override void OnCheckPlayerPosition(PlayerControl pc)
        {
            if (!GameStates.IsInTask || pc == null) return;

            long now = Utils.TimeStamp;
            if (LastChange.TryGetValue(pc.PlayerId, out var ts) && ts == now) return;

            var pos = pc.Pos();
            var radius = Radius.GetFloat();
            bool beaconNearby = Beacons.Any(x => Vector2.Distance(x.Pos(), pos) <= radius);
            bool affectedPlayer = AffectedPlayers.Contains(pc.PlayerId);

            switch (affectedPlayer)
            {
                case true when !beaconNearby:
                {
                    AffectedPlayers.Remove(pc.PlayerId);
                    if (Utils.IsActive(SystemTypes.Electrical)) pc.MarkDirtySettings();
                    LastChange[pc.PlayerId] = now;
                    break;
                }
                case false when beaconNearby:
                {
                    AffectedPlayers.Add(pc.PlayerId);
                    if (Utils.IsActive(SystemTypes.Electrical)) pc.MarkDirtySettings();
                    LastChange[pc.PlayerId] = now;
                    break;
                }
            }
        }

        public override void OnReportDeadBody()
        {
            AffectedPlayers.Clear();
            LastChange.Clear();
        }
    }
}