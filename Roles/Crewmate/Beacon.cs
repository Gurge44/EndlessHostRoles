using System.Collections.Generic;
using System.Linq;
using EHR.Modules;

namespace EHR.Crewmate;

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
        if (!GameStates.IsInTask || pc == null) return;

        long now = Utils.TimeStamp;
        if (LastChange.TryGetValue(pc.PlayerId, out long ts) && ts == now) return;

        Vector2 pos = pc.Pos();
        float radius = Radius.GetFloat();
        bool beaconNearby = Beacons.Any(x => Vector2.Distance(x.Pos(), pos) <= radius);
        bool affectedPlayer = AffectedPlayers.Contains(pc.PlayerId);
        bool lightsOff = Utils.IsActive(SystemTypes.Electrical);

        switch (affectedPlayer)
        {
            case true when !beaconNearby:
            {
                AffectedPlayers.Remove(pc.PlayerId);
                if (lightsOff) pc.MarkDirtySettings();

                LastChange[pc.PlayerId] = now;
                break;
            }
            case false when beaconNearby:
            {
                AffectedPlayers.Add(pc.PlayerId);
                if (lightsOff) pc.MarkDirtySettings();

                LastChange[pc.PlayerId] = now;

                if (pc.AmOwner && lightsOff)
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