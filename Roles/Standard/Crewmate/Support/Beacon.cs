using System.Collections.Generic;
using System.Linq;
using EHR.Modules;

namespace EHR.Roles;

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

    public override void Remove(byte playerId)
    {
        Beacons.RemoveAll(x => x.PlayerId == playerId);
    }

    public static bool IsAffectedPlayer(byte id)
    {
        return Utils.IsActive(SystemTypes.Electrical) && AffectedPlayers.Contains(id);
    }

    public override void OnCheckPlayerPosition(PlayerControl pc)
    {
        long now = Utils.TimeStamp;
        byte playerId = pc.PlayerId;
        if (LastChange.TryGetValue(playerId, out long ts) && ts == now) return;

        Vector2 pos = pc.Pos();
        float radius = Radius.GetFloat();
        bool affectedPlayer = AffectedPlayers.Contains(playerId);
        
        bool beaconNearby = false;
        if (Utils.IsActive(SystemTypes.Electrical))
        {
            for (int pcIndex = 0; pcIndex < Beacons.Count; pcIndex++)
                if (FastVector2.DistanceWithinRange(Beacons[pcIndex].Pos(), pos, radius))
                {
                    beaconNearby = true;
                    break;
                }
        }
        // It would be more logical to use "switch (affectedPlayer, beaconNearby)"
        // But it creates a ValueTuple<bool,bool> inside itself in FixedUpdate
        if (affectedPlayer != beaconNearby)
        {
            if (beaconNearby)
            {
                AffectedPlayers.Add(playerId);
                pc.MarkDirtySettings();
                LastChange[playerId] = now;

                if (pc.AmOwner)
                    Achievements.Type.ALightInTheShadows.CompleteAfterGameEnd();
            }
            else
            {
                AffectedPlayers.Remove(playerId);
                pc.MarkDirtySettings();
                LastChange[playerId] = now;
            }
        }
    }

    public override void OnReportDeadBody()
    {
        AffectedPlayers.Clear();
        LastChange.Clear();
    }
}