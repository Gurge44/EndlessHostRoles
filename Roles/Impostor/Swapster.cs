using System.Collections.Generic;
using AmongUs.GameOptions;

namespace EHR.Impostor;

internal class Swapster : RoleBase
{
    public static OptionItem SSCD;
    public static readonly Dictionary<byte, byte> FirstSwapTarget = [];
    public static bool On;
    private static int Id => 643320;
    public override bool IsEnable => On;

    public override void Init()
    {
        FirstSwapTarget.Clear();
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
    }

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Swapster);

        SSCD = new FloatOptionItem(Id + 2, "ShapeshiftCooldown", new(0f, 180f, 2.5f), 30f, TabGroup.ImpostorRoles)
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

        if (FirstSwapTarget.TryGetValue(swapster.PlayerId, out byte firstTargetId))
        {
            PlayerControl firstTarget = Utils.GetPlayerById(firstTargetId);
            Vector2 pos = firstTarget.Pos();
            firstTarget.TP(target);
            target.TP(pos);
            FirstSwapTarget.Remove(swapster.PlayerId);
        }
        else
            FirstSwapTarget[swapster.PlayerId] = target.PlayerId;

        return false;
    }
}