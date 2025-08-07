using System.Collections.Generic;

namespace EHR.Crewmate;

public class Gardener : RoleBase
{
    public static bool On;

    public override bool IsEnable => On;

    public static OptionItem AbilityCooldown;
    private static OptionItem PlantRange;
    private static OptionItem AbilityUseLimit;
    private static OptionItem AbilityUseGainWithEachTaskCompleted;
    private static OptionItem AbilityChargesWhenFinishedTasks;

    private static List<Plant> Plants = [];

    public override void SetupCustomOption()
    {
        StartSetup(653400)
            .AutoSetupOption(ref AbilityCooldown, 30, new IntegerValueRule(0, 120, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref PlantRange, 2.5f, new FloatValueRule(0.1f, 10f, 0.1f), OptionFormat.Multiplier)
            .AutoSetupOption(ref AbilityUseLimit, 1f, new FloatValueRule(0, 20, 0.05f), OptionFormat.Times)
            .AutoSetupOption(ref AbilityUseGainWithEachTaskCompleted, 0.4f, new FloatValueRule(0f, 5f, 0.05f), OptionFormat.Times)
            .AutoSetupOption(ref AbilityChargesWhenFinishedTasks, 0.05f, new FloatValueRule(0f, 5f, 0.05f), OptionFormat.Times);
    }

    public override void Init()
    {
        On = false;
        Plants = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetFloat());
    }

    public override void OnPet(PlayerControl pc)
    {
        if (pc.GetAbilityUseLimit() < 1f) return;

        Vector2 pos = pc.Pos();
        float range = PlantRange.GetFloat() * 2f;

        if (Plants.Exists(x => Vector2.Distance(x.Position, pos) <= range))
        {
            pc.Notify(Translator.GetString("Gardener.PlantAlreadyExistsNearby"));
            return;
        }

        Plants.Add(new Plant(pos));
        pc.Notify(Translator.GetString("Gardener.PlantCreated"));
        pc.RpcRemoveAbilityUse();
    }

    public override void AfterMeetingTasks()
    {
        Plants.ForEach(x => x.SpawnIfNotSpawned());
    }

    public static bool OnAnyoneCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (!On || Plants.Count == 0) return true;

        float range = PlantRange.GetFloat();
        List<Vector2> positions = [killer.Pos(), target.Pos()];

        if (Plants.FindFirst(x => x.Spawned && positions.Exists(p => Vector2.Distance(p, x.Position) <= range), out Plant plant))
        {
            plant.Despawn();
            Plants.Remove(plant);
            killer.SetKillCooldown();
            return false;
        }

        return true;
    }
}