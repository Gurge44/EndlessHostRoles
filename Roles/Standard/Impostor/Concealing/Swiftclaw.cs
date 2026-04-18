using AmongUs.GameOptions;

namespace EHR.Roles;

internal class Swiftclaw : RoleBase
{
    public static OptionItem DashCD;
    public static OptionItem DashDuration;
    public static OptionItem DashSpeed;

    public static bool On;
    private static int Id => 643340;
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Swiftclaw);

        DashCD = new FloatOptionItem(Id + 2, "SwiftclawDashCD", new(0f, 180f, 0.5f), 15f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Swiftclaw])
            .SetValueFormat(OptionFormat.Seconds);

        DashDuration = new IntegerOptionItem(Id + 3, "SwiftclawDashDur", new(0, 60, 1), 4, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Swiftclaw])
            .SetValueFormat(OptionFormat.Seconds);

        DashSpeed = new FloatOptionItem(Id + 4, "SwiftclawDashSpeed", new(0.05f, 3f, 0.05f), 2f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Swiftclaw])
            .SetValueFormat(OptionFormat.Multiplier);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        if (Options.UsePhantomBasis.GetBool()) AURoleOptions.PhantomCooldown = DashCD.GetFloat() + DashDuration.GetFloat();
    }

    public override void OnPet(PlayerControl pc)
    {
        Dash(pc);
    }

    public override bool OnVanish(PlayerControl pc)
    {
        Dash(pc);
        return false;
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (!shapeshifting) return true;

        Dash(shapeshifter);
        return false;
    }

    private static void Dash(PlayerControl pc)
    {
        Main.AllPlayerSpeed[pc.PlayerId] = DashSpeed.GetFloat();
        pc.MarkDirtySettings();
        
        LateTask.New(() =>
        {
            if (Main.RealOptionsData == null) return;
            Main.AllPlayerSpeed[pc.PlayerId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
            if (!GameStates.IsInTask || ExileController.Instance || AntiBlackout.SkipTasks) return;
            pc.MarkDirtySettings();
        }, DashDuration.GetInt(), "Swiftclaw Dash End");
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        if (!Options.UsePhantomBasis.GetBool())
            hud.PetButton?.OverrideText(Translator.GetString("SwiftclawAbilityButtonText"));
        else
            hud.AbilityButton?.OverrideText(Translator.GetString("SwiftclawAbilityButtonText"));
    }
}
