using AmongUs.GameOptions;

namespace EHR.Neutral;

public class Weatherman : RoleBase
{
    public static bool On;

    public static OptionItem AbilityCooldown;
    private static OptionItem KillCooldown;
    private static OptionItem CanVent;
    private static OptionItem HasImpostorVision;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(645150)
            .AutoSetupOption(ref AbilityCooldown, 30f, new FloatValueRule(0f, 180f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref KillCooldown, 22.5f, new FloatValueRule(0f, 180f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref CanVent, true)
            .AutoSetupOption(ref HasImpostorVision, true);
    }

    public override void Init()
    {
        On = false;
        NaturalDisasters.LoadAllDisasters();
    }

    public override void Add(byte playerId)
    {
        On = true;
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        opt.SetVision(HasImpostorVision.GetBool());

        if (Options.UseUnshiftTrigger.GetBool() && Options.UseUnshiftTriggerForNKs.GetBool())
            AURoleOptions.ShapeshifterCooldown = AbilityCooldown.GetFloat();
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return CanVent.GetBool();
    }

    public override void OnPet(PlayerControl pc)
    {
        SpawnRandomDisaster(pc);
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (!shapeshifting) SpawnRandomDisaster(shapeshifter);
        return false;
    }

    private static void SpawnRandomDisaster(PlayerControl pc)
    {
        LateTask.New(() =>
        {
            var disaster = NaturalDisasters.GetAllDisasters().RandomElement();
            var room = pc.GetPlainShipRoom();
            NaturalDisasters.FixedUpdatePatch.AddPreparingDisaster(pc.Pos(), disaster.Name, room == null ? null : room.RoomId);
        }, 3f, "Weatherman.SpawnRandomDisaster");
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        NaturalDisasters.FixedUpdatePatch.UpdatePreparingDisasters();
        NaturalDisasters.GetActiveDisasters().ToArray().Do(x => x.Update());
        NaturalDisasters.Sinkhole.OnFixedUpdate();
        NaturalDisasters.BuildingCollapse.OnFixedUpdate();
    }

    public override void OnReportDeadBody()
    {
        NaturalDisasters.BuildingCollapse.CollapsedRooms.Clear();
    }
}