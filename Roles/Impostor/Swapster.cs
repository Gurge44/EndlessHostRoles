using AmongUs.GameOptions;
using System.Collections.Generic;

namespace EHR.Roles.Impostor
{
    internal class Swapster : RoleBase
    {
        private static int Id => 643320;
        public static OptionItem SSCD;
        public static readonly Dictionary<byte, byte> FirstSwapTarget = [];
        public static bool On;
        public override bool IsEnable => On;

        public override void Init()
        {
            FirstSwapTarget.Clear();
            On = false;
        }

        public override void Add(byte playerId) => On = true;

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Swapster);
            SSCD = FloatOptionItem.Create(Id + 2, "ShapeshiftCooldown", new(0f, 180f, 2.5f), 30f, TabGroup.ImpostorRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Swapster])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            AURoleOptions.ShapeshifterCooldown = SSCD.GetFloat();
            AURoleOptions.ShapeshifterDuration = 1f;
        }

        public override bool OnShapeshift(PlayerControl swapster, PlayerControl target, bool shapeshifting)
        {
            if (swapster == null || target == null || swapster == target || !shapeshifting) return true;
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

            return false;
        }
    }
}