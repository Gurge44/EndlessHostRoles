using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EHR.AddOns.Common
{
    public class Sonar : IAddon
    {
        private static readonly Dictionary<byte, byte> Target = [];
        public AddonTypes Type => AddonTypes.Helpful;

        public void SetupCustomOption()
        {
            Options.SetupAdtRoleOptions(13644, CustomRoles.Sonar, canSetNum: true, teamSpawnOptions: true);
        }

        public static string GetSuffix(PlayerControl seer, bool meeting)
        {
            if (meeting || !seer.Is(CustomRoles.Sonar) || !Target.TryGetValue(seer.PlayerId, out var targetId)) return string.Empty;
            return TargetArrow.GetArrows(seer, targetId);
        }

        public static void OnFixedUpdate(PlayerControl seer)
        {
            if (!seer.Is(CustomRoles.Sonar) || !GameStates.IsInTask || seer.inVent) return;

            PlayerControl closest = Main.AllAlivePlayerControls.Where(x => x.PlayerId != seer.PlayerId).MinBy(x => Vector2.Distance(seer.Pos(), x.Pos()));
            if (Target.TryGetValue(seer.PlayerId, out var targetId))
            {
                if (targetId != closest.PlayerId)
                {
                    Target[seer.PlayerId] = closest.PlayerId;
                    TargetArrow.Remove(seer.PlayerId, targetId);
                    TargetArrow.Add(seer.PlayerId, closest.PlayerId);
                    Utils.NotifyRoles(SpecifySeer: seer, SpecifyTarget: closest);
                }
            }
            else
            {
                Target[seer.PlayerId] = closest.PlayerId;
                TargetArrow.Add(seer.PlayerId, closest.PlayerId);
                Utils.NotifyRoles(SpecifySeer: seer, SpecifyTarget: closest);
            }
        }
    }
}