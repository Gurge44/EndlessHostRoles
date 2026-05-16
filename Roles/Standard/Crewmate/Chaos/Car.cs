using System.Collections;
using System.Collections.Generic;
using EHR.Modules;
using UnityEngine;

namespace EHR.Roles;

public class Car : RoleBase
{
    public static bool On;
    private static bool IsSkeld;

    private static OptionItem PropelDistance;

    private int Count;
    private HashSet<byte> CurrentlyPropelling;
    private Vector2 LastPosition;

    public static string Name => "<voffset=7em><alpha=#00>.</alpha></voffset><size=150%><line-height=97%><cspace=0.16em><#0000>W</color><mark=#628d85>WWWWW</mark><#0000>WW</color>\n<mark=#586874>WW</mark><mark=#547a96>WW</mark><mark=#6894b6>WW</mark><mark=#586874>W</mark><#0000>W</color>\n<mark=#586874>WWW</mark><mark=#547a96>WW</mark><mark=#586874>WW</mark><mark=#f5ee2e>W</mark>\n<mark=#000000>W</mark><mark=#0d233f>W</mark><mark=#586874>WWW</mark><mark=#f5ee2e>W</mark><mark=#586874>W</mark><mark=#517a9a>W</mark>\n<mark=#000000>WW</mark><#0000>W</color><mark=#000000>W</mark><mark=#0d233f>W</mark><mark=#586874>W</mark><mark=#517a9a>W</mark><#0000>W\nWWW</color><mark=#000000>WW</mark><#0000>WWW";

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(5647, TabGroup.CrewmateRoles, CustomRoles.Car);

        PropelDistance = new FloatOptionItem(5649, "Car.PropelDistance", new(1f, 20f, 0.5f), 5f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Car]);
    }

    public override void Init()
    {
        On = false;
        IsSkeld = Main.CurrentMap is MapNames.Skeld or MapNames.Dleks;
    }

    public override void Add(byte playerId)
    {
        On = true;
        LastPosition = Utils.GetPlayerById(playerId).Pos();
        Count = 0;
        CurrentlyPropelling = [];
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!pc.IsAlive() || !GameStates.IsInTask || ExileController.Instance) return;

        if (Count++ < 5) return;

        Count = 0;

        Vector2 pos = pc.Pos();
        if (FastVector2.DistanceWithinRange(pos, LastPosition, 0.1f)) return;

        Direction direction = pos.x < LastPosition.x
            ? pos.y < LastPosition.y
                ? Direction.DownLeft
                : pos.y > LastPosition.y
                    ? Direction.UpLeft
                    : Direction.Left
            : pos.x > LastPosition.x
                ? pos.y < LastPosition.y
                    ? Direction.DownRight
                    : pos.y > LastPosition.y
                        ? Direction.UpRight
                        : Direction.Right
                : pos.y < LastPosition.y
                    ? Direction.Down
                    : pos.y > LastPosition.y
                        ? Direction.Up
                        : Direction.Left;

        LastPosition = pos;

        if (FastVector2.TryGetClosestPlayerInRange(pos, 1.2f, out PlayerControl target, x => x.PlayerId != pc.PlayerId) && CurrentlyPropelling.Add(target.PlayerId))
            Main.Instance.StartCoroutine(Propel(pc, target, direction));
    }

    private IEnumerator Propel(PlayerControl car, PlayerControl target, Direction direction)
    {
        if (car.AmOwner) Achievements.Type.DrivingTestFailed.Complete();
        
        float oldSpeed = Main.AllPlayerSpeed[target.PlayerId];
        Main.AllPlayerSpeed[target.PlayerId] = Main.MinSpeed;
        target.MarkDirtySettings();

        Vector2 pos = car.Pos();

        const float unit = 0.15f;

        Vector2 addVector = direction switch
        {
            Direction.Left => new(-unit, 0),
            Direction.UpLeft => new(-unit, unit),
            Direction.Up => new(0, unit),
            Direction.UpRight => new(unit, unit),
            Direction.Right => new(unit, 0),
            Direction.DownRight => new(unit, -unit),
            Direction.Down => new(0, -unit),
            Direction.DownLeft => new(-unit, -unit),
            _ => Vector2.zero
        };

        float distance = PropelDistance.GetFloat();
        Collider2D collider = target.Collider;

        for (Vector2 newPos = target.Pos(); FastVector2.DistanceWithinRange(pos, newPos, distance) && GameStates.IsInTask; newPos += addVector)
        {
            if (IsSkeld && (target.IsInRoom(SystemTypes.LowerEngine) || target.IsInRoom(SystemTypes.UpperEngine))) break;
            if (PhysicsHelpers.AnythingBetween(collider, collider.bounds.center, newPos + (addVector * 2), Constants.ShipOnlyMask, false)) break;

            target.TP(newPos, log: false);
            yield return new WaitForSecondsRealtime(0.05f);
        }

        Main.AllPlayerSpeed[target.PlayerId] = oldSpeed;
        target.MarkDirtySettings();

        CurrentlyPropelling.Remove(target.PlayerId);
    }

    private enum Direction
    {
        Left,
        UpLeft,
        Up,
        UpRight,
        Right,
        DownRight,
        Down,
        DownLeft
    }
}