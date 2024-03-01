using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TOHE.Roles.Crewmate
{
    internal class Beacon : RoleBase
    {
        private static int Id => 643480;
        private static OptionItem VisionIncrease;
        private static OptionItem Radius;
        private static List<byte> AffectedPlayers = [];
        private static Dictionary<byte, long> LastChange = [];

        public static bool On;
        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Beacon);
            VisionIncrease = FloatOptionItem.Create(Id + 2, "BeaconVisionIncrease", new(0.05f, 1.5f, 0.05f), 0.5f, TabGroup.CrewmateRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Beacon])
                .SetValueFormat(OptionFormat.Multiplier);
            Radius = FloatOptionItem.Create(Id + 3, "PerceiverRadius", new(0.1f, 5f, 0.1f), 1.5f, TabGroup.CrewmateRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Beacon])
                .SetValueFormat(OptionFormat.Multiplier);
        }

        public override void Init()
        {
            AffectedPlayers = [];
            LastChange = [];
            On = false;
        }

        public override void Add(byte playerId)
        {
            On = true;
        }

        public static bool IsAffectedPlayer(byte id) => Utils.IsActive(SystemTypes.Electrical) && AffectedPlayers.Contains(id);
        public static float IncreasedVision => VisionIncrease.GetFloat();

        public override void OnCheckPlayerPosition(PlayerControl pc)
        {
            if (!GameStates.IsInTask || pc == null) return;

            long now = Utils.TimeStamp;
            if (LastChange.TryGetValue(pc.PlayerId, out var ts) && ts == now) return;

            bool isBeaconNearby = Main.AllAlivePlayerControls.Any(x => x.Is(CustomRoles.Beacon) && Vector2.Distance(x.Pos(), pc.Pos()) <= Radius.GetFloat());
            bool isAffectedPlayer = AffectedPlayers.Contains(pc.PlayerId);

            switch (isAffectedPlayer)
            {
                case true when !isBeaconNearby:
                {
                    AffectedPlayers.Remove(pc.PlayerId);
                    if (Utils.IsActive(SystemTypes.Electrical)) pc.MarkDirtySettings();
                    LastChange[pc.PlayerId] = now;
                    break;
                }
                case false when isBeaconNearby:
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