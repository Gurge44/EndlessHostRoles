using UnityEngine;

namespace EHR.Crewmate
{
    public class Car : RoleBase
    {
        public static bool On;

        private static OptionItem PropelDistance;
        private int Count;
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
        }

        public override void OnFixedUpdate(PlayerControl pc)
        {
            if (!pc.IsAlive() || !GameStates.IsInTask || ExileController.Instance) return;

            if (Count++ < 5) return;
            Count = 0;

            var pos = pc.Pos();
            var direction = pos.x > LastPosition.x ? Direction.Right : pos.x < LastPosition.x ? Direction.Left : pos.y > LastPosition.y ? Direction.Up : Direction.Down;
            LastPosition = pos;

            if (Main.AllAlivePlayerControls.Without(pc).Find(x => Vector2.Distance(pos, x.Pos()) < 1f, out var target))
                Main.Instance.StartCoroutine(Propel(pc, target, direction));
        }

        static System.Collections.IEnumerator Propel(PlayerControl car, PlayerControl target, Direction direction)
        {
            var oldSpeed = Main.AllPlayerSpeed[target.PlayerId];
            Main.AllPlayerSpeed[target.PlayerId] = Main.MinSpeed;
            target.MarkDirtySettings();

            var pos = car.Pos();
            for (Vector2 newPos = target.Pos();
                 Vector2.Distance(pos, newPos) < PropelDistance.GetFloat() &&
                 !PhysicsHelpers.AnythingBetween(target.Collider, target.Collider.bounds.center, newPos, Constants.ShipOnlyMask, false) &&
                 GameStates.IsInTask;
                 newPos += direction switch
                 {
                     Direction.Left => Vector2.left,
                     Direction.Right => Vector2.right,
                     Direction.Up => Vector2.up,
                     Direction.Down => Vector2.down,
                     _ => Vector2.zero
                 })
            {
                target.TP(newPos, log: false);
                yield return new WaitForSeconds(0.2f);
            }

            Main.AllPlayerSpeed[target.PlayerId] = oldSpeed;
            target.MarkDirtySettings();
        }

        enum Direction
        {
            Left,
            Right,
            Up,
            Down
        }
    }
}