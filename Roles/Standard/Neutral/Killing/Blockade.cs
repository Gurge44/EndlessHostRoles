using System.Collections.Generic;
using AmongUs.GameOptions;

namespace EHR.Roles;

public class Blockade : RoleBase
{
    public static bool On;

    private static OptionItem BlockadeRadius;
    private static OptionItem KillCooldown;
    private static OptionItem CanVent;
    private static OptionItem ImpostorVision;
    private static OptionItem AbilityUseLimit;
    private static OptionItem AbilityUseGainWithEachKill;

    public override bool IsEnable => On;

    private List<Vector2> Blockades;
    private Dictionary<byte, Vector2> LastPosition;

    public override void SetupCustomOption()
    {
        StartSetup(658300)
            .AutoSetupOption(ref BlockadeRadius, 1.5f, new FloatValueRule(0.1f, 10f, 0.1f), OptionFormat.Multiplier)
            .AutoSetupOption(ref KillCooldown, 22.5f, new FloatValueRule(0f, 180f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref CanVent, true)
            .AutoSetupOption(ref ImpostorVision, true)
            .AutoSetupOption(ref AbilityUseLimit, 1f, new FloatValueRule(0, 20, 0.05f), OptionFormat.Times)
            .AutoSetupOption(ref AbilityUseGainWithEachKill, 0.5f, new FloatValueRule(0f, 5f, 0.1f), OptionFormat.Times);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        Blockades = [];
        LastPosition = [];
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetFloat());
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        opt.SetVision(ImpostorVision.GetBool());
        
        if (Options.UsePhantomBasis.GetBool())
        {
            AURoleOptions.PhantomCooldown = 5f;
            AURoleOptions.PhantomDuration = 1f;
        }
        else if (!Options.UsePets.GetBool())
        {
            AURoleOptions.ShapeshifterCooldown = 5f;
            AURoleOptions.ShapeshifterDuration = 1f;
        }
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return CanVent.GetBool();
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (shapeshifting) PlaceBlockade(shapeshifter);
        return false;
    }

    public override bool OnVanish(PlayerControl pc)
    {
        PlaceBlockade(pc);
        return false;
    }

    public override void OnPet(PlayerControl pc)
    {
        PlaceBlockade(pc);
    }

    void PlaceBlockade(PlayerControl pc)
    {
        if (pc.GetAbilityUseLimit() < 1f) return;
        pc.RpcRemoveAbilityUse();

        Blockades.Add(pc.Pos());
        pc.Notify(Translator.GetString("MarkDone"));
    }

    public override void OnCheckPlayerPosition(PlayerControl pc)
    {
        if (pc.Is(CustomRoles.Blockade)) return;

        Vector2 pos = pc.Pos();
        float radius = BlockadeRadius.GetFloat();

        if (Blockades.Exists(x => FastVector2.DistanceWithinRange(pos, x, radius)))
            pc.TP(LastPosition.GetValueOrDefault(pc.PlayerId, pc.transform.position));
        else
            LastPosition[pc.PlayerId] = pc.transform.position;
    }

    public override void OnReportDeadBody()
    {
        Blockades = [];
        LastPosition = [];
    }
}