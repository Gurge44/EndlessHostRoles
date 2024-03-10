using System.Collections.Generic;

namespace TOHE.Roles.AddOns.Common
{
    internal static class Stained
    {
        public static List<byte> VioletNameList = [];

        public static void OnDeath(PlayerControl pc, PlayerControl killer)
        {
            if (killer == null || pc == null || pc.PlayerId == killer.PlayerId || !killer.IsAlive() || !GameStates.IsInTask) return;

            VioletNameList.Add(killer.PlayerId);
            Utils.NotifyRoles(SpecifyTarget: killer);

            _ = new LateTask(() =>
            {
                VioletNameList.Remove(killer.PlayerId);
                Utils.NotifyRoles(SpecifyTarget: killer, isForMeeting: GameStates.IsMeeting);
            }, 3f, "Stained Killer Violet Name");
        }
    }
}
