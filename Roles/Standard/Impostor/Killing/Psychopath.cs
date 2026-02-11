using System.Linq;
using AmongUs.GameOptions;
using UnityEngine;

namespace EHR.Roles;

public class Psychopath : RoleBase
{
    public static bool On;

    public override bool IsEnable => On;

    private static OptionItem KillCooldown;

    private int Count;

    public override void SetupCustomOption()
    {
        StartSetup(653700)
            .AutoSetupOption(ref KillCooldown, 30f, new FloatValueRule(0.5f, 120f, 0.5f), OptionFormat.Seconds);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        Count = 0;
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!GameStates.IsInTask || ExileController.Instance || AntiBlackout.SkipTasks || !pc.IsAlive() || Main.KillTimers[pc.PlayerId] > 0f || Count++ < 10) return;
        var killRange = NormalGameOptionsV10.KillDistances[Mathf.Clamp(Main.NormalOptions.KillDistance, 0, 2)];
        if (!FastVector2.TryGetClosestPlayerInRange(pc.Pos(), killRange, out PlayerControl closestPlayer, x => x.PlayerId != pc.PlayerId && !x.IsImpostor())) return;
        Count = 0;
        pc.RpcCheckAndMurder(closestPlayer);
    }
}