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
        if (LastChange.TryGetValue(pc.PlayerId, out long ts) && ts == now) return;

        Vector2 pos = pc.Pos();
        float radius = Radius.GetFloat();

        switch (affectedPlayer: AffectedPlayers.Contains(pc.PlayerId), beaconNearby: Utils.IsActive(SystemTypes.Electrical) && Beacons.Any(x => FastVector2.DistanceWithinRange(x.Pos(), pos, radius)))
        {
            case (affectedPlayer: true, beaconNearby: false):
            {
                AffectedPlayers.Remove(pc.PlayerId);
                pc.MarkDirtySettings();

                LastChange[pc.PlayerId] = now;
                break;
            }
            case (affectedPlayer: false, beaconNearby: true):
            {
                AffectedPlayers.Add(pc.PlayerId);
                pc.MarkDirtySettings();

                LastChange[pc.PlayerId] = now;

                if (pc.AmOwner)
                    Achievements.Type.ALightInTheShadows.CompleteAfterGameEnd();

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