using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TOHE.Roles.Impostor
{
    internal class Swapster
    {
        private static int Id => 643320;
        public static OptionItem SSCD;
        public static readonly Dictionary<byte, byte> FirstSwapTarget = [];
        public static void Init() => FirstSwapTarget.Clear();
        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Swapster);
            SSCD = FloatOptionItem.Create(Id + 2, "ShapeshiftCooldown", new(0f, 180f, 2.5f), 30f, TabGroup.ImpostorRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Swapster])
                .SetValueFormat(OptionFormat.Seconds);
        }
        public static void OnShapeshift(PlayerControl swapster, PlayerControl target)
        {
            if (swapster == null || target == null || swapster == target) return;
            if (FirstSwapTarget.TryGetValue(swapster.PlayerId, out var firstTargetId))
            {
                var firstTarget = Utils.GetPlayerById(firstTargetId);
                var pos = firstTarget.Pos();
                firstTarget.TP(target);
                target.TP(pos);
                FirstSwapTarget.Remove(swapster.PlayerId);
            }
            else
            {
                FirstSwapTarget[swapster.PlayerId] = target.PlayerId;
            }
        }
    }
}
