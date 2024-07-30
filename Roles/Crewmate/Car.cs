using System.Collections.Generic;
using UnityEngine;

namespace EHR.Crewmate
{
    public class Car : RoleBase
    {
        public static bool On;

        private static OptionItem PropelDistance;

        private int Count;
        private HashSet<byte> CurrentlyPropelling;
        private Vector2 LastPosition;

        public static string Name => "<size=100%><font=\"VCR SDF\"><line-height=72%><br><alpha=#00>\u2588<#628d85>\u2588<#628d85>\u2588<#628d85>\u2588<#628d85>\u2588<#628d85>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br><#586874>\u2588<#586874>\u2588<#547a96>\u2588<#547a96>\u2588<#6894b6>\u2588<#6894b6>\u2588<#586874>\u2588<alpha=#00>\u2588<br><#586874>\u2588<#586874>\u2588<#586874>\u2588<#547a96>\u2588<#547a96>\u2588<#586874>\u2588<#586874>\u2588<#f5ee2e>\u2588<br><#000000>\u2588<#0d233f>\u2588<#586874>\u2588<#586874>\u2588<#586874>\u2588<#f5ee2e>\u2588<#586874>\u2588<#517a9a>\u2588<br><#000000>\u2588<#000000>\u2588<alpha=#00>\u2588<#000000>\u2588<#0d233f>\u2588<#586874>\u2588<#517a9a>\u2588<alpha=#00>\u2588<br><alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<#000000>\u2588<#000000>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<alpha=#00>\u2588<br></color></line-height></font></size>";

        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(5647, TabGroup.CrewmateRoles, CustomRoles.Car);
            PropelDistance = new FloatOptionItem(5649, "Car.PropelDistance", new(1f, 20f, 0.5f), 5f, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Car]);
        }

        public override void Init()
        {
            On = false;
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

            var pos = pc.Pos();
            if (Vector2.Distance(pos, LastPosition) < 0.1f) return;

            var direction = pos.x < LastPosition.x
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

            if (Main.AllAlivePlayerControls.Without(pc).Find(x => Vector2.Distance(pos, x.Pos()) < 1f, out var target) && CurrentlyPropelling.Add(target.PlayerId))
                Main.Instance.StartCoroutine(Propel(pc, target, direction));
        }

        System.Collections.IEnumerator Propel(PlayerControl car, PlayerControl target, Direction direction)
        {
            var oldSpeed = Main.AllPlayerSpeed[target.PlayerId];
            Main.AllPlayerSpeed[target.PlayerId] = Main.MinSpeed;
            target.MarkDirtySettings();

            var pos = car.Pos();
            var addVector = direction switch
            {
                Direction.Left => new(-0.25f, 0),
                Direction.UpLeft => new(-0.25f, 0.25f),
                Direction.Up => new(0, 0.25f),
                Direction.UpRight => new(0.25f, 0.25f),
                Direction.Right => new(0.25f, 0),
                Direction.DownRight => new(0.25f, -0.25f),
                Direction.Down => new(0, -0.25f),
                Direction.DownLeft => new(-0.25f, -0.25f),
                _ => Vector2.zero
            };

            var distance = PropelDistance.GetFloat();
            var collider = target.Collider;
            for (var newPos = target.Pos(); Vector2.Distance(pos, newPos) < distance && !PhysicsHelpers.AnythingBetween(collider, collider.bounds.center, newPos, Constants.ShipOnlyMask, false) && GameStates.IsInTask; newPos += addVector)
            {
                target.TP(newPos, log: false);
                yield return new WaitForSeconds(0.05f);
            }

            Main.AllPlayerSpeed[target.PlayerId] = oldSpeed;
            target.MarkDirtySettings();

            CurrentlyPropelling.Remove(target.PlayerId);
        }

        enum Direction
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
}