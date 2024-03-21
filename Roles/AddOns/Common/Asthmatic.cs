using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static EHR.Options;

namespace EHR.Roles.AddOns.Common
{
    internal class Asthmatic : IAddon
    {
        public AddonTypes Type => AddonTypes.Harmful;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(15420, CustomRoles.Asthmatic, canSetNum: true);
            AsthmaticMinRedTime = IntegerOptionItem.Create(15423, "AsthmaticMinRedTime", new(1, 90, 1), 5, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Asthmatic])
                .SetValueFormat(OptionFormat.Seconds);
            AsthmaticMaxRedTime = IntegerOptionItem.Create(15424, "AsthmaticMaxRedTime", new(1, 90, 1), 30, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Asthmatic])
                .SetValueFormat(OptionFormat.Seconds);
            AsthmaticMinGreenTime = IntegerOptionItem.Create(15425, "AsthmaticMinGreenTime", new(1, 90, 1), 5, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Asthmatic])
                .SetValueFormat(OptionFormat.Seconds);
            AsthmaticMaxGreenTime = IntegerOptionItem.Create(15426, "AsthmaticMaxGreenTime", new(1, 90, 1), 30, TabGroup.Addons, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Asthmatic])
                .SetValueFormat(OptionFormat.Seconds);
        }

        private static readonly Dictionary<byte, Counter> Timers = [];
        private static readonly Dictionary<byte, string> LastSuffix = [];
        private static readonly Dictionary<byte, Vector2> LastPosition = [];
        private static int MinRedTime;
        private static int MaxRedTime;
        private static int MinGreenTime;
        private static int MaxGreenTime;
        public static int RandomRedTime() => IRandom.Instance.Next(MinRedTime, MaxRedTime);
        public static int RandomGreenTime() => IRandom.Instance.Next(MinGreenTime, MaxGreenTime);
        public static void Init()
        {
            Timers.Clear();
            LastSuffix.Clear();
            LastPosition.Clear();

            MinRedTime = Options.AsthmaticMinRedTime.GetInt();
            MaxRedTime = Options.AsthmaticMaxRedTime.GetInt();
            MinGreenTime = Options.AsthmaticMinGreenTime.GetInt();
            MaxGreenTime = Options.AsthmaticMaxGreenTime.GetInt();
        }
        public static void Add()
        {
            _ = new LateTask(() =>
            {
                var r = IRandom.Instance;
                var now = Utils.TimeStamp;
                foreach (var pc in Main.AllAlivePlayerControls.Where(x => x.Is(CustomRoles.Asthmatic)).ToArray())
                {
                    Timers[pc.PlayerId] = new(30, r.Next(MinRedTime, MaxRedTime), now, '●', false);
                }
            }, 8f, "Add Asthmatic Timers");
        }
        public static void OnFixedUpdate()
        {
            foreach (var kvp in Timers)
            {
                PlayerState state = Main.PlayerStates[kvp.Key];
                if (state.IsDead || !state.SubRoles.Contains(CustomRoles.Asthmatic))
                {
                    state.RemoveSubRole(CustomRoles.Asthmatic);
                    Timers.Remove(kvp.Key);
                    LastSuffix.Remove(kvp.Key);
                    LastPosition.Remove(kvp.Key);
                    continue;
                }
                kvp.Value.Update();
            }
        }
        public static void OnCheckPlayerPosition(PlayerControl pc)
        {
            if (!pc.Is(CustomRoles.Asthmatic) || !Timers.TryGetValue(pc.PlayerId, out Counter counter)) return;

            Vector2 currentPosition = pc.transform.position;

            if (!LastPosition.TryGetValue(pc.PlayerId, out Vector2 previousPosition))
            {
                LastPosition[pc.PlayerId] = currentPosition;
                return;
            }

            currentPosition.x += previousPosition.x * Time.deltaTime;
            currentPosition.y += previousPosition.y * Time.deltaTime;

            Vector2 direction = currentPosition - previousPosition;

            direction.Normalize();

            float distanceX = currentPosition.x - previousPosition.x;
            float distanceY = currentPosition.y - previousPosition.y;

            float limit = 2f;

            if (direction.y is > 0 or < 0 || direction.x is > 0 or < 0)
            {
                if (counter.IsRed && (distanceY > limit || distanceY < -limit || distanceX > limit || distanceX < -limit))
                {
                    pc.Suicide();
                    Main.PlayerStates[pc.PlayerId].RemoveSubRole(CustomRoles.Asthmatic);
                    Timers.Remove(pc.PlayerId);
                    LastSuffix.Remove(pc.PlayerId);
                    LastPosition.Remove(pc.PlayerId);
                    return;
                }

                if (!counter.IsRed)
                {
                    LastPosition[pc.PlayerId] = currentPosition;
                }
            }

            string suffix = GetSuffixText(pc.PlayerId);

            if (!pc.IsModClient() && (!LastSuffix.TryGetValue(pc.PlayerId, out var beforeSuffix) || beforeSuffix != suffix))
            {
                Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
            }

            LastSuffix[pc.PlayerId] = suffix;
        }
        public static string GetSuffixText(byte id) => Timers.TryGetValue(id, out Counter counter) ? $"{counter.ColoredArrow} {counter.ColoredTimerString}" : string.Empty;
    }
}
