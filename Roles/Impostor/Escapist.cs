using AmongUs.GameOptions;
using EHR.Modules;

namespace EHR.Impostor;

internal class Escapist : RoleBase
{
    public static bool On;

    public static OptionItem EscapistSSCD;
    public static OptionItem CanVent;
    public static OptionItem OneMarkPerRound;

    public Vector2? EscapistLocation;
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(3600, TabGroup.ImpostorRoles, CustomRoles.Escapist);

        EscapistSSCD = new FloatOptionItem(3611, "ShapeshiftCooldown", new(1f, 180f, 1f), 5f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Escapist])
            .SetValueFormat(OptionFormat.Seconds);

        CanVent = new BooleanOptionItem(3612, "CanVent", false, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Escapist]);
        
        OneMarkPerRound = new BooleanOptionItem(3613, "Escapist.OneMarkPerRound", false, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Escapist]);
    }

    public override void Add(byte playerId)
    {
        On = true;
        EscapistLocation = null;
    }

    public override void Init()
    {
        On = false;
        EscapistLocation = null;
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return CanVent.GetBool();
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        if (Options.UsePets.GetBool() && !Options.UsePhantomBasis.GetBool())
            hud.PetButton?.OverrideText(Translator.GetString("EscapistAbilityButtonText"));
        else
            hud.AbilityButton?.OverrideText(Translator.GetString("EscapistAbilityButtonText"));
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        if (Options.UsePhantomBasis.GetBool())
            AURoleOptions.PhantomCooldown = EscapistSSCD.GetFloat();
        else
        {
            if (Options.UsePets.GetBool()) return;

            AURoleOptions.ShapeshifterCooldown = EscapistSSCD.GetFloat();
            AURoleOptions.ShapeshifterDuration = 1f;
        }
    }

    public override void OnPet(PlayerControl pc)
    {
        TeleportOrMark(pc);
    }

    public override bool OnVanish(PlayerControl pc)
    {
        TeleportOrMark(pc);
        return false;
    }

    private void TeleportOrMark(PlayerControl pc)
    {
        if (EscapistLocation.HasValue)
        {
            pc.TP(EscapistLocation.Value);
            pc.RPCPlayCustomSound("Teleport");
            if (!OneMarkPerRound.GetBool()) EscapistLocation = null;
        }
        else
            EscapistLocation = pc.Pos();
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (shapeshifting) TeleportOrMark(shapeshifter);
        return false;
    }

    public override void OnReportDeadBody()
    {
        EscapistLocation = null;
    }
}