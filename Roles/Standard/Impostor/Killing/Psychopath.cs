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
        var killRange = GameManager.Instance.LogicOptions.GetKillDistance();
        if (!FastVector2.TryGetClosestPlayerInRangeTo(pc, killRange, out PlayerControl closestPlayer, x => !x.IsImpostor())) return;
        Count = 0;
        pc.RpcCheckAndMurder(closestPlayer);
    }
}