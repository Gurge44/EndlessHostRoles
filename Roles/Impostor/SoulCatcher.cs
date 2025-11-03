using AmongUs.GameOptions;
using EHR.Modules;
using static EHR.Options;

namespace EHR.Impostor;

internal class SoulCatcher : RoleBase
{
    public static bool On;
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(3700, TabGroup.ImpostorRoles, CustomRoles.SoulCatcher);

        ShapeSoulCatcherShapeshiftDuration = new FloatOptionItem(3710, "ShapeshiftDuration", new(2.5f, 300f, 0.5f), 20f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.SoulCatcher])
            .SetValueFormat(OptionFormat.Seconds);

        SoulCatcherShapeshiftCooldown = new FloatOptionItem(3711, "ShapeshiftCooldown", new(1f, 180f, 1f), 30f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.SoulCatcher])
            .SetValueFormat(OptionFormat.Seconds);
    }

    public override void Add(byte playerId)
    {
        On = true;
    }

    public override void Init()
    {
        On = false;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        AURoleOptions.ShapeshifterCooldown = SoulCatcherShapeshiftCooldown.GetFloat();
        AURoleOptions.ShapeshifterLeaveSkin = false;
        AURoleOptions.ShapeshifterDuration = ShapeSoulCatcherShapeshiftDuration.GetFloat();
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        hud.AbilityButton?.OverrideText(Translator.GetString("SoulCatcherButtonText"));
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (shapeshifting)
        {
            LateTask.New(() =>
            {
                if (!(!GameStates.IsInTask || !shapeshifter.IsAlive() || !target.IsAlive() || shapeshifter.inVent || target.inVent))
                {
                    Vector2 originPs = target.Pos();
                    target.TP(shapeshifter.Pos());
                    shapeshifter.TP(originPs);

                    shapeshifter.RPCPlayCustomSound("Teleport");
                    target.RPCPlayCustomSound("Teleport");
                }
            }, 1.5f, "SoulCatcher TP");
        }

        return true;
    }
}