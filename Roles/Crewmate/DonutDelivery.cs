using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Crewmate;

public class DonutDelivery : RoleBase
{
    private const int Id = 642700;
    private static List<DonutDelivery> Instances = [];
    private static Dictionary<byte, float> StartingSpeed = [];

    private static OptionItem CD;
    private static OptionItem UseLimit;
    private static OptionItem SpeedEffect;
    private static OptionItem SEDelay;
    private static OptionItem SEAmount;
    private static OptionItem SEDuration;
    private static OptionItem SEEvilsGetDecreased;
    private static OptionItem SEEvilsDecreaseAmount;
    public static OptionItem UsePet;

    private byte DonutDeliveryId;
    private HashSet<byte> Players = [];

    public override bool IsEnable => Instances.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.DonutDelivery);

        CD = new FloatOptionItem(Id + 10, "DonutDeliverCD", new(2.5f, 60f, 0.5f), 30f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.DonutDelivery])
            .SetValueFormat(OptionFormat.Seconds);

        UseLimit = new FloatOptionItem(Id + 12, "AbilityUseLimit", new(0, 20, 1f), 5, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.DonutDelivery])
            .SetValueFormat(OptionFormat.Times);

        SpeedEffect = new BooleanOptionItem(Id + 14, "DonutDeliverSpeedEffect", false, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.DonutDelivery]);

        SEDelay = new FloatOptionItem(Id + 15, "DonutDeliverSEDelay", new(0f, 30f, 0.5f), 3f, TabGroup.CrewmateRoles)
            .SetParent(SpeedEffect)
            .SetValueFormat(OptionFormat.Seconds);

        SEAmount = new FloatOptionItem(Id + 16, "DonutDeliverSEAmount", new(0.05f, 3f, 0.05f), 0.5f, TabGroup.CrewmateRoles)
            .SetParent(SpeedEffect);

        SEDuration = new FloatOptionItem(Id + 17, "DonutDeliverSEDuration", new(0.5f, 60f, 0.5f), 5f, TabGroup.CrewmateRoles)
            .SetParent(SpeedEffect)
            .SetValueFormat(OptionFormat.Seconds);

        SEEvilsGetDecreased = new BooleanOptionItem(Id + 18, "DonutDeliverSEEvilsGetDecreased", true, TabGroup.CrewmateRoles)
            .SetParent(SpeedEffect);

        SEEvilsDecreaseAmount = new FloatOptionItem(Id + 19, "DonutDeliverSEEvilsDecreaseAmount", new(0.1f, 5f, 0.1f), 1f, TabGroup.CrewmateRoles)
            .SetParent(SEEvilsGetDecreased);

        UsePet = CreatePetUseSetting(Id + 13, CustomRoles.DonutDelivery);
    }

    public override void Init()
    {
        Instances = [];
        StartingSpeed = Main.AllPlayerSpeed.ToDictionary(x => x.Key, x => x.Value);
    }

    public override void Add(byte playerId)
    {
        Instances.Add(this);
        Players = [];
        DonutDeliveryId = playerId;
        playerId.SetAbilityUseLimit(UseLimit.GetFloat());
    }

    public override void Remove(byte playerId)
    {
        Instances.Remove(this);
    }

    public override void SetKillCooldown(byte playerId)
    {
        Main.AllPlayerKillCooldown[playerId] = CD.GetFloat();
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return pc.GetAbilityUseLimit() >= 1;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        opt.SetVision(false);
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (!IsEnable || killer == null || target == null || killer.GetAbilityUseLimit() <= 0) return false;

        killer.RpcRemoveAbilityUse();

        int num1 = IRandom.Instance.Next(0, 19);
        killer.Notify(GetString($"DonutDelivered-{num1}"));
        RandomNotifyTarget(target);
        Players.Add(target.PlayerId);

        killer.SetKillCooldown();

        if (SpeedEffect.GetBool())
        {
            LateTask.New(() =>
            {
                if (target.IsCrewmate() || target.GetCustomRole().IsNonNK() || !SEEvilsGetDecreased.GetBool())
                    Main.AllPlayerSpeed[target.PlayerId] += SEAmount.GetFloat();
                else
                    Main.AllPlayerSpeed[target.PlayerId] -= SEEvilsDecreaseAmount.GetFloat();

                target.MarkDirtySettings();

                LateTask.New(() =>
                {
                    Main.AllPlayerSpeed[target.PlayerId] = StartingSpeed[target.PlayerId];
                    target.MarkDirtySettings();
                }, SEDuration.GetFloat(), log: false);
            }, SEDelay.GetFloat(), log: false);
        }

        if (target.AmOwner)
            Achievements.Type.Delicious.Complete();

        return false;
    }

    public static void RandomNotifyTarget(PlayerControl target)
    {
        int num2 = IRandom.Instance.Next(0, 6);
        target.Notify(GetString($"DonutGot-{num2}"));
    }

    public static bool IsUnguessable(PlayerControl guesser, PlayerControl target)
    {
        foreach (DonutDelivery instance in Instances)
        {
            if (instance.DonutDeliveryId == target.PlayerId && instance.Players.Contains(guesser.PlayerId))
                return true;
        }

        return false;
    }
}
